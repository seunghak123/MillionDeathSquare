using Cysharp.Threading.Tasks;
using Seunghak.UIManager;
using Seunghak.Platform;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#if GOOGLE_PLAY_UPDATE
using Google.Play.AppUpdate;
using Google.Play.Common;
#endif
#elif UNITY_IOS
using UnityEngine.iOS;
#endif
namespace Seunghak.Common
{
    public class ApplicationManager : UnitySingleton<ApplicationManager>
    {
        [SerializeField] public static string cdnAddressPath = "https://d2fvmix8egxgsw.cloudfront.net/ARDefenceBundle/";
        [SerializeField] private bool isUseBundle;
        [SerializeField] public bool isStreamingAssetBundles;
        [SerializeField] public GameObject managersObject;
        [Header("Init UI")]
        [SerializeField] DownloadBundlePopup userDownloadBundlePopup;
        [SerializeField] BaseAwnserPopup userAwnserPopup;
        [Header("Boot Loading (Base 씬에 직접 배치)")]
        [SerializeField] private BootLoadingScreen bootLoadingScreen;

        //[SerializeField] private 
        private E_APPLICATION_STATE applicationState = E_APPLICATION_STATE.APPLICATION_START;

        private void Awake()
        {
            Application.targetFrameRate = 60;
        }

        private void InitApplication()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            //Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            //    var dependencyStatus = task.Result;
            //    if (dependencyStatus == Firebase.DependencyStatus.Available)
            //    {
            //         var app = Firebase.FirebaseApp.DefaultInstance;
            //    }
            //    else
            //    {
            //        UnityEngine.Debug.LogError(System.String.Format(
            //          "Could not resolve all Firebase dependencies: {0}", dependencyStatus));
            //    }
            //});
            if (managersObject != null)
            {
                DontDestroyOnLoad(managersObject);
            }
            //if (usertitleWindow == null)
            //{
            //    GameObject titleWindow = GameObject.Find("TitleWindow");
            //    if (titleWindow != null)
            //    {
            //        usertitleWindow = titleWindow.GetComponent<TitleWindow>();
            //    }
            //}
            //if (usertitleWindow != null)
            //{
            //    if (userDownloadBundlePopup == null)
            //    {
            //        userDownloadBundlePopup = usertitleWindow.downloadPopup;
            //    }
            //    if (userAwnserPopup == null)
            //    {
            //        userAwnserPopup = usertitleWindow.baseAwnserPopup;
            //    }
            //    usertitleWindow.InitTitleWindow();
            //}
            if (userDownloadBundlePopup != null)
            {
                userDownloadBundlePopup.gameObject.SetActive(false);
            }

            ApplicationWork(E_APPLICATION_STATE.APPLICATION_START);
        }
        private void Start()
        {
            InitApplication();
        }
        private void OnApplicationQuit()
        {
            GC.Collect();
        }

