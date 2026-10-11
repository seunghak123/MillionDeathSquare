using Seunghak.Manager;
using Seunghak.Platform;
using Seunghak.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>메인 화면 (기획서 11장): 캠페인 / 에디터 / 맵 찾기 / 멀티방 / 설정 / 종료</summary>
    public sealed class MDSMainWindow : BaseUIWindow
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private TMP_Text platformText;
        [SerializeField] private Button campaignButton;
        [SerializeField] private Button editorButton;
        [SerializeField] private Button findButton;
        [SerializeField] private Button roomButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.MDSMainWindow;
            MDSUi.Bind(campaignButton, () => UIManager.UIManager.Instance.PushUI(UI_TYPE.MDSCampaignWindow));
            MDSUi.Bind(editorButton, () => UIManager.UIManager.Instance.PushUI(UI_TYPE.MDSEditorWindow));
            MDSUi.Bind(findButton, () => UIManager.UIManager.Instance.PushUI(UI_TYPE.MDSMapFindWindow));
            MDSUi.Bind(roomButton, () => UIManager.UIManager.Instance.PushUI(UI_TYPE.MDSRoomWindow));
            MDSUi.Bind(settingsButton, () => UIManager.UIManager.Instance.PushUI(UI_TYPE.SettingPopup));
            MDSUi.Bind(quitButton, Quit);
#if UNITY_WEBGL
            MDSUi.SetActive(quitButton, false);
#endif
            Refresh();
        }

        public override void EnterWindow()
        {
            base.EnterWindow();
            Refresh();
        }

        public override void RestoreWindow()
        {
            base.RestoreWindow();
            Refresh();
        }

        private void Refresh()
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlayBGM(SoundKeys.BGM_LOBBY);
            MDSUi.SetText(titleText, "백만 번 죽는 네모");
            CampaignProgress p = MDSSaveStore.Campaign;
            MDSUi.SetText(statsText, "캠페인 클리어 " + p.TotalCleared + " / " + (Rules.WORLD_COUNT * Rules.STAGES_PER_WORLD) + "   ·   지금까지 죽은 횟수 " + p.TotalDeaths);
            MDSUi.SetText(platformText, MDSServices.UsingSteam ? "Steam 연결됨" : "오프라인 (로컬 창작마당·순위표)");
        }

        private static void Quit()
        {
            PlatformSaveSync.FlushNow();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
