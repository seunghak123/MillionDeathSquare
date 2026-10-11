using System;
using UnityEngine;

namespace Seunghak.Game.Suika3D
{
    /// <summary>스킬 표시 정보 (기획서 7장)</summary>
    public static class SuikaSkillInfo
    {
        public static string Name(E_FOSSIL_SKILL s)
        {
            switch (s)
            {
                case E_FOSSIL_SKILL.HAMMER:     return "망치";
                case E_FOSSIL_SKILL.BRUSH:      return "발굴 브러시";
                case E_FOSSIL_SKILL.EARTHQUAKE: return "지진";
                case E_FOSSIL_SKILL.MAGNET:     return "자석";
                case E_FOSSIL_SKILL.EVOLVE:     return "진화";
                default:                        return string.Empty;
            }
        }

        public static string Description(E_FOSSIL_SKILL s)
        {
            switch (s)
            {
                case E_FOSSIL_SKILL.HAMMER:     return "탭한 화석 1개를 부순다";
                case E_FOSSIL_SKILL.BRUSH:      return "1~2단계 작은 화석을 모두 쓸어낸다";
                case E_FOSSIL_SKILL.EARTHQUAKE: return "원하는 방향으로 3초간 흔든다 (벽이 솟아 안 떨어짐)";
                case E_FOSSIL_SKILL.MAGNET:     return "탭한 화석과 같은 화석을 끌어당긴다";
                case E_FOSSIL_SKILL.EVOLVE:     return "탭한 화석을 1단계 업그레이드한다";
                default:                        return string.Empty;
            }
        }

        /// <summary>발동 후 화석을 탭해야 하는 스킬인지</summary>
        public static bool NeedsTarget(E_FOSSIL_SKILL s) =>
            s == E_FOSSIL_SKILL.HAMMER || s == E_FOSSIL_SKILL.MAGNET || s == E_FOSSIL_SKILL.EVOLVE;
    }

    /// <summary>
    /// 스킬 보유/사용/레벨업 선택 (기획서 7장, 8장).
    ///  - 최대 4개 보유, 모두 1회용. 버튼을 누른 뒤 대상을 탭하면 발동 (브러시는 즉시, 지진은 방향 스와이프)
    ///  - 레벨업: 랜덤 3개 중 1개 선택, 광고 보고 다시 뽑기(레벨업당 1회). 가득 찼으면 하나 버리고 받는다
    /// 입력(탭/스와이프) 해석은 SuikaDropper가 하고, 여기서는 받은 입력으로 효과를 낸다.
    /// </summary>
    public class SuikaSkillSystem : MonoBehaviour
    {
        [SerializeField] private SuikaGameManager manager;
        [Tooltip("지진 스킬 방향 스와이프를 이 시간 안에 안 하면 랜덤 방향으로 시작")]
        [SerializeField] private float quakeAimTimeout = 2.5f;

        private readonly E_FOSSIL_SKILL[] _held = new E_FOSSIL_SKILL[8];
        private int _heldCount;
        private int _maxHeld = 4;

        // 발동 대기
        private E_FOSSIL_SKILL _pending = E_FOSSIL_SKILL.NONE;
        private int _pendingSlot = -1;
        private float _quakeAimTimer;

        // 자석
        private SuikaFruit _magnetTarget;
        private int _magnetTier;
        private float _magnetTimer;

        // 레벨업 선택
        private readonly E_FOSSIL_SKILL[] _offer = new E_FOSSIL_SKILL[3];
        private bool _offerOpen;
        private bool _rerollUsed;
        private E_FOSSIL_SKILL _overflow = E_FOSSIL_SKILL.NONE;

        public event Action OnInventoryChanged;
        /// <summary>발동 대기 상태 변경 (대기 중인 스킬, NONE이면 해제)</summary>
        public event Action<E_FOSSIL_SKILL> OnPendingChanged;
        public event Action<E_FOSSIL_SKILL> OnSkillUsed;
        /// <summary>레벨업 선택지 갱신 (처음 열림 / 다시 뽑기)</summary>
        public event Action OnOfferChanged;
        /// <summary>보유 칸이 가득 차서 버릴 스킬을 골라야 함 (새로 받을 스킬)</summary>
        public event Action<E_FOSSIL_SKILL> OnNeedReplace;
        /// <summary>레벨업 선택 완료 (게임 재개)</summary>
        public event Action OnOfferClosed;

        public int HeldCount => _heldCount;
        public int MaxHeld => _maxHeld;
        public E_FOSSIL_SKILL GetHeld(int slot) => slot >= 0 && slot < _heldCount ? _held[slot] : E_FOSSIL_SKILL.NONE;
        public E_FOSSIL_SKILL Pending => _pending;
        public bool IsTargeting => _pending != E_FOSSIL_SKILL.NONE && SuikaSkillInfo.NeedsTarget(_pending);
        public bool IsAimingQuake => _pending == E_FOSSIL_SKILL.EARTHQUAKE;
        /// <summary>드롭 대신 스킬 입력을 받아야 하는 상태인지</summary>
        public bool IsAwaitingInput => _pending != E_FOSSIL_SKILL.NONE;

