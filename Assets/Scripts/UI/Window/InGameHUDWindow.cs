using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Seunghak.Common;
using Seunghak.Manager;
using Seunghak.Data;
using Seunghak.Game.Suika3D;

namespace Seunghak.UIManager
{
    /// <summary>
    /// Fossil Merge 인게임 HUD (기획서 11장 화면 2).
    ///
    /// 구성 (모든 항목은 연결된 것만 갱신 — 미연결이면 건너뜀):
    ///   - 점수 / 최고 점수 / 남은 시간 / 라이프 / 경험치 게이지·레벨
    ///   - 다음 화석 (이름 / 아이콘)
    ///   - 보유 스킬 버튼 (최대 4) — 누른 뒤 대상을 탭하면 발동. 다시 누르면 취소
    ///   - 알림 한 줄: 연쇄, 시간 보너스, 공룡 부활, 지진 경보, 장외 낙하, 화석 수 상한
    ///   - 스킬 대상 안내 ("부술 화석을 탭하세요" 등)
    ///   - 재화(코인): 보유 + 이번 판 미적립분 (GoldDisplayUI)
    ///   - 정지 버튼 / 카메라 회전 버튼 / 위험 표시 / 모드 표시
    ///
    /// 기존 프리팹의 아이템 슬롯 3개(OnClickItemSlot(i) 연결)는 skillSlots가 비어 있으면 스킬 버튼으로 재사용한다.
    /// </summary>
    public class InGameHUDWindow : BaseUIWindow
    {
        [System.Serializable]
        private class ItemSlotUI
        {
            public Button button;
            public Image icon;
            public TextMeshProUGUI countText;
            [Tooltip("(구) 아이템 에셋 — 스킬 슬롯으로 재사용 시 무시")]
            public ShopItemData itemData;
        }

        [System.Serializable]
        private class SkillSlotUI
        {
            public Button button;
            public TextMeshProUGUI label;
            [Tooltip("스킬별 아이콘을 쓰려면 연결 (선택)")]
            public Image icon;
            [Tooltip("발동 대기 중 표시 (선택)")]
            public GameObject selectedMark;
        }

        [Header("점수")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI bestScoreText;

        [Header("시간 / 라이프 / 경험치 (선택)")]
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI livesText;
        [Tooltip("라이프 아이콘 (켜진 개수 = 남은 라이프)")]
        [SerializeField] private GameObject[] lifeIcons;
        [SerializeField] private Image xpFill;
        [SerializeField] private TextMeshProUGUI levelText;

        [Header("인게임 재화")]
        [Tooltip("재화 라벨. 비워두면 goldText 주변에서 자동으로 찾는다.")]
        [SerializeField] private GoldDisplayUI goldDisplay;
        [SerializeField] private TextMeshProUGUI goldText;

        [Header("정지 버튼")]
        [SerializeField] private Button pauseButton;

        [Header("다음 화석 (선택)")]
        [SerializeField] private TextMeshProUGUI nextFruitText;
        [SerializeField] private Image nextFruitIcon;
        [Tooltip("단계별 아이콘 (화석 테이블 단계 순서). 비우면 아이콘 갱신 안 함")]
        [SerializeField] private Sprite[] fruitIcons;

        [Header("보유 스킬 (최대 4)")]
        [SerializeField] private SkillSlotUI[] skillSlots;
        [Tooltip("스킬 아이콘 (E_FOSSIL_SKILL 순서: 망치, 브러시, 지진, 자석, 진화)")]
        [SerializeField] private Sprite[] skillIcons;
        [SerializeField] private TextMeshProUGUI targetHintText;

        [Header("알림 (선택)")]
        [SerializeField] private TextMeshProUGUI noticeText;
        [SerializeField] private float noticeSeconds = 1.4f;

        [Header("카메라 회전 버튼 (선택)")]
        [SerializeField] private float rotateStepDeg = 45f;

        [Header("위험 표시 (선택)")]
        [SerializeField] private GameObject dangerIndicator;

        [Header("모드 표시 (선택)")]
        [SerializeField] private TextMeshProUGUI difficultyText;

        [Header("(구) 아이템 슬롯 — skillSlots가 비어 있으면 스킬 버튼으로 재사용")]
        [SerializeField] private ItemSlotUI[] itemSlots = new ItemSlotUI[3];

        private SuikaGameManager _gm;
        private SuikaSkillSystem _skills;
        private int _shownSeconds = -1;
        private float _noticeTimer;

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.InGameHUDWindow;
            BuildSkillSlotsFromItemSlots();
        }

