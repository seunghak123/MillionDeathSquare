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

        // ── 백만 번 죽는 네모 (Base.unity SoundManager에 이미 등록된 클립 임시 재사용 — 전용 효과음이 생기면 값만 교체, 기획서 12.3) ──
        public const string MDS_BGM_EDITOR  = "sound_mystery";
        public const string MDS_BGM_MULTI   = "sound_border_world";
        // 죽음 효과음 3종 랜덤
        public const string MDS_DEATH_1     = "SFX_Collsion";
        public const string MDS_DEATH_2     = "SFX_ShieldBreak";
        public const string MDS_DEATH_3     = "SFX_TrapSound";
        public const string MDS_COIN        = "SFX_GetCoin";
        public const string MDS_CHECKPOINT  = "SFX_Feather";
        public const string MDS_GOAL        = "coin_earn";
        public const string MDS_DOOR_OPEN   = "SFX_TileMoveSound2";
        public const string MDS_KEY         = "SFX_FlyStart";
        public const string MDS_EDITOR_PLACE = "SFX_TileMoveSound1";
        public const string MDS_EDITOR_ERASE = "SFX_MoveSound_2";
    }
}