        public bool IsOfferOpen => _offerOpen;
        private int CurrentLevel => manager != null ? manager.Level : 0;
        public bool CanReroll => _offerOpen && !_rerollUsed && _overflow == E_FOSSIL_SKILL.NONE;
        public E_FOSSIL_SKILL GetOffer(int i) => _offer[i];
        public E_FOSSIL_SKILL Overflow => _overflow;

        private void Awake()
        {
            if (manager == null) manager = SuikaGameManager.Instance;
        }

        // ── 판 시작 ───────────────────────────────────────────────────

        public void ResetForRun(int maxHeld, E_FOSSIL_SKILL[] startSkills, int startCount)
        {
            _maxHeld = Mathf.Clamp(maxHeld, 1, _held.Length);
            _heldCount = 0;
            for (int i = 0; i < startCount && _heldCount < _maxHeld; i++)
                if (startSkills[i] != E_FOSSIL_SKILL.NONE) _held[_heldCount++] = startSkills[i];
            CancelPending();
            _magnetTarget = null;
            _magnetTimer = 0f;
            _offerOpen = false;
            _overflow = E_FOSSIL_SKILL.NONE;
            OnInventoryChanged?.Invoke();
        }

        // ── 사용 ───────────────────────────────────────────────────────

        /// <summary>스킬 버튼. 같은 버튼을 다시 누르면 취소.</summary>
        public void ActivateSlot(int slot)
        {
            if (manager == null || manager.GameState != E_SUIKA_GAME_STATE.PLAYING) return;
            if (slot < 0 || slot >= _heldCount) return;

            if (_pendingSlot == slot) { CancelPending(); return; }
            CancelPending();

            E_FOSSIL_SKILL s = _held[slot];
            switch (s)
            {
                case E_FOSSIL_SKILL.BRUSH:
                    if (manager.RemoveTiersUpTo(manager.Rules.brushMaxTier) > 0)
                        Consume(slot, s);
                    break;

                case E_FOSSIL_SKILL.EARTHQUAKE:
                    if (manager.IsTectonic)
                    {
                        // 지각 변동: 폰을 기울이는 방향으로 직접 조종
                        StartQuake(manager.Plate.ScreenToWorldDir(Vector2.right), followTilt: true);
                        Consume(slot, s);
                    }
                    else
                    {
                        SetPending(s, slot);
                        _quakeAimTimer = quakeAimTimeout;
                        manager.Plate?.RaiseWalls(true);
                    }
                    break;

                default:
                    SetPending(s, slot); // 망치/자석/진화: 대상 탭 대기
                    break;
            }
        }

        public void CancelPending()
        {
            if (_pending == E_FOSSIL_SKILL.EARTHQUAKE && manager != null && manager.Plate != null && !manager.Plate.IsSkillQuakeActive)
                manager.Plate.RaiseWalls(false);
            if (_pending == E_FOSSIL_SKILL.NONE) return;
            _pending = E_FOSSIL_SKILL.NONE;
            _pendingSlot = -1;
            OnPendingChanged?.Invoke(E_FOSSIL_SKILL.NONE);
        }

        /// <summary>대상 화석 탭 (망치/자석/진화). 효과가 났으면 true</summary>
        public bool ApplyToTarget(SuikaFruit target)
        {
            if (!IsTargeting || target == null || !target.IsDropped || target.IsMerging || target.IsHeld) return false;
            E_FOSSIL_SKILL s = _pending;
            int slot = _pendingSlot;
            bool ok = false;

            switch (s)
            {
                case E_FOSSIL_SKILL.HAMMER:
                    manager.RemoveFruit(target);
                    ok = true;
                    break;
                case E_FOSSIL_SKILL.EVOLVE:
                    ok = manager.EvolveFruit(target);
                    break;
                case E_FOSSIL_SKILL.MAGNET:
                    _magnetTarget = target;
                    _magnetTier = target.Tier;
                    _magnetTimer = manager.Rules.magnetDuration;
                    ok = true;
                    break;
            }

            if (!ok) return false;
            _pending = E_FOSSIL_SKILL.NONE;
            _pendingSlot = -1;
            OnPendingChanged?.Invoke(E_FOSSIL_SKILL.NONE);
            Consume(slot, s);
            return true;
        }

        /// <summary>지진 방향 스와이프 (일반 모드). screenDelta = 화면 픽셀 이동량</summary>
        public void ApplyQuakeSwipe(Vector2 screenDelta)
        {
            if (!IsAimingQuake || screenDelta.sqrMagnitude < 1f) return;
            int slot = _pendingSlot;
            Vector3 dir = manager.Plate.ScreenToWorldDir(screenDelta.normalized);
            _pending = E_FOSSIL_SKILL.NONE;
            _pendingSlot = -1;
            OnPendingChanged?.Invoke(E_FOSSIL_SKILL.NONE);
            StartQuake(dir, followTilt: false);
            Consume(slot, E_FOSSIL_SKILL.EARTHQUAKE);
        }

        private void StartQuake(Vector3 worldDir, bool followTilt)
        {
            manager.Plate?.StartSkillQuake(worldDir, manager.Rules.earthquakeDuration, manager.Rules.earthquakeTiltDeg, followTilt);
        }

