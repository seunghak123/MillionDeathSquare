using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Seunghak.Common;
using Seunghak.Platform;

namespace Seunghak.LoginSystem
{
    /// <summary>
    /// Facebook Instant Games WebGL 환경 로그인, 결제(미지원), 광고(미지원) 구현.
    ///
    /// 초기화 흐름:
    ///   1. FBInstant.initializeAsync()  → OnFBInitSuccess
    ///   2. FBInstant.startGameAsync()   → OnFBStartGameSuccess
    ///   3. player.getID() 로 유저 토큰 획득
    ///
    /// 비동기 콜백은 unityInstance.SendMessage 로 수신합니다.
    /// 이 클래스는 MonoBehaviour가 아니므로, ApplicationManager GameObject에 콜백을 위임합니다.
    /// </summary>
    public class FacebookInstantLogin : LoginInterface
    {
        #region 로그인

        public void InitLogin()
        {
            // FacebookPlatform은 PlatformFactory.Create()로 이미 생성됩니다. 별도 초기화 불필요.
        }

        public void PlatformLogin(Action<string, bool> successResultAct)
        {
            if (successResultAct == null) return;

#if !UNITY_WEBGL || UNITY_EDITOR
            Debug.LogWarning("[FacebookInstantLogin] Facebook Instant Games는 WebGL에서만 동작합니다.");
            // 에디터에서는 PlayerPrefs GUID 토큰으로 성공 처리
            string devToken = CommonUtil.GetPlayerPref<string>(PlayerPrefKey.UserToken);
            if (string.IsNullOrEmpty(devToken))
            {
                devToken = "fb_dev_" + System.Guid.NewGuid().ToString("N");
                CommonUtil.SavePlayerPref(PlayerPrefKey.UserToken, devToken);
            }
            UserDataManager.Instance.SetUserToken(devToken);
            successResultAct(devToken, true);
#else
            // ApplicationManager.UserLoginAsync()에서 FacebookPlatform.InitAsync() 완료 후 호출됨
            var platform = PlatformFactory.Current as FacebookPlatform;
            if (platform == null)
            {
                Debug.LogError("[FacebookInstantLogin] FacebookPlatform이 없습니다.");
                successResultAct(string.Empty, false);
                return;
            }

            string playerId = platform.GetPlayerId();
            if (string.IsNullOrEmpty(playerId))
            {
                Debug.LogError("[FacebookInstantLogin] 플레이어 ID 획득 실패.");
                successResultAct(string.Empty, false);
                return;
            }

            string playerName = platform.GetPlayerName();
            UserDataManager.Instance.SetUserToken(playerId);
            UserDataManager.Instance.SetUserNickName(playerName);

            Debug.Log($"[FacebookInstantLogin] 로그인 성공: id={playerId}, name={playerName}");
            successResultAct(playerId, true);
#endif
        }

        #endregion

        #region 결제 (Facebook Instant Games 미지원)

        public async UniTask<ProductInfo[]> GetProductListAsync()
        {
            Debug.LogWarning("[FacebookInstantLogin] Facebook Instant Games는 결제를 지원하지 않습니다.");
            await UniTask.Yield();
            return new ProductInfo[0];
        }

        public void PurchaseProduct(string sku, Action<string> onSuccess, Action<string> onError)
        {
            Debug.LogWarning("[FacebookInstantLogin] Facebook Instant Games는 결제를 지원하지 않습니다.");
            onError?.Invoke("Facebook Instant Games에서는 결제를 지원하지 않습니다.");
        }

        #endregion

        #region 광고 (Facebook Instant Games 미지원 — 별도 Audience Network 필요)

        public bool IsAdSupported() => false;

        public void LoadAd(string adGroupId, Action<bool> onLoadComplete)
        {
            Debug.LogWarning("[FacebookInstantLogin] 광고는 Audience Network 별도 연동이 필요합니다.");
            onLoadComplete?.Invoke(false);
        }

        public void ShowAd(string adGroupId, Action<bool> onShowComplete)
        {
            Debug.LogWarning("[FacebookInstantLogin] 광고는 Audience Network 별도 연동이 필요합니다.");
            onShowComplete?.Invoke(false);
        }

        #endregion
    }
}
