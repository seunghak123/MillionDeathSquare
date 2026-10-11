using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Seunghak.UIManager;
using Seunghak.Game.Suika3D;
using Seunghak.Manager;
using Seunghak.Platform;
using Seunghak.Common;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 판 종료 팝업 (기획서 5.3 이어하기 + 11장 결과 화면).
    ///  - 이어하기: 광고 보고 1회 (라이프 소진 → 라이프+1·브러시 / 시간 소진 → +30초), 5초 카운트다운
    ///  - 결과: 점수·최고점·획득 코인, 광고 보고 코인 2배, 다시 하기 / 로비로
    ///  - 지각 변동 모드: 순위표가 없으므로 "최고점" 자리에 "지각 변동 내 최고 기록"을 크게 표시
    ///  - 결과 → 로비 이동 시 3판마다 전면 광고, 배너는 결과 화면에서만
    /// 점수 제출 / 프로모션 지급은 SuikaPlatformBridge.NotifyGameOver에서 이미 처리된다.
    /// </summary>
    public class GameOverPopup : BaseUIPopup
    {
        [Header("바인딩")]
        [SerializeField] private TextMeshProUGUI finalScoreText;
        [SerializeField] private TextMeshProUGUI deathCauseText;

        [Header("최고 기록 (일반: 최고점 / 지각 변동: 내 최고 기록 크게)")]
        [SerializeField] private TextMeshProUGUI bestLabelText;
        [SerializeField] private TextMeshProUGUI bestScoreText;
        [Tooltip("최고 기록 갱신 시 켤 표시 (선택)")]
        [SerializeField] private GameObject newBestRoot;
        [Tooltip("지각 변동 모드에서 최고 기록 글자 크기 배율")]
        [SerializeField] private float tectonicBestFontScale = 1.8f;

        private float _bestFontSize = -1f;

        [Header("부활")]
        [SerializeField] private TextMeshProUGUI goldEarnedText; // "Gold +N 획득!" 표시

        [Header("골드 부활 (비용은 SuikaGameManager.reviveGoldCost)")]
        [SerializeField] private Button reviveButton;         // "골드 소모 부활" 버튼
        [SerializeField] private TextMeshProUGUI reviveGoldCostText; // 부활에 필요한 Gold 수량 표시 (예: "100 Gold")

        [Header("광고 보고 이어하기 (판당 1회, 5초 카운트다운)")]
        [SerializeField] private Button reviveAdButton;        // "광고 보고 이어하기" 버튼
        [Tooltip("이어하기 남은 시간 표시 (선택)")]
        [SerializeField] private TextMeshProUGUI continueCountdownText;
        [SerializeField] private float continueCountdownSeconds = 5f;

        private float _continueTimer;

        [Header("골드 2배 (광고)")]
        [SerializeField] private Button doubleGoldButton;    // "광고 보고 골드 2배 받기" 버튼 (광고 아이콘)
        [SerializeField] private GameObject doubleGoldRoot;  // 배경+문구+아이콘을 묶은 루트 (AdObjects)
        [SerializeField] private Image doubleGoldBackground; // 버튼 배경 이미지
        [SerializeField] private TextMeshProUGUI doubleGoldLabel; // "광고 시청 후 추가 재화 받기" 문구
        [Tooltip("사용 완료 시 배경에 곱할 회색 톤")]
        [SerializeField] private Color doubleGoldDisabledTint = new Color(0.55f, 0.55f, 0.55f, 1f);

        private bool _doubleGoldColorsCached;
        private Color _doubleGoldLabelColor;
        private Color _doubleGoldBackgroundColor;

        [Header("토스 출시 — 리더보드 / 공유 (미연결이면 해당 기능만 건너뜀)")]
        [SerializeField] private Button leaderboardButton;
        [SerializeField] private Button shareButton;

        private int _lastScore;
        private bool _leaderboardBusy;
        private bool _shareBusy;

        private static SuikaGameManager GM => SuikaGameManager.Instance;

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.GameOverPopup;
        }

        public override void EnterWindow()
        {
            base.EnterWindow();
            SuikaAnalytics.Screen("result");
            RefreshPlatformButtons();

            var gm = GM;
            if (gm != null)
                SetResult(gm.Score, gm.BestScore, gm.GameOverCause);
        }

        public override void ExitWindow()
        {
            base.ExitWindow();
        }

        // 재시작/나가기/부활 버튼은 프리팹 Inspector에서 OnClick이 이미 연결되어 있음
        // (RegistEvent에서 AddListener까지 하면 클릭 1회에 핸들러가 2번 호출되어, 두 번째 호출 시점엔
        // UIManager.currentUI가 이미 인게임 윈도우로 바뀐 뒤라 PopUI()가 PopupWindow() 경로를 타서
        // 인게임 윈도우까지 pop되고(+RestartGame 중복 호출) 버튼이 먹통이 되는 버그가 있었다).

        // 리더보드/공유 버튼 표시 갱신 + 코드로 리스너 연결 (Remove 후 Add로 중복 등록 방지).
        // 자동 공유는 제거되어 공유 버튼이 유일한 공유 경로다.
        private void RefreshPlatformButtons()
        {
            var platform = PlatformFactory.Current;

            if (leaderboardButton != null)
            {
                bool supported = platform != null && platform.SupportsLeaderboardUI;
                leaderboardButton.gameObject.SetActive(supported);
                leaderboardButton.onClick.RemoveListener(OnClickLeaderboard);
                if (supported)
                    leaderboardButton.onClick.AddListener(OnClickLeaderboard);
            }

            if (shareButton != null)
            {
                bool supported = platform != null && !(platform is DefaultPlatform);
                shareButton.gameObject.SetActive(supported);
                shareButton.interactable = true;
                shareButton.onClick.RemoveListener(OnClickShare);
                if (supported)
                    shareButton.onClick.AddListener(OnClickShare);
            }
        }

        private void OnDestroy()
        {
            if (leaderboardButton != null) leaderboardButton.onClick.RemoveListener(OnClickLeaderboard);
            if (shareButton != null) shareButton.onClick.RemoveListener(OnClickShare);
        }

        private void OnClickLeaderboard()
        {
            if (_leaderboardBusy) return;
            OpenLeaderboardAsync().Forget();
        }

        private async UniTaskVoid OpenLeaderboardAsync()
        {
            _leaderboardBusy = true;
            try
            {
                await PlatformFactory.Current.OpenLeaderboardAsync();
            }
            finally
            {
                _leaderboardBusy = false;
            }
        }

        private void OnClickShare()
        {
            if (_shareBusy) return;
            ShareAsync(_lastScore).Forget();
        }

        private async UniTaskVoid ShareAsync(int score)
        {
            _shareBusy = true;
            if (shareButton != null) shareButton.interactable = false;
            try
            {
                bool ok = await PlatformFactory.Current.ShareResultAsync(score);
                Debug.Log($"[GameOverPopup] 결과 공유 {(ok ? "완료" : "실패/취소")} (score={score})");
            }
            finally
            {
                _shareBusy = false;
                // 대기 중 팝업이 파괴됐을 수 있으므로 Unity null 체크 후 복구
                if (this != null && shareButton != null) shareButton.interactable = true;
            }
        }

        public void SetResult(int finalScore, int bestScore, E_SUIKA_GAMEOVER_CAUSE cause)
        {
            _lastScore = finalScore;
            if (finalScoreText != null) finalScoreText.text = finalScore.ToString();
            SetBest(finalScore, bestScore);
            if (deathCauseText != null) deathCauseText.text = GetGameOverCauseText(cause);

            // 획득 Gold 표시 (GameManager가 게임 오버 시 적립한 실제 값 사용)
            int goldEarned = GM != null ? GM.GoldEarnedLast : 0;
            if (goldEarnedText != null)
                goldEarnedText.text = $"코인 +{goldEarned} 획득!";

            // 부활 버튼: 아직 미사용일 때만 활성 (광고 부활은 광고 지원 여부도 확인)
            _continueTimer = continueCountdownSeconds;
            RefreshReviveButtons();
            RefreshDoubleGoldButton();
        }

        private void SetBest(int finalScore, int bestScore)
        {
            bool tectonic = GM != null && GM.Difficulty == E_SUIKA_DIFFICULTY.TECTONIC;
            if (bestLabelText != null)
                bestLabelText.text = tectonic ? "지각 변동 내 최고 기록" : "최고점";
            if (bestScoreText != null)
            {
                if (_bestFontSize < 0f) _bestFontSize = bestScoreText.fontSize;
                bestScoreText.fontSize = tectonic ? _bestFontSize * tectonicBestFontScale : _bestFontSize;
                bestScoreText.text = bestScore.ToString("N0");
            }
            if (newBestRoot != null)
                newBestRoot.SetActive(GM != null && GM.IsNewBestLast);
        }

        private string GetGameOverCauseText(E_SUIKA_GAMEOVER_CAUSE cause)
        {
            switch (cause)
            {
                case E_SUIKA_GAMEOVER_CAUSE.LIVES_OUT: return "화석을 모두 놓쳤어요!";
                case E_SUIKA_GAMEOVER_CAUSE.TIME_OUT:  return "시간 종료!";
                default:                              return "게임 오버!";
            }
        }

        public void OnClickRetry()
        {
            ExitWindow();
            GM?.RestartGame();
        }

        private bool _leaving;

        public void OnClickExit()
        {
            if (_leaving) return;
            ExitToLobbyAsync().Forget();
        }

        // 결과 → 로비 이동: 배너 내림 + 3판마다 전면 광고 (첫 판 / 보상형 본 판 제외)
        private async UniTaskVoid ExitToLobbyAsync()
        {
            _leaving = true;
            try
            {
                await SuikaPlatformBridge.OnLeaveResultToLobbyAsync();
            }
            finally
            {
                _leaving = false;
            }
            if (this == null) return;
            ExitWindow();
            GM?.ExitGame();
            Seunghak.SceneManager.SceneManager.Instance.ChangeScene(E_SCENE_TYPE.LOBBY);
        }

        // 이어하기 카운트다운 — 시간이 지나면 이어하기 버튼을 내린다
        private void Update()
        {
            if (reviveAdButton == null || !reviveAdButton.gameObject.activeSelf) return;
            if (!reviveAdButton.interactable) return; // 광고 시청 중엔 멈춤
            _continueTimer -= Time.unscaledDeltaTime;
            if (continueCountdownText != null)
                continueCountdownText.text = Mathf.CeilToInt(Mathf.Max(0f, _continueTimer)).ToString();
            if (_continueTimer <= 0f)
            {
                reviveAdButton.gameObject.SetActive(false);
                if (continueCountdownText != null) continueCountdownText.gameObject.SetActive(false);
            }
        }

        /// <summary>Gold를 소모해 즉시 부활. 광고 부활과 별개의 1회권이라, 이미 광고로 부활한
        /// 뒤 다시 죽어도(광고 부활을 이미 썼어도) 아직 안 쓴 골드 부활은 그대로 사용 가능하다.</summary>
        public void OnClickRevive()
        {
            var gm = GM;
            if (gm == null || !gm.CanReviveWithGold) return;

            if (!UserDataManager.Instance.SpendGold(gm.ReviveGoldCost))
            {
                Debug.Log("[GameOverPopup] Gold 부족 → 부활 불가");
                // 플레이어에게 골드 부족을 명시적으로 표시 — 없으면 버튼이 눌려도 아무것도
                // 안 일어나는 것처럼 보여 "부활 자체가 막혔다"는 오해를 낳는다.
                if (reviveGoldCostText != null)
                    reviveGoldCostText.text = "골드 부족!";
                if (reviveButton != null)
                    reviveButton.interactable = false;
                return;
            }

            Debug.Log("[GameOverPopup] Gold 소모 부활");
            gm.Revive(viaAd: false);
            ExitWindow();
        }

        /// <summary>광고를 보고 부활. 골드 부활과 별개의 1회권.</summary>
        public void OnClickReviveAd()
        {
            ReviveWithAdAsync().Forget();
        }

        private async UniTaskVoid ReviveWithAdAsync()
        {
            if (GM == null || !GM.CanReviveWithAd) return;

            if (reviveAdButton != null) reviveAdButton.interactable = false;

            bool revived = await SuikaPlatformBridge.ReviveWithAdAsync();

            if (revived)
            {
                Debug.Log("[GameOverPopup] 광고 시청 완료 → 부활");
                if (this != null) ExitWindow();
            }
            else
            {
                Debug.Log("[GameOverPopup] 광고 시청 실패/취소 → 부활 불가");
                if (reviveAdButton != null) reviveAdButton.interactable = true;
                RefreshReviveButtons();
            }
        }

        private void RefreshReviveButtons()
        {
            var gm = GM;

            // 골드/광고 부활은 독립 1회권 — 사용한 쪽 버튼만 끈다. 이전 시도(골드 부족, 광고 대기)에서
            // interactable=false로 남아 있을 수 있어 매번 되돌려준다.
            if (reviveButton != null)
            {
                bool canGold = gm != null && gm.CanReviveWithGold;
                reviveButton.gameObject.SetActive(canGold);
                reviveButton.interactable = canGold;
            }
            if (reviveGoldCostText != null)
                reviveGoldCostText.text = $"{(gm != null ? gm.ReviveGoldCost : 0)} Gold";

            if (reviveAdButton != null)
            {
                bool adSupported = AdManager.Instance != null && AdManager.Instance.IsAdSupported();
                bool canAd = gm != null && gm.CanReviveWithAd && adSupported && _continueTimer > 0f;
                reviveAdButton.gameObject.SetActive(canAd);
                reviveAdButton.interactable = canAd;
                if (continueCountdownText != null) continueCountdownText.gameObject.SetActive(canAd);
            }
        }

        // ── 골드 2배 (광고) ──────────────────────────────────────────────

        public void OnClickDoubleGold()
        {
            DoubleGoldWithAdAsync().Forget();
        }

        private async UniTaskVoid DoubleGoldWithAdAsync()
        {
            var gm = GM;
            if (gm == null || !gm.CanDoubleGold) return;

            if (doubleGoldButton != null) doubleGoldButton.interactable = false;

            bool rewarded = await SuikaPlatformBridge.ShowRewardedAsync(duringRun: true, placement: "coin_double");

            gm = GM; // 광고 대기 중 상태가 바뀌었을 수 있다
            if (!rewarded || gm == null)
            {
                Debug.Log("[GameOverPopup] 광고 시청 실패/취소 → 골드 2배 미지급");
                if (doubleGoldButton != null) doubleGoldButton.interactable = true;
                return;
            }

            // 이번 판 획득분은 게임 오버 시 이미 1배가 적립돼 있으므로, 같은 금액을
            // 한 번 더 지급해 총 2배를 맞춘다.
            int bonus = gm.ConsumeDoubleGoldBonus();
            if (bonus <= 0)
            {
                RefreshDoubleGoldButton();
                return;
            }

            UserDataManager.Instance.AddGold(bonus);
            Debug.Log($"[GameOverPopup] 광고 시청 완료 → 골드 2배 지급 (+{bonus})");

            if (goldEarnedText != null)
                goldEarnedText.text = $"코인 +{bonus * 2} 획득! (2배)";

            RefreshDoubleGoldButton();
        }

        private void RefreshDoubleGoldButton()
        {
            if (doubleGoldButton == null) return;

            var gm = GM;
            bool adSupported = AdManager.Instance != null && AdManager.Instance.IsAdSupported();
            bool canUse = gm != null && gm.CanDoubleGold && adSupported;
            // 부활(광고/골드) 버튼과 통일: 이미 2배를 받았으면 배경까지 루트째 숨긴다.
            if (doubleGoldRoot != null)
                doubleGoldRoot.SetActive(canUse);
            // 광고 아이콘은 사용 가능할 때만 노출
            doubleGoldButton.gameObject.SetActive(canUse);
            // 이전 판에서 interactable=false로 꺼둔 채 재사용될 수 있어 매번 되돌려준다.
            doubleGoldButton.interactable = canUse;

            ApplyDoubleGoldVisual(canUse);
        }

        // 사용 가능하면 원래 색, 아니면 문구는 흑백(휘도), 배경은 회색 톤으로 표시.
        private void ApplyDoubleGoldVisual(bool enabled)
        {
            if (!_doubleGoldColorsCached)
            {
                if (doubleGoldLabel != null) _doubleGoldLabelColor = doubleGoldLabel.color;
                if (doubleGoldBackground != null) _doubleGoldBackgroundColor = doubleGoldBackground.color;
                _doubleGoldColorsCached = true;
            }

            if (doubleGoldLabel != null)
            {
                Color c = _doubleGoldLabelColor;
                if (!enabled)
                {
                    float g = Mathf.Lerp(c.grayscale, 0.5f, 0.5f); // 너무 어둡거나 밝지 않게 중간 회색 쪽으로
                    c = new Color(g, g, g, c.a);
                }
                doubleGoldLabel.color = c;
            }

            if (doubleGoldBackground != null)
                doubleGoldBackground.color = enabled
                    ? _doubleGoldBackgroundColor
                    : _doubleGoldBackgroundColor * doubleGoldDisabledTint;
        }
    }
}
