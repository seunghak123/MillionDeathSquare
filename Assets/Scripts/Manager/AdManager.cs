using System;
using Cysharp.Threading.Tasks;
using Seunghak.Platform;
using UnityEngine;

namespace Seunghak.Manager
{
    /// <summary>
    /// 플랫폼별 광고 분기 매니저.
    ///
    /// 런타임에 GameDistribution / CrazyGames 환경을 자동 감지하고, 그 외에는 PlatformFactory.Current.Ads(토스 = TossAds)를 사용하여
    /// 적절한 IPlatformAds 구현체로 광고 요청을 라우팅합니다.
    ///
    /// 사용법:
    ///   AdManager.Instance.ShowInterstitialAdAsync();  // 전면 광고
    ///   AdManager.Instance.ShowRewardedAdAsync(onSuccess);  // 보상 광고
    ///
    /// 광고 타이밍 규칙:
    ///   - 게임오버 시 전면 광고 (GameplayStop → ShowInterstitial → GameplayStart)
    ///   - 재시작 버튼 클릭 후 광고 (최소 간격 조건 충족 시)
    ///   - 보상 광고는 플레이어 명시 요청 시만 표시
    ///
    /// ApplicationManager.InitApplicationInfo() 에서 초기화됩니다.
    /// </summary>
    public class AdManager : UnitySingleton<AdManager>
    {
        // 전면 광고 최소 노출 간격 (초) — 너무 자주 보여주면 규정 위반
        private const float INTERSTITIAL_COOLDOWN_SEC = 120f;

        private IPlatformAds _ads = NullAds.Instance;
        private float _lastInterstitialTime = -9999f;
        private bool _isShowingAd;

        // ── 초기화 ────────────────────────────────────────────────────────

        /// <summary>
        /// ApplicationManager에서 호출. 플랫폼을 감지하고 SDK를 초기화합니다.
        /// </summary>
        public async UniTask InitAsync()
        {
            _ads = CreateAdsForPlatform();
            if (_ads is NullAds)
            {
                Debug.Log("[AdManager] 광고 미지원 플랫폼 — NullAds 사용");
                return;
            }

            Debug.Log($"[AdManager] 광고 SDK 초기화: {_ads.GetType().Name}");
            await _ads.InitAsync();
            Debug.Log($"[AdManager] 광고 SDK 준비 완료. IsAvailable={_ads.IsAvailable()}");
        }

        // ── 게임플레이 상태 알림 ──────────────────────────────────────────

        /// <summary>게임 시작 시 호출. SDK에 게임플레이 시작을 알립니다.</summary>
        public void NotifyGameplayStart()
        {
            _ads.GameplayStart();
        }

        /// <summary>게임오버/일시정지 시 호출. SDK에 게임플레이 종료를 알립니다.</summary>
        public void NotifyGameplayStop()
        {
            _ads.GameplayStop();
        }

        // ── 전면 광고 ─────────────────────────────────────────────────────

        /// <summary>
        /// 전면 광고를 표시합니다.
        /// 쿨다운 미충족 또는 광고 미지원 시 즉시 onComplete(false) 호출.
        ///
        /// 광고 전후 GameplayStop/GameplayStart 및 AudioListener.pause 자동 처리.
        /// </summary>
        public void ShowInterstitialAd(Action<bool> onComplete = null)
        {
#if UNITY_EDITOR
            Debug.Log("[AdManager] 에디터 플레이 — 전면 광고 자동 성공 처리");
            onComplete?.Invoke(true);
            return;
#else
            if (!CanShowInterstitial())
            {
                onComplete?.Invoke(false);
                return;
            }
            ShowInterstitialInternal(onComplete).Forget();
#endif
        }

        private async UniTaskVoid ShowInterstitialInternal(Action<bool> onComplete)
        {
            if (_isShowingAd)
            {
                onComplete?.Invoke(false);
                return;
            }

            _isShowingAd = true;
            _lastInterstitialTime = Time.realtimeSinceStartup;

            // 광고 전: 게임 일시정지 + 오디오 뮤트
            _ads.GameplayStop();
            AudioListener.pause = true;
            // AudioListener.pause만으론 광고 중 visibilitychange(포그라운드) → ResumeAll()이 소스를 다시 살리는 걸
            // 막지 못한다. SoundManager의 광고 뮤트 플래그도 같이 걸어서 광고가 끝날 때까지 재개를 차단한다.
            SoundManager.Instance?.PauseForAd();

            bool result = false;
            bool completed = false;

            _ads.ShowInterstitialAd(success =>
            {
                result = success;
                completed = true;
            });

            // 콜백 대기 (최대 120초 타임아웃 — 영상 광고가 30초를 넘는 경우가 있어 여유를 둔다)
            float elapsed = 0f;
            while (!completed && elapsed < 120f)
            {
                await UniTask.Yield();
                elapsed += Time.unscaledDeltaTime;
            }

            // 광고 후: 오디오 재개 + 게임 재개
            AudioListener.pause = false;
            SoundManager.Instance?.ResumeFromAd();
            _ads.GameplayStart();
            _isShowingAd = false;

            Debug.Log($"[AdManager] 전면 광고 결과: {(result ? "시청" : "스킵/에러")}");
            onComplete?.Invoke(result);
        }

