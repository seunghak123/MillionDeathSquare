#region ResourceType
public enum E_ANIMATION_EVENT
{
    CREATE_OBJECT,          //오브젝트 생성 이벤트
    CREATE_PARTICLE,        //파티클 생성 이벤트
    PLAY_SOUND,             //사운드 플레이 이벤트
    CAMERA_ACATION,         //카메라 이벤트

}
public enum E_SOUND_TYPE
{
    BGM_SOUND,
    VOICE_SOUND,
    FBX_SOUND,
}
#endregion ResourceType
#region EnviromentType
public enum E_APPLICATION_STATE
{
    APPLICATION_START,
    APPLICATION_UPDATE,
    REQUEST_PERMISSION,
    USER_LOGIN,
    BUNDLE_UPDATE,
    GAME_RESOURCE_LOAD,
    INAPP_UPDATE,
    TITLE,
}
public enum E_APPLICATION_PERMISSION_TYPE
{
    //퍼미션 타입
    STORAGE_PERMISSION,
    CAMERA_PERMISSION,
    END,
}
public enum E_DATABASE_TYPE
{
    USER_INFO,
    USER_STAGE,
    USER_ITEM,
    USER_OPTION,
    USER_LOBBYINFO,
}
#endregion EnviromentType
#region UserInfoType
public enum E_LOGIN_TYPE
{
    GUEST_LOGIN,
    /// <summary>앱인토스(Apps in Toss) WebGL 환경 로그인</summary>
    APPS_IN_TOSS_LOGIN,
    /// <summary>Facebook Instant Games WebGL 환경 로그인</summary>
    FACEBOOK_INSTANT_LOGIN,
}
public enum E_LANGUAGE_TYPE
{
    //Default 는 한국어
    KOREAN,
    ENGLISH,
}
#endregion UserInfoType
#region UserDataType
public enum E_ITEM_TYPE
{
    GOLD,
    CRYSTALS,
    VALID_CRYSTALS,
}
#endregion UserDataType

public enum E_STOREITEM_TYPE
{
    CONSUME,
    NON_CONSUME,
    SUBSCRIPTION,
}
//서버에 보낼 행동 타입
public enum E_OBJECT_ACTION_TYPE
{
    ATTACK_OTHER,
    MOVE_SELF,
    MOVE_OTHER,
    HEAL_OTHER,
    DEFENCE_SELF,
}

#region IngameType
public enum E_INGAME_AI_TYPE
{
    NONE,
    UNIT_IDLE,
    UNIT_MOVE,
    UNIT_INTERACTION,
    UNIT_EVENT,

    END,
}
public enum E_STATVALUE_TYPE
{
    INTERACT_RANGE,
    UNIT_MOVE_SPEED,
    UNIT_ADVANTAGE_WORK_SPEED,
}
public enum E_STAT_DISPLAY_TYPE
{
    NONE,
    SKILL_DISPLAY,
    EVENT_DISPLAY,
}
public enum E_UNIT_AI_TYPE
{
    //Ingame AI Type
    MELEE_AI,
    RANGE_AI,

    //External Ai Type
    ANIMATION_AI = 100,//로비용 AI 로비외엔 생성 금지
}
/// <summary>
/// 카드 타입에 대한 enum
/// </summary>
public enum CardType
{
    E_CHARACTER = 1,
    E_MAGIC = 2,
    E_COUNTER = 3,
}
/// <summary>
/// 카드 랭크
/// </summary>
public enum CardRank
{
    NONE,
    C_RANK = 1,
    B_RANK = 2,
    A_RANK = 3,
    S_RANK = 4,
    SS_RANK = 5,
    SSS_RANK = 6,
}

/// <summary>
/// 캐릭터 속성 타입
/// </summary>
public enum CardElementType
{
    NONE,
    E_MOON,
    E_SUN,
    E_OCEAN,
}
/// <summary>
/// 카드 스텟 관련 Enum
/// </summary>
public enum CardStatEnum
{
    STAT_HP,
    STAT_MAXHP,
    STAT_ATTACK,
    STAT_ORIGIN_ATTACK,
    STAT_BREAK_DOWN,
    STAT_MAX_BREAK_DOWN,
    STAT_OVERWRITE,
    STAT_MAX_OVERWRITE,
    STAT_SLIP_TURN,
}
/// <summary>
/// 카드 각성 정도
/// </summary>
public enum CardExceedType
{
    BASIC,
    EXCEED1,
    EXCEED2,
}
public enum CardBuffReasonType
{
    E_USERCHARACTER_REASON,
    E_FIELDCHARACTER_REASON,
    E_OVERRITE_REASON,
}
public enum CardRemoveType
{
    E_TURN_DISCOUNT,
    E_FIELD_CHARACTER_DEAD,
}
#endregion IngameType

#region BundleType
/// <summary>
/// AssetBundle 종류. BundleManager에서 번들명으로 변환.
/// CHARACTER 타입은 suffix(캐릭터 ID 소문자)를 함께 전달.
/// </summary>
public enum E_BUNDLE_TYPE
{
    MAIN,       // "main" - 앱 시작 시 자동 로드, 절대 언로드 불가
    INGAME,     // "ingame" - 인게임 진입 시 로드
    LOBBY,      // "lobby" - 로비 진입 시 로드
    CHARACTER,  // "character_xxx" - 캐릭터 선택 시 로드 (suffix 필요)
}
#endregion BundleType

#region CharacterType
/// <summary>
/// 캐릭터 고유 ID.
/// CHICK(1)은 무료 기본 캐릭터 (main 번들 포함).
/// 나머지는 개별 character_xxx 번들.
/// </summary>
public enum E_CHARACTER_ID
{
    NONE      = 0,
    CHICK     = 1,  // 오목눈이 솜이 — 기본 캐릭터, 무료
    CAT       = 2,  // 코리안 숏헤어 망고
    DOG       = 3,  // 포메라니안 루비
    CHINCHILLA = 4, // 친칠라 포포
    SQUIRREL   = 5, // 다람쥐
    RABBIT     = 6, // 토끼
    DRAGON     = 7, // 드래곤
}
#endregion CharacterType