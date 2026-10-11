using Cysharp.Threading.Tasks;
using UnityEngine;
using Seunghak.Manager;
using Seunghak.Platform;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// Fossil Merge ↔ 플랫폼(Toss / Facebook / 웹 포털) 연동을 한곳에 모은 정적 브리지.
    /// 게임 로직은 IPlatform / AdManager / PlatformSaveSync를 직접 부르지 않고 여기만 호출한다.
    ///
    /// 광고 규칙 (기획서 10.2): 게임 중에는 어떤 광고도 띄우지 않는다.
    ///   - 보상형: 로비 랜덤 스킬(하루 10회) / 레벨업 다시 뽑기(레벨업당 1회) / 이어하기(판당 1회) / 코인 2배(판당 1회)
    ///   - 전면: 결과 → 로비 이동 시 3판마다 1회. 첫 판과 보상형을 본 판은 제외
    ///   - 배너: 결과 화면에서만 (OnGameEnd에서 표시, 로비/게임 시작 시 숨김)
    ///
    /// Toss 연동 항목:
    ///   - 게임 시작/종료 알림 (하단 배너 숨김/노출)        → IPlatform.OnGameStart / OnGameEnd
    ///   - 리더보드 점수 제출(일반 모드만) / 리더보드 열기 → SubmitScoreAsync / OpenLeaderboardAsync
    ///   - 결과 공유 / 친구 초대                         → ShareResultAsync / InviteFriendsAsync
    ///   - 전면 광고, 보상형 광고                         → AdManager
    ///   - 햅틱                                          → SuikaHaptics (IPlatform.GenerateHaptic)
    ///   - 프로모션(토스포인트) 지급                      → TossPlatform.GrantPromotionAsync + TossConfig
    ///   - 저장 동기화 (플레이 중 푸시 지연, 게임 오버 즉시 푸시) → PlatformSaveSync
    ///   - 뒤로가기                                      → IPlatform.RequestBack
    /// </summary>
    public static class SuikaPlatformBridge
    {
        /// <summary>프로모션 지급 최소 점수</summary>
        public static int TossPromotionMinScore = 100;

        /// <summary>전면 광고 간격 (판)</summary>
        public static int InterstitialEveryRuns = 3;

        private static bool _busy;
        private static int _runsStarted;
        private static int _runsSinceInterstitial;
        private static bool _rewardedThisRun;

        // ── 게임 흐름 ──────────────────────────────────────────────────

        /// <summary>새 판 준비 (다시하기 / 인게임 진입). 전면 광고 판 수 집계용.</summary>
        public static void NotifyRunStarted()
        {
            _runsStarted++;
            _runsSinceInterstitial++;
            _rewardedThisRun = false;
        }

        public static void NotifyGameStart()
        {
            PlatformSaveSync.GameplayActive = true;
            PlatformFactory.Current.OnGameStart();
            AdManager.Instance?.NotifyGameplayStart();
        }

        /// <summary>플레이 도중 재시작/로비 이동 — 결과 제출 없이 플레이 상태만 정리</summary>
        public static void NotifyGameplayAborted()
        {
            PlatformSaveSync.GameplayActive = false;
            AdManager.Instance?.NotifyGameplayStop();
        }

        public static void NotifyGameOver(int score, int bestScore, E_SUIKA_DIFFICULTY mode)
        {
            PlatformSaveSync.GameplayActive = false;
            PlatformSaveSync.FlushNow();
            AdManager.Instance?.NotifyGameplayStop();

            PlatformFactory.Current.OnGameEnd(score, bestScore); // Toss: 결과 화면 배너 표시
            // 판 결과 상세 로그는 SuikaAnalytics.RunEnded (토스 이벤트 로그)

            // 플랫폼 리더보드는 하나뿐이라 일반 모드 점수만 제출한다 (지각 변동은 로컬 최고점만)
            if (mode == E_SUIKA_DIFFICULTY.NORMAL)
                SubmitScoreAsync(score).Forget();
            TryGrantTossPromotionAsync(score).Forget();
        }

        /// <summary>결과 화면을 떠날 때 (로비 이동). 배너를 내리고, 규칙에 맞으면 전면 광고.</summary>
        public static async UniTask OnLeaveResultToLobbyAsync()
        {
            HideBanner();
            bool firstRun = _runsStarted <= 1;
            if (firstRun || _rewardedThisRun || _runsSinceInterstitial < InterstitialEveryRuns) return;
            if (AdManager.Instance == null || !AdManager.Instance.CanShowInterstitial()) return;

            _runsSinceInterstitial = 0;
            bool shown = await AdManager.Instance.ShowInterstitialAdAsync();
            SuikaAnalytics.Ad("interstitial", "result_to_lobby", shown);
        }

        /// <summary>로비/게임 시작 시 배너 숨김 (배너는 결과 화면에서만)</summary>
        public static void HideBanner()
        {
            if (PlatformFactory.Current is TossPlatform) TossPlatform.HideBanner();
        }

        /// <summary>보상형 광고 1회 (레벨업 다시 뽑기, 코인 2배, 로비 랜덤 스킬). 판 중이면 전면 광고 제외 판으로 기록.</summary>
        /// <param name="placement">분석용 광고 위치: lobby_skill / reroll / continue / coin_double</param>
        public static async UniTask<bool> ShowRewardedAsync(bool duringRun, string placement)
        {
            if (_busy || AdManager.Instance == null) return false;
            _busy = true;
            try
            {
                bool ok = await AdManager.Instance.ShowRewardedAdAsync();
                if (ok && duringRun) _rewardedThisRun = true;
                SuikaAnalytics.Ad("rewarded", placement, ok);
                return ok;
            }
            finally
            {
                _busy = false;
            }
        }

        private static async UniTaskVoid SubmitScoreAsync(int score)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // 점수 제출은 게임오버당 1회
            bool submitted = await PlatformFactory.Current.SubmitScoreAsync(score);
            Debug.Log($"[SuikaPlatformBridge] 점수 제출 — result:{submitted} score:{score}");

            // 토스는 게임오버마다 공유 시트가 뜨면 안 되므로 자동 공유하지 않는다 (공유 버튼으로만)
            if (submitted && score > 0 && !(PlatformFactory.Current is TossPlatform))
                PlatformFactory.Current.ShareResultAsync(score).Forget();

#if NEWGROUNDS
            var ngBridge = Seunghak.Common.ApplicationManager.Instance?.GetComponent<NewgroundsBridgeHelper>();
            if (ngBridge != null)
                ngBridge.SubmitGameResult(score).Forget();
#endif
#else
            Debug.Log($"[SuikaPlatformBridge] (에디터) 점수 제출 생략 score:{score}");
            await UniTask.CompletedTask;
#endif
        }

        private static async UniTaskVoid TryGrantTossPromotionAsync(int score)
        {
            if (score < TossPromotionMinScore) return;
            if (!(PlatformFactory.Current is TossPlatform toss)) return;

            string code = TossConfig.Instance.PromotionCode;
            int amount = TossConfig.Instance.PromotionAmount;
            if (string.IsNullOrEmpty(code) || amount <= 0) return;

            string result = await toss.GrantPromotionAsync(code, amount);
            if (!string.IsNullOrEmpty(result))
                Debug.Log($"[SuikaPlatformBridge] Toss 프로모션 지급 완료: key={result}");
            else
                Debug.LogWarning("[SuikaPlatformBridge] Toss 프로모션 지급 실패 또는 미지원");
        }

        // ── 광고 ───────────────────────────────────────────────────────

        public static bool IsAdSupported => AdManager.Instance != null && AdManager.Instance.IsAdSupported();

        /// <summary>보상형 광고 시청 후 이어하기 (판당 1회). 성공 여부 반환.</summary>
        public static async UniTask<bool> ReviveWithAdAsync()
        {
            var gm = SuikaGameManager.Instance;
            if (gm == null || !gm.CanContinue) return false;
            bool rewarded = await ShowRewardedAsync(duringRun: true, placement: "continue");
            gm = SuikaGameManager.Instance;
            if (!rewarded || gm == null || !gm.CanContinue) return false;
            gm.Continue();
            return true;
        }

        /// <summary>레벨업 선택지 다시 뽑기 (광고, 레벨업당 1회)</summary>
        public static async UniTask<bool> RerollSkillsWithAdAsync()
        {
            var skills = SuikaGameManager.Instance != null ? SuikaGameManager.Instance.Skills : null;
            if (skills == null || !skills.CanReroll) return false;
            if (!await ShowRewardedAsync(duringRun: true, placement: "reroll")) return false;
            return skills.Reroll();
        }

        /// <summary>로비: 광고 보고 랜덤 스킬 1개 장착 (하루 10회). 받은 스킬 반환</summary>
        public static async UniTask<E_FOSSIL_SKILL> GetRandomSkillWithAdAsync()
        {
            if (SuikaProgress.AdSkillsLeftToday <= 0 || !SuikaProgress.HasEmptyLoadoutSlot) return E_FOSSIL_SKILL.NONE;
            if (!await ShowRewardedAsync(duringRun: false, placement: "lobby_skill")) return E_FOSSIL_SKILL.NONE;
            return SuikaProgress.GrantRandomSkill();
        }

        // ── 리더보드 / 공유 / 초대 ────────────────────────────────────

        public static bool SupportsLeaderboard => PlatformFactory.Current.SupportsLeaderboardUI;
        public static bool SupportsShare => !(PlatformFactory.Current is DefaultPlatform);
        public static bool SupportsInvite => PlatformFactory.Current.SupportsInvite;

        public static UniTask<bool> OpenLeaderboardAsync() => PlatformFactory.Current.OpenLeaderboardAsync();
        public static UniTask<bool> ShareAsync(int score) => PlatformFactory.Current.ShareResultAsync(score);
        public static UniTask<int> InviteAsync() => PlatformFactory.Current.InviteFriendsAsync();

        // ── 기타 ───────────────────────────────────────────────────────

        public static void RequestBack() => PlatformFactory.Current.RequestBack();

        public static bool IsTossDevBuild => TossConfig.IsDevBuild;
    }
}
