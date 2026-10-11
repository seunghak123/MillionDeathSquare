using System;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>
    /// GameDistribution HTML5 SDK 광고 구현체.
    /// GameDistributionSDK.jslib과 통신합니다.
    ///
    /// 사용 시 주의:
    ///   - Init() → GameplayStart() → (게임 진행) → GameplayStop() → 광고 → GameplayStart() 순서 필수
    ///   - GD 규정상 게임플레이 중 광고 노출 금지 (GameplayStop 후 광고)
    /// </summary>
    public class GameDistributionAds : MonoBehaviour, IPlatformAds
    {
        private UniTaskCompletionSource<bool> _initTcs;
        private UniTaskCompletionSource<bool> _adTcs;

        // GD SDK 게임 ID — GameDistribution 개발자 콘솔에서 발급
        // 실제 배포 전에 올바른 gameId로 교체 필요
        private const string GD_GAME_ID = "YOUR_GD_GAME_ID_HERE";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void GDBridge_Init(string goName, string gameId);
        [DllImport("__Internal")] private static extern void GDBridge_GameplayStart();
        [DllImport("__Internal")] private static extern void GDBridge_GameplayStop();
        [DllImport("__Internal")] private static extern void GDBridge_ShowInterstitial(string goName);
        [DllImport("__Internal")] private static extern void GDBridge_ShowRewarded(string goName);
        [DllImport("__Internal")] private static extern int  GDBridge_IsAvailable();
#endif

        // ── IPlatformAds 구현 ─────────────────────────────────────────────

        public bool IsAvailable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return GDBridge_IsAvailable() == 1;
#else
            return false;
#endif
        }

        public async UniTask InitAsync()
        {
            _initTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            GDBridge_Init(gameObject.name, GD_GAME_ID);
#else
            Debug.Log("[GameDistributionAds] Init (dev 모드)");
            _initTcs.TrySetResult(true);
#endif
            await _initTcs.Task;
        }

        public void GameplayStart()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            GDBridge_GameplayStart();
#else
            Debug.Log("[GameDistributionAds] GameplayStart (dev)");
#endif
        }

        public void GameplayStop()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            GDBridge_GameplayStop();
#else
            Debug.Log("[GameDistributionAds] GameplayStop (dev)");
#endif
        }

        public void ShowInterstitialAd(Action<bool> onComplete)
        {
            ShowAdInternal(isRewarded: false, onComplete);
        }

        public void ShowRewardedAd(Action<bool> onComplete)
        {
            ShowAdInternal(isRewarded: true, onComplete);
        }

        // ── JS 콜백 수신 ──────────────────────────────────────────────────

        /// <summary>GD SDK → SendMessage("OnGDInitSuccess")</summary>
        public void OnGDInitSuccess(string _)
        {
            Debug.Log("[GameDistributionAds] 초기화 완료");
            _initTcs?.TrySetResult(true);
        }

        /// <summary>GD SDK → SendMessage("OnGDInitFailed")</summary>
        public void OnGDInitFailed(string error)
        {
            Debug.LogWarning($"[GameDistributionAds] 초기화 실패: {error}");
            _initTcs?.TrySetResult(false);
        }

        /// <summary>GD SDK → SendMessage("OnGDAdComplete", "1"/"0")</summary>
        public void OnGDAdComplete(string result)
        {
            bool success = result == "1";
            Debug.Log($"[GameDistributionAds] 광고 완료: {(success ? "시청" : "스킵/에러")}");
            _adTcs?.TrySetResult(success);
            _adTcs = null;
        }

        // ── 내부 ──────────────────────────────────────────────────────────

        private void ShowAdInternal(bool isRewarded, Action<bool> onComplete)
        {
            if (_adTcs != null)
            {
                Debug.LogWarning("[GameDistributionAds] 이미 광고 진행 중");
                onComplete?.Invoke(false);
                return;
            }

            _adTcs = new UniTaskCompletionSource<bool>();
            _adTcs.Task.ContinueWith(result => onComplete?.Invoke(result)).Forget();

#if UNITY_WEBGL && !UNITY_EDITOR
            if (isRewarded)
                GDBridge_ShowRewarded(gameObject.name);
            else
                GDBridge_ShowInterstitial(gameObject.name);
#else
            Debug.Log($"[GameDistributionAds] ShowAd (dev): {(isRewarded ? "rewarded" : "interstitial")}");
            _adTcs.TrySetResult(true);
            _adTcs = null;
#endif
        }
    }
}
