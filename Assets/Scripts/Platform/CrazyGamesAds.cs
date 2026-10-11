using System;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>
    /// CrazyGames SDK 광고 구현체.
    /// CrazyGamesSDK.jslib과 통신합니다.
    ///
    /// CrazyGames 특이사항:
    ///   - SDK는 포털이 index.html에 자동 삽입 (별도 script 로드 불필요)
    ///   - 광고 시작(OnCGAdStarted) 시 오디오/게임을 일시정지해야 함
    ///   - 광고 완료 후 오디오/게임 재개
    /// </summary>
    public class CrazyGamesAds : MonoBehaviour, IPlatformAds
    {
        private UniTaskCompletionSource<bool> _initTcs;
        private UniTaskCompletionSource<bool> _adTcs;
        private Action<bool> _pendingAdCallback;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CGBridge_Init(string goName);
        [DllImport("__Internal")] private static extern void CGBridge_LoadingStart();
        [DllImport("__Internal")] private static extern void CGBridge_LoadingStop();
        [DllImport("__Internal")] private static extern void CGBridge_GameplayStart();
        [DllImport("__Internal")] private static extern void CGBridge_GameplayStop();
        [DllImport("__Internal")] private static extern void CGBridge_HappyTime();
        [DllImport("__Internal")] private static extern void CGBridge_ShowInterstitial(string goName);
        [DllImport("__Internal")] private static extern void CGBridge_ShowRewarded(string goName);
        [DllImport("__Internal")] private static extern int  CGBridge_IsAvailable();
#endif

        // ── IPlatformAds 구현 ─────────────────────────────────────────────

        public bool IsAvailable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return CGBridge_IsAvailable() == 1;
#else
            return false;
#endif
        }

        public async UniTask InitAsync()
        {
            _initTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            CGBridge_Init(gameObject.name);
#else
            Debug.Log("[CrazyGamesAds] Init (dev 모드)");
            _initTcs.TrySetResult(true);
#endif
            await _initTcs.Task;
        }

        /// <summary>[필수] Unity 번들 로드 시작 시 호출 — CrazyGames 포털 로딩 화면 표시</summary>
        public void LoadingStart()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CGBridge_LoadingStart();
#else
            Debug.Log("[CrazyGamesAds] LoadingStart (dev)");
#endif
        }

        /// <summary>[필수] Unity 로드 완료 시 호출 — CrazyGames 포털 로딩 화면 해제</summary>
        public void LoadingStop()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CGBridge_LoadingStop();
#else
            Debug.Log("[CrazyGamesAds] LoadingStop (dev)");
#endif
        }

        public void GameplayStart()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CGBridge_GameplayStart();
#else
            Debug.Log("[CrazyGamesAds] GameplayStart (dev)");
#endif
        }

        public void GameplayStop()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CGBridge_GameplayStop();
#else
            Debug.Log("[CrazyGamesAds] GameplayStop (dev)");
#endif
        }

        /// <summary>플레이어 기쁜 순간(고득점 등)에 호출 — CrazyGames confetti 연출 트리거</summary>
        public void HappyTime()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CGBridge_HappyTime();
#else
            Debug.Log("[CrazyGamesAds] HappyTime (dev)");
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

        /// <summary>CG SDK → SendMessage("OnCGInitSuccess")</summary>
        public void OnCGInitSuccess(string _)
        {
            Debug.Log("[CrazyGamesAds] 초기화 완료");
            _initTcs?.TrySetResult(true);
        }

        /// <summary>CG SDK → SendMessage("OnCGAdStarted") — 광고 시작, 오디오 뮤트 필요</summary>
        public void OnCGAdStarted(string _)
        {
            Debug.Log("[CrazyGamesAds] 광고 시작 — 오디오 뮤트");
            // AudioListener.pause는 AdManager에서 처리
        }

        /// <summary>CG SDK → SendMessage("OnCGAdFinished", "1"/"0")</summary>
        public void OnCGAdFinished(string result)
        {
            bool success = result == "1";
            Debug.Log($"[CrazyGamesAds] 광고 완료: {(success ? "시청" : "스킵/에러")}");
            _adTcs?.TrySetResult(success);
            _adTcs = null;
        }

        // ── 내부 ──────────────────────────────────────────────────────────

        private void ShowAdInternal(bool isRewarded, Action<bool> onComplete)
        {
            if (_adTcs != null)
            {
                Debug.LogWarning("[CrazyGamesAds] 이미 광고 진행 중");
                onComplete?.Invoke(false);
                return;
            }

            _adTcs = new UniTaskCompletionSource<bool>();
            _adTcs.Task.ContinueWith(result => onComplete?.Invoke(result)).Forget();

#if UNITY_WEBGL && !UNITY_EDITOR
            if (isRewarded)
                CGBridge_ShowRewarded(gameObject.name);
            else
                CGBridge_ShowInterstitial(gameObject.name);
#else
            Debug.Log($"[CrazyGamesAds] ShowAd (dev): {(isRewarded ? "rewarded" : "interstitial")}");
            _adTcs.TrySetResult(true);
            _adTcs = null;
#endif
        }
    }
}
