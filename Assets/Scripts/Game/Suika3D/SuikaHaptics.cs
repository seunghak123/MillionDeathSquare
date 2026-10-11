using UnityEngine;
using Seunghak.Platform;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// 햅틱 정책. 이벤트별 세기를 한곳에서 정하고, 연쇄 합체 등으로 진동이 몰릴 때 간격과 우선순위로 거른다.
    /// (기획서 13.5 진동 패턴: 합체 / 지진 / 장외 낙하 + 보조 이벤트)
    ///
    ///   이벤트               Toss 햅틱      의도
    ///   ─────────────────────────────────────────────────────────
    ///   조준 가장자리 닿음    tickWeak     "여기가 끝" 경계 느낌
    ///   드롭                 tap          손을 뗀 확인
    ///   합체 하위/중간/상위   tickWeak / tickMedium / basicMedium
    ///   이번 판 첫 단계       success      성취감
    ///   연쇄 합체(3연쇄 이상) success
    ///   공룡 부활            confetti     축하
    ///   스킬 사용            tickMedium
    ///   레벨업 / 시간 보너스  success
    ///   지진 경보            wiggle       "곧 흔들린다"
    ///   지진 시작            basicMedium
    ///   화석 떨어지는 중      softMedium   위기 신호
    ///   장외 낙하(라이프 -1)  error
    ///   게임 오버            error
    ///   이어하기             success
    ///
    /// Toss/Facebook 외(안드로이드/iOS 네이티브)는 플랫폼 햅틱이 없으므로 강한 이벤트만 Handheld.Vibrate로 대체한다.
    /// 설정 키 PlayerPrefs "HapticEnabled" (기본 켬) — 설정 UI에서 Enabled로 토글.
    /// </summary>
    public static class SuikaHaptics
    {
        public const string AIM_EDGE      = "tickWeak";
        public const string DROP          = "tap";
        public const string NEW_TIER      = "success";
        public const string CHAIN         = "success";
        public const string MAX_CLEAR     = "confetti";
        public const string DINO          = "confetti";
        public const string SKILL         = "tickMedium";
        public const string LEVEL_UP      = "success";
        public const string TIME_BONUS    = "success";
        public const string QUAKE_WARNING = "wiggle";
        public const string QUAKE         = "basicMedium";
        public const string DANGER        = "softMedium";
        public const string LIFE_LOST     = "error";
        public const string GAME_OVER     = "error";
        public const string REVIVE        = "success";

        private const string PREF_KEY = "HapticEnabled";
        // 이 간격 안에 들어온 더 약한 햅틱은 버린다 (연쇄 합치기 시 진동이 뭉개지는 것 방지)
        private const float MIN_INTERVAL = 0.06f;

        private static float _lastTime = -1f;
        private static int _lastPriority;
        private static int _enabledCache = -1;

        public static bool Enabled
        {
            get
            {
                if (_enabledCache < 0) _enabledCache = PlayerPrefs.GetInt(PREF_KEY, 1);
                return _enabledCache != 0;
            }
            set
            {
                _enabledCache = value ? 1 : 0;
                PlayerPrefs.SetInt(PREF_KEY, _enabledCache);
            }
        }

        /// <summary>플랫폼 저장소 복원 후 캐시 무효화</summary>
        public static void Reload() => _enabledCache = -1;

        /// <summary>합치기로 생긴 단계별 햅틱 (단계가 클수록 강하게)</summary>
        public static string ForMerge(int newTier, int tierCount)
        {
            float t = tierCount > 1 ? (float)newTier / (tierCount - 1) : 0f;
            if (t < 0.34f) return "tickWeak";
            if (t < 0.67f) return "tickMedium";
            return "basicMedium";
        }

        public static void Play(string type)
        {
            if (!Enabled || string.IsNullOrEmpty(type)) return;

            float now = Time.unscaledTime;
            int priority = Priority(type);
            if (now - _lastTime < MIN_INTERVAL && priority <= _lastPriority) return;
            _lastTime = now;
            _lastPriority = priority;

            IPlatform platform = PlatformFactory.Current;
            if (!(platform is DefaultPlatform))
            {
                platform.GenerateHaptic(type);
                return;
            }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            // 네이티브 모바일: 세기 조절이 안 되는 단일 진동이라 강한 이벤트만 사용
            if (priority >= 4) Handheld.Vibrate();
#endif
        }

        private static int Priority(string type)
        {
            switch (type)
            {
                case "tickWeak":    return 0;
                case "tap":         return 1;
                case "tickMedium":  return 2;
                case "softMedium":  return 2;
                case "basicMedium": return 3;
                case "wiggle":      return 3;
                case "success":     return 4;
                case "confetti":    return 5;
                case "error":       return 5;
                default:            return 1;
            }
        }
    }
}
