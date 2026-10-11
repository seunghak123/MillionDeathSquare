using Cysharp.Threading.Tasks;
using Seunghak.Manager;
using Seunghak.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 베이스 프로젝트에서 넘어온 범용 로비 창 (레거시).
    ///
    /// 백만 번 죽는 네모의 메인 화면은 MDSMainWindow이며 LobbySceneController는 그 창을 띄운다.
    /// 이 창은 기존 프리팹/에디터 툴(TossUiPrefabSetup, UiResolutionFixer 등)과의 호환을 위해 남겨 둔
    /// 최소 구현이다 — 게임 고유 로직은 없다.
    /// 버튼 이벤트는 인스펙터 OnClick()에서 직접 연결합니다.
    /// </summary>
    public class GameStartWindow : BaseUIWindow
    {
        [SerializeField] private GoldDisplayUI goldDisplay;

        [Header("토스 출시 — dev 표시 / 리더보드 / 친구 초대 (미연결이면 해당 기능만 건너뜀)")]
        [SerializeField] private GameObject devBuildLabel;
        [SerializeField] private Button leaderboardButton;
        [SerializeField] private Button inviteButton;

        private bool _leaderboardBusy;
        private bool _inviteBusy;

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.GameStartWindow;
            Refresh();
            if (SoundManager.Instance != null) SoundManager.Instance.PlayBGM(SoundKeys.BGM_LOBBY);
        }

        public override void RestoreWindow()
        {
            base.RestoreWindow();
            Refresh();
            if (SoundManager.Instance != null) SoundManager.Instance.PlayBGM(SoundKeys.BGM_LOBBY);
        }

        public override void ExitWindow()
        {
            UIManager.Instance.PopUI();
        }

        private void Refresh()
        {
            RefreshPlatformButtons();
        }

        private void RefreshPlatformButtons()
        {
            if (devBuildLabel != null)
                devBuildLabel.SetActive(TossConfig.IsDevBuild);

            var platform = PlatformFactory.Current;

            if (leaderboardButton != null)
            {
                bool supported = platform != null && platform.SupportsLeaderboardUI;
                leaderboardButton.gameObject.SetActive(supported);
                leaderboardButton.onClick.RemoveListener(OnClickLeaderboard);
                if (supported) leaderboardButton.onClick.AddListener(OnClickLeaderboard);
            }

            if (inviteButton != null)
            {
                bool supported = platform != null && platform.SupportsInvite;
                inviteButton.gameObject.SetActive(supported);
                inviteButton.onClick.RemoveListener(OnClickInvite);
                if (supported) inviteButton.onClick.AddListener(OnClickInvite);
            }
        }

        private void OnDestroy()
        {
            if (leaderboardButton != null) leaderboardButton.onClick.RemoveListener(OnClickLeaderboard);
            if (inviteButton != null) inviteButton.onClick.RemoveListener(OnClickInvite);
        }

        // ── 버튼 핸들러 (인스펙터 OnClick 연결용 — public) ─────────────────

        /// <summary>메인 화면(MDSMainWindow)으로</summary>
        public void OnClickPlay()
        {
            UIManager.Instance.PushUI(UI_TYPE.MDSMainWindow);
        }

        public void OnClickMyInfo() { UIManager.Instance.PushUI(UI_TYPE.UserInfoPopup); }
        public void OnClickRanking() { UIManager.Instance.PushUI(UI_TYPE.RankingWindow); }
        public void OnClickShop() { UIManager.Instance.PushUI(UI_TYPE.CharacterShopWindow); }
        public void OnClickSettings() { UIManager.Instance.PushUI(UI_TYPE.SettingPopup); }

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

        private void OnClickInvite()
        {
            if (_inviteBusy) return;
            InviteAsync().Forget();
        }

        private async UniTaskVoid InviteAsync()
        {
            _inviteBusy = true;
            if (inviteButton != null) inviteButton.interactable = false;
            try
            {
                int reward = await PlatformFactory.Current.InviteFriendsAsync();
                Debug.Log("[GameStartWindow] 친구 초대 완료 — 플랫폼 리워드 합계: " + reward);
            }
            finally
            {
                _inviteBusy = false;
                if (this != null && inviteButton != null) inviteButton.interactable = true;
            }
        }
    }
}
