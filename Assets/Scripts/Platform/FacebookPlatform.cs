using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>
    /// Facebook Instant Games 플랫폼 (SDK 8.0).
    /// FacebookBridge.jslib와 통신하며, 초기화 / 점수 제출 / 공유 / 리더보드 /
    /// 봇 구독 / 홈화면 바로가기 / 토너먼트 / 초대 / 플레이어 데이터를 담당합니다.
    /// PlatformFactory.Create()로 생성됩니다.
    ///
    /// InitAsync 흐름:
    ///   initializeAsync → SetLoadingProgress(100) → RegisterPauseCallback → startGameAsync
    /// </summary>
    public class FacebookPlatform : MonoBehaviour, IPlatform
    {
        public const string LeaderboardName = "milliondeathsquare_score";

#if UNITY_WEBGL && !UNITY_EDITOR
        // ── 초기화 / 필수 ──────────────────────────────────────────────────
        [DllImport("__Internal")] private static extern void FBBridge_Initialize(string goName);
        [DllImport("__Internal")] private static extern void FBBridge_SetLoadingProgress(int percent);
        [DllImport("__Internal")] private static extern void FBBridge_RegisterPauseCallback(string goName);
        [DllImport("__Internal")] private static extern void FBBridge_StartGame(string goName);
        [DllImport("__Internal")] private static extern int  FBBridge_IsAvailable();

        // ── 플레이어 정보 ──────────────────────────────────────────────────
        [DllImport("__Internal")] private static extern string FBBridge_GetPlayerId();
        [DllImport("__Internal")] private static extern string FBBridge_GetPlayerName();
        [DllImport("__Internal")] private static extern string FBBridge_GetPlayerPhoto();

        // ── 플레이어 클라우드 저장소 ────────────────────────────────────────
        [DllImport("__Internal")] private static extern void FBBridge_SetPlayerData(string goName, string dataJson);
        [DllImport("__Internal")] private static extern void FBBridge_GetPlayerData(string goName, string keysJson);

        // ── 봇 구독 ───────────────────────────────────────────────────────
        [DllImport("__Internal")] private static extern void FBBridge_CanSubscribeBot(string goName);
        [DllImport("__Internal")] private static extern void FBBridge_SubscribeBot(string goName);

        // ── 홈화면 바로가기 ────────────────────────────────────────────────
        [DllImport("__Internal")] private static extern void FBBridge_CanCreateShortcut(string goName);
        [DllImport("__Internal")] private static extern void FBBridge_CreateShortcut(string goName);

        // ── 분석 ─────────────────────────────────────────────────────────
        [DllImport("__Internal")] private static extern void FBBridge_LogEvent(string eventName, float value, string paramsJson);

        // ── 리더보드 ──────────────────────────────────────────────────────
        [DllImport("__Internal")] private static extern void FBBridge_SubmitScore(string goName, string leaderboard, int score);
        [DllImport("__Internal")] private static extern void FBBridge_GetFriendScores(string goName, string leaderboard, int count);

        // ── 공유 / 초대 ───────────────────────────────────────────────────
        [DllImport("__Internal")] private static extern void FBBridge_Share(string goName, string text, string imageUrl);
        [DllImport("__Internal")] private static extern void FBBridge_Invite(string goName, string text, string imageUrl);

        // ── 토너먼트 ──────────────────────────────────────────────────────
        [DllImport("__Internal")] private static extern void FBBridge_GetTournament(string goName);
        [DllImport("__Internal")] private static extern void FBBridge_CreateTournament(string goName, string score, string title);
        [DllImport("__Internal")] private static extern void FBBridge_PostTournamentScore(string goName, int score);
        [DllImport("__Internal")] private static extern void FBBridge_ShareTournament(string goName, int score);
#endif

        // ── TCS 필드 ─────────────────────────────────────────────────────
        private UniTaskCompletionSource<bool>                    _initTcs;
        private UniTaskCompletionSource<bool>                    _startGameTcs;
        private UniTaskCompletionSource<bool>                    _submitScoreTcs;
        private UniTaskCompletionSource<bool>                    _shareTcs;
        private UniTaskCompletionSource<List<FBLeaderboardEntry>> _friendScoresTcs;
        private UniTaskCompletionSource<bool>                    _canSubscribeBotTcs;
        private UniTaskCompletionSource<bool>                    _subscribeBotTcs;
        private UniTaskCompletionSource<bool>                    _canCreateShortcutTcs;
        private UniTaskCompletionSource<bool>                    _createShortcutTcs;
        private UniTaskCompletionSource<bool>                    _inviteTcs;
        private UniTaskCompletionSource<string>                  _getPlayerDataTcs;
        private UniTaskCompletionSource<bool>                    _setPlayerDataTcs;
        private UniTaskCompletionSource<TournamentData>          _getTournamentTcs;
        private UniTaskCompletionSource<bool>                    _createTournamentTcs;
        private UniTaskCompletionSource<bool>                    _postTournamentScoreTcs;
        private UniTaskCompletionSource<bool>                    _shareTournamentTcs;

        // ── 이벤트 ───────────────────────────────────────────────────────
        /// <summary>앱 일시정지(전화·알림 등) 시 발생</summary>
        public event Action OnPause;

        // ── 정적 환경 감지 (PlatformFactory에서 호출) ────────────────────

        public static bool CheckIsAvailable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return FBBridge_IsAvailable() == 1;
#else
            return false;
#endif
        }

        // ── IPlatform 구현 ────────────────────────────────────────────────

        public async UniTask InitAsync()
        {
            // Step 1: initializeAsync
            _initTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_Initialize(gameObject.name);
#else
            Debug.Log("[FacebookPlatform] Initialize (dev)");
            _initTcs.TrySetResult(true);
#endif
            await _initTcs.Task;

            // Step 2: setLoadingProgress(100) + onPause 등록 (FB 정책 필수)
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_SetLoadingProgress(100);
            FBBridge_RegisterPauseCallback(gameObject.name);
#endif

            // Step 3: startGameAsync
            _startGameTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_StartGame(gameObject.name);
#else
            Debug.Log("[FacebookPlatform] StartGame (dev)");
            _startGameTcs.TrySetResult(true);
#endif
            await _startGameTcs.Task;
            Debug.Log("[FacebookPlatform] 초기화 완료");
        }

        public string GetPlayerId()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return FBBridge_GetPlayerId() ?? string.Empty;
