using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using AppsInToss;
#endif

namespace Seunghak.Platform
{
    /// <summary>
    /// 토스(앱인토스) 통합 전면 광고(AIT.LoadFullScreenAd / ShowFullScreenAd) 구현체.
    ///
    /// 흐름:
    ///   InitAsync  → 보상형·전면 광고 미리 로드
    ///   Show*      → 로드돼 있으면 즉시 노출, 아니면 로드 시도(최대 LOAD_WAIT_SEC) 후 노출
    ///   노출 종료(dismissed / failedToShow / onError) → 콜백 1회 호출 후 다음 광고 재로드
    ///
    /// 보상형은 userEarnedReward 이벤트를 받은 경우에만 성공(true)으로 처리하며,
    /// 이벤트를 끝까지 받기 위해 노출(Show) 구독은 dismissed 시점까지 해제하지 않습니다.
    /// 에디터/비WebGL 에서는 AdManager 가 자동 처리하므로 항상 실패(false)를 반환합니다.
    /// </summary>
    public sealed class TossAds : IPlatformAds
    {
        private const float LOAD_WAIT_SEC = 10f;       // 미로드 상태에서 로드 완료 대기 한도
        private const float SHOW_TIMEOUT_SEC = 90f;    // 노출 이벤트가 끊길 때 안전 타임아웃
        private const float REWARD_GRACE_SEC = 0.3f;   // dismissed 직후 userEarnedReward 지연 도착 대비

        private sealed class AdSlot
        {
            public bool IsRewarded;
            public bool IsLoaded;
            public bool IsLoading;
            public bool IsShowing;
            public Action UnsubLoad;
        }

        private readonly AdSlot _rewarded = new AdSlot { IsRewarded = true };
        private readonly AdSlot _interstitial = new AdSlot();

        // ── IPlatformAds 구현 ─────────────────────────────────────────────

        public bool IsAvailable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return !string.IsNullOrEmpty(GetGroupId(_rewarded)) || !string.IsNullOrEmpty(GetGroupId(_interstitial));
#else
            return false;
#endif
        }

        public UniTask InitAsync()
        {
            Load(_rewarded);
            Load(_interstitial);
            return UniTask.CompletedTask;
        }

        public void GameplayStart() { }

        public void GameplayStop() { }

        public void ShowInterstitialAd(Action<bool> onComplete)
        {
            ShowAsync(_interstitial, onComplete).Forget();
        }

        public void ShowRewardedAd(Action<bool> onComplete)
        {
            ShowAsync(_rewarded, onComplete).Forget();
        }

        // ── 내부 구현 ─────────────────────────────────────────────────────

        private static string GetGroupId(AdSlot slot)
        {
            var config = TossConfig.Instance;
            return slot.IsRewarded ? config.RewardedAdGroupId : config.InterstitialAdGroupId;
        }

        private void Load(AdSlot slot)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string groupId = GetGroupId(slot);
            if (string.IsNullOrEmpty(groupId) || slot.IsLoaded || slot.IsLoading) return;

            slot.IsLoading = true;
            try
            {
                // 로드 구독은 loaded(성공) 또는 onError(실패) 에서만 해제한다.
                // (userEarnedReward 는 Show 구독 이벤트이므로 로드 구독과 무관)
                slot.UnsubLoad = AIT.LoadFullScreenAd(
                    groupId,
                    e =>
                    {
                        if (e == null || e.Type != "loaded") return;
                        slot.IsLoading = false;
                        slot.IsLoaded = true;
                        ReleaseLoad(slot);
                    },
                    ex =>
                    {
                        Debug.LogWarning($"[TossAds] 광고 로드 실패({(slot.IsRewarded ? "보상형" : "전면")}): {ex?.Message}");
                        slot.IsLoading = false;
                        ReleaseLoad(slot);
                    });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TossAds] LoadFullScreenAd 예외: {ex.Message}");
                slot.IsLoading = false;
            }
#endif
        }

        private static void ReleaseLoad(AdSlot slot)
        {
            var unsub = slot.UnsubLoad;
            slot.UnsubLoad = null;
            unsub?.Invoke();
        }

        private async UniTaskVoid ShowAsync(AdSlot slot, Action<bool> onComplete)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string groupId = GetGroupId(slot);
            if (string.IsNullOrEmpty(groupId) || slot.IsShowing)
            {
                onComplete?.Invoke(false);
                return;
            }

            // 미로드 상태면 로드를 시도하고 일정 시간 내 완료되길 기다린다.
            if (!slot.IsLoaded)
            {
                Load(slot);
                float waited = 0f;
                while (!slot.IsLoaded && slot.IsLoading && waited < LOAD_WAIT_SEC)
                {
                    await UniTask.Yield();
                    waited += Time.unscaledDeltaTime;
                }
                if (!slot.IsLoaded)
                {
                    Debug.LogWarning("[TossAds] 광고 로드 대기 시간 초과 — 노출 취소");
                    onComplete?.Invoke(false);
                    return;
                }
            }

            slot.IsShowing = true;
            slot.IsLoaded = false; // 한 번 노출한 광고는 재사용 불가

            bool finished = false;
            bool earned = false;
            Action unsubShow = null;

            void Finish(bool result)
            {
                if (finished) return; // 콜백은 정확히 한 번
                finished = true;
                slot.IsShowing = false;
                unsubShow?.Invoke();
                onComplete?.Invoke(result);
                Load(slot); // 다음 광고 미리 로드
            }

            async UniTaskVoid FinishAfterGrace()
            {
                // dismissed 가 userEarnedReward 보다 먼저 올 가능성에 대비해 잠시 대기 후 확정
                await UniTask.Delay(TimeSpan.FromSeconds(REWARD_GRACE_SEC), ignoreTimeScale: true);
                Finish(earned);
            }

            try
            {
                unsubShow = AIT.ShowFullScreenAd(
                    groupId,
                    e =>
                    {
                        if (e == null || finished) return;
                        switch (e.Type)
                        {
                            case "userEarnedReward":
                                earned = true;
                                break;
                            case "dismissed":
                                if (slot.IsRewarded) FinishAfterGrace().Forget();
                                else Finish(true);
                                break;
                            case "failedToShow":
                                Finish(false);
                                break;
                        }
                    },
                    ex =>
                    {
                        Debug.LogWarning($"[TossAds] 광고 노출 실패: {ex?.Message}");
                        Finish(false);
                    });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TossAds] ShowFullScreenAd 예외: {ex.Message}");
                Finish(false);
                return;
            }

            // 이벤트가 오지 않는 경우를 대비한 안전 타임아웃
            await UniTask.Delay(TimeSpan.FromSeconds(SHOW_TIMEOUT_SEC), ignoreTimeScale: true);
            if (!finished)
            {
                Debug.LogWarning("[TossAds] 광고 노출 타임아웃");
                Finish(false);
            }
#else
            onComplete?.Invoke(false);
            await UniTask.CompletedTask;
#endif
        }
    }
}
