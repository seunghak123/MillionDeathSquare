using System;
using System.Collections.Generic;
using UnityEngine;
using Seunghak.Common;
using Seunghak.Manager;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// Fossil Merge 한 판의 흐름 총괄 (기획: 민수님, 기획서 2~8장).
    ///
    ///  상태: READY → (첫 드롭) PLAYING ⇄ PAUSED / LEVEL_UP → GAME_OVER → (이어하기) PLAYING 또는 (다시하기) READY
    ///  - 타이머 120초 + 점수 마일스톤 시간 연장, 라이프 3 (화석이 발굴대 밖으로 떨어질 때마다 -1)
    ///  - 합체 점수 n(n+1)/2×10 × 연쇄 배율 × 모드 배율(지각 변동 1.5) × 굴리기 배율
    ///  - 합체 점수만큼 경험치 → 레벨업 시 스킬 3개 중 1개 선택 (SuikaSkillSystem)
    ///  - 최종 단계(공룡 골격) 완성 → 공룡 부활: 1.5초 타이머 정지, 작은 화석 정리, 보너스 점수/시간, 경험치 가득
    ///  - 판 종료 시 코인 = 점수 ÷ 50 (지각 변동 ×1.5), 이어하기는 판당 1회(보상형 광고)
    /// 플랫폼(Toss 등) 연동은 SuikaPlatformBridge, 햅틱은 SuikaHaptics가 담당한다.
    /// (클래스/네임스페이스 이름은 초기 프로토타입의 Suika를 유지한다)
    /// </summary>
    public class SuikaGameManager : UnitySingleton<SuikaGameManager>
    {
        [Tooltip("false면 Start에서 자동 시작하지 않고 외부(SuikaTestBootstrap / InGameSceneController)가 시작")]
        [SerializeField] private bool autoInitialize = true;
        [Tooltip("테스트용: 해금 전에도 지각 변동 모드 선택 허용")]
        [SerializeField] private bool allowLockedModes = false;

        [Header("참조")]
        [SerializeField] private SuikaFruitTable fruitTable;
        [SerializeField] private SuikaPlate plate;
        [SerializeField] private SuikaCameraRig cameraRig;
        [SerializeField] private SuikaSkillSystem skillSystem;
        [SerializeField] private SuikaQuakeDirector quakeDirector;
        [SerializeField] private Transform fruitRoot;

        [Header("밸런스 (기획서 14장)")]
        [SerializeField] private SuikaRules rules = new SuikaRules();

        [Header("드롭 높이")]
        [Tooltip("발굴대 위 기본 드롭 높이")]
        [SerializeField] private float baseDropHeight = 4f;
        [Tooltip("가장 높이 쌓인 화석 위로 띄울 여유")]
        [SerializeField] private float dropMarginAboveStack = 2f;

        [Header("장외 낙하")]
        [Tooltip("화석이 발굴대 높이보다 이만큼 아래로 떨어지면 라이프 -1")]
        [SerializeField] private float fallDepth = 1.5f;
        [Tooltip("이만큼 아래로 내려가면 '떨어지는 중' 경고 (햅틱 + OnDanger)")]
        [SerializeField] private float dangerDepth = 0.35f;

        [Header("사운드 키 (SoundManager 등록 키, 비우면 재생 안 함)")]
        [SerializeField] private bool playGameBgm = true;
        [SerializeField] private string sfxDrop = SoundKeys.SUIKA_DROP;
        [SerializeField] private string sfxMerge = SoundKeys.SUIKA_MERGE;
        [SerializeField] private string sfxMergeBig = SoundKeys.SUIKA_MERGE_BIG;
        [SerializeField] private string sfxMaxClear = SoundKeys.SUIKA_MAX_CLEAR;
        [SerializeField] private string sfxGameOver = SoundKeys.SUIKA_GAME_OVER;

        [Header("풀")]
        [SerializeField] private int prewarmPerTier = 4;

        private const string DIFFICULTY_PREF_KEY = "FossilMode";
        private const float SETTLED_SPEED_SQR = 0.6f * 0.6f;
        private const float STACK_STEP = 0.5f;
        private const float TIME_WARNING_SECONDS = 10f;

        private SuikaFruitPool _pool;
        private readonly List<SuikaFruit> _activeFruits = new List<SuikaFruit>(64);
        private readonly E_FOSSIL_SKILL[] _loadoutBuffer = new E_FOSSIL_SKILL[SuikaProgress.LOADOUT_SLOTS];
        private E_SUIKA_GAME_STATE _state = E_SUIKA_GAME_STATE.NONE;
        private E_SUIKA_GAME_STATE _stateBeforePause;
        private E_SUIKA_DIFFICULTY _difficulty;
        private bool _initialized;

        private int _nextTier;
        private int _dropCount;
        private float _stackTop;
        private bool _dangerActive;

        private int _milestoneIndex;
        private int _nextMilestoneScore;
        private float _nextMilestoneBonus;
        private bool _timeWarned;

        private SuikaFruit _dinoFruit;
        private float _dinoTimer;
        private bool _levelUpPending;

        private int _coinsCredited;
        private bool _doubleCoinUsed;

        // ── 이벤트 ─────────────────────────────────────────────────────

        public event Action<int> OnScoreChanged;
        public event Action<E_SUIKA_GAME_STATE> OnStateChanged;
        public event Action OnGameOver;
        public event Action<int> OnNextTierChanged;
        /// <summary>(새로 생긴 단계, 위치). 최종 단계 2개가 닿아 사라지면 단계 = Count</summary>
        public event Action<int, Vector3> OnMerged;
        /// <summary>(연쇄 수 2 이상, 위치)</summary>
        public event Action<int, Vector3> OnChain;
        /// <summary>화석이 발굴대 밖으로 떨어지기 시작함 (true) / 위험 해소 (false)</summary>
        public event Action<bool> OnDanger;
        /// <summary>라이프 변경 (남은 라이프)</summary>
        public event Action<int> OnLivesChanged;
        /// <summary>장외 낙하로 라이프 -1 (떨어진 위치)</summary>
        public event Action<Vector3> OnLifeLost;
        /// <summary>시간 보너스 (추가 초)</summary>
        public event Action<float> OnTimeBonus;
        /// <summary>남은 10초 경고</summary>
        public event Action OnTimeWarning;
        /// <summary>경험치 변경 (현재, 필요량, 레벨)</summary>
        public event Action<float, float, int> OnXpChanged;
        /// <summary>레벨업 → 스킬 선택 시작</summary>
        public event Action OnLevelUp;
        /// <summary>공룡 부활 시작 (위치) / 종료</summary>
        public event Action<Vector3> OnDinoRevival;
        public event Action OnDinoRevivalEnd;
        public event Action<E_SUIKA_DIFFICULTY> OnDifficultyChanged;
        /// <summary>일반 모드 해금 점수를 처음 넘어 지각 변동 모드가 열림</summary>
        public event Action OnTectonicUnlocked;

        // ── 상태 조회 ─────────────────────────────────────────────────

        public E_SUIKA_GAME_STATE GameState => _state;
        public E_SUIKA_GAMEOVER_CAUSE GameOverCause { get; private set; }
        public SuikaRules Rules => rules;
        public bool IsInitialized => _initialized;
        public E_SUIKA_DIFFICULTY Difficulty => _difficulty;
        public bool IsTectonic => _difficulty == E_SUIKA_DIFFICULTY.TECTONIC;
        public float RewardMultiplier => IsTectonic ? rules.tectonicRewardMultiplier : 1f;

        public int Score { get; private set; }
        /// <summary>현재 모드 최고점</summary>
        public int BestScore { get; private set; }
        public float TimeLeft { get; private set; }
        public int Lives { get; private set; }
        public int Level { get; private set; }
        public float Xp { get; private set; }
        public float XpToNext { get; private set; }
        public int LastChain { get; private set; }
        public int HighestTierThisRun { get; private set; }
        public int NextTier => _nextTier;
        public int DropCount => _dropCount;
        public bool IsDinoRevivalActive => _dinoFruit != null;
        public bool IsTimerRunning => _state == E_SUIKA_GAME_STATE.PLAYING && _dinoFruit == null;
        public bool IsFossilCapReached => _activeFruits.Count >= rules.maxFossils;
        /// <summary>다음 시간 보너스까지 필요한 누적 점수</summary>
        public int NextMilestoneScore => _nextMilestoneScore;

        public bool HasContinued { get; private set; }
        /// <summary>이어하기 (판당 1회, 보상형 광고)</summary>
        public bool CanContinue => _state == E_SUIKA_GAME_STATE.GAME_OVER && !HasContinued;

        /// <summary>이번 판에서 지갑에 적립된 코인 (판 종료 시 적립, 이어하기 후 재적립분 포함)</summary>
        public int GoldEarnedLast => _coinsCredited;
        /// <summary>이번 판에 현재 모드 최고 기록을 갱신했는지 (이어하기 전 종료 포함)</summary>
        public bool IsNewBestLast { get; private set; }
        /// <summary>점수로 확정됐지만 아직 지갑에 적립되지 않은 코인 (HUD 표시용)</summary>
        public int GoldEarnedPending => Mathf.Max(0, TotalCoinsForScore() - _coinsCredited);
        /// <summary>결과 화면: 광고 보고 코인 2배 (판당 1회)</summary>
        public bool CanDoubleGold => _state == E_SUIKA_GAME_STATE.GAME_OVER && !_doubleCoinUsed && _coinsCredited > 0;

        // 기존 GameOverPopup 호환: 골드 부활은 기획에서 빠짐 (이어하기는 광고만)
        public bool CanReviveWithGold => false;
        public bool CanReviveWithAd => CanContinue;
        public int ReviveGoldCost => 0;

        public SuikaFruitTable FruitTable => fruitTable;
        public SuikaPlate Plate => plate;
        public SuikaCameraRig CameraRig => cameraRig;
        public SuikaSkillSystem Skills => skillSystem;
        public SuikaQuakeDirector Quakes => quakeDirector;
        public IReadOnlyList<SuikaFruit> ActiveFruits => _activeFruits;

        /// <summary>현재 드롭 높이 (월드 Y)</summary>
        public float DropHeight
        {
            get
            {
                float plateY = plate != null ? plate.transform.position.y : 0f;
                return Mathf.Max(plateY + baseDropHeight, _stackTop + dropMarginAboveStack);
            }
        }

        public bool CanDrop =>
            (_state == E_SUIKA_GAME_STATE.READY || _state == E_SUIKA_GAME_STATE.PLAYING) && !IsFossilCapReached;

        // ── 초기화 ─────────────────────────────────────────────────────

        protected override void InitSingleton()
        {
            _difficulty = (E_SUIKA_DIFFICULTY)PlayerPrefs.GetInt(DIFFICULTY_PREF_KEY, (int)E_SUIKA_DIFFICULTY.NORMAL);
            if (!IsModeAvailable(_difficulty)) _difficulty = E_SUIKA_DIFFICULTY.NORMAL;
            ReloadBestScore();
        }

        public void ReloadBestScore() => BestScore = SuikaProgress.GetBest(_difficulty);

        private void Start()
        {
            if (autoInitialize) Initialize();
        }

        /// <summary>풀 생성 + READY 진입. 여러 번 불러도 한 번만 초기화된다.</summary>
        public void Initialize()
        {
            if (_initialized) return;
            if (fruitTable == null || fruitTable.Count == 0)
            {
                Debug.LogError("[SuikaGameManager] 화석 테이블이 비어 있습니다. Tools/Fossil Merge 메뉴로 생성하세요.");
                return;
            }
            if (fruitRoot == null) fruitRoot = transform;
            if (skillSystem == null) skillSystem = GetComponent<SuikaSkillSystem>();
            if (quakeDirector == null) quakeDirector = GetComponent<SuikaQuakeDirector>();
            if (skillSystem != null)
            {
                skillSystem.OnOfferClosed -= OnLevelUpChosen;
                skillSystem.OnOfferClosed += OnLevelUpChosen;
            }
            if (quakeDirector != null)
            {
                quakeDirector.OnQuakeEnd -= OnQuakeEnd;
                quakeDirector.OnQuakeEnd += OnQuakeEnd;
            }

            _pool = new SuikaFruitPool(fruitTable, fruitRoot);
            _pool.Prewarm(prewarmPerTier);

            _initialized = true;
            ResetRun();
        }

        // OnDestroy는 정의하지 않는다 — UnitySingleton의 private OnDestroy(인스턴스 해제)를 가리게 된다.
        // skillSystem 구독은 같은 오브젝트 수명이라 해제가 필요 없다.

        // ── 모드 ───────────────────────────────────────────────────────

        /// <summary>모드를 고를 수 있는지 (지각 변동: 해금 + 기울기 센서 필요)</summary>
        public bool IsModeAvailable(E_SUIKA_DIFFICULTY mode)
        {
            if (mode == E_SUIKA_DIFFICULTY.NORMAL) return true;
            return (allowLockedModes || SuikaProgress.IsTectonicUnlocked) && SuikaTiltInput.IsSupported;
        }

        /// <summary>모드 선택. 플레이 중이 아니면 즉시 발굴대에 적용, 플레이 중이면 다음 판부터.</summary>
        public bool SetDifficulty(E_SUIKA_DIFFICULTY mode)
        {
            if (!IsModeAvailable(mode)) return false;
            if (_difficulty != mode)
            {
                _difficulty = mode;
                PlayerPrefs.SetInt(DIFFICULTY_PREF_KEY, (int)mode);
                ReloadBestScore();
                OnDifficultyChanged?.Invoke(mode);
            }
            if (_state == E_SUIKA_GAME_STATE.READY || _state == E_SUIKA_GAME_STATE.NONE)
                plate?.SetDifficulty(_difficulty);
            return true;
        }

        // ── 상태 전환 ─────────────────────────────────────────────────

        private void SetState(E_SUIKA_GAME_STATE next)
        {
            if (_state == next) return;
            _state = next;
            OnStateChanged?.Invoke(next);
        }

        private void ResetRun()
        {
            SuikaPlatformBridge.NotifyRunStarted(); // 전면 광고 판 수 집계
            for (int i = _activeFruits.Count - 1; i >= 0; i--)
                _pool.Release(_activeFruits[i]);
            _activeFruits.Clear();

            Score = 0;
            IsNewBestLast = false;
            TimeLeft = rules.startTime;
            Lives = rules.startLives;
            Level = 1;
            Xp = 0f;
            XpToNext = rules.XpToNext(Level);
            LastChain = 0;
            HighestTierThisRun = 0;
            HasContinued = false;
            GameOverCause = E_SUIKA_GAMEOVER_CAUSE.NONE;
            _coinsCredited = 0;
            _doubleCoinUsed = false;
            _dropCount = 0;
            _milestoneIndex = 0;
            rules.GetMilestone(0, out _nextMilestoneScore, out _nextMilestoneBonus);
            _timeWarned = false;
            _dinoFruit = null;
            _dinoTimer = 0f;
            _levelUpPending = false;
            _stackTop = plate != null ? plate.transform.position.y : 0f;
            SetDanger(false);

            if (!IsModeAvailable(_difficulty)) _difficulty = E_SUIKA_DIFFICULTY.NORMAL;
            plate?.SetDifficulty(_difficulty); // 내부에서 ResetPlate
            quakeDirector?.Stop();
            cameraRig?.SetFocusHeight(0f);
            ReloadBestScore();

            // 로비에서 장착한 스킬(1회용)을 들고 시작
            int n = SuikaProgress.TakeLoadout(_loadoutBuffer);
            skillSystem?.ResetForRun(rules.maxHeldSkills, _loadoutBuffer, n);

            RollNextTier();
            OnScoreChanged?.Invoke(Score);
            OnLivesChanged?.Invoke(Lives);
            OnXpChanged?.Invoke(Xp, XpToNext, Level);
            SetState(E_SUIKA_GAME_STATE.READY);

            if (playGameBgm) SoundManager.Instance?.PlayBGM(SoundKeys.BGM_GAME);
        }

        /// <summary>새 판 시작 (인게임 진입 / 다시하기). 초기화 전이면 초기화부터 한다.</summary>
        public void RestartGame()
        {
            Time.timeScale = 1f;
            if (!_initialized) { Initialize(); return; }
            if (_state == E_SUIKA_GAME_STATE.PLAYING || _state == E_SUIKA_GAME_STATE.PAUSED || _state == E_SUIKA_GAME_STATE.LEVEL_UP)
            {
                SuikaAnalytics.RunAbandoned(this);
                SuikaPlatformBridge.NotifyGameplayAborted();
            }
            ResetRun();
        }

        /// <summary>인게임을 떠날 때 (로비 이동). 플레이 상태만 정리하고 화석은 다음 진입 때 리셋.</summary>
        public void ExitGame()
        {
            Time.timeScale = 1f;
            if (_state == E_SUIKA_GAME_STATE.PLAYING || _state == E_SUIKA_GAME_STATE.PAUSED || _state == E_SUIKA_GAME_STATE.LEVEL_UP)
            {
                SuikaAnalytics.RunAbandoned(this);
                SuikaPlatformBridge.NotifyGameplayAborted();
            }
            quakeDirector?.Stop();
            skillSystem?.CancelPending();
            SetDanger(false);
            SetState(E_SUIKA_GAME_STATE.NONE);
        }

        public void Pause()
        {
            if (_state != E_SUIKA_GAME_STATE.PLAYING && _state != E_SUIKA_GAME_STATE.READY) return;
            _stateBeforePause = _state;
            Time.timeScale = 0f;
            SetState(E_SUIKA_GAME_STATE.PAUSED);
        }

        public void Resume()
        {
            if (_state != E_SUIKA_GAME_STATE.PAUSED) return;
            Time.timeScale = 1f;
            SetState(_stateBeforePause);
        }

        // ── 드롭 ───────────────────────────────────────────────────────

        private void RollNextTier()
        {
            _nextTier = fruitTable.PickSpawnTier();
            OnNextTierChanged?.Invoke(_nextTier);
        }

        /// <summary>드롭 대기용 화석을 꺼낸다 (물리 꺼진 미리보기 상태). 다음 화석은 새로 뽑는다.</summary>
        public SuikaFruit AcquirePreviewFruit()
        {
            if (!_initialized) return null;
            int tier = _nextTier;
            SuikaFruit fruit = _pool.Get(tier);
            fruit.Setup(this, tier, fruitTable.Get(tier));
            RollNextTier();
            return fruit;
        }

        /// <summary>미리보기 화석 반납 (재시작 등으로 드롭하지 않고 버릴 때)</summary>
        public void ReleasePreviewFruit(SuikaFruit fruit)
        {
            if (fruit != null && !fruit.IsDropped) _pool.Release(fruit);
        }

        /// <summary>드롭된 화석 등록. 첫 드롭이면 PLAYING으로 전환하고 타이머 시작.</summary>
        public void RegisterDropped(SuikaFruit fruit)
        {
            fruit.Drop();
            _activeFruits.Add(fruit);
            _dropCount++;

            if (_state == E_SUIKA_GAME_STATE.READY)
            {
                SetState(E_SUIKA_GAME_STATE.PLAYING);
                UserDataManager.Instance?.IncrementPlayCount();
                if (IsTectonic) quakeDirector?.Begin(this);
                SuikaAnalytics.RunStarted(_difficulty, skillSystem != null ? skillSystem.HeldCount : 0);
                SuikaPlatformBridge.NotifyGameStart();
            }

            SuikaHaptics.Play(SuikaHaptics.DROP);
            PlaySfx(sfxDrop);
        }

        // ── 합체 ───────────────────────────────────────────────────────

        public void RequestMerge(SuikaFruit a, SuikaFruit b)
        {
            if (_state != E_SUIKA_GAME_STATE.PLAYING) return;
            if (a.IsMerging || b.IsMerging || a.IsHeld || b.IsHeld || a.Tier != b.Tier) return;

            a.IsMerging = true;
            b.IsMerging = true;

            int tier = a.Tier;
            int chain = Mathf.Max(a.ActiveChain(rules.chainWindow), b.ActiveChain(rules.chainWindow)) + 1;
            Vector3 pos = (a.transform.position + b.transform.position) * 0.5f;
            Vector3 vel = (a.Body.velocity + b.Body.velocity) * 0.5f;

            RemoveActive(a);
            RemoveActive(b);
            _pool.Release(a);
            _pool.Release(b);

            int newTier = tier + 1;
            if (newTier >= fruitTable.Count)
            {
                // 최종 단계끼리 (보통은 공룡 부활로 사라져 일어나지 않음)
                AddScore(Mathf.RoundToInt(fruitTable.maxTierClearBonus * RewardMultiplier));
                SuikaHaptics.Play(SuikaHaptics.MAX_CLEAR);
                PlaySfx(sfxMaxClear);
                OnMerged?.Invoke(fruitTable.Count, pos);
                return;
            }

            SuikaFruit merged = SpawnFossil(newTier, pos);
            merged.SpawnAsMerged(vel, chain);

            // 점수 = 단계 점수 × 연쇄 × 모드 × 굴리기(지각 변동에서 폰을 기울인 채 합체)
            float mult = rules.ChainMultiplier(chain) * RewardMultiplier;
            if (IsTectonic && plate != null && plate.PlayerTiltDeg >= rules.rollingTiltDeg)
                mult *= rules.rollingMultiplier;
            AddScore(Mathf.RoundToInt(SuikaRules.MergeScore(newTier) * mult));

            bool firstOfRun = newTier > HighestTierThisRun;
            if (firstOfRun) HighestTierThisRun = newTier;

            LastChain = chain;
            if (chain >= 2) OnChain?.Invoke(chain, pos);

            if (chain >= 3) SuikaHaptics.Play(SuikaHaptics.CHAIN);
            else if (firstOfRun && newTier >= 2) SuikaHaptics.Play(SuikaHaptics.NEW_TIER);
            else SuikaHaptics.Play(SuikaHaptics.ForMerge(newTier, fruitTable.Count));

            PlaySfx(newTier >= fruitTable.Count / 2 ? sfxMergeBig : sfxMerge);
            OnMerged?.Invoke(newTier, pos);

            if (newTier == fruitTable.LastTier) BeginDinoRevival(merged);
        }

        private SuikaFruit SpawnFossil(int tier, Vector3 pos)
        {
            SuikaFruit f = _pool.Get(tier);
            f.Setup(this, tier, fruitTable.Get(tier));
            f.transform.position = pos;
            _activeFruits.Add(f);
            return f;
        }

        private void RemoveActive(SuikaFruit fruit)
        {
            int idx = _activeFruits.IndexOf(fruit);
            if (idx < 0) return;
            int last = _activeFruits.Count - 1;
            _activeFruits[idx] = _activeFruits[last];
            _activeFruits.RemoveAt(last);
        }

        // ── 스킬/연출이 쓰는 화석 조작 ────────────────────────────────

        /// <summary>화석 1개 제거 (망치)</summary>
        public void RemoveFruit(SuikaFruit fruit)
        {
            if (fruit == null || fruit.IsMerging) return;
            fruit.IsMerging = true;
            RemoveActive(fruit);
            _pool.Release(fruit);
        }

        /// <summary>maxTier 이하 화석을 모두 제거 (브러시, 공룡 부활, 이어하기). 제거 수 반환</summary>
        public int RemoveTiersUpTo(int maxTier)
        {
            int removed = 0;
            for (int i = _activeFruits.Count - 1; i >= 0; i--)
            {
                SuikaFruit f = _activeFruits[i];
                if (f.Tier > maxTier || f.IsHeld) continue;
                f.IsMerging = true;
                _activeFruits.RemoveAt(i);
                _pool.Release(f);
                removed++;
            }
            return removed;
        }

        /// <summary>화석을 1단계 업그레이드 (진화). 최종 단계면 불가.</summary>
        public bool EvolveFruit(SuikaFruit fruit)
        {
            if (fruit == null || fruit.IsMerging || fruit.Tier >= fruitTable.LastTier) return false;
            int newTier = fruit.Tier + 1;
            Vector3 pos = fruit.transform.position;
            Vector3 vel = fruit.Body.velocity;
            RemoveFruit(fruit);
            SuikaFruit evolved = SpawnFossil(newTier, pos);
            evolved.SpawnAsMerged(vel, 1); // 진화로 생긴 화석도 연쇄 합체를 노릴 수 있게
            if (newTier > HighestTierThisRun) HighestTierThisRun = newTier;
            OnMerged?.Invoke(newTier, pos);
            if (newTier == fruitTable.LastTier) BeginDinoRevival(evolved);
            return true;
        }

        // ── 점수 / 마일스톤 / 경험치 ─────────────────────────────────

        private void AddScore(int amount, bool gainXp = true)
        {
            if (amount <= 0) return;
            Score += amount;
            OnScoreChanged?.Invoke(Score);

            // 시간 연장 마일스톤 (한 번에 여러 개를 넘을 수도 있음)
            while (Score >= _nextMilestoneScore)
            {
                AddTime(_nextMilestoneBonus);
                _milestoneIndex++;
                rules.GetMilestone(_milestoneIndex, out _nextMilestoneScore, out _nextMilestoneBonus);
            }

            if (gainXp) AddXp(amount);
        }

        private void AddTime(float seconds)
        {
            TimeLeft += seconds;
            if (TimeLeft > TIME_WARNING_SECONDS) _timeWarned = false;
            SuikaHaptics.Play(SuikaHaptics.TIME_BONUS);
            OnTimeBonus?.Invoke(seconds);
        }

        private void AddXp(float amount)
        {
            Xp += amount;
            if (Xp >= XpToNext) _levelUpPending = true;
            OnXpChanged?.Invoke(Xp, XpToNext, Level);
        }

        private void TryOpenLevelUp()
        {
            if (!_levelUpPending || _state != E_SUIKA_GAME_STATE.PLAYING || _dinoFruit != null) return;
            if (skillSystem == null) { _levelUpPending = false; return; }
            _levelUpPending = false;

            // 선택하는 동안 게임/타이머 정지
            Time.timeScale = 0f;
            SetState(E_SUIKA_GAME_STATE.LEVEL_UP);
            SuikaHaptics.Play(SuikaHaptics.LEVEL_UP);
            skillSystem.OpenOffer();
            OnLevelUp?.Invoke();
        }

        private void OnLevelUpChosen()
        {
            if (_state != E_SUIKA_GAME_STATE.LEVEL_UP) return;
            Xp -= XpToNext;
            Level++;
            XpToNext = rules.XpToNext(Level);
            if (Xp >= XpToNext) _levelUpPending = true; // 넘친 경험치로 연속 레벨업
            OnXpChanged?.Invoke(Xp, XpToNext, Level);
            Time.timeScale = 1f;
            SetState(E_SUIKA_GAME_STATE.PLAYING);
        }

        // ── 공룡 부활 ─────────────────────────────────────────────────

        private void BeginDinoRevival(SuikaFruit skeleton)
        {
            _dinoFruit = skeleton;
            _dinoTimer = rules.dinoRevivalDuration;
            skeleton.Hold();
            SuikaAnalytics.DinoRevived(_difficulty);
            SuikaHaptics.Play(SuikaHaptics.DINO);
            PlaySfx(sfxMaxClear);
            OnDinoRevival?.Invoke(skeleton.transform.position);
        }

        private void EndDinoRevival()
        {
            SuikaFruit dino = _dinoFruit;
            _dinoFruit = null;

            // 작은 화석 날려버리기 (라이프는 깎이지 않음) + 공룡은 사라짐
            RemoveTiersUpTo(rules.dinoClearMaxTier);
            if (dino != null)
            {
                dino.Release();
                RemoveFruit(dino);
            }

            AddScore(Mathf.RoundToInt(rules.dinoBonusScore * RewardMultiplier), gainXp: false);
            AddTime(rules.dinoBonusTime);

            // 경험치 게이지 가득 → 즉시 스킬 선택
            Xp = Mathf.Max(Xp, XpToNext);
            _levelUpPending = true;
            OnXpChanged?.Invoke(Xp, XpToNext, Level);
            OnDinoRevivalEnd?.Invoke();
        }

        // ── 매 프레임: 타이머 / 장외 낙하 / 높이 ─────────────────────

        private void Update()
        {
            if (!_initialized) return;
            if (_state != E_SUIKA_GAME_STATE.PLAYING && _state != E_SUIKA_GAME_STATE.READY) return;

            float dt = Time.deltaTime;

            if (_state == E_SUIKA_GAME_STATE.PLAYING)
            {
                if (_dinoFruit != null)
                {
                    _dinoTimer -= dt; // 부활 연출 동안 타이머 정지
                    if (_dinoTimer <= 0f) EndDinoRevival();
                }
                else
                {
                    TimeLeft -= dt;
                    quakeDirector?.Tick(dt);
                    if (!_timeWarned && TimeLeft <= TIME_WARNING_SECONDS)
                    {
                        _timeWarned = true;
                        OnTimeWarning?.Invoke();
                    }
                    if (TimeLeft <= 0f)
                    {
                        TimeLeft = 0f;
                        TriggerGameOver(E_SUIKA_GAMEOVER_CAUSE.TIME_OUT);
                        return;
                    }
                }
            }

            UpdateFossils();
            if (_state == E_SUIKA_GAME_STATE.PLAYING) TryOpenLevelUp();
        }

        private void UpdateFossils()
        {
            float plateY = plate != null ? plate.transform.position.y : 0f;
            float killY = plateY - fallDepth;
            float dangerY = plateY - dangerDepth;
            float top = plateY;
            bool danger = false;

            for (int i = _activeFruits.Count - 1; i >= 0; i--)
            {
                SuikaFruit f = _activeFruits[i];
                Vector3 p = f.transform.position;
                if (p.y < killY)
                {
                    LoseLife(f);
                    if (_state != E_SUIKA_GAME_STATE.PLAYING && _state != E_SUIKA_GAME_STATE.READY) return;
                    continue;
                }
                if (p.y < dangerY) { danger = true; continue; }
                // 튀거나 굴러다니는 화석은 높이 계산에서 제외 — 카메라/드롭 높이가 출렁이지 않게
                if (!f.HasLanded || f.Body.velocity.sqrMagnitude > SETTLED_SPEED_SQR) continue;
                float fruitTop = p.y + f.Radius;
                if (fruitTop > top) top = fruitTop;
            }

            // 계단식으로만 갱신 (STACK_STEP 단위) — 미세한 높이 변화로 화면이 흔들려 보이지 않게
            float stepped = plateY + Mathf.Ceil((top - plateY) / STACK_STEP) * STACK_STEP;
            if (!Mathf.Approximately(stepped, _stackTop))
            {
                _stackTop = stepped;
                cameraRig?.SetFocusHeight(Mathf.Max(0f, stepped - plateY - 1f));
            }
            SetDanger(danger);
        }

        private void LoseLife(SuikaFruit fallen)
        {
            Vector3 pos = fallen.transform.position;
            RemoveFruit(fallen);
            Lives = Mathf.Max(0, Lives - 1);
            SuikaHaptics.Play(SuikaHaptics.LIFE_LOST);
            SuikaAnalytics.LifeLost(Lives, HighestTierThisRun);
            OnLifeLost?.Invoke(pos);
            OnLivesChanged?.Invoke(Lives);
            if (Lives <= 0 && _state == E_SUIKA_GAME_STATE.PLAYING)
                TriggerGameOver(E_SUIKA_GAMEOVER_CAUSE.LIVES_OUT);
        }

        private void SetDanger(bool danger)
        {
            if (_dangerActive == danger) return;
            _dangerActive = danger;
            if (danger) SuikaHaptics.Play(SuikaHaptics.DANGER);
            OnDanger?.Invoke(danger);
        }

        // 지진 버티기 성공 보너스 (지각 변동 모드). 보너스 값은 SuikaQuakeDirector 구간 설정 × 모드 배율
        private void OnQuakeEnd(bool survived, int bonus)
        {
            if (!survived || _state != E_SUIKA_GAME_STATE.PLAYING) return;
            SuikaAnalytics.QuakeSurvived();
            if (bonus > 0) AddScore(Mathf.RoundToInt(bonus * RewardMultiplier));
        }

        // ── 판 종료 / 이어하기 ───────────────────────────────────────

        private void TriggerGameOver(E_SUIKA_GAMEOVER_CAUSE cause)
        {
            GameOverCause = cause;
            quakeDirector?.Stop();
            skillSystem?.CancelPending();

            // 공룡 부활 연출 중이었다면 바로 마무리 (보너스는 지급)
            if (_dinoFruit != null) EndDinoRevival();
            _levelUpPending = false;

            bool newBest = Score > BestScore;
            if (newBest) IsNewBestLast = true;
            if (SuikaProgress.RecordResult(_difficulty, Score, rules.tectonicUnlockScore))
            {
                SuikaAnalytics.ModeUnlocked(Score);
                OnTectonicUnlocked?.Invoke();
            }
            ReloadBestScore();
            if (Score > BestScore) BestScore = Score;

            CreditPendingCoins();

            SetState(E_SUIKA_GAME_STATE.GAME_OVER);
            SuikaHaptics.Play(SuikaHaptics.GAME_OVER);
            PlaySfx(sfxGameOver);
            SuikaPlatformBridge.NotifyGameOver(Score, BestScore, _difficulty);
            SuikaAnalytics.RunEnded(this, newBest);
            OnGameOver?.Invoke();
        }

        private int TotalCoinsForScore() =>
            Mathf.FloorToInt(Score / Mathf.Max(1f, rules.coinDivisor) * RewardMultiplier);

        // 미적립 코인을 지갑에 적립. 이어하기 후 다시 끝나면 그 사이 늘어난 점수분만 추가 적립된다.
        private void CreditPendingCoins()
        {
            int pending = GoldEarnedPending;
            if (pending <= 0) return;
            _coinsCredited += pending;
            UserDataManager.Instance?.AddGold(pending);
        }

        /// <summary>코인 2배: 이번 판 적립 코인만큼 한 번 더 지급할 양을 반환하고 사용 처리 (지급은 호출부)</summary>
        public int ConsumeDoubleGoldBonus()
        {
            if (!CanDoubleGold) return 0;
            _doubleCoinUsed = true;
            return _coinsCredited;
        }

        /// <summary>
        /// 이어하기 (판당 1회, 광고 시청은 호출부 — SuikaPlatformBridge.ReviveWithAdAsync).
        ///  - 라이프 소진: 라이프 +1 + 발굴 브러시 자동 발동
        ///  - 시간 소진: +30초
        /// </summary>
        public void Continue()
        {
            if (!CanContinue) return;
            HasContinued = true;
            SuikaAnalytics.Continued(GameOverCause);

            if (GameOverCause == E_SUIKA_GAMEOVER_CAUSE.LIVES_OUT)
            {
                Lives += rules.continueLives;
                RemoveTiersUpTo(rules.brushMaxTier);
                OnLivesChanged?.Invoke(Lives);
            }
            else
            {
                TimeLeft += rules.continueTimeBonus;
                _timeWarned = TimeLeft <= TIME_WARNING_SECONDS;
                OnTimeBonus?.Invoke(rules.continueTimeBonus);
            }

            GameOverCause = E_SUIKA_GAMEOVER_CAUSE.NONE;
            plate?.ResetPlate();
            SetDanger(false);
            Time.timeScale = 1f;
            SetState(E_SUIKA_GAME_STATE.PLAYING);
            if (IsTectonic) quakeDirector?.Begin(this);
            SuikaHaptics.Play(SuikaHaptics.REVIVE);
            SuikaPlatformBridge.NotifyGameStart();
            if (playGameBgm) SoundManager.Instance?.PlayBGM(SoundKeys.BGM_GAME);
        }

        /// <summary>기존 UI 호환 — 광고 이어하기만 지원</summary>
        public void Revive(bool viaAd)
        {
            if (viaAd) Continue();
        }

        // ── 아이템 (구 상점 아이템 슬롯 호환) ────────────────────────

        /// <summary>인게임 아이템 효과 처리기. 효과를 적용했으면 true (HUD가 그때만 보유 수량을 차감)</summary>
        public Func<Seunghak.Data.ShopItemData, bool> ItemUseHandler { get; set; }

        public bool TryUseItem(Seunghak.Data.ShopItemData item)
        {
            if (item == null || _state != E_SUIKA_GAME_STATE.PLAYING || ItemUseHandler == null) return false;
            return ItemUseHandler(item);
        }

        private static void PlaySfx(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            SoundManager.Instance?.PlaySFX(key);
        }
    }
}
