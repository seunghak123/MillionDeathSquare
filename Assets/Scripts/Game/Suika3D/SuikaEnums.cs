namespace Seunghak.Game.Suika3D
{
    /// <summary>판 진행 상태</summary>
    public enum E_SUIKA_GAME_STATE
    {
        NONE,
        READY,      // 시작 대기 (첫 드롭 전, 타이머 정지)
        PLAYING,    // 플레이 중
        PAUSED,     // 일시정지
        LEVEL_UP,   // 레벨업 스킬 선택 중 (게임/타이머 정지)
        GAME_OVER,  // 시간 또는 라이프 소진 (이어하기 대기 포함)
    }

    /// <summary>판 종료 원인</summary>
    public enum E_SUIKA_GAMEOVER_CAUSE
    {
        NONE,
        LIVES_OUT,  // 라이프 소진 (화석이 발굴대 밖으로 3번 떨어짐)
        TIME_OUT,   // 시간 소진
    }

    /// <summary>게임 모드. 일반 = 발굴대 고정, 지각 변동 = 폰 기울기 + 지진 이벤트 (해금형)</summary>
    public enum E_SUIKA_DIFFICULTY
    {
        NORMAL = 0,
        TECTONIC = 1,
    }

    /// <summary>스킬 5종 (모두 1회용)</summary>
    public enum E_FOSSIL_SKILL
    {
        NONE = -1,
        HAMMER = 0,      // 탭한 화석 1개 부수기
        BRUSH = 1,       // 1~2단계 화석 모두 쓸어내기
        EARTHQUAKE = 2,  // 정한 방향으로 3초 흔들기 (벽이 솟아 낙하 방지)
        MAGNET = 3,      // 탭한 화석과 같은 화석 끌어당기기
        EVOLVE = 4,      // 탭한 화석 1단계 업그레이드
    }
}