#else
            return "fb_dev_player";
#endif
        }

        public string GetPlayerName()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return FBBridge_GetPlayerName() ?? string.Empty;
#else
            return "Dev Player";
#endif
        }

        /// <summary>플레이어 프로필 사진 URL. 없으면 빈 문자열 반환.</summary>
        public string GetPlayerPhotoUrl()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return FBBridge_GetPlayerPhoto() ?? string.Empty;
#else
            return string.Empty;
#endif
        }

        public void OnGameStart()
        {
            Debug.Log("[FacebookPlatform] OnGameStart");
        }

        public void OnGameEnd(int score, int bestScore)
        {
            Debug.Log($"[FacebookPlatform] OnGameEnd: score={score}");
            SubmitScoreAsync(score).Forget();
        }

        public async UniTask<bool> SubmitScoreAsync(int score)
        {
            if (!CheckIsAvailable()) return false;
            _submitScoreTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_SubmitScore(gameObject.name, LeaderboardName, score);
#else
            Debug.Log($"[FacebookPlatform] SubmitScore (dev): {score}");
            _submitScoreTcs.TrySetResult(true);
#endif
            return await _submitScoreTcs.Task;
        }

        public async UniTask<List<FBLeaderboardEntry>> GetFriendScoresAsync(int count = 10)
        {
            if (!CheckIsAvailable()) return new List<FBLeaderboardEntry>();
            _friendScoresTcs = new UniTaskCompletionSource<List<FBLeaderboardEntry>>();
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_GetFriendScores(gameObject.name, LeaderboardName, count);
#else
            Debug.Log($"[FacebookPlatform] GetFriendScores (dev): top {count}");
            _friendScoresTcs.TrySetResult(new List<FBLeaderboardEntry>());
#endif
            return await _friendScoresTcs.Task;
        }

        public async UniTask<bool> ShareResultAsync(int score, string imageUrl = "")
        {
            if (!CheckIsAvailable()) return false;
            _shareTcs = new UniTaskCompletionSource<bool>();
            string text = $"백만 번 죽는 네모에서 {score}번 죽고 클리어했어요! 나보다 덜 죽을 수 있을까요?";
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_Share(gameObject.name, text, imageUrl);
#else
            Debug.Log($"[FacebookPlatform] Share (dev): {text}");
            _shareTcs.TrySetResult(true);
#endif
            return await _shareTcs.Task;
        }

        public void RequestBack()
        {
            Debug.Log("[FacebookPlatform] RequestBack (Facebook Instant Games는 뒤로가기 미지원)");
        }

        public void Vibrate(int type = 0) { }

        public void SetLoadingProgress(float percent)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_SetLoadingProgress((int)percent);
