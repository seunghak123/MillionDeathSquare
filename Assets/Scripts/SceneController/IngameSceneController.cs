using Cysharp.Threading.Tasks;
using UnityEngine;
using Seunghak.Manager;
using Seunghak.UIManager;
using Seunghak.Game.MillionDeathSquare;

namespace Seunghak.SceneManager
{
    /// <summary>
    /// 인게임(플레이 보드) 씬 컨트롤러.
    ///
    /// 흐름:
    ///   1. 메뉴 쪽에서 MDSGameDirector.RequestPlay(요청)으로 무엇을 플레이할지 정한 뒤 ChangeScene(INGAME)
    ///   2. 여기서 INGAME 번들 로드 → 플레이 HUD(MDSPlayWindow) 표시 → 디렉터가 보드를 만들고 판 시작
    ///   3. 클리어 시 디렉터가 결과 팝업(MDSResultPopup)을 띄운다
    /// 요청 없이 들어오면(테스트 등) 디렉터가 캠페인 1-1을 시작한다.
    /// </summary>
    public class InGameSceneController : SceneController
    {
        [Header("백만 번 죽는 네모 연결 (비우면 씬에서 찾음)")]
        [SerializeField] private MDSGameDirector director;

        protected override E_SCENE_TYPE GetSceneType() { return E_SCENE_TYPE.INGAME; }

        public override void InitSceneController()
        {
            LoadIngameBundleAndInit().Forget();
        }

        private MDSGameDirector Director
        {
            get
            {
                if (director == null) director = MDSGameDirector.Instance;
                return director;
            }
        }

        private async UniTaskVoid LoadIngameBundleAndInit()
        {
            BundleManager.Instance.UnloadBundle(E_BUNDLE_TYPE.LOBBY);
            await BundleManager.Instance.LoadBundleAsync(E_BUNDLE_TYPE.INGAME);

            UIManager.UIManager.Instance.OpenUI();
            UIManager.UIManager.Instance.PushUI(UI_TYPE.MDSPlayWindow);
            MDSAnalytics.Screen("play");

            Director.BeginPendingPlay();
        }
    }
}
