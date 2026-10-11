using Seunghak.Platform;
using UnityEngine;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 기기별 표시/조작 설정 (PlayerPrefs, 키 접두사 MDS). PlatformSaveSync 스냅샷에도 포함된다.
    /// </summary>
    public static class MDSSettings
    {
        public const string KEY_SHAKE = "MDSShakePercent";
        public const string KEY_RUMBLE = "MDSRumbleEnabled";
        public const string KEY_SHOW_GHOSTS = "MDSShowGhosts";

        /// <summary>화면 흔들림 세기 0~1 (0이면 흔들림 끔). 저장은 정수 퍼센트(스냅샷 IntKeys)</summary>
        public static float ShakeIntensity
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(KEY_SHAKE, 100), 0, 100) / 100f; }
            set { PlayerPrefs.SetInt(KEY_SHAKE, Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)); PlatformSaveSync.MarkDirty(); }
        }

        /// <summary>게임패드 진동 / 모바일 햅틱 사용 여부</summary>
        public static bool RumbleEnabled
        {
            get { return PlayerPrefs.GetInt(KEY_RUMBLE, 1) != 0; }
            set { PlayerPrefs.SetInt(KEY_RUMBLE, value ? 1 : 0); PlatformSaveSync.MarkDirty(); }
        }

        /// <summary>멀티방에서 다른 플레이어(반투명) 표시 여부</summary>
        public static bool ShowGhosts
        {
            get { return PlayerPrefs.GetInt(KEY_SHOW_GHOSTS, 1) != 0; }
            set { PlayerPrefs.SetInt(KEY_SHOW_GHOSTS, value ? 1 : 0); PlatformSaveSync.MarkDirty(); }
        }
    }
}
