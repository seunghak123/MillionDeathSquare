using Cysharp.Threading.Tasks;
using UnityEngine;
using Seunghak.Manager;
using Seunghak.UIManager;
using Seunghak.Game.Suika3D;
using Seunghak.Platform;

namespace Seunghak.SceneManager
{
    /// <summary>
    /// 인게임 씬 컨트롤러.
    ///
    /// 로딩 흐름:
    ///   1. 필수 번들만 빠르게 로드
    ///   2. HUD 표시 → SuikaGameManager.RestartGame() → READY (발굴대 위 첫 화석 대기)
    ///   3. 첫 드롭 → PLAYING (플랫폼 시작 알림은 SuikaPlatformBridge가 처리)
    ///   4. 레벨업 → LevelUpPopup (스킬 선택)
    ///   5. 판 종료 → GameOverPopup (이어하기 / 결과). 전면 광고는 결과 → 로비 이동 시에만
    /// </summary>
    public class InGameSceneController : SceneController
    {
        [Header("Fossil Merge 연결")]
        [SerializeField] private SuikaGameManager gameManager;

        protected override E_SCENE_TYPE GetSceneType() => E_SCENE_TYPE.INGAME;

        protected override void Awake()
        {
            base.Awake();
        }

        public override void InitSceneController()
        {
            LoadIngameBundleAndInit().Forget();
        }

        private SuikaGameManager GM => gameManager != null ? gameManager : (gameManager = SuikaGameManager.Instance);

        private async UniTaskVoid LoadIngameBundleAndInit()
        {
            if (gameManager == null)
                Debug.LogWarning("[InGameSceneController] SuikaGameManager가 Inspector에 연결되지 않아 씬에서 찾습니다.");

            // LOBBY 번들 언로드 (인게임 진입 시)
            BundleManager.Instance.UnloadBundle(E_BUNDLE_TYPE.LOBBY);

            // INGAME 번들 로드 (필수 에셋만)
            await BundleManager.Instance.LoadBundleAsync(E_BUNDLE_TYPE.INGAME);

            UIManager.UIManager.Instance.OpenUI();
            UIManager.UIManager.Instance.PushUI(UI_TYPE.InGameHUDWindow);
            SuikaAnalytics.Screen("ingame");

            var gm = GM;
            gm.OnGameOver -= OnGameOver;
            gm.OnGameOver += OnGameOver;
            gm.OnLevelUp -= OnLevelUp;
            gm.OnLevelUp += OnLevelUp;

            // 새 판 → READY (첫 드롭 시 PLAYING + 플랫폼 시작 알림)
            gm.RestartGame();
        }

        private void OnLevelUp()
        {
            var popup = UIManager.UIManager.Instance.PushUI(UI_TYPE.LevelUpPopup);
            if (popup != null) return;

            // 프리팹이 없으면(아직 생성 전) 게임이 멈춘 채 갇히지 않도록 첫 선택지를 자동 선택
            Debug.LogWarning("[InGameSceneController] LevelUpPopup 프리팹이 없어 첫 스킬을 자동 선택합니다. Tools/Fossil Merge/6 메뉴로 생성하세요.");
            var skills = GM != null ? GM.Skills : null;
            if (skills == null) return;
            skills.ChooseOffer(0);
            if (skills.Overflow != E_FOSSIL_SKILL.NONE) skills.DiscardOverflow();
        }

        private void OnGameOver()
        {
            // 점수 제출 / 저장 푸시 / 프로모션은 SuikaPlatformBridge.NotifyGameOver가 처리.
            // 게임 중·게임오버 직후에는 광고를 띄우지 않는다 (전면 광고는 결과 → 로비 이동 시).
            ShowResultAsync().Forget();
        }

        private async UniTaskVoid ShowResultAsync()
        {
            // 떨어지는 연출을 잠깐 보여준 뒤 이어하기/결과 팝업
            await UniTask.Delay(800, DelayType.Realtime);
            if (this == null) return;

            var gm = GM;
            if (gm == null || gm.GameState != E_SUIKA_GAME_STATE.GAME_OVER) return;

            var popup = UIManager.UIManager.Instance.PushUI(UI_TYPE.GameOverPopup) as GameOverPopup;
            popup?.SetResult(gm.Score, gm.BestScore, gm.GameOverCause);
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameOver -= OnGameOver;
                gameManager.OnLevelUp -= OnLevelUp;
            }
        }

        public override void RegistSceneController()
        {
            base.RegistSceneController();
        }

        public override void RegistSceneStepAction(E_SCENESTEP_TYPE actionType, System.Action playAction)
        {
            base.RegistSceneStepAction(actionType, playAction);
        }
    }
}
