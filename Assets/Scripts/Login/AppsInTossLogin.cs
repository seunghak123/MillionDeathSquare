using System;
using UnityEngine;
using Seunghak.Common;
using Seunghak.Platform;
using Cysharp.Threading.Tasks;

namespace Seunghak.LoginSystem
{
    /// <summary>
    /// 앱인토스(Apps in Toss) 로그인.
    /// 서버가 없으므로 인가 코드(AppLogin)는 쓰지 않고, TossPlatform이 초기화 때 받아둔
    /// 게임 유저 키(getUserKeyForGame)를 그대로 토큰으로 씁니다.
    /// 광고 / 결제는 PlatformFactory.Current.Ads / .IAP 핸들러로 위임합니다.
    /// </summary>
    public class AppsInTossLogin : LoginInterface
    {
        #region 로그인

        public void InitLogin()
        {
            // 앱인토스 환경에서는 별도 초기화 없음 (SDK가 WebGL 로드 시 준비됨)
        }

        /// <summary>토스 게임 로그인(getUserKeyForGame) 추가 재시도 횟수와 간격.</summary>
        private const int UserKeyRetryCount = 2;
        private const int UserKeyRetryDelayMs = 1000;

        public void PlatformLogin(Action<string, bool> successResultAct)
        {
            if (successResultAct == null)
                return;
            PlatformLoginAsync(successResultAct).Forget();
        }

        /// <summary>
        /// 토스 게임 로그인. 유저 키를 받아야만 성공으로 처리합니다 (게스트 GUID 폴백 없음).
        /// 실패하면 successResultAct(null, false) — 호출부가 재시도 후 진입시킵니다.
        /// </summary>
        private static async UniTaskVoid PlatformLoginAsync(Action<string, bool> successResultAct)
        {
            if (!(PlatformFactory.Current is TossPlatform toss))
            {
                successResultAct(null, false);
                return;
            }

            // TossPlatform.InitAsync()에서 이미 조회한 유저 키 재사용, 실패했으면 다시 요청
            string token = toss.UserKey;
            for (int i = 0; i < UserKeyRetryCount && string.IsNullOrEmpty(token); i++)
            {
                await UniTask.Delay(UserKeyRetryDelayMs, DelayType.Realtime);
                token = await toss.GetUserKeyAsync();
            }

            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning("[AppsInTossLogin] 토스 게임 로그인 실패 (유저 키 없음)");
                successResultAct(null, false);
                return;
            }

            UserDataManager.Instance.SetUserToken(token);
            successResultAct(token, true);
        }

        #endregion

        #region 결제

        public UniTask<ProductInfo[]> GetProductListAsync()
            => PlatformFactory.Current.IAP.GetProductListAsync();

        public void PurchaseProduct(string sku, Action<string> onSuccess, Action<string> onError)
        {
            if (string.IsNullOrEmpty(sku))
            {
                onError?.Invoke("상품 ID가 비어있습니다.");
                return;
            }
            PurchaseAsync(sku, onSuccess, onError).Forget();
        }

        private static async UniTaskVoid PurchaseAsync(string sku, Action<string> onSuccess, Action<string> onError)
        {
            try
            {
                PurchaseResult result = await PlatformFactory.Current.IAP.PurchaseAsync(sku);
                if (result != null && result.success)
                    onSuccess?.Invoke(result.orderId);
                else
                    onError?.Invoke(result?.error ?? "알 수 없는 오류");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                onError?.Invoke(ex.Message);
            }
        }

        #endregion

        #region 광고

        public bool IsAdSupported() => PlatformFactory.Current.Ads.IsAvailable();

        /// <summary>광고 선로드는 Ads 핸들러가 알아서 하므로, 여기서는 사용 가능 여부만 알려줍니다.</summary>
        public void LoadAd(string adGroupId, Action<bool> onLoadComplete)
            => onLoadComplete?.Invoke(PlatformFactory.Current.Ads.IsAvailable());

        /// <summary>전면 광고 그룹 ID면 전면 광고, 그 외에는 보상형 광고를 표시합니다.</summary>
        public void ShowAd(string adGroupId, Action<bool> onShowComplete)
        {
            IPlatformAds ads = PlatformFactory.Current.Ads;
            if (!string.IsNullOrEmpty(adGroupId) && adGroupId == TossConfig.Instance.InterstitialAdGroupId)
                ads.ShowInterstitialAd(onShowComplete);
            else
                ads.ShowRewardedAd(onShowComplete);
        }

        #endregion
    }
}