        // WebGL에서는 OnApplicationFocus가 탭 전환에 반응하지 않으므로
        // TossBridge_RegisterFocusCallback으로 JS visibilitychange를 수신합니다.
        private void OnApplicationPause(bool pauseStatus)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (pauseStatus)
                Seunghak.Manager.SoundManager.Instance?.PauseAll();
            else
                Seunghak.Manager.SoundManager.Instance?.ResumeAll();
#endif
        }

        /// <summary>
        /// JS visibilitychange 콜백 수신 (TossBridge_RegisterFocusCallback으로 등록됨)
        /// "true" = 포그라운드, "false" = 백그라운드
        /// </summary>
        public void OnWebViewFocusChanged(string focused)
        {
            bool isFocused = focused == "true";
            if (isFocused)
            {
                // 일시정지 팝업이 떠 있는 상태로 백그라운드에 다녀오면 게임은 멈춰있는데 BGM만 다시 나오던 문제 방지.
                if (!Seunghak.Game.MillionDeathSquare.MDSSession.IsPaused)
                    Seunghak.Manager.SoundManager.Instance?.ResumeAll();
            }
            else
            {
                Seunghak.Manager.SoundManager.Instance?.PauseAll();
                // 백그라운드로 가면 그대로 종료될 수 있으니 저장 스냅샷을 즉시 푸시
                PlatformSaveSync.FlushNow();
            }

            Debug.Log($"[ApplicationManager] WebView 포커스 변경: {(isFocused ? "foreground" : "background")}");
        }
        private void ApplicationWork(E_APPLICATION_STATE nextType,bool isLocalAsset = false)
        {
            applicationState = nextType;
            bool isLocal = isLocalAsset;
            UpdateBootLoading(nextType);
            switch (applicationState)
            {
                case E_APPLICATION_STATE.APPLICATION_START:
                    InitApplicationInfo();
                    break;
                case E_APPLICATION_STATE.REQUEST_PERMISSION:
                    StartCoroutine(RequestPermission());
                    break;
                case E_APPLICATION_STATE.APPLICATION_UPDATE:
                    StartCoroutine(ApplicationUpdate());
                    break;
                case E_APPLICATION_STATE.USER_LOGIN:
                    StartCoroutine(UserLogin());
                    break;
                case E_APPLICATION_STATE.BUNDLE_UPDATE:
                    BundleUpdate().Forget();
                    break;
                case E_APPLICATION_STATE.GAME_RESOURCE_LOAD:
                    GameResourceLoad(isLocal).Forget();
                    break;
                case E_APPLICATION_STATE.INAPP_UPDATE:
                    StartCoroutine(InAppUpdate());
                    break;
                case E_APPLICATION_STATE.TITLE:
                    StartCoroutine(GoToTitle());
                    break;
            }

        }
        /// <summary>상태 진입 시 부트 로딩 화면의 진행률/문구 갱신 (null이면 건너뜀)</summary>
        private void UpdateBootLoading(E_APPLICATION_STATE state)
        {
            if (bootLoadingScreen == null) return;
            switch (state)
            {
                case E_APPLICATION_STATE.APPLICATION_START:
                    bootLoadingScreen.SetProgress(0.05f, "게임을 준비하고 있어요");
                    break;
                case E_APPLICATION_STATE.USER_LOGIN:
                    bootLoadingScreen.SetProgress(0.20f, "로그인 중...");
                    break;
                case E_APPLICATION_STATE.BUNDLE_UPDATE:
                    bootLoadingScreen.SetProgress(0.60f, "리소스를 불러오는 중...");
                    break;
                case E_APPLICATION_STATE.TITLE:
                    bootLoadingScreen.SetProgress(0.85f, "거의 다 됐어요!");
                    break;
            }
        }

        /// <summary>로비 UI가 처음 뜬 직후 호출. 여러 번 호출돼도 안전.</summary>
        public void HideBootLoading()
        {
            if (bootLoadingScreen != null)
                bootLoadingScreen.Hide();
        }

        private void MoveNextState(E_APPLICATION_STATE nextType,bool isLocal = false)
        {
            //State사이 시간 부여 등등 action들을 등록하고 대기하는 시간을 갖도록 변경
            ApplicationWork(nextType, isLocal);

        }
        #region ApplicationLogic
        private void InitApplicationInfo()
        {
            // 플랫폼 감지 및 컴포넌트 생성 (TossPlatform / FacebookPlatform / DefaultPlatform)
            PlatformFactory.Create(gameObject);

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: 캔버스 자동 포커스 + 키 기본 동작(화살표/Space 스크롤) 차단
            WebGLInputHelper.Init();
#endif

            // 광고 SDK 초기화 (플랫폼 감지 → GD/CrazyGames SDK 준비)
            Seunghak.Manager.AdManager.Instance.InitAsync().Forget();

            MoveNextState(E_APPLICATION_STATE.REQUEST_PERMISSION);
        }
        private IEnumerator RequestPermission()
        {
            MoveNextState(E_APPLICATION_STATE.APPLICATION_UPDATE);
            yield return null;
        }
        private IEnumerator ApplicationUpdate()
        {
#if UNITY_EDITOR
            MoveNextState(E_APPLICATION_STATE.USER_LOGIN);
            yield break;
            //IOS는 
#elif UNITY_ANDROID
#if GOOGLE_PLAY_UPDATE
            AppUpdateManager appUpdater = new AppUpdateManager();
            PlayAsyncOperation<AppUpdateInfo, AppUpdateErrorCode> appUpdateInfoOperaion =
                appUpdater.GetAppUpdateInfo();

            yield return appUpdateInfoOperaion;

            if (appUpdateInfoOperaion.IsSuccessful)
            {
                AppUpdateInfo result = appUpdateInfoOperaion.GetResult();

                if(result.UpdateAvailability == UpdateAvailability.UpdateAvailable)
                {
                    //업데이트 팝업 띄우고
                    userAwnserPopup.gameObject.SetActive(true);
                    userAwnserPopup.OpenPopup("확인",
                        () =>
                        {
                            appUpdater.StartUpdate(result, AppUpdateOptions.ImmediateAppUpdateOptions());
                            AppUpdateOptions.ImmediateAppUpdateOptions();
                            userAwnserPopup.gameObject.SetActive(false);
                            MoveNextState(E_APPLICATION_STATE.USER_LOGIN);
                        },
                        "취소",
                        () =>
                        {
                            Application.Quit();
                        }
                        );

                }
                else
                {
                    MoveNextState(E_APPLICATION_STATE.USER_LOGIN);
                }
            }else
            {
                MoveNextState(E_APPLICATION_STATE.USER_LOGIN);
            }
#else
            // Google Play In-App Update SDK 미설치 시 스킵
            MoveNextState(E_APPLICATION_STATE.USER_LOGIN);
            yield break;
#endif
#elif UNITY_IOS

#else
#endif
            MoveNextState(E_APPLICATION_STATE.USER_LOGIN);
            yield break;
        }
        public void UserLoginSuccess()
        {
            MoveNextState(E_APPLICATION_STATE.BUNDLE_UPDATE);
        }
        private IEnumerator UserLogin()
        {
            yield return UserLoginAsync().ToCoroutine();
        }

        private async UniTask UserLoginAsync()
        {
            // 플랫폼 SDK 초기화 (Toss: 포커스 콜백 등록 + 유저 키 / Facebook: initializeAsync + startGameAsync)
            await PlatformFactory.Current.InitAsync();

            if (bootLoadingScreen != null)
                bootLoadingScreen.SetProgress(0.40f, "데이터를 불러오는 중...");

            // 플랫폼 저장소(원본) → PlayerPrefs(로컬 캐시) 복원. 로그인 전에 끝내야 한다.
            await RestorePlatformSaveAsync();

            // 플랫폼 타입으로 로그인 방식 결정
            if (PlatformFactory.Current is TossPlatform)
                UserDataManager.Instance.SetLoginInfo(E_LOGIN_TYPE.APPS_IN_TOSS_LOGIN);
            else if (PlatformFactory.Current is FacebookPlatform)
                UserDataManager.Instance.SetLoginInfo(E_LOGIN_TYPE.FACEBOOK_INSTANT_LOGIN);
            else
                UserDataManager.Instance.SetLoginInfo(E_LOGIN_TYPE.GUEST_LOGIN);

            // 토스: 게임 로그인(유저 키) 성공 후에만 진입. 게스트 폴백 없이 성공할 때까지 재시도한다.
            if (PlatformFactory.Current is TossPlatform)
            {
                while (!await TryLoginAsync())
                {
                    if (bootLoadingScreen != null)
                        bootLoadingScreen.SetProgress(0.40f, "토스 로그인에 실패했어요. 다시 시도하는 중...");
                    await UniTask.Delay(TossLoginRetryDelayMs, DelayType.Realtime);
                }
                UserLoginSuccess();
                PlatformFactory.Current.IAP.InitAsync().Forget();
                return;
            }

            var loginTcs = new UniTaskCompletionSource<bool>();
            UserDataManager.Instance.LoginPlatform((token, success) =>
            {
                if (success)
                {
                    UserLoginSuccess();
                    loginTcs.TrySetResult(true);
                }
                else
                {
                    Debug.LogWarning("[ApplicationManager] 로그인 실패. GuestLogin으로 폴백합니다.");
                    UserDataManager.Instance.SetLoginInfo(E_LOGIN_TYPE.GUEST_LOGIN);
                    UserDataManager.Instance.LoginPlatform((guestToken, guestSuccess) =>
                    {
                        if (guestSuccess)
                            UserLoginSuccess();
                        else
                            Debug.LogError("[ApplicationManager] GuestLogin도 실패했습니다.");
                        loginTcs.TrySetResult(guestSuccess);
                    });
                }
            });
            bool loggedIn = await loginTcs.Task;

            // 결제는 됐지만 지급이 끝나지 않은 주문 복구 (미지원 플랫폼은 no-op)
            if (loggedIn)
                PlatformFactory.Current.IAP.InitAsync().Forget();
        }

        private const int TossLoginRetryDelayMs = 3000;

        /// <summary>현재 로그인 방식으로 한 번 로그인 시도. 성공 여부만 돌려준다.</summary>
        private static UniTask<bool> TryLoginAsync()
        {
            var tcs = new UniTaskCompletionSource<bool>();
            UserDataManager.Instance.LoginPlatform((token, success) => tcs.TrySetResult(success));
            return tcs.Task;
        }

        private async UniTask RestorePlatformSaveAsync()
        {
            try
            {
                if (await PlatformSaveSync.RestoreAsync())
                {
                    // 매니저들은 복원 전 PlayerPrefs로 이미 로드돼 있으므로 다시 읽게 한다
                    UserDataManager.Instance.ReloadFromLocalCache();
                    Seunghak.Manager.CharacterManager.Instance.ReloadFromLocalCache();
                    Seunghak.Game.MillionDeathSquare.MDSSaveStore.ReloadAfterRestore();
                }
            }
            catch (Exception e)
            {
                // 복원 실패가 앱 시작을 막지 않게 한다 (로컬 캐시로 계속 진행)
                Debug.LogWarning($"[ApplicationManager] 플랫폼 저장 복원 실패: {e.Message}");
            }
        }
        private async UniTask BundleUpdate()
        {
            Debug.Log("[App] BundleUpdate 시작");
            // StreamingAssets 기반 BundleManager로 Main 번들 로드
            await Seunghak.Manager.BundleManager.Instance.InitializeAsync(
                this.GetCancellationTokenOnDestroy());
            Debug.Log("[App] BundleUpdate 완료 → GAME_RESOURCE_LOAD");
            MoveNextState(E_APPLICATION_STATE.GAME_RESOURCE_LOAD);
        }

        private async UniTask GameResourceLoad(bool isLocal = false)
        {
            Debug.Log("[App] GameResourceLoad 시작");
            // BundleManager가 Main 번들 로드를 완료한 상태
            // 개별 씬 번들(ingame/lobby/character)은 각 SceneController에서 로드
            await UniTask.CompletedTask;
            Debug.Log("[App] GameResourceLoad 완료 → INAPP_UPDATE");
            MoveNextState(E_APPLICATION_STATE.INAPP_UPDATE);
        }
        private IEnumerator InAppUpdate()
        {
            MoveNextState(E_APPLICATION_STATE.TITLE);

            yield break;
        }
        private IEnumerator GoToTitle()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name == "MapCreate")
                {
                    yield break;
                }
            }
