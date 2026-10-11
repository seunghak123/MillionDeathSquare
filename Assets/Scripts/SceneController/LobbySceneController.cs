using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Seunghak.Game.Suika3D;

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
    public class LobbySceneController : SceneController
    {
        [Header("Fossil Merge 연결")]
        [SerializeField] private SuikaGameManager gameManager;

        protected override E_SCENE_TYPE GetSceneType() => E_SCENE_TYPE.LOBBY;

        protected override void Awake()
        {
            base.Awake();
        }
        public override void InitSceneController()
        {
            LoadLobbyBundleAndInit().Forget();
        }

        private async UniTaskVoid LoadLobbyBundleAndInit()
        {
            Debug.Log("[Lobby] 1. UnloadBundle(INGAME)");
            BundleManager.Instance.UnloadBundle(E_BUNDLE_TYPE.INGAME);

            Debug.Log("[Lobby] 2. LoadBundleAsync(LOBBY) 시작");
            await BundleManager.Instance.LoadBundleAsync(E_BUNDLE_TYPE.LOBBY);
            Debug.Log("[Lobby] 3. LoadBundleAsync(LOBBY) 완료");

            // 로비에서는 게임을 멈춰 둔다 (접시/카메라는 배경으로 계속 보임). 인게임 진입 시 RestartGame()으로 새 판.
            var gm = gameManager != null ? gameManager : SuikaGameManager.Instance;
            gm.ExitGame();
            Seunghak.Platform.PlatformSaveSync.GameplayActive = false;

            Debug.Log("[Lobby] 4. OpenUI");
            UIManager.Instance.OpenUI();

            Debug.Log("[Lobby] 5. PushUI(GameStartWindow)");
            UIManager.Instance.PushUI(UI_TYPE.GameStartWindow);
            Debug.Log("[Lobby] 6. PushUI 완료");

            // 로비 UI가 떴으니 부트 로딩 화면 해제 (재진입 시 호출돼도 안전)
            if (ApplicationManager.Instance != null)
                ApplicationManager.Instance.HideBootLoading();

            // 배너는 결과 화면에서만 (기획서 10.2) — 로비에서는 내린다
            SuikaPlatformBridge.HideBanner();
            SuikaAnalytics.Screen("lobby");
        }
        public override void RegistSceneController()
        {
            base.RegistSceneController();
        }
        public override void RegistSceneStepAction(E_SCENESTEP_TYPE actionType, Action playAction)
        {
            base.RegistSceneStepAction(actionType, playAction);
        }
    }
}
