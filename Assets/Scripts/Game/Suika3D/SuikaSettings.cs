using UnityEngine;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// 설정 (기획서 11.1 설정 팝업): 기울기 감도, 흔들림 강도. 진동은 SuikaHaptics.Enabled, 사운드는 SoundManager.
    /// PlayerPrefs에 저장하고 PlatformSaveSync 스냅샷에도 포함된다.
    /// </summary>
    public static class SuikaSettings
    {
        public const string PREF_TILT_SENSITIVITY = "FossilTiltSensitivity";
        public const string PREF_SHAKE_INTENSITY = "FossilShakeIntensity";

        private static int _tilt = -1;
        private static float _shake = -1f;

        /// <summary>기울기 감도 0 = 낮음, 1 = 보통, 2 = 높음</summary>
        public static int TiltSensitivity
        {
            get
            {
                if (_tilt < 0) _tilt = Mathf.Clamp(PlayerPrefs.GetInt(PREF_TILT_SENSITIVITY, 1), 0, 2);
                return _tilt;
            }
            set
            {
                _tilt = Mathf.Clamp(value, 0, 2);
                PlayerPrefs.SetInt(PREF_TILT_SENSITIVITY, _tilt);
                Seunghak.Platform.PlatformSaveSync.MarkDirty();
            }
        }

        /// <summary>흔들림 강도 0~1. 0이면 시각적 흔들림(진동 성분)을 완전히 끈다 (멀미 방지)</summary>
        public static float ShakeIntensity
        {
            get
            {
                if (_shake < 0f) _shake = Mathf.Clamp(PlayerPrefs.GetInt(PREF_SHAKE_INTENSITY, 100), 0, 100) / 100f;
                return _shake;
            }
            set
            {
                _shake = Mathf.Clamp01(value);
                // 플랫폼 저장 동기화가 int/string 키만 다루므로 퍼센트 정수로 저장
                PlayerPrefs.SetInt(PREF_SHAKE_INTENSITY, Mathf.RoundToInt(_shake * 100f));
                Seunghak.Platform.PlatformSaveSync.MarkDirty();
            }
        }

        /// <summary>플랫폼 저장소 복원 후 캐시 무효화</summary>
        public static void Reload()
        {
            _tilt = -1;
            _shake = -1f;
        }
    }
}
