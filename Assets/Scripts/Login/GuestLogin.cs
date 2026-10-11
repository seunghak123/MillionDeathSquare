using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Seunghak.Common;
using Cysharp.Threading.Tasks;

namespace Seunghak.LoginSystem
{
    /// <summary>
    /// 테스트용 로그인이기 때문에, 실제 라이브 환경에서는 제거가 필요합니다.
    /// 게스트 로그인시 디바이스 GUID로 토큰값을 생성하기 때문에 디바이스가 동일하다면 동일한 계정으로 로그인 됩니다.
    /// 게스트 모드에서는 결제와 광고 기능을 지원하지 않습니다.
    /// </summary>
    public class GuestLogin : LoginInterface
    {
        #region 로그인

        public void InitLogin()
        {
        }

        public void PlatformLogin(Action<string, bool> successAction)
        {
            // WebGL: PlayerPrefs는 IndexedDB에 영속화됨 → 같은 브라우저에서 재접속 시 동일 토큰 유지
            // 저장된 토큰이 없으면 새 GUID 생성 후 저장
            string userToken = CommonUtil.GetPlayerPref<string>(PlayerPrefKey.UserToken);
            if (string.IsNullOrEmpty(userToken))
            {
                userToken = System.Guid.NewGuid().ToString("N"); // 32자 hex
                CommonUtil.SavePlayerPref(PlayerPrefKey.UserToken, userToken);
                Debug.Log($"[GuestLogin] 신규 게스트 토큰 생성 및 저장: {userToken[..8]}...");
            }
            else
            {
                Debug.Log($"[GuestLogin] 기존 게스트 토큰 재사용: {userToken[..8]}...");
            }

            UserDataManager.Instance.SetUserToken(userToken);
            successAction?.Invoke(userToken, true);
        }

        #endregion

        #region 결제

        public async UniTask<ProductInfo[]> GetProductListAsync()
        {
            Debug.LogWarning("[GuestLogin] 게스트 모드에서는 결제 기능을 지원하지 않습니다.");
            await UniTask.Yield();
            return new ProductInfo[0];
        }

        public void PurchaseProduct(string sku, Action<string> onSuccess, Action<string> onError)
        {
            Debug.LogWarning("[GuestLogin] 게스트 모드에서는 결제 기능을 지원하지 않습니다.");
            onError?.Invoke("게스트 모드에서는 결제를 사용할 수 없습니다.");
        }

        #endregion

        #region 광고

        public bool IsAdSupported()
        {
            return false;
        }

        public void LoadAd(string adGroupId, Action<bool> onLoadComplete)
        {
            Debug.LogWarning("[GuestLogin] 게스트 모드에서는 광고 기능을 지원하지 않습니다.");
            onLoadComplete?.Invoke(false);
        }

        public void ShowAd(string adGroupId, Action<bool> onShowComplete)
        {
            Debug.LogWarning("[GuestLogin] 게스트 모드에서는 광고 기능을 지원하지 않습니다.");
            onShowComplete?.Invoke(false);
        }

        #endregion
    }
}
