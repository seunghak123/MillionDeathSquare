namespace Seunghak.Manager
{
    /// <summary>
    /// SoundManager에 등록된 클립 키 모음 (Base.unity SoundManager bgmClips/sfxClips에 등록된 이름 그대로).
    /// 게임 코드는 문자열 대신 이 상수를 사용한다.
    /// </summary>
    public static class SoundKeys
    {
        // ── BGM ───────────────────────────────────────────
        public const string BGM_LOBBY = "game_ui_bgm";
        public const string BGM_GAME  = "sound_magic_forest";
        public const string BGM_GAME2 = "sound_magic_forest2";

        // ── 공용 SFX ──────────────────────────────────────
        public const string BUTTON_CLICK = "button_click";
        public const string COIN_EARN    = "coin_earn";
        public const string ITEM_GET     = "SFX_ItemGet";

        // ── Fossil Merge (기존 클립 재사용, 전용 클립이 생기면 값만 교체) ──
        public const string SUIKA_DROP       = "SFX_TileMoveSound1";
        public const string SUIKA_MERGE      = "SFX_TileMoveSound2";
        public const string SUIKA_MERGE_BIG  = "SFX_ItemGet";
        public const string SUIKA_MAX_CLEAR  = "coin_earn";
        public const string SUIKA_GAME_OVER  = "SFX_FallDown";
    }
}