#else
            Debug.Log($"[FacebookPlatform] SetLoadingProgress (dev): {percent}%");
#endif
        }

        public void LogEvent(string eventName, float value = 0f, string paramsJson = "")
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_LogEvent(eventName, value, paramsJson ?? "");
#else
            Debug.Log($"[FacebookPlatform] LogEvent (dev): {eventName}, value={value}");
#endif
        }

        public void GenerateHaptic(string hapticType) { /* FB는 햅틱 미지원 */ }

        // 플랫폼 핸들러 (contracts.md 6장) — FB는 아직 전용 핸들러가 없어 기본값 사용
        public IPlatformStorage Storage => PlayerPrefsStorage.Instance;
        public IPlatformAds Ads => NullAds.Instance;
        public IPlatformIAP IAP => NullIAP.Instance;
        public bool SupportsLeaderboardUI => false;
        public UniTask<bool> OpenLeaderboardAsync() => UniTask.FromResult(false);
        public bool SupportsInvite => false;
        public UniTask<int> InviteFriendsAsync() => UniTask.FromResult(0);
        public SafeAreaInset SafeAreaInset => SafeAreaInset.Zero;

        // ── Facebook 전용 기능 ────────────────────────────────────────────

        /// <summary>봇 구독 가능 여부 확인 후 구독 다이얼로그 표시 (주당 1회 제한)</summary>
        public async UniTask<bool> TrySubscribeBotAsync()
        {
            if (!CheckIsAvailable()) return false;

            // canSubscribeBot 확인
            _canSubscribeBotTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_CanSubscribeBot(gameObject.name);
#else
            _canSubscribeBotTcs.TrySetResult(false);
#endif
            bool can = await _canSubscribeBotTcs.Task;
            if (!can) return false;

            // 구독 다이얼로그
            _subscribeBotTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_SubscribeBot(gameObject.name);
#else
            _subscribeBotTcs.TrySetResult(false);
#endif
            return await _subscribeBotTcs.Task;
        }

        /// <summary>홈화면 바로가기 추가 가능 여부 확인 후 추가 (세션당 1회 제한)</summary>
        public async UniTask<bool> TryCreateShortcutAsync()
        {
            if (!CheckIsAvailable()) return false;

            _canCreateShortcutTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_CanCreateShortcut(gameObject.name);
#else
            _canCreateShortcutTcs.TrySetResult(false);
#endif
            bool can = await _canCreateShortcutTcs.Task;
            if (!can) return false;

            _createShortcutTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_CreateShortcut(gameObject.name);
#else
            _createShortcutTcs.TrySetResult(false);
#endif
            return await _createShortcutTcs.Task;
        }

        /// <summary>친구 초대 다이얼로그 표시</summary>
        public async UniTask<bool> InviteAsync(string text, string imageUrl = "")
        {
            if (!CheckIsAvailable()) return false;
            _inviteTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_Invite(gameObject.name, text, imageUrl ?? "");
#else
            Debug.Log($"[FacebookPlatform] Invite (dev): {text}");
            _inviteTcs.TrySetResult(false);
#endif
            return await _inviteTcs.Task;
        }

        /// <summary>플레이어 클라우드 데이터 저장. dataJson: JSON 직렬화된 key-value 객체</summary>
        public async UniTask<bool> SetPlayerDataAsync(string dataJson)
        {
            if (!CheckIsAvailable()) return false;
            _setPlayerDataTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_SetPlayerData(gameObject.name, dataJson);
#else
            Debug.Log($"[FacebookPlatform] SetPlayerData (dev): {dataJson}");
            _setPlayerDataTcs.TrySetResult(true);
#endif
            return await _setPlayerDataTcs.Task;
        }

        /// <summary>플레이어 클라우드 데이터 조회. keysJson: JSON 배열 ex) ["score","level"]</summary>
        public async UniTask<string> GetPlayerDataAsync(string keysJson)
        {
            if (!CheckIsAvailable()) return "{}";
            _getPlayerDataTcs = new UniTaskCompletionSource<string>();
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_GetPlayerData(gameObject.name, keysJson);
#else
            Debug.Log($"[FacebookPlatform] GetPlayerData (dev): {keysJson}");
            _getPlayerDataTcs.TrySetResult("{}");
#endif
            return await _getPlayerDataTcs.Task;
        }

        /// <summary>현재 Context의 토너먼트 정보 조회</summary>
        public async UniTask<TournamentData> GetTournamentAsync()
        {
            if (!CheckIsAvailable()) return null;
            _getTournamentTcs = new UniTaskCompletionSource<TournamentData>();
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_GetTournament(gameObject.name);
#else
            _getTournamentTcs.TrySetResult(null);
#endif
            return await _getTournamentTcs.Task;
        }

        /// <summary>토너먼트 생성 (초기 점수, 타이틀 지정)</summary>
        public async UniTask<bool> CreateTournamentAsync(int initialScore, string title = "")
        {
            if (!CheckIsAvailable()) return false;
            _createTournamentTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_CreateTournament(gameObject.name, initialScore.ToString(), title ?? "");
#else
            Debug.Log($"[FacebookPlatform] CreateTournament (dev): score={initialScore}, title={title}");
            _createTournamentTcs.TrySetResult(false);
#endif
            return await _createTournamentTcs.Task;
        }

        /// <summary>토너먼트 점수 등록 (토너먼트 세션 중 게임 종료 시)</summary>
        public async UniTask<bool> PostTournamentScoreAsync(int score)
        {
            if (!CheckIsAvailable()) return false;
            _postTournamentScoreTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_PostTournamentScore(gameObject.name, score);
#else
            Debug.Log($"[FacebookPlatform] PostTournamentScore (dev): {score}");
            _postTournamentScoreTcs.TrySetResult(false);
#endif
            return await _postTournamentScoreTcs.Task;
        }

        /// <summary>토너먼트 공유 (현재 토너먼트 세션 중에만 호출 가능)</summary>
        public async UniTask<bool> ShareTournamentAsync(int score)
        {
            if (!CheckIsAvailable()) return false;
            _shareTournamentTcs = new UniTaskCompletionSource<bool>();
#if UNITY_WEBGL && !UNITY_EDITOR
            FBBridge_ShareTournament(gameObject.name, score);
#else
            Debug.Log($"[FacebookPlatform] ShareTournament (dev): {score}");
            _shareTournamentTcs.TrySetResult(false);
#endif
            return await _shareTournamentTcs.Task;
        }

        // ── JS 콜백 수신 (FacebookBridge.jslib → SendMessage) ────────────

        // 초기화
        public void OnFBInitSuccess(string _)   { Debug.Log("[FacebookPlatform] 초기화 성공"); _initTcs?.TrySetResult(true); }
        public void OnFBInitFailed(string err)  { Debug.LogError($"[FacebookPlatform] 초기화 실패: {err}"); _initTcs?.TrySetResult(false); }
        public void OnFBStartGameSuccess(string _)  { Debug.Log("[FacebookPlatform] StartGame 성공"); _startGameTcs?.TrySetResult(true); }
        public void OnFBStartGameFailed(string err) { Debug.LogWarning($"[FacebookPlatform] StartGame 실패: {err}"); _startGameTcs?.TrySetResult(false); }

        // 일시정지
        public void OnFBPause(string _) { Debug.Log("[FacebookPlatform] onPause"); OnPause?.Invoke(); }

        // 리더보드
        public void OnFBScoreSubmitSuccess(string result) { Debug.Log($"[FacebookPlatform] 점수 제출 성공: {result}"); _submitScoreTcs?.TrySetResult(true); }
        public void OnFBScoreSubmitFailed(string err)     { Debug.LogWarning($"[FacebookPlatform] 점수 제출 실패: {err}"); _submitScoreTcs?.TrySetResult(false); }

        public void OnFBFriendScoresLoaded(string json)
        {
            try
            {
                string wrapped = "{\"items\":" + json + "}";
                var wrapper = JsonUtility.FromJson<FBLeaderboardEntryWrapper>(wrapped);
                var list = wrapper?.items ?? new List<FBLeaderboardEntry>();
                Debug.Log($"[FacebookPlatform] 친구 점수 {list.Count}명 조회 완료");
                _friendScoresTcs?.TrySetResult(list);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FacebookPlatform] 친구 점수 파싱 오류: {ex.Message}");
                _friendScoresTcs?.TrySetResult(new List<FBLeaderboardEntry>());
            }
        }
        public void OnFBFriendScoresFailed(string err) { Debug.LogWarning($"[FacebookPlatform] 친구 점수 조회 실패: {err}"); _friendScoresTcs?.TrySetResult(new List<FBLeaderboardEntry>()); }

        // 공유
        public void OnFBShareSuccess(string _)  { Debug.Log("[FacebookPlatform] 공유 완료"); _shareTcs?.TrySetResult(true); }
        public void OnFBShareFailed(string err) { Debug.LogWarning($"[FacebookPlatform] 공유 취소/실패: {err}"); _shareTcs?.TrySetResult(false); }

        // 봇 구독
        public void OnFBCanSubscribeBot(string value)      { _canSubscribeBotTcs?.TrySetResult(value == "1"); }
        public void OnFBSubscribeBotSuccess(string _)      { Debug.Log("[FacebookPlatform] 봇 구독 완료"); _subscribeBotTcs?.TrySetResult(true); }
        public void OnFBSubscribeBotFailed(string err)     { Debug.LogWarning($"[FacebookPlatform] 봇 구독 실패: {err}"); _subscribeBotTcs?.TrySetResult(false); }

        // 홈화면 바로가기
        public void OnFBCanCreateShortcut(string value)    { _canCreateShortcutTcs?.TrySetResult(value == "1"); }
        public void OnFBCreateShortcutSuccess(string _)    { Debug.Log("[FacebookPlatform] 바로가기 추가 완료"); _createShortcutTcs?.TrySetResult(true); }
        public void OnFBCreateShortcutFailed(string err)   { Debug.LogWarning($"[FacebookPlatform] 바로가기 실패: {err}"); _createShortcutTcs?.TrySetResult(false); }

        // 초대
        public void OnFBInviteSuccess(string _)  { Debug.Log("[FacebookPlatform] 초대 완료"); _inviteTcs?.TrySetResult(true); }
        public void OnFBInviteFailed(string err) { Debug.LogWarning($"[FacebookPlatform] 초대 취소/실패: {err}"); _inviteTcs?.TrySetResult(false); }

        // 플레이어 데이터
        public void OnFBSetDataSuccess(string _)   { Debug.Log("[FacebookPlatform] 데이터 저장 완료"); _setPlayerDataTcs?.TrySetResult(true); }
        public void OnFBSetDataFailed(string err)  { Debug.LogWarning($"[FacebookPlatform] 데이터 저장 실패: {err}"); _setPlayerDataTcs?.TrySetResult(false); }
        public void OnFBGetDataLoaded(string json) { Debug.Log("[FacebookPlatform] 데이터 로드 완료"); _getPlayerDataTcs?.TrySetResult(json); }
        public void OnFBGetDataFailed(string err)  { Debug.LogWarning($"[FacebookPlatform] 데이터 로드 실패: {err}"); _getPlayerDataTcs?.TrySetResult("{}"); }

        // 토너먼트
        public void OnFBTournamentLoaded(string json)
        {
            try { _getTournamentTcs?.TrySetResult(JsonUtility.FromJson<TournamentData>(json)); }
            catch { _getTournamentTcs?.TrySetResult(null); }
        }
        public void OnFBTournamentFailed(string err)         { Debug.LogWarning($"[FacebookPlatform] 토너먼트 조회 실패: {err}"); _getTournamentTcs?.TrySetResult(null); }
        public void OnFBTournamentCreated(string _)          { Debug.Log("[FacebookPlatform] 토너먼트 생성 완료"); _createTournamentTcs?.TrySetResult(true); }
        public void OnFBTournamentCreateFailed(string err)   { Debug.LogWarning($"[FacebookPlatform] 토너먼트 생성 실패: {err}"); _createTournamentTcs?.TrySetResult(false); }
        public void OnFBTournamentScorePosted(string _)      { Debug.Log("[FacebookPlatform] 토너먼트 점수 등록 완료"); _postTournamentScoreTcs?.TrySetResult(true); }
        public void OnFBTournamentScorePostFailed(string err){ Debug.LogWarning($"[FacebookPlatform] 토너먼트 점수 등록 실패: {err}"); _postTournamentScoreTcs?.TrySetResult(false); }
        public void OnFBTournamentShared(string _)           { Debug.Log("[FacebookPlatform] 토너먼트 공유 완료"); _shareTournamentTcs?.TrySetResult(true); }
        public void OnFBTournamentShareFailed(string err)    { Debug.LogWarning($"[FacebookPlatform] 토너먼트 공유 실패: {err}"); _shareTournamentTcs?.TrySetResult(false); }
    }
}
