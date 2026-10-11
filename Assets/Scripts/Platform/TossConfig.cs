using System;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>
    /// 토스(앱인토스) 콘솔에서 발급받는 ID 모음. dev / prod 두 벌을 가집니다.
    ///
    /// 환경은 빌드 시점에 스크립팅 심볼로 고정됩니다:
    ///   - TOSS_PROD 정의됨  → prod (실제 광고 / 실제 프로모션)
    ///   - TOSS_PROD 없음    → dev  (테스트 광고 / 테스트 프로모션)
    /// 심볼은 WebGLBuildTool의 토스 빌드가 설정합니다 (직접 건드리지 마세요).
    ///
    /// Assets/Resources/TossConfig.asset 에서 로드하며, 에셋이 없으면 기본값으로 동작합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "TossConfig", menuName = "Seunghak/Toss Config")]
    public class TossConfig : ScriptableObject
    {
        private const string RESOURCE_PATH = "TossConfig";

#if TOSS_PROD
        public const bool IsProd = true;
#else
        public const bool IsProd = false;
#endif

        /// <summary>토스 dev 배포 빌드 여부 (TOSS_DEV 심볼). 로비 dev 표시에 사용.</summary>
#if TOSS_DEV
        public const bool IsDevBuild = true;
#else
        public const bool IsDevBuild = false;
#endif

        [Serializable]
        public class EnvIds
        {
            [Header("광고 그룹 ID")]
            public string rewardedAdGroupId = "";
            public string interstitialAdGroupId = "";
            public string bannerAdGroupId = "";

            [Header("친구 초대 (contactsViral) — 콘솔 발급 UUID")]
            public string contactsViralModuleId = "";

            [Header("프로모션 (토스포인트 지급)")]
            public string promotionCode = "";
            public int promotionAmount = 0;
        }

        [Serializable]
        public class IapProduct
        {
            public string sku;
            public int goldAmount;
        }

        [Header("dev — 테스트 광고 / 테스트 프로모션")]
        public EnvIds dev = new EnvIds
        {
            rewardedAdGroupId = "ait-ad-test-rewarded-id",
            interstitialAdGroupId = "ait-ad-test-interstitial-id",
            bannerAdGroupId = "ait-ad-test-banner-id",
        };

        [Header("prod — 실제 광고 / 실제 프로모션 (콘솔 발급값 입력)")]
        public EnvIds prod = new EnvIds();

        [Header("공유 딥링크")]
        public string shareDeepLink = "intoss://gotohome";

        [Header("인앱결제 상품 → 지급 Gold")]
        public IapProduct[] iapProducts = new IapProduct[0];

        /// <summary>현재 빌드 환경(dev/prod)의 ID 세트.</summary>
        public EnvIds Current => IsProd ? prod : dev;

        public string RewardedAdGroupId => Current.rewardedAdGroupId;
        public string InterstitialAdGroupId => Current.interstitialAdGroupId;
        public string BannerAdGroupId => Current.bannerAdGroupId;
        public string ContactsViralModuleId => Current.contactsViralModuleId;
        public string PromotionCode => Current.promotionCode;
        public int PromotionAmount => Current.promotionAmount;

        private static TossConfig _instance;

        public static TossConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<TossConfig>(RESOURCE_PATH);
                    if (_instance == null)
                    {
                        Debug.LogWarning("[TossConfig] Resources/TossConfig.asset 없음 — 기본값 사용");
                        _instance = CreateInstance<TossConfig>();
                    }
                }
                return _instance;
            }
        }

        /// <summary>sku에 대응하는 지급 Gold. 등록되지 않은 sku면 0.</summary>
        public int GetGoldAmount(string sku)
        {
            for (int i = 0; i < iapProducts.Length; i++)
            {
                if (iapProducts[i].sku == sku)
                    return iapProducts[i].goldAmount;
            }
            return 0;
        }
    }
}
