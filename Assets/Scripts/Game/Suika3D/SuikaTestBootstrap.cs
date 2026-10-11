using Cysharp.Threading.Tasks;
using UnityEngine;
using Seunghak.Common;
using Seunghak.Manager;
using Seunghak.Platform;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// [테스트 전용] FossilMergeTest 씬을 단독으로 실행/빌드할 때 플랫폼을 초기화한다.
    /// ApplicationManager(Base.unity 부팅 흐름)와 같은 순서로 Toss 기능을 준비한 뒤 게임을 시작한다:
    ///   1. PlatformFactory.Create → TossPlatform / FacebookPlatform / DefaultPlatform 감지
    ///   2. WebGL 입력 포커스, 광고 SDK 초기화
    ///   3. 플랫폼 SDK InitAsync (Toss: 포커스 콜백 + 유저 키)
    ///   4. 플랫폼 저장소 → PlayerPrefs 복원 (PlatformSaveSync)
    ///   5. 로그인 (Toss는 성공할 때까지 재시도) → IAP 초기화
    ///   6. SuikaGameManager.Initialize() (배너는 결과 화면에서만)
    /// 씬에 ApplicationManager가 이미 있으면 그쪽이 플랫폼을 맡으므로 게임 초기화만 한다.
    /// </summary>
    public class SuikaTestBootstrap : MonoBehaviour
    {
        [SerializeField] private SuikaGameManager manager;

        private const int TossLoginRetryDelayMs = 3000;

        private void Start()
        {
            if (manager == null) manager = SuikaGameManager.Instance;
            BootAsync().Forget();
        }

        private async UniTaskVoid BootAsync()
        {
            if (FindFirstObjectByType<ApplicationManager>() == null)
            {
                try
                {
                    await InitPlatformAsync();
                }
                catch (System.Exception e)
                {
                    // 플랫폼 초기화 실패가 테스트 플레이를 막지 않게 한다
                    Debug.LogWarning($"[SuikaTestBootstrap] 플랫폼 초기화 실패 → 기본 플랫폼으로 진행: {e.Message}");
                }
            }

            if (this == null) return;
            manager.ReloadBestScore();
            manager.Initialize();
        }

        private async UniTask InitPlatformAsync()
        {
            PlatformFactory.Create(gameObject);

#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInputHelper.Init();
#endif
            AdManager.Instance.InitAsync().Forget();

            await PlatformFactory.Current.InitAsync();

            if (await PlatformSaveSync.RestoreAsync())
            {
                UserDataManager.Instance.ReloadFromLocalCache();
                SuikaProgress.ReloadAllAfterRestore();
            }

            IPlatform platform = PlatformFactory.Current;
            if (platform is TossPlatform)
            {
                UserDataManager.Instance.SetLoginInfo(E_LOGIN_TYPE.APPS_IN_TOSS_LOGIN);
                while (!await TryLoginAsync())
                {
                    if (this == null) return;
                    Debug.LogWarning("[SuikaTestBootstrap] Toss 로그인 실패 — 재시도");
                    await UniTask.Delay(TossLoginRetryDelayMs, DelayType.Realtime);
                }
                platform.IAP.InitAsync().Forget();
            }
            else
            {
                UserDataManager.Instance.SetLoginInfo(platform is FacebookPlatform
                    ? E_LOGIN_TYPE.FACEBOOK_INSTANT_LOGIN
                    : E_LOGIN_TYPE.GUEST_LOGIN);
                if (!await TryLoginAsync())
                {
                    UserDataManager.Instance.SetLoginInfo(E_LOGIN_TYPE.GUEST_LOGIN);
                    await TryLoginAsync();
                }
                platform.IAP.InitAsync().Forget();
            }

            Debug.Log($"[SuikaTestBootstrap] 플랫폼 준비 완료: {platform.GetType().Name}");
        }

        private static UniTask<bool> TryLoginAsync()
        {
            var tcs = new UniTaskCompletionSource<bool>();
            UserDataManager.Instance.LoginPlatform((token, success) => tcs.TrySetResult(success));
            return tcs.Task;
        }
    }
}