        private void SetPending(E_FOSSIL_SKILL s, int slot)
        {
            _pending = s;
            _pendingSlot = slot;
            OnPendingChanged?.Invoke(s);
        }

        private void Consume(int slot, E_FOSSIL_SKILL s)
        {
            for (int i = slot; i < _heldCount - 1; i++) _held[i] = _held[i + 1];
            _heldCount--;
            SuikaHaptics.Play(SuikaHaptics.SKILL);
            SuikaAnalytics.SkillUsed(s);
            OnSkillUsed?.Invoke(s);
            OnInventoryChanged?.Invoke();
        }

        private void Update()
        {
            if (!IsAimingQuake || manager == null || manager.GameState != E_SUIKA_GAME_STATE.PLAYING) return;
            _quakeAimTimer -= Time.deltaTime;
            if (_quakeAimTimer <= 0f)
            {
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                ApplyQuakeSwipe(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 100f);
            }
        }

        private void FixedUpdate()
        {
            if (_magnetTimer <= 0f || manager == null) return;
            _magnetTimer -= Time.fixedDeltaTime;

            if (_magnetTarget == null || !_magnetTarget.isActiveAndEnabled || _magnetTarget.IsMerging || _magnetTarget.Tier != _magnetTier)
            {
                _magnetTimer = 0f;
                _magnetTarget = null;
                return;
            }

            // 같은 단계 화석을 대상 쪽으로 끌어당긴다 (질량 무관 가속)
            Vector3 targetPos = _magnetTarget.transform.position;
            float accel = manager.Rules.magnetAcceleration;
            var fruits = manager.ActiveFruits;
            for (int i = 0; i < fruits.Count; i++)
            {
                SuikaFruit f = fruits[i];
                if (f == _magnetTarget || f.Tier != _magnetTier || f.IsMerging || f.IsHeld) continue;
                Vector3 to = targetPos - f.transform.position;
                if (to.sqrMagnitude < 0.0001f) continue;
                f.Body.AddForce(to.normalized * accel, ForceMode.Acceleration);
            }
        }

        // ── 레벨업 선택 ────────────────────────────────────────────────

        public void OpenOffer()
        {
            _offerOpen = true;
            _rerollUsed = false;
            _overflow = E_FOSSIL_SKILL.NONE;
            CancelPending();
            RollOffer();
        }

        /// <summary>광고 보고 다시 뽑기 (레벨업당 1회). 광고 시청은 호출부.</summary>
        public bool Reroll()
        {
            if (!CanReroll) return false;
            _rerollUsed = true;
            RollOffer();
            return true;
        }

        /// <summary>선택. 보유 칸이 가득 차면 OnNeedReplace 후 ReplaceHeld/DiscardOverflow를 기다린다.</summary>
        public void ChooseOffer(int index)
        {
            if (!_offerOpen || _overflow != E_FOSSIL_SKILL.NONE || index < 0 || index >= _offer.Length) return;
            E_FOSSIL_SKILL s = _offer[index];
            if (_heldCount < _maxHeld)
            {
                SuikaAnalytics.LevelUpResolved(CurrentLevel, s, _rerollUsed, "take");
                _held[_heldCount++] = s;
                OnInventoryChanged?.Invoke();
                CloseOffer();
                return;
            }
            _overflow = s;
            OnNeedReplace?.Invoke(s);
        }

        /// <summary>가득 찼을 때: slot의 스킬을 버리고 새 스킬을 받는다</summary>
        public void ReplaceHeld(int slot)
        {
            if (_overflow == E_FOSSIL_SKILL.NONE || slot < 0 || slot >= _heldCount) return;
            SuikaAnalytics.LevelUpResolved(CurrentLevel, _overflow, _rerollUsed, "replace");
            _held[slot] = _overflow;
            _overflow = E_FOSSIL_SKILL.NONE;
            OnInventoryChanged?.Invoke();
            CloseOffer();
        }

        /// <summary>가득 찼을 때: 새 스킬을 포기</summary>
        public void DiscardOverflow()
        {
            if (_overflow == E_FOSSIL_SKILL.NONE) return;
            SuikaAnalytics.LevelUpResolved(CurrentLevel, _overflow, _rerollUsed, "discard");
            _overflow = E_FOSSIL_SKILL.NONE;
            CloseOffer();
        }

        private void CloseOffer()
        {
            _offerOpen = false;
            OnOfferClosed?.Invoke();
        }

        private void RollOffer()
        {
            // 5종 중 서로 다른 3개 (부분 셔플, 할당 없음)
            int n = SuikaProgress.SKILL_COUNT;
            Span<int> pool = stackalloc int[SuikaProgress.SKILL_COUNT];
            for (int i = 0; i < n; i++) pool[i] = i;
            for (int i = 0; i < _offer.Length; i++)
            {
                int j = UnityEngine.Random.Range(i, n);
                int tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
                _offer[i] = (E_FOSSIL_SKILL)pool[i];
            }
            OnOfferChanged?.Invoke();
        }
    }
}
