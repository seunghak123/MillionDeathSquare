using System;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>
    /// Newgrounds.io SDK C# 래퍼.
    /// NewgroundsIO.jslib과 통신합니다.
    ///
    /// 사용 전 준비:
    ///   1. NewgroundsTemplate/index.html에서 ngio.js 로드 (자동)
    ///   2. NewgroundsBridgeHelper.InitAsync() 완료 후 API 호출
    ///
    /// 메달 ID / 스코어보드 ID는 Newgrounds 개발자 포털에서 발급:
    ///   https://www.newgrounds.com/account/games/
    /// </summary>
    public class NewgroundsBridgeHelper : MonoBehaviour
    {
        // ── Newgrounds 게임 설정 ── (Newgrounds 개발자 포털에서 발급)
        // 실제 배포 전에 교체 필요
        public const int SCOREBOARD_MAIN = 1001;  // 스코어보드 ID (포털에서 생성)
        public const int MEDAL_FIRST_STEP  = 2001;  // 메달: 첫 번째 이동
        public const int MEDAL_SCORE_10    = 2002;  // 메달: 10점 달성
        public const int MEDAL_SCORE_50    = 2003;  // 메달: 50점 달성
        public const int MEDAL_SCORE_100   = 2004;  // 메달: 100점 달성

        private UniTaskCompletionSource<bool>   _initTcs;
        private UniTaskCompletionSource<bool>   _loginTcs;
        private UniTaskCompletionSource<bool>   _scoreTcs;
        // 메달은 여러 개 동시 해제 가능하므로 ID별 TCS 관리
        private System.Collections.Generic.Dictionary<int, UniTaskCompletionSource<bool>>
            _medalTcs = new System.Collections.Generic.Dictionary<int, UniTaskCompletionSource<bool>>();

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void   NGBridge_Init(string goName);
        [DllImport("__Internal")] private static extern void   NGBridge_Login(string goName);
        [DllImport("__Internal")] private static extern string NGBridge_GetUserName();
        [DllImport("__Internal")] private static extern int    NGBridge_IsLoggedIn();
        [DllImport("__Internal")] private static extern void   NGBridge_UnlockMedal(string goName, int medalId);
        [DllImport("__Internal")] private static extern void   NGBridge_PostScore(string goName, int scoreboardId, int score, string tag);
        [DllImport("__Internal")] private static extern int    NGBridge_IsAvailable();
#endif

        // ── 공개 API ──────────────────────────────────────────────────────

        public bool IsAvailable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return NGBridge_IsAvailable() == 1;
#else
            return false;
#endif
        }

        /// <summary>Newgrounds SDK 초기화를 대기합니다.</summary>
        public async UniTask<bool> InitAsync()
        {
            _initTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            NGBridge_Init(gameObject.name);
#else
            Debug.Log("[NewgroundsBridge] Init (dev 모드)");
            _initTcs.TrySetResult(true);
#endif
            return await _initTcs.Task;
        }

        /// <summary>Newgrounds 계정 로그인 다이얼로그를 표시합니다.</summary>
        public async UniTask<bool> LoginAsync()
        {
            _loginTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            NGBridge_Login(gameObject.name);
#else
            Debug.Log("[NewgroundsBridge] Login (dev)");
            _loginTcs.TrySetResult(false);
#endif
            return await _loginTcs.Task;
        }

        public bool IsLoggedIn()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return NGBridge_IsLoggedIn() == 1;
#else
            return false;
#endif
        }

        public string GetUserName()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return NGBridge_GetUserName() ?? string.Empty;
#else
            return "dev_player";
#endif
        }

        /// <summary>메달(업적)을 해제합니다. 이미 해제됐으면 NGIO가 무시합니다.</summary>
        public async UniTask<bool> UnlockMedalAsync(int medalId)
        {
            var tcs = new UniTaskCompletionSource<bool>();
            _medalTcs[medalId] = tcs;
#if UNITY_WEBGL && !UNITY_EDITOR
            NGBridge_UnlockMedal(gameObject.name, medalId);
#else
            Debug.Log($"[NewgroundsBridge] UnlockMedal (dev): {medalId}");
            tcs.TrySetResult(true);
#endif
            return await tcs.Task;
        }

        /// <summary>스코어보드에 점수를 제출합니다.</summary>
        public async UniTask<bool> PostScoreAsync(int score, int scoreboardId = SCOREBOARD_MAIN, string tag = "")
        {
            _scoreTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            NGBridge_PostScore(gameObject.name, scoreboardId, score, tag);
#else
            Debug.Log($"[NewgroundsBridge] PostScore (dev): {score} to board {scoreboardId}");
            _scoreTcs.TrySetResult(true);
#endif
            return await _scoreTcs.Task;
        }

        // ── JS 콜백 수신 ──────────────────────────────────────────────────

        public void OnNGInitSuccess(string _)
        {
            Debug.Log("[NewgroundsBridge] 초기화 완료");
            _initTcs?.TrySetResult(true);
        }

        public void OnNGLoginResult(string result)
        {
            bool success = result == "1";
            Debug.Log($"[NewgroundsBridge] 로그인 결과: {(success ? "성공" : "실패/취소")}");
            _loginTcs?.TrySetResult(success);
        }

        /// <summary>형식: "id:{medalId}:1" 또는 "0"</summary>
        public void OnNGMedalResult(string result)
        {
            if (result == "0")
            {
                // 어떤 메달인지 모르는 실패 — 모든 대기 중 TCS 실패 처리
                foreach (var kv in _medalTcs)
                    kv.Value.TrySetResult(false);
                _medalTcs.Clear();
                return;
            }

            // 형식: "id:{medalId}:{1|0}"
            var parts = result.Split(':');
            if (parts.Length == 3 && int.TryParse(parts[1], out int medalId))
            {
                bool success = parts[2] == "1";
                Debug.Log($"[NewgroundsBridge] 메달 {medalId} 해제: {(success ? "성공" : "실패")}");
                if (_medalTcs.TryGetValue(medalId, out var tcs))
                {
                    tcs.TrySetResult(success);
                    _medalTcs.Remove(medalId);
                }
            }
        }

        public void OnNGScoreResult(string result)
        {
            bool success = result == "1";
            Debug.Log($"[NewgroundsBridge] 점수 제출 결과: {(success ? "성공" : "실패")}");
            _scoreTcs?.TrySetResult(success);
        }

        // ── 점수 연동 헬퍼 ────────────────────────────────────────────────

        /// <summary>
        /// 게임 종료 시 호출 편의 메서드.
        /// 점수 제출 + 점수별 메달 자동 해제.
        /// </summary>
        public async UniTaskVoid SubmitGameResult(int score)
        {
            if (!IsAvailable()) return;

            // 점수 제출
            await PostScoreAsync(score);

            // 점수 구간별 메달 해제 (비동기, 독립 실행)
            if (score >= 1)   UnlockMedalAsync(MEDAL_FIRST_STEP).Forget();
            if (score >= 10)  UnlockMedalAsync(MEDAL_SCORE_10).Forget();
            if (score >= 50)  UnlockMedalAsync(MEDAL_SCORE_50).Forget();
            if (score >= 100) UnlockMedalAsync(MEDAL_SCORE_100).Forget();
        }
    }
}
