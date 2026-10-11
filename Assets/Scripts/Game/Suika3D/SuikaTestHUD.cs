using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// [테스트 전용] OnGUI 임시 HUD — FossilMergeTest 씬에서 Fossil Merge 전체 루프를 확인/튜닝하기 위한 화면.
    /// 출시 UI는 기존 UI 프레임워크(InGameHUDWindow / LevelUpPopup / GameOverPopup / GameStartWindow)를 쓴다.
    /// (OnGUI는 GC를 만들므로 출시 빌드에서는 쓰지 않는다)
    ///
    ///   상단: 점수 / 최고 / 남은 시간 / 라이프 / 레벨·경험치 / 다음 화석 / 모드 / 일시정지
    ///   하단: 보유 스킬 4칸 / 회전 ◀ ▶ / 모드 전환 / 재시작
    ///   READY: 로비 대용 장착 패널 (코인 확정 구매 / 광고 랜덤)
    ///   레벨업: 스킬 3개 중 1개 / 광고 다시 뽑기 / 가득 차면 버릴 스킬 선택
    ///   판 종료: 광고 이어하기(5초) / 코인 2배 / 다시 하기 / 리더보드 / 공유 / 친구 초대
    /// </summary>
    public class SuikaTestHUD : MonoBehaviour
    {
        [SerializeField] private SuikaGameManager manager;
        [SerializeField] private float rotateStepDeg = 45f;
        [Tooltip("판 종료 후 결과창까지 지연 (떨어지는 연출 감상)")]
        [SerializeField] private float gameOverPanelDelay = 0.8f;
        [SerializeField] private float continueCountdown = 5f;

        private string _toast;
        private float _toastTimer;
        private bool _showGameOverPanel;
        private float _continueTimer;
        private bool _busy;
        private string _resultMsg;

        private GUIStyle _label, _small, _bigLabel, _button, _panel;
        private float _styleScale = -1f;

        private void Awake()
        {
            if (manager == null) manager = SuikaGameManager.Instance;
        }

        private void OnEnable()
        {
            if (manager == null) return;
            manager.OnGameOver += OnGameOver;
            manager.OnStateChanged += OnStateChanged;
            manager.OnMerged += OnMerged;
            manager.OnChain += OnChain;
            manager.OnTimeBonus += OnTimeBonus;
            manager.OnLifeLost += OnLifeLost;
            manager.OnDinoRevival += OnDino;
            manager.OnTectonicUnlocked += OnUnlocked;
        }

        private void OnDisable()
        {
            if (manager == null) return;
            manager.OnGameOver -= OnGameOver;
            manager.OnStateChanged -= OnStateChanged;
            manager.OnMerged -= OnMerged;
            manager.OnChain -= OnChain;
            manager.OnTimeBonus -= OnTimeBonus;
            manager.OnLifeLost -= OnLifeLost;
            manager.OnDinoRevival -= OnDino;
            manager.OnTectonicUnlocked -= OnUnlocked;
        }

        private void Start()
        {
            var q = manager != null ? manager.Quakes : null;
            if (q != null)
            {
                q.OnWarning += _ => Toast("지진 경보! 균형을 잡으세요");
                q.OnQuakeEnd += (ok, bonus) => { if (ok && bonus > 0) Toast($"버티기 성공! +{bonus}"); };
            }
        }

        private void OnStateChanged(E_SUIKA_GAME_STATE state)
        {
            if (state != E_SUIKA_GAME_STATE.GAME_OVER) _showGameOverPanel = false;
        }

        private void OnMerged(int tier, Vector3 pos)
        {
            var table = manager.FruitTable;
            if (tier < table.Count && tier == manager.HighestTierThisRun && tier >= 4)
                Toast($"{table.Get(tier).displayName} 발굴!");
        }

        private void OnChain(int chain, Vector3 pos) => Toast($"{chain}연쇄!");
        private void OnTimeBonus(float s) => Toast($"+{Mathf.RoundToInt(s)}초");
        private void OnLifeLost(Vector3 p) => Toast("화석이 떨어졌어요! 라이프 -1");
        private void OnDino(Vector3 p) => Toast("공룡 부활!!");
        private void OnUnlocked() => Toast("지각 변동 모드 해금!");

        private void OnGameOver()
        {
            ShowGameOverAsync().Forget();
        }

        private async UniTaskVoid ShowGameOverAsync()
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(gameOverPanelDelay), ignoreTimeScale: true);
            if (this == null || manager.GameState != E_SUIKA_GAME_STATE.GAME_OVER) return;
            _continueTimer = continueCountdown;
            _resultMsg = null;
            _showGameOverPanel = true;
        }

        private void Toast(string msg)
        {
            _toast = msg;
            _toastTimer = 1.5f;
        }

        private void Update()
        {
            if (_toastTimer > 0f) _toastTimer -= Time.unscaledDeltaTime;
            if (_showGameOverPanel && _continueTimer > 0f && !_busy) _continueTimer -= Time.unscaledDeltaTime;

            // Android 뒤로가기 / 웹 ESC → 일시정지 토글 (스킬 대기 중이면 취소)
            if (Input.GetKeyDown(KeyCode.Escape) && manager != null)
            {
                if (manager.Skills != null && manager.Skills.IsAwaitingInput) manager.Skills.CancelPending();
                else if (manager.GameState == E_SUIKA_GAME_STATE.PAUSED) manager.Resume();
                else manager.Pause();
            }
        }

        // ── 그리기 ─────────────────────────────────────────────────────

        private void EnsureStyles()
        {
            float scale = Mathf.Max(0.5f, Screen.height / 1280f);
            if (Mathf.Approximately(scale, _styleScale) && _label != null) return;
            _styleScale = scale;

            _label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(28 * scale), fontStyle = FontStyle.Bold };
            _label.normal.textColor = Color.white;
            _small = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(22 * scale), fontStyle = FontStyle.Normal, wordWrap = true };
            _bigLabel = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(52 * scale), alignment = TextAnchor.MiddleCenter };
            _button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(24 * scale), fontStyle = FontStyle.Bold, wordWrap = true };
            _panel = new GUIStyle(GUI.skin.box);
        }

        private void OnGUI()
        {
            if (manager == null) return;
            EnsureStyles();

            float w = Screen.width, h = Screen.height;
            float pad = 12f * _styleScale;
            var state = manager.GameState;

            DrawTopBar(w, h, pad);
            DrawBottomBar(w, h, pad);

            var skills = manager.Skills;
            if (skills != null && skills.IsAwaitingInput)
            {
                string hint = skills.Pending == E_FOSSIL_SKILL.EARTHQUAKE ? "흔들 방향으로 스와이프!" : $"{SuikaSkillInfo.Name(skills.Pending)}: 대상 화석을 탭하세요";
                GUI.Label(new Rect(0, h * 0.14f, w, h * 0.06f), hint, _bigLabel);
            }
            else if (state == E_SUIKA_GAME_STATE.READY)
            {
                GUI.Label(new Rect(0, h * 0.14f, w, h * 0.06f), "발굴대를 탭해서 화석을 떨어뜨리세요", _label);
                DrawLoadoutPanel(w, h, pad);
            }
            else if (manager.IsFossilCapReached)
            {
                GUI.Label(new Rect(0, h * 0.14f, w, h * 0.06f), "화석이 너무 많아요! 합체해서 줄이세요", _label);
            }

            if (_toastTimer > 0f && !string.IsNullOrEmpty(_toast))
                GUI.Label(new Rect(0, h * 0.22f, w, h * 0.08f), _toast, _bigLabel);

            if (state == E_SUIKA_GAME_STATE.PAUSED) DrawPausePanel(w, h);
            if (state == E_SUIKA_GAME_STATE.LEVEL_UP) DrawLevelUpPanel(w, h);
            if (state == E_SUIKA_GAME_STATE.GAME_OVER && _showGameOverPanel) DrawGameOverPanel(w, h);
        }

        private void DrawTopBar(float w, float h, float pad)
        {
            float topH = h * 0.12f;
            GUI.Box(new Rect(0, 0, w, topH), GUIContent.none, _panel);
            float row = topH * 0.5f;
            int sec = Mathf.CeilToInt(manager.TimeLeft);

            GUI.Label(new Rect(pad, pad * 0.5f, w * 0.35f, row), $"점수 {manager.Score}", _label);
            GUI.Label(new Rect(w * 0.35f, pad * 0.5f, w * 0.25f, row), $"{sec / 60}:{sec % 60:00}", _label);
            GUI.Label(new Rect(w * 0.55f, pad * 0.5f, w * 0.25f, row), "♥ " + manager.Lives, _label);

            // 레벨 + 경험치 바
            GUI.Label(new Rect(pad, row, w * 0.15f, row), $"Lv.{manager.Level}", _small);
            Rect bar = new Rect(w * 0.14f, row + row * 0.3f, w * 0.3f, row * 0.35f);
            GUI.Box(bar, GUIContent.none, _panel);
            float t = manager.XpToNext > 0f ? Mathf.Clamp01(manager.Xp / manager.XpToNext) : 0f;
            Color old = GUI.color;
            GUI.color = new Color(0.4f, 0.9f, 0.5f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * t, bar.height), Texture2D.whiteTexture);
            GUI.color = old;

            var table = manager.FruitTable;
            string next = table != null && manager.NextTier < table.Count ? table.Get(manager.NextTier).displayName : "-";
            GUI.Label(new Rect(w * 0.47f, row, w * 0.3f, row), $"다음: {next}", _small);
            GUI.Label(new Rect(w * 0.47f + w * 0.2f, row, w * 0.15f, row), manager.IsTectonic ? "지각변동" : "일반", _small);
            if (SuikaPlatformBridge.IsTossDevBuild)
                GUI.Label(new Rect(w * 0.75f, pad * 0.5f, w * 0.1f, row), "dev", _small);

            var st = manager.GameState;
            if (st == E_SUIKA_GAME_STATE.PLAYING || st == E_SUIKA_GAME_STATE.READY)
            {
                if (GUI.Button(new Rect(w - topH * 1.1f, pad, topH * 1.1f - pad, topH - pad * 2), "II", _button))
                    manager.Pause();
            }
        }

        private void DrawBottomBar(float w, float h, float pad)
        {
            float botH = h * 0.14f;
            float by = h - botH;
            float rowH = botH * 0.5f;
            GUI.Box(new Rect(0, by, w, botH), GUIContent.none, _panel);

            // 1행: 스킬 4칸
            var skills = manager.Skills;
            float sw = w / 4f;
            for (int i = 0; i < 4; i++)
            {
                E_FOSSIL_SKILL s = skills != null ? skills.GetHeld(i) : E_FOSSIL_SKILL.NONE;
                bool pending = skills != null && skills.IsAwaitingInput && skills.Pending == s && s != E_FOSSIL_SKILL.NONE;
                GUI.enabled = s != E_FOSSIL_SKILL.NONE && manager.GameState == E_SUIKA_GAME_STATE.PLAYING;
                string label = s == E_FOSSIL_SKILL.NONE ? "-" : (pending ? $"[{SuikaSkillInfo.Name(s)}]" : SuikaSkillInfo.Name(s));
                if (GUI.Button(new Rect(sw * i + pad * 0.5f, by + pad * 0.5f, sw - pad, rowH - pad), label, _button))
                    skills?.ActivateSlot(i);
            }
            GUI.enabled = true;

            // 2행: 회전 / 모드 / 재시작
            float bw = w / 4f;
            float ry = by + rowH;
            if (GUI.Button(new Rect(bw * 0 + pad * 0.5f, ry + pad * 0.5f, bw - pad, rowH - pad), "◀ 회전", _button))
                manager.CameraRig?.RotateBy(-rotateStepDeg);
            if (GUI.Button(new Rect(bw * 1 + pad * 0.5f, ry + pad * 0.5f, bw - pad, rowH - pad), "회전 ▶", _button))
                manager.CameraRig?.RotateBy(rotateStepDeg);

            bool tectonic = manager.IsTectonic;
            bool canTectonic = manager.IsModeAvailable(E_SUIKA_DIFFICULTY.TECTONIC);
            string modeLabel = tectonic ? "지각변동" : (canTectonic ? "일반" : "일반 (지각변동 잠김)");
            if (GUI.Button(new Rect(bw * 2 + pad * 0.5f, ry + pad * 0.5f, bw - pad, rowH - pad), modeLabel, _button))
            {
                // 모드 전환은 새 판부터 적용되므로 바로 재시작
                if (manager.SetDifficulty(tectonic ? E_SUIKA_DIFFICULTY.NORMAL : E_SUIKA_DIFFICULTY.TECTONIC))
                    manager.RestartGame();
                else
                    Toast($"일반 모드 {manager.Rules.tectonicUnlockScore:N0}점 달성 시 해금");
            }
            if (GUI.Button(new Rect(bw * 3 + pad * 0.5f, ry + pad * 0.5f, bw - pad, rowH - pad), "재시작", _button))
                manager.RestartGame();
        }

        // READY 상태에서 로비 대용: 다음 판에 들고 갈 스킬 장착 (장착분은 다음 재시작부터 적용)
        private void DrawLoadoutPanel(float w, float h, float pad)
        {
            Rect r = new Rect(w * 0.05f, h * 0.62f, w * 0.9f, h * 0.2f);
            GUI.Box(r, GUIContent.none, _panel);
            string slots = $"장착(다음 판): {SlotName(0)} / {SlotName(1)}   코인 {(Seunghak.Common.UserDataManager.Instance != null ? Seunghak.Common.UserDataManager.Instance.GetGold() : 0)}";
            GUI.Label(new Rect(r.x + pad, r.y + pad * 0.5f, r.width, r.height * 0.3f), slots, _small);

            float bw = (r.width - pad) / 5f;
            float y = r.y + r.height * 0.32f;
            for (int i = 0; i < SuikaProgress.SKILL_COUNT; i++)
            {
                var s = (E_FOSSIL_SKILL)i;
                if (GUI.Button(new Rect(r.x + pad * 0.5f + bw * i, y, bw - pad * 0.5f, r.height * 0.3f), $"{SuikaSkillInfo.Name(s)}\n{SuikaProgress.SKILL_PRICE}", _button))
                    Toast(SuikaProgress.TryBuySkill(s) ? $"{SuikaSkillInfo.Name(s)} 장착" : "코인 부족 또는 칸 없음");
            }
            GUI.enabled = !_busy && SuikaProgress.AdSkillsLeftToday > 0 && SuikaProgress.HasEmptyLoadoutSlot;
            if (GUI.Button(new Rect(r.x + pad * 0.5f, r.y + r.height * 0.66f, r.width * 0.6f, r.height * 0.3f), $"광고 보고 랜덤 스킬 ({SuikaProgress.AdSkillsLeftToday}회 남음)", _button))
                AdSkillAsync().Forget();
            GUI.enabled = true;
            if (GUI.Button(new Rect(r.x + r.width * 0.62f, r.y + r.height * 0.66f, r.width * 0.36f, r.height * 0.3f), "장착분으로 시작", _button))
                manager.RestartGame();
        }

        private static string SlotName(int i)
        {
            var s = SuikaProgress.GetLoadout(i);
            return s == E_FOSSIL_SKILL.NONE ? "빈 칸" : SuikaSkillInfo.Name(s);
        }

        private void DrawLevelUpPanel(float w, float h)
        {
            var skills = manager.Skills;
            if (skills == null) return;
            Rect r = new Rect(w * 0.08f, h * 0.2f, w * 0.84f, h * 0.6f);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.Box(r, GUIContent.none, _panel);
            float row = r.height / 7f;
            float x = r.x + 16, bw = r.width - 32;

            if (skills.Overflow != E_FOSSIL_SKILL.NONE)
            {
                GUI.Label(new Rect(r.x, r.y, r.width, row), "스킬 칸이 가득 찼어요", _bigLabel);
                GUI.Label(new Rect(x, r.y + row, bw, row), $"새 스킬: {SuikaSkillInfo.Name(skills.Overflow)} — 버릴 스킬을 고르세요", _small);
                for (int i = 0; i < skills.HeldCount; i++)
                {
                    if (GUI.Button(new Rect(x, r.y + row * (2 + i), bw, row * 0.85f), $"{SuikaSkillInfo.Name(skills.GetHeld(i))} 버리기", _button))
                        skills.ReplaceHeld(i);
                }
                if (GUI.Button(new Rect(x, r.y + row * 6, bw, row * 0.85f), "새 스킬 포기", _button))
                    skills.DiscardOverflow();
                return;
            }

            GUI.Label(new Rect(r.x, r.y, r.width, row), "레벨업! 스킬 1개를 고르세요", _bigLabel);
            for (int i = 0; i < 3; i++)
            {
                var s = skills.GetOffer(i);
                if (GUI.Button(new Rect(x, r.y + row * (1 + i * 1.5f), bw, row * 1.35f), $"{SuikaSkillInfo.Name(s)}\n{SuikaSkillInfo.Description(s)}", _button))
                    skills.ChooseOffer(i);
            }
            GUI.enabled = !_busy && skills.CanReroll && SuikaPlatformBridge.IsAdSupported;
            if (GUI.Button(new Rect(x, r.y + row * 5.8f, bw, row * 0.9f), "광고 보고 다시 뽑기", _button))
                RerollAsync().Forget();
            GUI.enabled = true;
        }

        private void DrawPausePanel(float w, float h)
        {
            Rect r = new Rect(w * 0.15f, h * 0.3f, w * 0.7f, h * 0.4f);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.Box(r, GUIContent.none, _panel);
            float bh = r.height / 5f;
            GUI.Label(new Rect(r.x, r.y, r.width, bh), "일시정지", _bigLabel);
            if (GUI.Button(new Rect(r.x + 20, r.y + bh * 1.1f, r.width - 40, bh * 0.8f), "계속하기", _button))
                manager.Resume();
            if (GUI.Button(new Rect(r.x + 20, r.y + bh * 2.1f, r.width - 40, bh * 0.8f), "다시하기", _button))
                manager.RestartGame();
            if (GUI.Button(new Rect(r.x + 20, r.y + bh * 3.1f, r.width - 40, bh * 0.8f), "기준 자세 재설정 (기울기)", _button))
                manager.Plate?.TiltInput?.Calibrate();
            if (GUI.Button(new Rect(r.x + 20, r.y + bh * 4.1f, r.width - 40, bh * 0.8f), "나가기 (플랫폼 뒤로가기)", _button))
                SuikaPlatformBridge.RequestBack();
        }

        private void DrawGameOverPanel(float w, float h)
        {
            Rect r = new Rect(w * 0.08f, h * 0.18f, w * 0.84f, h * 0.64f);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.Box(r, GUIContent.none, _panel);
            float row = r.height / 9f;
            float x = r.x + 16, bw = r.width - 32;

            string cause = manager.GameOverCause == E_SUIKA_GAMEOVER_CAUSE.TIME_OUT ? "시간 종료!" : "화석을 모두 놓쳤어요!";
            GUI.Label(new Rect(r.x, r.y, r.width, row), cause, _bigLabel);
            GUI.Label(new Rect(x, r.y + row, bw, row * 0.6f), $"점수 {manager.Score}   최고 {manager.BestScore}", _label);
            GUI.Label(new Rect(x, r.y + row * 1.6f, bw, row * 0.6f), $"코인 +{manager.GoldEarnedLast}" + (string.IsNullOrEmpty(_resultMsg) ? "" : $"  {_resultMsg}"), _label);

            GUI.enabled = !_busy;
            float y = r.y + row * 2.4f;
            if (manager.CanContinue && SuikaPlatformBridge.IsAdSupported && _continueTimer > 0f)
            {
                string what = manager.GameOverCause == E_SUIKA_GAMEOVER_CAUSE.TIME_OUT ? "+30초" : "라이프+1·브러시";
                if (GUI.Button(new Rect(x, y, bw, row * 0.85f), $"광고 보고 이어하기 ({what}) {Mathf.CeilToInt(_continueTimer)}", _button))
                    ContinueAsync().Forget();
                y += row;
            }
            if (manager.CanDoubleGold && SuikaPlatformBridge.IsAdSupported)
            {
                if (GUI.Button(new Rect(x, y, bw, row * 0.85f), "광고 보고 코인 2배", _button))
                    DoubleCoinAsync().Forget();
                y += row;
            }
            if (GUI.Button(new Rect(x, y, bw * 0.48f, row * 0.85f), "다시 하기", _button))
                manager.RestartGame();
            if (GUI.Button(new Rect(x + bw * 0.52f, y, bw * 0.48f, row * 0.85f), "로비로 (광고 규칙)", _button))
                LeaveAsync().Forget();
            y += row;
            if (SuikaPlatformBridge.SupportsLeaderboard)
            {
                if (GUI.Button(new Rect(x, y, bw, row * 0.85f), "리더보드", _button))
                    RunBusy(SuikaPlatformBridge.OpenLeaderboardAsync()).Forget();
                y += row;
            }
            if (SuikaPlatformBridge.SupportsShare)
            {
                if (GUI.Button(new Rect(x, y, bw * 0.48f, row * 0.85f), "공유", _button))
                    RunBusy(SuikaPlatformBridge.ShareAsync(manager.Score)).Forget();
            }
            if (SuikaPlatformBridge.SupportsInvite)
            {
                if (GUI.Button(new Rect(x + bw * 0.52f, y, bw * 0.48f, row * 0.85f), "친구 초대", _button))
                    InviteAsync().Forget();
            }
            GUI.enabled = true;
        }

        // ── 비동기 버튼 ────────────────────────────────────────────────

        private async UniTaskVoid ContinueAsync()
        {
            _busy = true;
            try { if (!await SuikaPlatformBridge.ReviveWithAdAsync()) Toast("광고 시청 실패"); }
            finally { _busy = false; }
        }

        private async UniTaskVoid DoubleCoinAsync()
        {
            _busy = true;
            try
            {
                if (await SuikaPlatformBridge.ShowRewardedAsync(duringRun: true, placement: "coin_double"))
                {
                    int bonus = manager.ConsumeDoubleGoldBonus();
                    if (bonus > 0) { Seunghak.Common.UserDataManager.Instance?.AddGold(bonus); _resultMsg = $"(2배 +{bonus})"; }
                }
            }
            finally { _busy = false; }
        }

        private async UniTaskVoid RerollAsync()
        {
            _busy = true;
            try { await SuikaPlatformBridge.RerollSkillsWithAdAsync(); }
            finally { _busy = false; }
        }

        private async UniTaskVoid AdSkillAsync()
        {
            _busy = true;
            try
            {
                var got = await SuikaPlatformBridge.GetRandomSkillWithAdAsync();
                if (got != E_FOSSIL_SKILL.NONE) Toast($"광고 보상: {SuikaSkillInfo.Name(got)}");
            }
            finally { _busy = false; }
        }

        private async UniTaskVoid LeaveAsync()
        {
            _busy = true;
            try { await SuikaPlatformBridge.OnLeaveResultToLobbyAsync(); }
            finally { _busy = false; }
            if (this != null) manager.RestartGame(); // 테스트 씬엔 로비가 없으므로 새 판으로 대신
        }

        private async UniTaskVoid RunBusy(UniTask<bool> task)
        {
            _busy = true;
            try { await task; }
            finally { _busy = false; }
        }

        private async UniTaskVoid InviteAsync()
        {
            _busy = true;
            try
            {
                int reward = await SuikaPlatformBridge.InviteAsync();
                Debug.Log($"[SuikaTestHUD] 친구 초대 완료 — 플랫폼 리워드 합계: {reward}");
            }
            finally { _busy = false; }
        }
    }
}
