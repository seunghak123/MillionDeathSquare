using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AppsInToss;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>
    /// Toss(앱인토스) 미니게임 플랫폼. 공식 Unity SDK(AppsInToss.AIT.*)를 직접 호출합니다.
    /// 유저 키 / 리더보드 / 공유 / 친구 초대(contactsViral) / 프로모션 보상 / 햅틱을 담당하고,
    /// 저장·광고·결제는 Storage / Ads / IAP 핸들러로 노출합니다.
    /// PlatformFactory.Create()로 생성됩니다.
    ///
    /// TossBridge.jslib는 환경 감지와 포커스 콜백에만 씁니다 (window.__TossSDK 경로는 제거됨).
    /// 각 호출은 자기 Task만 기다리므로 동시 호출해도 서로의 결과를 덮어쓰지 않습니다.
    /// </summary>
    public class TossPlatform : MonoBehaviour, IPlatform
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int  TossBridge_IsInsideTossApp();
        [DllImport("__Internal")] private static extern void TossBridge_RegisterFocusCallback(string gameObjectName);
#endif

        /// <summary>네트워크성 호출의 브릿지 응답 대기 한도(ms).</summary>
        private const int UserKeyTimeoutMs = 5000;
        private const int RequestTimeoutMs = 10000;

        // ── 캐시 ─────────────────────────────────────────────────────────
        private string _cachedUserKey = string.Empty;
        private IPlatformStorage _storage;
        private IPlatformAds _ads;
        private IPlatformIAP _iap;
        private bool _inviteInProgress;
        private static bool _bannerEventHooked;

        // 햅틱 옵션은 타입별로 한 번만 만들어 재사용 (호출마다 new 금지)
        private static readonly HapticFeedbackOptions[] _hapticOptions =
            new HapticFeedbackOptions[Enum.GetValues(typeof(HapticFeedbackType)).Length];

        /// <summary>getUserKeyForGame으로 받은 유저 해시. 실패했으면 빈 문자열.</summary>
        public string UserKey => _cachedUserKey;

        // ── 정적 환경 감지 (PlatformFactory에서 호출) ────────────────────

        public static bool CheckIsInsideTossApp()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return TossBridge_IsInsideTossApp() == 1;
#else
            return false;
#endif
        }

        // ── IPlatform 구현 ────────────────────────────────────────────────

        public async UniTask InitAsync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // 포커스 콜백은 같은 GameObject의 ApplicationManager.OnWebViewFocusChanged가 받는다
            TossBridge_RegisterFocusCallback(gameObject.name);
#endif
            // 유저 키 사전 로드 (GetPlayerId() / 로그인에서 동기로 재사용)
            await GetUserKeyAsync();
            Debug.Log($"[TossPlatform] 초기화 완료, userKey={(string.IsNullOrEmpty(_cachedUserKey) ? "(없음)" : "OK")}");
            // 배너는 결과 화면에서만 노출한다 (토스 미니게임판 규칙) — 부팅 시에는 띄우지 않음
        }

        public string GetPlayerId() => string.IsNullOrEmpty(_cachedUserKey) ? "toss_player" : _cachedUserKey;
        public string GetPlayerName() => "Toss Player";

        // 토스에는 게임 시작/종료 알림 API가 없다 (예전 raw postMessage는 비공식이라 제거).
        // 점수 제출은 호출부가 SubmitScoreAsync로 게임오버당 1회만 한다.
        // 하단 배너: 결과 화면에서만 노출 (OnGameEnd), 로비/인게임에서는 숨김
        public void OnGameStart() => HideBanner();
        public void OnGameEnd(int score, int bestScore) => ShowBanner();

        /// <summary>하단 배너 표시. 이미 떠 있으면 무시, 이전 시도가 실패했으면 다시 시도한다.</summary>
        public static void ShowBanner()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string id = TossConfig.Instance.BannerAdGroupId;
            if (string.IsNullOrEmpty(id))
            {
                // prod ID 미입력이면 조용히 배너가 안 나오므로 원인을 남긴다
                Debug.LogWarning($"[TossPlatform] 배너 광고 그룹 ID가 비어 있음 ({(TossConfig.IsProd ? "prod" : "dev")}) — TossConfig.asset 확인");
                return;
            }
            if (AITBannerAd.IsShowing) return;
            if (!_bannerEventHooked)
            {
                _bannerEventHooked = true;
                AITBannerAd.OnAdEvent += evt =>
                {
                    if (evt.Kind == AITBannerAdEventKind.InitializationFailed
                        || evt.Kind == AITBannerAdEventKind.FailedToRender
                        || evt.Kind == AITBannerAdEventKind.NoFill)
                    {
                        Debug.LogWarning($"[TossPlatform] 배너 광고 실패: {evt}");
                        // 실패해도 슬롯이 남아 IsShowing=true로 고정되면 이후 Show가 전부 무시된다.
                        // 슬롯을 정리해서 다음 ShowBanner(로비 진입/게임오버)에서 재시도되게 한다.
                        AITBannerAd.Hide();
                    }
                };
                AITBannerAd.OnError += msg => Debug.LogWarning($"[TossPlatform] 배너 광고 오류: {msg}");
            }
            AITBannerAd.Show(id, AITBannerPosition.Bottom);
