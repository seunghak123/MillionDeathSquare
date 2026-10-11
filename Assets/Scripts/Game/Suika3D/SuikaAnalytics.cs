using System.Collections.Generic;
using UnityEngine;
using Seunghak.Platform;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// 플레이 데이터 수집 (기획서 13.6 — 밸런스 조정 / 광고 분석용). 토스에서는 토스 이벤트 로그로 남긴다.
    ///  - Toss: TossPlatform.LogEventParams (AIT.EventLog) / 화면은 LogScreen (AIT.AnalyticsScreen)
    ///  - 그 외 플랫폼: IPlatform.LogEvent(이름, 대표값)
    /// 이벤트 이름은 모두 "fossil_" 접두사. 판 단위 통계(공룡 부활 수, 스킬 획득/사용 수 등)는 여기서 모아
    /// fossil_run_end 한 번에 보낸다 — 기획서 14장 목표("평균 9~10단계 도달, 스킬 3~4개 획득") 검증용.
    ///
    /// 이벤트 목록
    ///   fossil_run_start      mode, loadout_skills
    ///   fossil_run_end        mode, score, best, new_best, cause, play_sec, level, highest_tier, drops,
    ///                         dino_revivals, skills_gained, skills_used, lives_lost, continued, coins, quakes_survived
    ///   fossil_run_abandon    mode, score, play_sec, highest_tier (판 도중 재시작/로비 이동)
    ///   fossil_dino_revival   mode, play_sec, count
    ///   fossil_level_up       level, skill, rerolled, result(take/replace/discard)
    ///   fossil_skill_use      skill, play_sec
    ///   fossil_life_lost      lives_left, play_sec, highest_tier
    ///   fossil_continue       cause
    ///   fossil_ad             type(rewarded/interstitial), placement, success
    ///   fossil_skill_acquire  skill, method(coin/ad)
    ///   fossil_mode_unlock    score
    ///   화면: lobby / ingame / level_up / result
    /// </summary>
    public static class SuikaAnalytics
    {
        // 판 단위 집계
        private static float _runStartTime;
        private static int _dinoRevivals;
        private static int _skillsGained;
        private static int _skillsUsed;
        private static int _livesLost;
        private static int _quakesSurvived;
        private static bool _runActive;

        private static float PlaySeconds => _runActive ? Time.realtimeSinceStartup - _runStartTime : 0f;

        private static string Mode(E_SUIKA_DIFFICULTY m) => m == E_SUIKA_DIFFICULTY.TECTONIC ? "tectonic" : "normal";

        // ── 판 ─────────────────────────────────────────────────────────

        public static void RunStarted(E_SUIKA_DIFFICULTY mode, int loadoutSkills)
        {
            _runActive = true;
            _runStartTime = Time.realtimeSinceStartup;
            _dinoRevivals = _skillsGained = _skillsUsed = _livesLost = _quakesSurvived = 0;
            Send("fossil_run_start", loadoutSkills, new Dictionary<string, object>
            {
                { "mode", Mode(mode) },
                { "loadout_skills", loadoutSkills },
            });
        }

        public static void RunEnded(SuikaGameManager gm, bool newBest)
        {
            if (gm == null) return;
            Send("fossil_run_end", gm.Score, new Dictionary<string, object>
            {
                { "mode", Mode(gm.Difficulty) },
                { "score", gm.Score },
                { "best", gm.BestScore },
                { "new_best", newBest },
                { "cause", gm.GameOverCause == E_SUIKA_GAMEOVER_CAUSE.TIME_OUT ? "time_out" : "lives_out" },
                { "play_sec", Mathf.RoundToInt(PlaySeconds) },
                { "level", gm.Level },
                { "highest_tier", gm.HighestTierThisRun + 1 }, // 1부터 (기획서 단계 표기)
                { "drops", gm.DropCount },
                { "dino_revivals", _dinoRevivals },
                { "skills_gained", _skillsGained },
                { "skills_used", _skillsUsed },
                { "lives_lost", _livesLost },
                { "continued", gm.HasContinued },
                { "coins", gm.GoldEarnedLast },
                { "quakes_survived", _quakesSurvived },
            });
        }

        /// <summary>판 도중 재시작/로비 이동 (이탈 지점 분석용)</summary>
        public static void RunAbandoned(SuikaGameManager gm)
        {
            if (gm == null || !_runActive) return;
            Send("fossil_run_abandon", gm.Score, new Dictionary<string, object>
            {
                { "mode", Mode(gm.Difficulty) },
                { "score", gm.Score },
                { "play_sec", Mathf.RoundToInt(PlaySeconds) },
                { "highest_tier", gm.HighestTierThisRun + 1 },
            });
            _runActive = false;
        }

        public static void DinoRevived(E_SUIKA_DIFFICULTY mode)
        {
            _dinoRevivals++;
            Send("fossil_dino_revival", _dinoRevivals, new Dictionary<string, object>
            {
                { "mode", Mode(mode) },
                { "play_sec", Mathf.RoundToInt(PlaySeconds) },
                { "count", _dinoRevivals },
            });
        }

        public static void LifeLost(int livesLeft, int highestTier)
        {
            _livesLost++;
            Send("fossil_life_lost", livesLeft, new Dictionary<string, object>
            {
                { "lives_left", livesLeft },
                { "play_sec", Mathf.RoundToInt(PlaySeconds) },
                { "highest_tier", highestTier + 1 },
            });
        }

        public static void Continued(E_SUIKA_GAMEOVER_CAUSE cause)
        {
            Send("fossil_continue", 0f, new Dictionary<string, object>
            {
                { "cause", cause == E_SUIKA_GAMEOVER_CAUSE.TIME_OUT ? "time_out" : "lives_out" },
            });
        }

        public static void QuakeSurvived() => _quakesSurvived++;

        // ── 스킬 ───────────────────────────────────────────────────────

        /// <summary>레벨업 선택 결과. result = take(빈 칸에 받음) / replace(교체) / discard(포기)</summary>
        public static void LevelUpResolved(int level, E_FOSSIL_SKILL skill, bool rerolled, string result)
        {
            if (result != "discard") _skillsGained++;
            Send("fossil_level_up", level, new Dictionary<string, object>
            {
                { "level", level },
                { "skill", SkillId(skill) },
                { "rerolled", rerolled },
                { "result", result },
            });
        }

        public static void SkillUsed(E_FOSSIL_SKILL skill)
        {
            _skillsUsed++;
            Send("fossil_skill_use", 0f, new Dictionary<string, object>
            {
                { "skill", SkillId(skill) },
                { "play_sec", Mathf.RoundToInt(PlaySeconds) },
            });
        }

        /// <summary>로비 장착 획득 (method = coin / ad)</summary>
        public static void SkillAcquiredInLobby(E_FOSSIL_SKILL skill, string method)
        {
            Send("fossil_skill_acquire", 0f, new Dictionary<string, object>
            {
                { "skill", SkillId(skill) },
                { "method", method },
            });
        }

        // ── 광고 / 해금 / 화면 ────────────────────────────────────────

        /// <summary>placement: lobby_skill / reroll / continue / coin_double / result_to_lobby</summary>
        public static void Ad(string type, string placement, bool success)
        {
            Send("fossil_ad", success ? 1f : 0f, new Dictionary<string, object>
            {
                { "type", type },
                { "placement", placement },
                { "success", success },
            });
        }

        public static void ModeUnlocked(int score)
        {
            Send("fossil_mode_unlock", score, new Dictionary<string, object> { { "score", score } });
        }

        /// <summary>화면 진입: lobby / ingame / level_up / result</summary>
        public static void Screen(string screenName)
        {
            if (PlatformFactory.Current is TossPlatform toss) toss.LogScreen(screenName);
            else PlatformFactory.Current.LogEvent("screen_" + screenName);
        }

        // ── 공통 ───────────────────────────────────────────────────────

        private static string SkillId(E_FOSSIL_SKILL s)
        {
            switch (s)
            {
                case E_FOSSIL_SKILL.HAMMER:     return "hammer";
                case E_FOSSIL_SKILL.BRUSH:      return "brush";
                case E_FOSSIL_SKILL.EARTHQUAKE: return "earthquake";
                case E_FOSSIL_SKILL.MAGNET:     return "magnet";
                case E_FOSSIL_SKILL.EVOLVE:     return "evolve";
                default:                        return "none";
            }
        }

        private static void Send(string name, float value, Dictionary<string, object> parameters)
        {
            if (PlatformFactory.Current is TossPlatform toss) toss.LogEventParams(name, parameters);
            else PlatformFactory.Current.LogEvent(name, value);
#if UNITY_EDITOR
            Debug.Log($"[SuikaAnalytics] {name} ({parameters.Count} params)");
#endif
        }
    }
}
