using Cysharp.Threading.Tasks;
using Seunghak.Game.MillionDeathSquare;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>
    /// 창작마당·순위표·로비 서비스 연결 지점. Steam이 준비됐으면 Steam 구현, 아니면 로컬(오프라인) 구현.
    /// 처음 쓸 때 PlatformFactory.Current를 보고 정한다 (ApplicationManager가 플랫폼을 먼저 만든다).
    /// </summary>
    public static class MDSServices
    {
        private static bool _inited;
        private static IWorkshopService _workshop;
        private static ILeaderboardService _leaderboards;
        private static ILobbyService _lobby;

        public static bool UsingSteam { get; private set; }

        public static IWorkshopService Workshop { get { Init(); return _workshop; } }
        public static ILeaderboardService Leaderboards { get { Init(); return _leaderboards; } }
        public static ILobbyService Lobby { get { Init(); return _lobby; } }

        /// <summary>이 빌드의 기록 플랫폼 (Steam/에디터 = PC, 토스 = 모바일)</summary>
        public static RecordPlatform RecordPlatform
        {
            get { return PlatformFactory.Current is TossPlatform ? Seunghak.Game.MillionDeathSquare.RecordPlatform.Mobile : Seunghak.Game.MillionDeathSquare.RecordPlatform.PC; }
        }

        public static void Init()
        {
            if (_inited) return;
            _inited = true;
#if STEAMWORKS_NET
            if (SteamPlatform.IsReady)
            {
                _workshop = new SteamWorkshopService();
                _leaderboards = new SteamLeaderboardService();
                _lobby = new SteamLobbyService();
                UsingSteam = true;
                Debug.Log("[MDSServices] Steam 서비스 사용");
                return;
            }
#endif
            IPlatform p = PlatformFactory.Current;
            string id = p != null ? p.GetPlayerId() : "local";
            string name = p != null ? p.GetPlayerName() : "Player";
            _workshop = new LocalWorkshopService(id, name);
            _leaderboards = new LocalLeaderboardService(RecordPlatform, id, name);
            _lobby = new LocalLobbyService(StableId(id), name);
            UsingSteam = false;
            Debug.Log("[MDSServices] 로컬(오프라인) 서비스 사용");
        }

        /// <summary>문자열 플레이어 ID → ulong (FNV-1a). 로컬 로비용</summary>
        public static ulong StableId(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in s ?? "") { h ^= c; h *= 1099511628211UL; }
            return h == 0 ? 1 : h;
        }

        /// <summary>테스트/에디터: 강제로 다시 고르기</summary>
        public static void Reset() { _inited = false; }
    }
}