#endif
        }

        public static void HideBanner()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            AITBannerAd.Hide();
#endif
        }

        // 게임 프로필이 없을 때 제출하지 못한 점수 (프로필 생성 후 다시 제출). 기기 로컬에만 둔다.
        private const string PendingLeaderboardScoreKey = "MDSPendingLeaderboardScore";

        /// <summary>
        /// 게임센터 순위표 점수 제출. 게임 프로필이 생기기 전에 제출하면 오류가 날 수 있어 (기획서 12.1)
        /// 프로필을 먼저 확인하고, 없으면 점수를 보관했다가 순위표를 열어 프로필이 생긴 뒤 제출한다.
        /// </summary>
        public async UniTask<bool> SubmitScoreAsync(int score)
        {
            int pending = PlayerPrefs.GetInt(PendingLeaderboardScoreKey, 0);
            int toSubmit = Mathf.Max(score, pending);
            if (!await HasGameProfileAsync())
            {
                if (toSubmit > pending) PlayerPrefs.SetInt(PendingLeaderboardScoreKey, toSubmit);
                Debug.Log($"[TossPlatform] 게임 프로필 없음 — 점수 {toSubmit} 보관 (순위표를 열면 제출)");
                return false;
            }
            bool success = await SubmitScoreNowAsync(toSubmit);
            if (success && pending > 0) PlayerPrefs.DeleteKey(PendingLeaderboardScoreKey);
            return success;
        }

        private async UniTask<bool> HasGameProfileAsync()
        {
            try
            {
                var profile = await AIT.GetGameCenterGameProfile(RequestTimeoutMs);
                // 앱 버전이 낮으면 응답이 없을 수 있다 (null) → 확인 불가, 제출은 시도한다
                return profile == null || profile.StatusCode == "SUCCESS";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TossPlatform] 게임 프로필 확인 실패: {e.Message}");
                return true;
            }
        }

        private async UniTask<bool> SubmitScoreNowAsync(int score)
        {
            try
            {
                var response = await AIT.SubmitGameCenterLeaderBoardScore(
                    new SubmitGameCenterLeaderBoardScoreParams { Score = score.ToString() },
                    RequestTimeoutMs);
                // 앱 버전이 낮으면 응답이 없을 수 있다 (null)
                bool success = response != null && response.StatusCode == "SUCCESS";
                if (!success)
                    Debug.LogWarning($"[TossPlatform] 리더보드 점수 제출 실패: status={response?.StatusCode}");
                return success;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TossPlatform] 리더보드 점수 제출 실패: {e.Message}");
                return false;
            }
        }

        public UniTask<List<FBLeaderboardEntry>> GetFriendScoresAsync(int count = 10)
            => UniTask.FromResult(new List<FBLeaderboardEntry>());

        public async UniTask<bool> ShareResultAsync(int score, string imageUrl = "")
        {
            // Toss 공유 흐름: getTossShareLink → share
            try
            {
                string link = await AIT.GetTossShareLink(
                    TossConfig.Instance.shareDeepLink,
                    string.IsNullOrEmpty(imageUrl) ? null : imageUrl,
                    RequestTimeoutMs);
                // 공유 시트는 사용자가 닫을 때까지 열려 있으므로 타임아웃을 걸지 않는다
                await AIT.Share(new ShareMessage { Message = $"백만 번 죽는 네모에서 {score}번 죽고 클리어! {link}" });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TossPlatform] 공유 실패: {e.Message}");
                return false;
            }
        }

        public void RequestBack() => CloseViewAsync().Forget();

        public void Vibrate(int type = 0)
        {
            switch (type)
            {
                case 1:  HapticAsync(HapticFeedbackType.BasicMedium).Forget(); break;
                case 2:  HapticAsync(HapticFeedbackType.Success).Forget(); break;
                default: HapticAsync(HapticFeedbackType.BasicWeak).Forget(); break;
            }
        }

        public void SetLoadingProgress(float percent) { /* Toss 미지원 */ }

        /// <summary>IPlatform 공용 이벤트 — value만 params에 담아 보낸다 (세부 파라미터는 LogEventParams)</summary>
        public void LogEvent(string eventName, float value = 0f, string paramsJson = "")
        {
            var p = new Dictionary<string, object> { { "value", value } };
            if (!string.IsNullOrEmpty(paramsJson)) p["params_json"] = paramsJson;
            LogEventParams(eventName, p);
        }

        /// <summary>
        /// 토스 이벤트 로그 (AIT.EventLog — log_name / log_type / params).
        /// v3.5.0 기준 EventLog는 deprecated 표시지만, 후속 AnalyticsLog는 아직 C# 구현이 없고
        /// SDK 자체(AITPerformanceLogger)도 같은 EventLogParams 형식을 쓴다. SDK 업데이트 시 교체 지점은 여기 한 곳.
        /// 실패해도 게임에 영향이 없도록 결과를 기다리지 않고 예외는 삼킨다.
        /// </summary>
        public void LogEventParams(string logName, Dictionary<string, object> parameters)
        {
            SendEventLogAsync(logName, parameters).Forget();
        }

        /// <summary>토스 화면 진입 로그 (AIT.AnalyticsScreen)</summary>
        public void LogScreen(string screenName)
        {
            SendScreenLogAsync(screenName).Forget();
        }

        private static async UniTaskVoid SendEventLogAsync(string logName, Dictionary<string, object> parameters)
        {
            try
            {
                await AIT.EventLog(new EventLogParams
                {
                    Log_name = logName,
                    Log_type = "event",
                    Params = parameters,
                }, RequestTimeoutMs);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TossPlatform] 이벤트 로그 실패 ({logName}): {e.Message}");
            }
        }

        private static async UniTaskVoid SendScreenLogAsync(string screenName)
        {
            try
            {
                await AIT.AnalyticsScreen(new Dictionary<string, object> { { "log_name", screenName } }, RequestTimeoutMs);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TossPlatform] 화면 로그 실패 ({screenName}): {e.Message}");
            }
        }

        public void GenerateHaptic(string hapticType)
        {
            HapticFeedbackType type;
            switch (hapticType)
            {
                case "tickWeak":    type = HapticFeedbackType.TickWeak; break;
                case "tap":         type = HapticFeedbackType.Tap; break;
                case "tickMedium":  type = HapticFeedbackType.TickMedium; break;
                case "softMedium":  type = HapticFeedbackType.SoftMedium; break;
                case "basicMedium": type = HapticFeedbackType.BasicMedium; break;
                case "success":     type = HapticFeedbackType.Success; break;
                case "error":       type = HapticFeedbackType.Error; break;
                case "wiggle":      type = HapticFeedbackType.Wiggle; break;
                case "confetti":    type = HapticFeedbackType.Confetti; break;
                default:            type = HapticFeedbackType.BasicWeak; break;
            }
            HapticAsync(type).Forget();
        }

        // ── 플랫폼 핸들러 (한 번 만든 인스턴스를 계속 반환) ───────────────

        public IPlatformStorage Storage => _storage ??= new TossStorage();
        public IPlatformAds Ads => _ads ??= new TossAds();
        public IPlatformIAP IAP => _iap ??= new TossIAP();

        public bool SupportsLeaderboardUI => true;

        public async UniTask<bool> OpenLeaderboardAsync()
        {
            try
            {
                await AIT.OpenGameCenterLeaderboard();
                // 순위표를 열면 게임 프로필이 만들어진다 → 보관해 둔 점수 제출
                int pending = PlayerPrefs.GetInt(PendingLeaderboardScoreKey, 0);
                if (pending > 0) SubmitScoreAsync(pending).Forget();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TossPlatform] 리더보드 열기 실패: {e.Message}");
                return false;
            }
        }

        // 토스 앱 상단 내비게이션 바(뒤로가기/닫기)에 가리지 않도록 상단 UI를 내리는 양 (캔버스 단위)
        public SafeAreaInset SafeAreaInset => new SafeAreaInset(top: 110f);

        public bool SupportsInvite => !string.IsNullOrEmpty(TossConfig.Instance.ContactsViralModuleId);

        /// <summary>
        /// 친구 초대 + 리워드 (contactsViral). 모듈이 닫힐 때까지 기다린 뒤
        /// 그동안 지급된 리워드 수량 합계를 반환합니다.
        /// ※ 토스앱 5.223.0 이상, 미니앱 승인 필수
        /// </summary>
        public async UniTask<int> InviteFriendsAsync()
        {
            if (!SupportsInvite || _inviteInProgress) return 0;
            _inviteInProgress = true;

            // 호출마다 지역 TCS를 써서 다른 호출과 섞이지 않게 한다
            var tcs = new UniTaskCompletionSource<int>();
            double rewardTotal = 0;
            Action unsubscribe = null;
            try
            {
                unsubscribe = AIT.ContactsViral(
                    ev =>
                    {
                        if (ev == null) return;
                        // close 이벤트의 data에는 rewardAmount가 없어 0으로 들어오므로
                        // 타입 이름에 기대지 않고 리워드가 실린 이벤트만 합산한다
                        if (ev.Type == "close")
                            tcs.TrySetResult((int)rewardTotal);
                        else if (ev.Data != null && ev.Data.RewardAmount > 0)
                            rewardTotal += ev.Data.RewardAmount;
                    },
                    new ContactsViralParamsOptions { ModuleId = TossConfig.Instance.ContactsViralModuleId },
                    error =>
                    {
                        Debug.LogWarning($"[TossPlatform] 친구 초대 오류: {error?.Message}");
                        tcs.TrySetResult((int)rewardTotal);
                    });

                return await tcs.Task;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TossPlatform] 친구 초대 실패: {e.Message}");
                return (int)rewardTotal;
            }
            finally
            {
                unsubscribe?.Invoke();
                _inviteInProgress = false;
            }
        }

        // ── Toss 전용 기능 ────────────────────────────────────────────────

        /// <summary>
        /// 게임 전용 사용자 식별 해시 획득 (getUserKeyForGame). 이미 받았으면 캐시를 반환합니다.
        /// 실패 시 빈 문자열. ※ 토스앱 5.232.0 이상, 게임 카테고리 전용
        /// </summary>
        public async UniTask<string> GetUserKeyAsync()
        {
            if (!string.IsNullOrEmpty(_cachedUserKey)) return _cachedUserKey;

            try
            {
                // 앱 버전이 낮으면 응답 자체가 오지 않을 수 있어 타임아웃 필수
                string raw = await AIT.GetUserKeyForGame(UserKeyTimeoutMs);
                string hash = ParseUserKey(raw);
                if (string.IsNullOrEmpty(hash))
                    Debug.LogWarning($"[TossPlatform] 유저 키 없음: {raw}");
                else
                    _cachedUserKey = hash;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TossPlatform] 유저 키 실패: {e.Message}");
            }
            return _cachedUserKey;
        }

        /// <summary>
        /// 프로모션 보상 지급 (grantPromotionRewardForGame).
        /// 성공 시 리워드 key 반환, 실패 시 빈 문자열.
        /// ※ 토스앱 5.232.0 이상, 게임 카테고리 전용
        /// </summary>
        public async UniTask<string> GrantPromotionAsync(string promotionCode, int amount)
        {
            try
            {
                var result = await AIT.GrantPromotionRewardForGame(
                    new GrantPromotionRewardForGameParams
                    {
                        Params = new GrantPromotionRewardForGameParamsParams
                        {
                            PromotionCode = promotionCode,
                            Amount = amount
                        }
                    },
                    RequestTimeoutMs);

                if (result != null && !string.IsNullOrEmpty(result.Key))
                    return result.Key;

                Debug.LogWarning($"[TossPlatform] 프로모션 지급 실패: {result?.ErrorCode}:{result?.Message}");
                return string.Empty;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TossPlatform] 프로모션 지급 실패: {e.Message}");
                return string.Empty;
            }
        }

        // ── 내부 ──────────────────────────────────────────────────────────

        [Serializable]
        private class UserKeyPayload
        {
            public string hash;
            public string type;
        }

        /// <summary>
        /// getUserKeyForGame 응답에서 해시 추출.
        /// SDK가 string으로 돌려주는 값은 성공이면 {"hash":"...","type":"HASH"} JSON 원문,
        /// 실패면 "INVALID_CATEGORY" / "ERROR" 이다.
        /// </summary>
        private static string ParseUserKey(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            raw = raw.Trim();
            if (raw.Length == 0 || raw[0] != '{') return string.Empty;

            try
            {
                var payload = JsonUtility.FromJson<UserKeyPayload>(raw);
                return payload != null && !string.IsNullOrEmpty(payload.hash) ? payload.hash : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private async UniTaskVoid HapticAsync(HapticFeedbackType type)
        {
            try
            {
                var options = _hapticOptions[(int)type];
                if (options == null)
                    options = _hapticOptions[(int)type] = new HapticFeedbackOptions { Type = type };
                await AIT.GenerateHapticFeedback(options);
            }
            catch (Exception)
            {
                // 햅틱 실패는 무시 (미지원 기기 / 구버전 앱)
            }
        }

        private async UniTaskVoid CloseViewAsync()
        {
            try
            {
                await AIT.CloseView();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TossPlatform] 화면 닫기 실패: {e.Message}");
            }
        }
    }
}
