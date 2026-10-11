using System;
using Cysharp.Threading.Tasks;

namespace Seunghak.Platform
{
    /// <summary>
    /// 웹 광고 플랫폼 추상화 인터페이스.
    ///
    /// 구현체:
    ///   - GameDistributionAds  — GameDistribution HTML5 SDK
    ///   - CrazyGamesAds        — CrazyGames SDK
    ///   - NullAds              — 미지원 플랫폼용 no-op 구현
    ///
    /// 사용 예 (AdManager 경유):
    ///   AdManager.Instance.ShowInterstitialAd(success => { ... });
    ///   AdManager.Instance.ShowRewardedAd(success => { ... });
    /// </summary>
    public interface IPlatformAds
    {
        /// <summary>현재 환경에서 광고 SDK가 사용 가능한지 확인합니다.</summary>
        bool IsAvailable();

        /// <summary>SDK 초기화를 비동기로 수행합니다.</summary>
        UniTask InitAsync();

        /// <summary>
        /// 게임플레이 시작을 광고 SDK에 알립니다.
        /// GameDistribution, CrazyGames 모두 필수 호출 — 호출하지 않으면 광고가 올바르게 동작하지 않습니다.
        /// </summary>
        void GameplayStart();

        /// <summary>
        /// 게임플레이 종료(게임오버, 일시정지 등)를 SDK에 알립니다.
        /// GameplayStart() 후에는 반드시 쌍으로 호출해야 합니다.
        /// </summary>
        void GameplayStop();

        /// <summary>
        /// 전면 광고를 표시합니다. (게임오버 / 씬 전환 시 자동 노출)
        /// <para><paramref name="onComplete"/>: 광고 완료 또는 에러 시 호출. success=true이면 정상 시청.</para>
        /// </summary>
        void ShowInterstitialAd(Action<bool> onComplete);

        /// <summary>
        /// 보상형 광고를 표시합니다. (플레이어가 직접 요청 시)
        /// <para><paramref name="onComplete"/>: success=true이면 보상 지급 가능.</para>
        /// </summary>
        void ShowRewardedAd(Action<bool> onComplete);
    }

    /// <summary>
    /// 광고 미지원 플랫폼(Toss, Editor 등)용 no-op 구현.
    /// AdManager가 내부적으로 사용합니다.
    /// </summary>
    public sealed class NullAds : IPlatformAds
    {
        public static readonly NullAds Instance = new NullAds();
        public bool IsAvailable() => false;
        public UniTask InitAsync() => UniTask.CompletedTask;
        public void GameplayStart() { }
        public void GameplayStop() { }
        public void ShowInterstitialAd(Action<bool> onComplete) => onComplete?.Invoke(false);
        public void ShowRewardedAd(Action<bool> onComplete) => onComplete?.Invoke(false);
    }
}