        // 프리팹에 스킬 슬롯이 따로 없으면 기존 아이템 슬롯(버튼 + 개수 텍스트)을 스킬 버튼으로 쓴다
        private void BuildSkillSlotsFromItemSlots()
        {
            if (skillSlots != null && skillSlots.Length > 0) return;
            if (itemSlots == null) return;
            skillSlots = new SkillSlotUI[itemSlots.Length];
            for (int i = 0; i < itemSlots.Length; i++)
            {
                skillSlots[i] = new SkillSlotUI
                {
                    button = itemSlots[i]?.button,
                    label = itemSlots[i]?.countText,
                    icon = itemSlots[i]?.icon,
                };
            }
        }

        public override void EnterWindow()
        {
            _gm = SuikaGameManager.Instance;
            _skills = _gm != null ? _gm.Skills : null;
            if (_gm != null)
            {
                _gm.OnScoreChanged += OnScoreChanged;
                _gm.OnGameOver += OnGameOver;
                _gm.OnNextTierChanged += UpdateNextFruit;
                _gm.OnDanger += OnDanger;
                _gm.OnLivesChanged += UpdateLives;
                _gm.OnXpChanged += UpdateXp;
                _gm.OnChain += OnChain;
                _gm.OnTimeBonus += OnTimeBonus;
                _gm.OnLifeLost += OnLifeLost;
                _gm.OnDinoRevival += OnDinoRevival;
                _gm.OnTimeWarning += OnTimeWarning;
                if (_gm.Quakes != null) _gm.Quakes.OnWarning += OnQuakeWarning;
                if (_gm.Quakes != null) _gm.Quakes.OnQuakeEnd += OnQuakeEnd;
            }
            if (_skills != null)
            {
                _skills.OnInventoryChanged += UpdateSkillSlots;
                _skills.OnPendingChanged += OnPendingChanged;
            }

            BindGoldDisplay();
            base.EnterWindow();

            UpdateScore(_gm != null ? _gm.Score : 0);
            RefreshGold(false);
            if (_gm != null)
            {
                UpdateNextFruit(_gm.NextTier);
                UpdateLives(_gm.Lives);
                UpdateXp(_gm.Xp, _gm.XpToNext, _gm.Level);
                if (bestScoreText != null) bestScoreText.text = _gm.BestScore.ToString();
                if (difficultyText != null) difficultyText.text = _gm.IsTectonic ? "지각 변동" : "일반";
            }
            OnDanger(false);
            UpdateSkillSlots();
            OnPendingChanged(E_FOSSIL_SKILL.NONE);
            ShowNotice(null);
            _shownSeconds = -1;
        }

        public override void ExitWindow()
        {
            if (_gm != null)
            {
                _gm.OnScoreChanged -= OnScoreChanged;
                _gm.OnGameOver -= OnGameOver;
                _gm.OnNextTierChanged -= UpdateNextFruit;
                _gm.OnDanger -= OnDanger;
                _gm.OnLivesChanged -= UpdateLives;
                _gm.OnXpChanged -= UpdateXp;
                _gm.OnChain -= OnChain;
                _gm.OnTimeBonus -= OnTimeBonus;
                _gm.OnLifeLost -= OnLifeLost;
                _gm.OnDinoRevival -= OnDinoRevival;
                _gm.OnTimeWarning -= OnTimeWarning;
                if (_gm.Quakes != null) _gm.Quakes.OnWarning -= OnQuakeWarning;
                if (_gm.Quakes != null) _gm.Quakes.OnQuakeEnd -= OnQuakeEnd;
            }
            if (_skills != null)
            {
                _skills.OnInventoryChanged -= UpdateSkillSlots;
                _skills.OnPendingChanged -= OnPendingChanged;
            }

            // 인게임을 벗어나면 라벨은 다시 "지갑 잔액만" 보여준다.
            if (goldDisplay != null)
            {
                goldDisplay.SetBonusProvider(null);
                goldDisplay.Refresh(false);
            }

            CloseUI();
        }

