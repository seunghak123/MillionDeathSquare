namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 공용 UI(일시정지/설정 팝업 등)가 현재 판을 제어할 때 쓰는 얇은 연결 지점.
    /// 실제 구현은 MDSGameDirector가 Control에 자신을 등록한다. 등록 전이면 모든 호출은 무시된다.
    /// (공용 UI가 특정 게임 매니저 타입에 직접 의존하지 않게 하기 위함)
    /// </summary>
    public interface IMDSSessionControl
    {
        bool IsPlaying { get; }
        bool IsPaused { get; }
        /// <summary>현재 판의 데스 수 (공용 일시정지 팝업 표시용)</summary>
        int CurrentDeaths { get; }
        void Pause();
        void Resume();
        /// <summary>현재 맵을 처음부터 다시 (R 키와 같음)</summary>
        void Restart();
        /// <summary>판을 정리하고 메인 화면으로</summary>
        void ExitToMenu();
    }

    public static class MDSSession
    {
        public static IMDSSessionControl Control;

        public static bool IsPlaying { get { return Control != null && Control.IsPlaying; } }
        public static bool IsPaused { get { return Control != null && Control.IsPaused; } }
        public static int CurrentDeaths { get { return Control != null ? Control.CurrentDeaths : 0; } }

        public static void Pause() { if (Control != null) Control.Pause(); }
        public static void Resume() { if (Control != null) Control.Resume(); }
        public static void Restart() { if (Control != null) Control.Restart(); }
        /// <summary>디렉터가 없으면(아직 등록 전) 로비 씬으로 바로 전환한다.</summary>
        public static void ExitToMenu()
        {
            if (Control != null) Control.ExitToMenu();
            else Seunghak.SceneManager.SceneManager.Instance.ChangeScene(E_SCENE_TYPE.LOBBY);
        }
    }
}
