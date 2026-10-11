using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Seunghak.Game.MillionDeathSquare;

namespace Seunghak.SceneManager
{
    using Seunghak.Common;
    using Seunghak.Manager;
    using Seunghak.UIManager;

    public class LobbySceneData : SceneDeliverData
    {
        public bool openLobbyWindow = true;
        public LobbySceneData(bool openLobbyWindow = true)
        {
            this.openLobbyWindow = openLobbyWindow;
        }
    }

    /// <summary>
    /// 로비(메뉴) 씬 컨트롤러. 판을 정리하고 메인 화면(MDSMainWindow)을 띄운다.
    /// 캠페인 선택/에디터/맵 찾기/멀티방은 모두 메인 화면에서 PushUI로 연다.
    /// </summary>
    public class LobbySceneController : SceneController
    {
        [Header("백만 번 죽는 네모 연결 (비우면 씬에서 찾음)")]
        [SerializeField] private MDSGameDirector director;

        protected override E_SCENE_TYPE GetSceneType() { return E_SCENE_TYPE.LOBBY; }

        public override void InitSceneController()
        {
            LoadLobbyBundleAndInit().Forget();
        }

        private async UniTaskVoid LoadLobbyBundleAndInit()
        {
            BundleManager.Instance.UnloadBundle(E_BUNDLE_TYPE.INGAME);
            await BundleManager.Instance.LoadBundleAsync(E_BUNDLE_TYPE.LOBBY);

            // 메뉴에서는 보드를 멈춰 둔다 (에디터 시험 플레이 복귀는 디렉터가 에디터 창으로 처리)
            var d = director != null ? director : MDSGameDirector.Instance;
            if (d != null) d.StopPlay();
            Seunghak.Platform.PlatformSaveSync.GameplayActive = false;

            UIManager.Instance.OpenUI();
            UIManager.Instance.PushUI(UI_TYPE.MDSMainWindow);

            if (ApplicationManager.Instance != null)
                ApplicationManager.Instance.HideBootLoading();

            MDSAnalytics.Screen("main");
        }

        public override void RegistSceneStepAction(E_SCENESTEP_TYPE actionType, Action playAction)
        {
            base.RegistSceneStepAction(actionType, playAction);
        }
    }
}