        // pauseButton / 스킬(구 아이템) 버튼은 프리팹 Inspector OnClick으로 연결되어 있다.
        // 여기서 AddListener까지 하면 클릭 1회에 2번 호출되므로 코드 연결은 하지 않는다.
        public override void RegistEvent() { }
        public override void DeleteRegistedEvent() { }

        private void Update()
        {
            if (_gm == null) return;

            // 남은 시간: 초가 바뀔 때만 문자열 갱신 (GC 최소화)
            if (timeText != null)
            {
                int sec = Mathf.CeilToInt(_gm.TimeLeft);
                if (sec != _shownSeconds)
                {
                    _shownSeconds = sec;
                    timeText.text = $"{sec / 60}:{sec % 60:00}";
                    timeText.color = sec <= 10 ? new Color(1f, 0.35f, 0.3f) : Color.white;
                }
            }

            if (_noticeTimer > 0f)
            {
                _noticeTimer -= Time.unscaledDeltaTime;
                if (_noticeTimer <= 0f) ShowNotice(null);
            }
        }

        // ── 점수 / 시간 / 라이프 / 경험치 ─────────────────────────────

        private void UpdateScore(int score)
        {
            if (scoreText != null) scoreText.text = score.ToString();
        }

        private void UpdateLives(int lives)
        {
            if (livesText != null) livesText.text = lives.ToString();
            if (lifeIcons == null) return;
            for (int i = 0; i < lifeIcons.Length; i++)
                if (lifeIcons[i] != null) lifeIcons[i].SetActive(i < lives);
        }

        private void UpdateXp(float xp, float need, int level)
        {
            if (xpFill != null) xpFill.fillAmount = need > 0f ? Mathf.Clamp01(xp / need) : 0f;
            if (levelText != null) levelText.text = $"Lv.{level}";
        }

        // ── 재화 ───────────────────────────────────────────────────────

        /// <summary>
        /// 라벨을 소유한 GoldDisplayUI를 찾아 "지갑 + 이번 판 미적립 코인"을 보여주도록 연결한다.
        /// 더하는 값은 GoldEarnedPending(미적립분)이라 판 종료 시 적립돼도 합계가 그대로 유지된다.
        /// </summary>
        private void BindGoldDisplay()
        {
            if (goldDisplay == null && goldText != null)
                goldDisplay = goldText.GetComponentInParent<GoldDisplayUI>(true);
            if (goldDisplay == null)
                goldDisplay = GetComponentInChildren<GoldDisplayUI>(true);
            if (goldDisplay == null) return;

            goldDisplay.enabled = true;
            goldDisplay.SetBonusProvider(() =>
                SuikaGameManager.Instance != null ? SuikaGameManager.Instance.GoldEarnedPending : 0);
        }

        private void RefreshGold(bool animate) => goldDisplay?.Refresh(animate);

        // ── 다음 화석 / 위험 ───────────────────────────────────────────

        private void UpdateNextFruit(int tier)
        {
            var table = _gm != null ? _gm.FruitTable : null;
            if (nextFruitText != null)
                nextFruitText.text = table != null && tier < table.Count ? table.Get(tier).displayName : string.Empty;
            if (nextFruitIcon != null && fruitIcons != null && tier >= 0 && tier < fruitIcons.Length)
                nextFruitIcon.sprite = fruitIcons[tier];
        }

        private void OnDanger(bool danger)
        {
            if (dangerIndicator != null && dangerIndicator.activeSelf != danger)
                dangerIndicator.SetActive(danger);
        }

        // ── 스킬 ───────────────────────────────────────────────────────