        // ── 보상형 광고 ───────────────────────────────────────────────────

        /// <summary>
        /// 보상형 광고를 표시합니다.
        /// 성공(시청 완료) 시 onSuccess 호출.
        /// </summary>
        public void ShowRewardedAd(Action onSuccess, Action onFailed = null)
        {
#if UNITY_EDITOR
            Debug.Log("[AdManager] 에디터 플레이 — 보상 광고 자동 성공 처리");
            onSuccess?.Invoke();
            return;
#else
            if (!_ads.IsAvailable())
            {
                Debug.Log("[AdManager] 보상 광고 미지원 플랫폼");
                onFailed?.Invoke();
                return;
            }
            ShowRewardedInternal(onSuccess, onFailed).Forget();
#endif
        }

        private async UniTaskVoid ShowRewardedInternal(Action onSuccess, Action onFailed)
        {
            if (_isShowingAd)
            {
                onFailed?.Invoke();
                return;
            }

            _isShowingAd = true;
            _ads.GameplayStop();
            AudioListener.pause = true;
            // AudioListener.pause만으론 광고 중 visibilitychange(포그라운드) → ResumeAll()이 소스를 다시 살리는 걸
            // 막지 못한다. SoundManager의 광고 뮤트 플래그도 같이 걸어서 광고가 끝날 때까지 재개를 차단한다.
            SoundManager.Instance?.PauseForAd();

            bool result = false;
            bool completed = false;

            _ads.ShowRewardedAd(success =>
            {
                result = success;
                completed = true;
            });

            float elapsed = 0f;
            while (!completed && elapsed < 120f)
            {
                await UniTask.Yield();
                elapsed += Time.unscaledDeltaTime;
            }

            AudioListener.pause = false;
            SoundManager.Instance?.ResumeFromAd();
            _ads.GameplayStart();
            _isShowingAd = false;

            if (result)
                onSuccess?.Invoke();
            else
                onFailed?.Invoke();
        }

        // ── UniTask 래퍼 ──────────────────────────────────────────────────

        /// <summary>전면 광고를 표시하고 결과(시청=true)를 반환합니다.</summary>
        public UniTask<bool> ShowInterstitialAdAsync()
        {
            var tcs = new UniTaskCompletionSource<bool>();
            ShowInterstitialAd(result => tcs.TrySetResult(result));
            return tcs.Task;
        }

        /// <summary>보상형 광고를 표시하고 시청 완료 여부를 반환합니다.</summary>
        public UniTask<bool> ShowRewardedAdAsync()
        {
            var tcs = new UniTaskCompletionSource<bool>();
            ShowRewardedAd(
                onSuccess: () => tcs.TrySetResult(true),
                onFailed:  () => tcs.TrySetResult(false)
            );
            return tcs.Task;
        }

        // ── 상태 조회 ─────────────────────────────────────────────────────

#if UNITY_EDITOR
        public bool IsAdSupported() => true;

        public bool CanShowInterstitial() => true;
#else
        public bool IsAdSupported() => _ads.IsAvailable();

        public bool CanShowInterstitial()
        {
            if (!_ads.IsAvailable()) return false;
            if (_isShowingAd) return false;
            float elapsed = Time.realtimeSinceStartup - _lastInterstitialTime;
            return elapsed >= INTERSTITIAL_COOLDOWN_SEC;
        }
#endif

        // ── 플랫폼 감지 ───────────────────────────────────────────────────

        private IPlatformAds CreateAdsForPlatform()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // CrazyGames: window.CrazyGames 객체가 있으면 CrazyGames 환경
            // GameDistribution: 그 외 GD 빌드 환경 (빌드 시 GAME_DISTRIBUTION 심볼 정의)
#if GAME_DISTRIBUTION
            var gdAds = gameObject.AddComponent<GameDistributionAds>();
            return gdAds;
#elif CRAZY_GAMES
            var cgAds = gameObject.AddComponent<CrazyGamesAds>();
            return cgAds;
#else
            // 그 외(토스 등): 현재 플랫폼이 제공하는 광고 구현체 사용 (토스 → TossAds)
            return PlatformFactory.Current.Ads;
#endif
#else
            // 에디터: 플랫폼 심볼로 테스트 가능
#if GAME_DISTRIBUTION
            var gdAds = gameObject.AddComponent<GameDistributionAds>();
            return gdAds;
#elif CRAZY_GAMES
            var cgAds = gameObject.AddComponent<CrazyGamesAds>();
            return cgAds;
#else
            return PlatformFactory.Current.Ads;
#endif
#endif
        }
    }
}