#endif
            //로비로 가야하는가 ? 아니면 인트로로 가야하는가에 따라 결정 우선 인트로는 빠지고 
            //바로 로비로 인토르는, 스킵가능하도록 만들자
            SceneManager.SceneManager.Instance.ChangeScene(E_SCENE_TYPE.LOBBY);
            yield break;
        }

        public Coroutine CustomStartCoroutine(IEnumerator callback)
        {
            return StartCoroutine(callback);
        }

        public void CustomEndCoroutine(Coroutine callback)
        {
            StopCoroutine(callback);
        }

        #endregion ApplicationLogic

        #region InsertLogic
        //FCM 설정
        public void InitializeFCM()
        {
            // Google Play 버전 체크 (비동기)
            //FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            //    var dependencyStatus = task.Result;
            //    if (dependencyStatus == DependencyStatus.Available)
            //    {
            //        Debug.Log("Google Play version OK");

            //        // FCM 초기화
            //        FirebaseMessaging.TokenReceived += OnTokenReceived; 
            //        FirebaseMessaging.MessageReceived += OnMessageReceived; 
            //        FirebaseMessaging.RequestPermissionAsync().ContinueWithOnMainThread(task => {
            //            Debug.Log("push permission: " + task.Status.ToString());
            //        });
            //    }
            //    else
            //    {
            //        Debug.LogError(string.Format(
            //            "Could not resolve all Firebase dependencies: {0}",
            //            dependencyStatus
            //        ));
            //    }
            //});
        }
        //public void OnTokenReceived(object sender, TokenReceivedEventArgs token)
        //{
        //    Debug.Log("OnTokenReceived: " + token.Token);
        //}
        //public void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        //{
        //    // [3. FCM 서버 푸시 수신]에서 작성
        //}
        public void InitializeAndroidLocalPush()
        {
        //    // 디바이스의 안드로이드 api level 얻기
        //    string androidInfo = SystemInfo.operatingSystem;
        //    Debug.Log("androidInfo: " + androidInfo);
        //    apiLevel = int.Parse(androidInfo.Substring(androidInfo.IndexOf("-") + 1, 2));
        //    Debug.Log("apiLevel: " + apiLevel);

        //    // 디바이스의 api level이 33 이상이라면 퍼미션 요청
        //    if (apiLevel >= 33 &&
        //        !Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS"))
        //    {
        //        Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
        //    }

        //    // 디바이스의 api level이 26 이상이라면 알림 채널 설정
        //    if (apiLevel >= 26)
        //    {
        //        var channel = new AndroidNotificationChannel()
        //        {
        //            Id = CHANNEL_ID,
        //            Name = "pubSdk",
        //            Importance = Importance.High, // 아래 참고
        //            Description = "for test",
        //        };
        //        AndroidNotificationCenter.RegisterNotificationChannel(channel);
        //    }
        }
        //로컬 알람
        public static void SendNotification(string title, string explain, DateTime time)
        {
//            try
//            {
//#if UNITY_ANDROID
//                var notification = new AndroidNotification();
//                notification.Title = title;
//                notification.Text = explain;
//                notification.FireTime = time;

//                notification.SmallIcon = "icon_1";
//                notification.LargeIcon = "icon_0";
//                notification.ShowInForeground = false;
//                string channelId = "my_channel_id";

//                AndroidNotificationCenter.SendNotification(notification, channelId);
//#elif UNITY_IOS
//        var timeTrigger = new iOSNotificationTimeIntervalTrigger()
//        {
//            TimeInterval = time - DateTime.Now,
//            Repeats = false
//        };

//        var notification = new iOSNotification()
//        {
//            Identifier = "_notification",
//            Title = title,
//            Body = explain,
//            //Subtitle = explain,
//            ShowInForeground = false,
//            ForegroundPresentationOption = (PresentationOption.Alert | PresentationOption.Sound),
//            CategoryIdentifier = "category_a",
//            ThreadIdentifier = "thread1",
//            Trigger = timeTrigger,
//        };

//        iOSNotificationCenter.ScheduleNotification(notification);
//#endif
//            }
//            catch (Exception e)
//            {

//            }
        }
        public static void CancelAllNotifications()
        {
//#if UNITY_ANDROID
//            AndroidNotificationCenter.CancelAllNotifications();
//#elif UNITY_IOS
//        iOSNotificationCenter.RemoveAllScheduledNotifications();
//#endif
        }
#endregion InsertLogic
    }
    [SerializeField]
    public class UpdateVersionInfo
    {
        public string cdnAddressInfoPath;
        //최신 버젼
        public string currentVersion;
        //특정 버젼 이하 강제 업데이트
        public string forcedUpdateVersion;
    }
    [SerializeField]
    public class CDNUpdateAddressInfo
    {
        //업데이트 CDN 경로
        public string updateCDNPath;
        //CDN Path는 cdnAddressPath + 플랫폼타입 + updateCDNPath
    }
}