        private void UpdateSkillSlots()
        {
            if (skillSlots == null) return;
            for (int i = 0; i < skillSlots.Length; i++)
            {
                var slot = skillSlots[i];
                if (slot == null) continue;
                E_FOSSIL_SKILL s = _skills != null ? _skills.GetHeld(i) : E_FOSSIL_SKILL.NONE;
                bool has = s != E_FOSSIL_SKILL.NONE;
                if (slot.label != null) slot.label.text = has ? SuikaSkillInfo.Name(s) : string.Empty;
                if (slot.button != null) slot.button.interactable = has;
                if (slot.icon != null && skillIcons != null)
                {
                    int idx = (int)s;
                    slot.icon.enabled = has && idx >= 0 && idx < skillIcons.Length && skillIcons[idx] != null;
                    if (slot.icon.enabled) slot.icon.sprite = skillIcons[idx];
                }
            }
        }

        private void OnPendingChanged(E_FOSSIL_SKILL pending)
        {
            if (skillSlots != null && _skills != null)
            {
                for (int i = 0; i < skillSlots.Length; i++)
                {
                    var mark = skillSlots[i]?.selectedMark;
                    if (mark != null) mark.SetActive(pending != E_FOSSIL_SKILL.NONE && _skills.GetHeld(i) == pending);
                }
            }

            if (targetHintText == null) return;
            switch (pending)
            {
                case E_FOSSIL_SKILL.HAMMER:     targetHintText.text = "부술 화석을 탭하세요"; break;
                case E_FOSSIL_SKILL.MAGNET:     targetHintText.text = "끌어당길 화석을 탭하세요"; break;
                case E_FOSSIL_SKILL.EVOLVE:     targetHintText.text = "업그레이드할 화석을 탭하세요"; break;
                case E_FOSSIL_SKILL.EARTHQUAKE: targetHintText.text = "흔들 방향으로 스와이프하세요"; break;
                default:                        targetHintText.text = string.Empty; break;
            }
            targetHintText.gameObject.SetActive(pending != E_FOSSIL_SKILL.NONE);
        }

        /// <summary>스킬 버튼 (OnClick에 슬롯 번호 전달). 대기 중인 같은 버튼을 다시 누르면 취소.</summary>
        public void OnClickSkillSlot(int slotIndex) => _skills?.ActivateSlot(slotIndex);

        /// <summary>(구) 아이템 슬롯 버튼 — 스킬 버튼으로 동작</summary>
        public void OnClickItemSlot(int slotIndex) => OnClickSkillSlot(slotIndex);

        // ── 알림 ───────────────────────────────────────────────────────

        private void ShowNotice(string msg)
        {
            if (noticeText == null) return;
            bool on = !string.IsNullOrEmpty(msg);
            noticeText.gameObject.SetActive(on);
            if (on)
            {
                noticeText.text = msg;
                _noticeTimer = noticeSeconds;
            }
        }

        private void OnChain(int chain, Vector3 pos) => ShowNotice($"{chain}연쇄!");
        private void OnTimeBonus(float sec) => ShowNotice($"+{Mathf.RoundToInt(sec)}초");
        private void OnLifeLost(Vector3 pos) => ShowNotice("화석이 떨어졌어요! 라이프 -1");
        private void OnDinoRevival(Vector3 pos) => ShowNotice("공룡 부활!");
        private void OnTimeWarning() => ShowNotice("10초 남았어요!");
        private void OnQuakeWarning(float sec) => ShowNotice("지진 경보! 균형을 잡으세요");
        private void OnQuakeEnd(bool survived, int bonus)
        {
            if (survived && bonus > 0) ShowNotice($"버티기 성공! +{bonus}");
        }

        // ── 이벤트 ─────────────────────────────────────────────────────

        // 점수가 오르면 미적립 코인도 늘어나므로 재화 라벨도 갱신(카운팅 연출 포함)
        private void OnScoreChanged(int score)
        {
            UpdateScore(score);
            RefreshGold(true);
        }

        private void OnGameOver()
        {
            if (_gm != null && bestScoreText != null) bestScoreText.text = _gm.BestScore.ToString();
            _skills?.CancelPending();
        }

        public void OnClickPause()
        {
            SuikaGameManager.Instance?.Pause();
            UIManager.Instance.PushUI(UI_TYPE.InGamePause);
        }

        public void OnClickRotateLeft() => SuikaGameManager.Instance?.CameraRig?.RotateBy(-rotateStepDeg);
        public void OnClickRotateRight() => SuikaGameManager.Instance?.CameraRig?.RotateBy(rotateStepDeg);
    }
}
