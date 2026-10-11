namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 밸런스 초기값 (기획서 13장) + 판정 크기. 모두 정수 단위(Fixed.CELL = 1칸).
    /// 값을 바꾸면 기존 리플레이/기록과 호환되지 않으므로 RULES_VERSION을 올린다.
    /// </summary>
    public static class Rules
    {
        /// <summary>판정 규칙 버전 — 맵 데이터/리플레이에 기록되어, 다르면 검증을 거부한다</summary>
        public const int RULES_VERSION = 1;

        // ── 플레이어 ──
        /// <summary>네모 이동 속도: 초당 4칸 = 틱당 80단위 (축마다)</summary>
        public const int PLAYER_SPEED = 4 * Fixed.CELL / Fixed.TICKS_PER_SECOND;
        /// <summary>네모 반폭 (0.3칸 → 한 변 0.6칸)</summary>
        public const int PLAYER_HALF = Fixed.CELL * 3 / 10;
        /// <summary>죽은 뒤 재시작까지 0.3초</summary>
        public const int RESPAWN_TICKS = Fixed.TICKS_PER_SECOND * 3 / 10;

        // ── 장애물 ──
        /// <summary>장애물(동그라미) 반지름 0.25칸</summary>
        public const int OBSTACLE_RADIUS = Fixed.CELL / 4;
        /// <summary>속도 단계 1/2/3 → 초당 2/4/6칸</summary>
        public static int ObstacleSpeedPerTick(int tier)
        {
            if (tier < 1) tier = 1;
            if (tier > 3) tier = 3;
            return tier * 2 * Fixed.CELL / Fixed.TICKS_PER_SECOND;
        }
        /// <summary>출발 지연 1단위 = 0.1초 = 6틱</summary>
        public const int DELAY_UNIT_TICKS = Fixed.TICKS_PER_SECOND / 10;

        // ── 아이템 ──
        public const int COIN_RADIUS = Fixed.CELL / 5;
        public const int KEY_RADIUS = Fixed.CELL / 4;

        // ── 바닥 ──
        /// <summary>컨베이어가 미는 속도: 초당 2칸</summary>
        public const int CONVEYOR_SPEED = 2 * Fixed.CELL / Fixed.TICKS_PER_SECOND;
        /// <summary>사라지는 바닥 주기 3초 (보임 2초 + 사라짐 1초). 위상 0~3 = 0.75초씩 어긋남</summary>
        public const int VANISH_PERIOD = 3 * Fixed.TICKS_PER_SECOND;
        public const int VANISH_SOLID_TICKS = 2 * Fixed.TICKS_PER_SECOND;
        public const int VANISH_PHASE_STEP = VANISH_PERIOD / 4;
        /// <summary>사라지기 직전 경고(깜빡임) 구간 — 표시용</summary>
        public const int VANISH_WARN_TICKS = Fixed.TICKS_PER_SECOND / 2;

        // ── 맵 제한 (기획서 5.3) ──
        public const int MAX_WIDTH = 32;
        public const int MAX_HEIGHT = 18;
        public const int MIN_SIZE = 3;
        public const int MAX_OBSTACLES = 60;
        public const int MAX_COINS = 30;
        public const int MAX_PATH_POINTS = 8;
        public const int MAX_GOALS = 16;
        public const int MAX_CHECKPOINTS = 16;
        public const int MAX_KEYS = 9;
        public const int MAX_DOORS = 48;
        public const int KEY_COLORS = 3;
        public const int PALETTE_COUNT = 8;
        public const int MAX_CIRCLE_RADIUS_HALF = 16;
        public const int MAX_NAME_LENGTH = 24;

        // ── 캠페인 ──
        public const int WORLD_COUNT = 5;
        public const int STAGES_PER_WORLD = 10;
        public const int STAGES_TO_UNLOCK_NEXT_WORLD = 7;

        // ── 멀티 ──
        public const int ROOM_MIN_PLAYERS = 2;
        public const int ROOM_MAX_PLAYERS = 4;
        public const int SURVIVAL_TICKS = 3 * 60 * Fixed.TICKS_PER_SECOND;
        /// <summary>레이스: 첫 골인 후 나머지가 들어올 수 있는 유예 30초</summary>
        public const int RACE_GRACE_TICKS = 30 * Fixed.TICKS_PER_SECOND;
        /// <summary>시작 카운트다운 3초</summary>
        public const int START_COUNTDOWN_TICKS = 3 * Fixed.TICKS_PER_SECOND;

        // ── 공유 ──
        /// <summary>창작마당 1인당 업로드 수 (기획서 13장)</summary>
        public const int MAX_UPLOADS_PER_USER = 50;

        // ── 검증 ──
        /// <summary>리플레이 검증 상한 1시간</summary>
        public const int MAX_REPLAY_TICKS = 60 * 60 * Fixed.TICKS_PER_SECOND;
    }
}
