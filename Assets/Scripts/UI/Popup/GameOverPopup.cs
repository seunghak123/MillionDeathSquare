using Cysharp.Threading.Tasks;
using Seunghak.Game.MillionDeathSquare;
using Seunghak.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 베이스 프로젝트에서 넘어온 범용 결과 팝업 (레거시).
    ///
    /// 백만 번 죽는 네모의 결과 화면은 MDSResultPopup이다. 이 팝업은 기존 프리팹/에디터 툴
    /// (TossUiPrefabSetup, UiResolutionFixer) 호환용 최소 구현으로 재시작/나가기/리더보드/공유만 처리한다.
    /// 재시작/나가기 버튼은 프리팹 Inspector OnClick에 연결되어 있다 (코드에서 AddListener 하지 않음).
    /// </summary>
    public class GameOverPopup : BaseUIPopup
    {
        [Header("결과 표시")]
        [SerializeField] private TextMeshProUGUI finalScoreText;
        [SerializeField] private TextMeshProUGUI bestScoreText;

        [Header("리더보드 / 공유 (미연결이면 해당 기능만 건너뜀)")]
        [SerializeField] private Button leaderboardButton;
        [SerializeField] private Button shareButton;

        private int _lastScore;
        private bool _leaderboardBusy;
        private bool _shareBusy;

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.GameOverPopup;
        }

        public override void EnterWindow()
        {
            base.EnterWindow();
            RefreshPlatformButtons();
        }

        public void SetResult(int finalScore, int bestScore)
        {
            _lastScore = finalScore;
            if (finalScoreText != null) finalScoreText.text = finalScore.ToString();
            if (bestScoreText != null) bestScoreText.text = bestScore.ToString();
        }

        private void RefreshPlatformButtons()
        {
            var platform = PlatformFactory.Current;
            if (leaderboardButton != null)
            {
                bool supported = platform != null && platform.SupportsLeaderboardUI;
                leaderboardButton.gameObject.SetActive(supported);
                leaderboardButton.onClick.RemoveListener(OnClickLeaderboard);
                if (supported) leaderboardButton.onClick.AddListener(OnClickLeaderboard);
            }
            if (shareButton != null)
            {
                bool supported = platform != null && !(platform is DefaultPlatform);
                shareButton.gameObject.SetActive(supported);
                shareButton.interactable = true;
                shareButton.onClick.RemoveListener(OnClickShare);
                if (supported) shareButton.onClick.AddListener(OnClickShare);
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
            try { await PlatformFactory.Current.OpenLeaderboardAsync(); }
            finally { _leaderboardBusy = false; }
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
            try { await PlatformFactory.Current.ShareResultAsync(score); }
            finally
            {
                _shareBusy = false;
                if (this != null && shareButton != null) shareButton.interactable = true;
            }
        }

        public void OnClickRetry()
        {
            ExitWindow();
            MDSSession.Restart();
        }

        public void OnClickExit()
        {
            ExitWindow();
            MDSSession.ExitToMenu();
        }
    }
}
