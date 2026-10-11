using System;
using System.Collections.Generic;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>바이트를 특정 상대에게 보내는 최소 전송 계층 (Steam 네트워킹 / 루프백 등이 구현)</summary>
    public interface IRoomTransport
    {
        void Send(ulong peerId, byte[] data);
    }

    /// <summary>
    /// 방장 중계 멀티방 세션 (엔진 독립, 기획서 7장). 방장과 참가자 모두 이 클래스를 쓴다.
    ///   - 참가자는 방장에게만 보내고, 방장이 RelayRouter 규칙대로 다른 사람에게 다시 보낸다.
    ///   - 방장이 Room/MatchTracker의 원본을 갖고 RoomState·Results를 모두에게 알린다.
    ///   - 시작: 방장이 Start(맵 해시, 방식, 카운트다운)를 보내고, 각자 받은 순간 + 카운트다운 뒤를 틱 0으로 삼는다.
    ///     장애물은 틱의 순수 함수라 이것만으로 모두의 화면이 맞는다. 죽음 판정은 각자 PC에서 한다 (7.3).
    /// </summary>
    public sealed class RoomSession
    {
        private readonly IRoomTransport _transport;

        public readonly ulong LocalId;
        public readonly string LocalName;
        public ulong HostId { get; private set; }
        public bool IsHost { get { return LocalId == HostId; } }
        public Room Room { get; private set; }
        /// <summary>방장만 가짐 (판 집계 원본)</summary>
        public MatchTracker Match { get; private set; }
        public List<PlayerResult> LastResults { get; private set; }

        public event Action<Room> RoomChanged;
        /// <summary>판 시작 (맵 해시, 방식, 카운트다운 틱)</summary>
        public event Action<NetMessage> MatchStarting;
        public event Action<NetMessage> RemotePosition;
        public event Action<NetMessage> RemoteDeath;
        /// <summary>협동: 다른 사람이 먹은 동전 번호 → 내 Simulation.ApplyTeamCoin</summary>
        public event Action<int> TeamCoin;
        public event Action<NetMessage> RemoteFinish;
        public event Action<List<PlayerResult>> ResultsReady;
        public event Action<JoinResult> JoinRejected;

        private RoomSession(IRoomTransport transport, ulong localId, string localName)
        {
            _transport = transport;
            LocalId = localId;
            LocalName = localName ?? "";
        }

        public static RoomSession CreateHost(IRoomTransport transport, ulong localId, string localName, string roomCode)
        {
            var s = new RoomSession(transport, localId, localName);
            s.HostId = localId;
            s.Room = new Room(roomCode, localId, localName);
            return s;
        }

        /// <summary>참가자 세션. 방장에게 Hello를 보낸다 (방장 ID는 로비 서비스가 방 코드로 찾아 준다).</summary>
        public static RoomSession CreateClient(IRoomTransport transport, ulong localId, string localName, ulong hostId)
        {
            var s = new RoomSession(transport, localId, localName);
            s.HostId = hostId;
            s.SendToHost(new NetMessage { Type = NetMessageType.Hello, Sender = localId, Text = s.LocalName });
            return s;
        }

        // ── 보내기 ──

        private void SendToHost(NetMessage m)
        {
            if (IsHost) HandleAsHost(LocalId, m);
            else _transport.Send(HostId, NetCodec.Encode(m));
        }

        private void Broadcast(NetMessage m, ulong except)
        {
            byte[] data = NetCodec.Encode(m);
            foreach (RoomMember mem in Room.Members)
            {
                if (mem.PlayerId == LocalId || mem.PlayerId == except) continue;
                _transport.Send(mem.PlayerId, data);
            }
        }

        private void BroadcastRoom()
        {
            Broadcast(new NetMessage { Type = NetMessageType.RoomState, Sender = LocalId, RoomSnapshot = Room }, 0);
            if (RoomChanged != null) RoomChanged(Room);
        }

        // ── 방장 조작 ──

        public bool SelectMap(string mapRef, uint mapHash)
        {
            if (!IsHost || !Room.SelectMap(LocalId, mapRef, mapHash)) return false;
            BroadcastRoom();
            return true;
        }

        public bool SelectMode(MatchMode mode)
        {
            if (!IsHost || !Room.SelectMode(LocalId, mode)) return false;
            BroadcastRoom();
            return true;
        }

        /// <summary>판 시작 (방장만). map은 Room.MapRef가 가리키는 맵 (해시가 같아야 함)</summary>
        public bool StartMatch(MapData map)
        {
            if (!IsHost || map == null || map.ContentHash() != Room.MapHash) return false;
            if (!Room.Start(LocalId)) return false;
            var ids = new List<ulong>();
            foreach (RoomMember mem in Room.Members) ids.Add(mem.PlayerId);
            Match = new MatchTracker(Room.Mode, map, ids);
            LastResults = null;
            NetMessage start = NetMessage.StartMatch(LocalId, Room.MapHash, Room.Mode, Room.CountdownTicks);
            Broadcast(start, 0);
            Room.State = RoomState.Playing;
            BroadcastRoom();
            if (MatchStarting != null) MatchStarting(start);
            return true;
        }

        /// <summary>방장의 판 틱 진행 — 시간 제한/종료 판정 후 결과 공유</summary>
        public void HostAdvance(int tick)
        {
            if (!IsHost || Match == null || Room.State != RoomState.Playing) return;
            Match.Advance(tick);
            if (Match.IsOver) FinishMatch();
        }

        private void FinishMatch()
        {
            LastResults = Match.ComputeResults();
            Room.State = RoomState.Results;
            Broadcast(new NetMessage { Type = NetMessageType.Results, Sender = LocalId, Results = LastResults }, 0);
            BroadcastRoom();
            if (ResultsReady != null) ResultsReady(LastResults);
        }

        // ── 내 플레이 보고 (모두) ──

        public void SendPosition(int tick, int x, int y, int flags) { SendToHost(NetMessage.Position(LocalId, tick, x, y, flags)); }
        public void ReportDeath(int tick, int x, int y, int deaths) { SendToHost(NetMessage.Death(LocalId, tick, x, y, deaths)); }
        public void ReportCoin(int tick, int index) { SendToHost(new NetMessage { Type = NetMessageType.Coin, Sender = LocalId, Tick = tick, Value = index }); }
        public void ReportFinish(int clearTick, int deaths) { SendToHost(NetMessage.Finish(LocalId, clearTick, deaths)); }
        public void ReportRestart(int tick, int x, int y) { SendToHost(new NetMessage { Type = NetMessageType.Restart, Sender = LocalId, Tick = tick, X = x, Y = y }); }

        public void Leave()
        {
            if (IsHost)
            {
                Room.Leave(LocalId);
                if (Room.Count > 0)
                {
                    // 방장 넘기기: 남은 사람에게 새 방 상태 (새 방장이 이후 원본이 됨)
                    Broadcast(new NetMessage { Type = NetMessageType.RoomState, Sender = LocalId, RoomSnapshot = Room }, 0);
                }
            }
            else
            {
                _transport.Send(HostId, NetCodec.Encode(new NetMessage { Type = NetMessageType.Leave, Sender = LocalId }));
            }
        }

        // ── 받기 ──

        /// <summary>전송 계층이 받은 바이트를 넘긴다. 깨진 메시지는 무시하고 false.</summary>
        public bool Receive(ulong from, byte[] data)
        {
            NetMessage m;
            try { m = NetCodec.Decode(data); }
            catch (FormatException) { return false; }
            if (IsHost)
            {
                if (m.Sender != from) return false; // 다른 사람인 척하는 메시지 차단
                HandleAsHost(from, m);
            }
            else
            {
                if (from != HostId) return false; // 참가자는 방장(중계자)이 보낸 것만 받는다
                HandleAsClient(from, m);
            }
            return true;
        }

        private void HandleAsHost(ulong from, NetMessage m)
        {
            switch (m.Type)
            {
                case NetMessageType.Hello:
                {
                    JoinResult jr = Room.TryJoin(m.Sender, m.Text);
                    if (jr == JoinResult.Ok || jr == JoinResult.AlreadyIn) BroadcastRoom();
                    else _transport.Send(m.Sender, NetCodec.Encode(new NetMessage { Type = NetMessageType.Leave, Sender = LocalId, Text = jr.ToString() }));
                    return;
                }
                case NetMessageType.Leave:
                    if (Room.Leave(m.Sender)) return;
                    if (Match != null) Match.RemovePlayer(m.Sender);
                    BroadcastRoom();
                    return;
            }

            RelayRouter.Route route = RelayRouter.RouteFor(m, Room);
            if (route == RelayRouter.Route.Drop) return;

            // 방장 집계
            if (Match != null && Room.State == RoomState.Playing)
            {
                switch (m.Type)
                {
                    case NetMessageType.Death: Match.OnDeath(m.Sender, m.Tick, m.X, m.Y); break;
                    case NetMessageType.Finish: Match.OnFinish(m.Sender, m.Tick); break;
                    case NetMessageType.Coin:
                        bool fresh = Match.OnCoin(m.Sender, m.Value);
                        if (!fresh && Room.Mode == MatchMode.Coop) return; // 이미 팀이 먹은 동전은 중계 안 함
                        break;
                }
            }

            if (route == RelayRouter.Route.OthersExceptSender || route == RelayRouter.Route.Everyone)
                Broadcast(m, m.Sender);

            // 방장 자신의 화면에도 반영 (다른 사람이 보낸 것만)
            if (m.Sender != LocalId) Dispatch(m);

            if (Match != null && Room.State == RoomState.Playing && m.Type == NetMessageType.Finish && Match.IsOver) FinishMatch();
        }

        private void HandleAsClient(ulong from, NetMessage m)
        {
            switch (m.Type)
            {
                case NetMessageType.RoomState:
                    Room = m.RoomSnapshot;
                    HostId = Room.HostId;
                    if (RoomChanged != null) RoomChanged(Room);
                    return;
                case NetMessageType.Leave:
                    JoinResult jr;
                    if (JoinRejected != null && Enum.TryParse(m.Text, out jr)) JoinRejected(jr);
                    return;
                case NetMessageType.Start:
                    if (MatchStarting != null) MatchStarting(m);
                    return;
                case NetMessageType.Results:
                    LastResults = m.Results;
                    if (ResultsReady != null) ResultsReady(m.Results);
                    return;
            }
            if (m.Sender != LocalId) Dispatch(m);
        }

        private void Dispatch(NetMessage m)
        {
            switch (m.Type)
            {
                case NetMessageType.Position:
                case NetMessageType.Restart:
                    if (RemotePosition != null) RemotePosition(m);
                    break;
                case NetMessageType.Death:
                    if (RemoteDeath != null) RemoteDeath(m);
                    break;
                case NetMessageType.Coin:
                    if (TeamCoin != null && (Room == null || Room.Mode == MatchMode.Coop)) TeamCoin(m.Value);
                    break;
                case NetMessageType.Finish:
                    if (RemoteFinish != null) RemoteFinish(m);
                    break;
            }
        }
    }

    /// <summary>테스트·에디터용 메모리 안 전송 (같은 프로세스의 세션끼리 연결). 보낸 순서대로 Pump()에서 전달.</summary>
    public sealed class LoopbackHub
    {
        private struct Packet { public ulong From; public ulong To; public byte[] Data; }

        private readonly Dictionary<ulong, RoomSession> _sessions = new Dictionary<ulong, RoomSession>();
        private readonly Queue<Packet> _queue = new Queue<Packet>();

        public int Sent { get; private set; }
        public long BytesSent { get; private set; }

        public IRoomTransport TransportFor(ulong localId) { return new Endpoint(this, localId); }

        public void Register(RoomSession session) { _sessions[session.LocalId] = session; }

        /// <summary>쌓인 패킷을 모두 전달 (전달 중 생긴 패킷까지)</summary>
        public int Pump()
        {
            int n = 0;
            while (_queue.Count > 0)
            {
                Packet p = _queue.Dequeue();
                RoomSession s;
                if (_sessions.TryGetValue(p.To, out s)) s.Receive(p.From, p.Data);
                n++;
            }
            return n;
        }

        private sealed class Endpoint : IRoomTransport
        {
            private readonly LoopbackHub _hub;
            private readonly ulong _from;
            public Endpoint(LoopbackHub hub, ulong from) { _hub = hub; _from = from; }
            public void Send(ulong peerId, byte[] data)
            {
                _hub._queue.Enqueue(new Packet { From = _from, To = peerId, Data = data });
                _hub.Sent++;
                _hub.BytesSent += data.Length;
            }
        }
    }
}
