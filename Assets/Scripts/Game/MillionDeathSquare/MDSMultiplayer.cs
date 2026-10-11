using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Seunghak.Platform;
using UnityEngine;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>다른 플레이어 표시 상태 (방장 중계로 받은 최근 위치)</summary>
    public sealed class RemotePlayer
    {
        public ulong Id;
        public string Name = "";
        public int Slot;
        public int Tick;
        public int X, Y;
        public int PrevX, PrevY;
        public float ReceivedAt;
        public bool Dead;
        public bool Finished;
        public int Deaths;
    }

    /// <summary>
    /// 멀티방 런타임 (기획서 7장): 로비 서비스 + RoomSession(방장 중계) + 전송 폴링 + 다른 플레이어 상태.
    /// 실제 판 진행은 MDSGameDirector가 하고, 여기서는 방과 메시지만 다룬다.
    /// </summary>
    public sealed class MDSMultiplayer
    {
        /// <summary>RoomSession → INetTransport 연결. 위치 메시지는 비신뢰 전송으로.</summary>
        private sealed class TransportAdapter : IRoomTransport
        {
            private readonly INetTransport _t;
            public TransportAdapter(INetTransport t) { _t = t; }
            public void Send(ulong peerId, byte[] data)
            {
                if (data.Length > 1 && data[1] == (byte)NetMessageType.Position) _t.SendUnreliable(peerId, data);
                else _t.Send(peerId, data);
            }
        }

        private readonly Dictionary<ulong, RemotePlayer> _remotes = new Dictionary<ulong, RemotePlayer>();
        private readonly List<MDSRoomBot> _bots = new List<MDSRoomBot>();
        private ILobbyService _lobby;
        private INetTransport _transport;
        private ResolvedMap _selected;
        private string _resolvingRef;
        private Action<ulong, byte[]> _receive;

        public RoomSession Session { get; private set; }
        public bool InRoom { get { return Session != null; } }
        public string LastError { get; private set; }
        public ResolvedMap SelectedMap { get { return _selected; } }
        public Dictionary<ulong, RemotePlayer> Remotes { get { return _remotes; } }

        public event Action RoomChanged;
        /// <summary>판 시작 (맵, 시작 메시지) — 디렉터가 카운트다운 후 틱 0부터 진행</summary>
        public event Action<ResolvedMap, NetMessage> MatchStarting;
        public event Action<List<PlayerResult>> ResultsReady;
        public event Action<int> TeamCoin;
        public event Action<RemotePlayer> RemoteDied;

        private ILobbyService Lobby
        {
            get
            {
                if (_lobby == null)
                {
                    _lobby = MDSServices.Lobby;
                    _lobby.InviteAccepted += OnInviteAccepted;
                }
                return _lobby;
            }
        }

        public bool SupportsFriendInvite { get { return Lobby.SupportsFriendInvite; } }
        public ulong LocalId { get { return Lobby.LocalPlayerId; } }

        public async UniTask<bool> CreateRoomAsync()
        {
            Leave();
            string code = Room.GenerateCode(new DeterministicRandom((ulong)DateTime.UtcNow.Ticks ^ Lobby.LocalPlayerId));
            LobbyResult r = await Lobby.CreateRoomAsync(code);
            if (!r.Success) { LastError = r.Error; return false; }
            _transport = Lobby.Transport;
            AttachSession(RoomSession.CreateHost(new TransportAdapter(_transport), Lobby.LocalPlayerId, Lobby.LocalPlayerName, r.RoomCode));
            return true;
        }

        public async UniTask<bool> JoinRoomAsync(string code)
        {
            Leave();
            LobbyResult r = await Lobby.JoinByCodeAsync(code);
            if (!r.Success) { LastError = r.Error; return false; }
            return JoinWith(r);
        }

        private bool JoinWith(LobbyResult r)
        {
            _transport = Lobby.Transport;
            if (r.HostId == Lobby.LocalPlayerId) return false;
            AttachSession(RoomSession.CreateClient(new TransportAdapter(_transport), Lobby.LocalPlayerId, Lobby.LocalPlayerName, r.HostId));
            return true;
        }

        private void OnInviteAccepted(LobbyResult r)
        {
            if (r == null || !r.Success) { LastError = r != null ? r.Error : "초대 입장 실패"; return; }
            Leave();
            JoinWith(r);
        }

        private void AttachSession(RoomSession s)
        {
            Session = s;
            _receive = (from, data) => s.Receive(from, data); // 매 프레임 폴링에 쓰는 콜백은 한 번만 만든다
            _remotes.Clear();
            s.RoomChanged += OnRoomChanged;
            s.MatchStarting += OnMatchStarting;
            s.RemotePosition += OnRemotePosition;
            s.RemoteDeath += OnRemoteDeath;
            s.RemoteFinish += OnRemoteFinish;
            s.TeamCoin += i => { if (TeamCoin != null) TeamCoin(i); };
            s.ResultsReady += r => { if (ResultsReady != null) ResultsReady(r); };
            s.JoinRejected += jr => { LastError = jr == JoinResult.Full ? "방이 가득 찼습니다" : "지금은 들어갈 수 없습니다 (" + jr + ")"; Leave(); };
            if (RoomChanged != null) RoomChanged();
        }

        public void Leave()
        {
            foreach (MDSRoomBot b in _bots) b.Dispose();
            _bots.Clear();
            if (Session != null) Session.Leave();
            Session = null;
            if (_lobby != null) _lobby.LeaveRoom();
            _transport = null;
            _selected = null;
            _remotes.Clear();
            if (RoomChanged != null) RoomChanged();
        }

        public bool InviteFriends() { return Lobby.InviteFriends(); }

        /// <summary>매 프레임: 받은 메시지 처리 + 봇</summary>
        public void Update(float dt)
        {
            if (_lobby != null) _lobby.Update();
            if (_transport != null && Session != null && _receive != null) _transport.Poll(_receive);
            for (int i = 0; i < _bots.Count; i++) _bots[i].Update(dt);
        }

        // ── 방장 조작 ──

        public async UniTask<bool> SelectMapAsync(string mapRef, MDSCampaignAsset asset)
        {
            if (Session == null || !Session.IsHost) return false;
            ResolvedMap m = await MDSMapResolver.ResolveAsync(mapRef, asset);
            if (!m.Ok) { LastError = m.Error; return false; }
            _selected = m;
            // 공유 코드로 고른 맵도 모두가 같은 데이터를 받도록 참조는 공유 코드 그대로 보낸다
            return Session.SelectMap(m.MapRef, m.Map.ContentHash());
        }

        public bool SelectMode(MatchMode mode) { return Session != null && Session.SelectMode(mode); }

        public bool CanStart { get { return Session != null && Session.Room != null && Session.Room.CanStart(Session.LocalId) && _selected != null; } }

        public bool StartMatch()
        {
            if (!CanStart) return false;
            return Session.StartMatch(_selected.Map);
        }

        /// <summary>디버그: 로컬 로비에 봇 참가 (혼자 멀티 화면 시험)</summary>
        public bool AddDebugBot()
        {
            if (Session == null || !Session.IsHost || MDSServices.UsingSteam) return false;
            ulong botId = 900000 + (ulong)_bots.Count + 1;
            _bots.Add(new MDSRoomBot(botId, "봇 " + (_bots.Count + 1), Session.HostId, this));
            return true;
        }

        // ── 받은 메시지 ──

        private void OnRoomChanged(Room room)
        {
            // 참가자: 방장이 고른 맵을 미리 풀어 둔다 (시작 신호에 바로 들어가도록)
            if (Session != null && !Session.IsHost && !string.IsNullOrEmpty(room.MapRef) && room.MapRef != _resolvingRef
                && (_selected == null || _selected.MapRef != room.MapRef))
            {
                ResolveForClient(room.MapRef, room.MapHash).Forget();
            }
            foreach (RoomMember m in room.Members)
            {
                RemotePlayer rp;
                if (m.PlayerId == LocalId) continue;
                if (!_remotes.TryGetValue(m.PlayerId, out rp)) { rp = new RemotePlayer { Id = m.PlayerId }; _remotes[m.PlayerId] = rp; }
                rp.Name = m.Name;
                rp.Slot = m.Slot;
            }
            var gone = new List<ulong>();
            foreach (ulong id in _remotes.Keys) if (room.Find(id) == null) gone.Add(id);
            foreach (ulong id in gone) _remotes.Remove(id);
            if (RoomChanged != null) RoomChanged();
        }

        private async UniTaskVoid ResolveForClient(string mapRef, uint hash)
        {
            _resolvingRef = mapRef;
            ResolvedMap m = await MDSMapResolver.ResolveAsync(mapRef, null);
            _resolvingRef = null;
            if (!m.Ok) { LastError = m.Error; return; }
            if (m.Map.ContentHash() != hash) { LastError = "방장의 맵과 내 맵이 다릅니다 (게임 버전 확인)"; return; }
            _selected = m;
            if (RoomChanged != null) RoomChanged();
        }

        private void OnMatchStarting(NetMessage start)
        {
            foreach (RemotePlayer rp in _remotes.Values) { rp.Dead = false; rp.Finished = false; rp.Deaths = 0; rp.Tick = 0; }
            if (_selected == null || _selected.Map.ContentHash() != start.MapHash)
            {
                LastError = "맵을 아직 받지 못했습니다";
                return;
            }
            if (MatchStarting != null) MatchStarting(_selected, start);
        }

        private RemotePlayer Remote(ulong id)
        {
            RemotePlayer rp;
            if (!_remotes.TryGetValue(id, out rp)) { rp = new RemotePlayer { Id = id }; _remotes[id] = rp; }
            return rp;
        }

        private void OnRemotePosition(NetMessage m)
        {
            RemotePlayer rp = Remote(m.Sender);
            if (m.Type == NetMessageType.Position && m.Tick < rp.Tick) return; // 늦게 온 비신뢰 패킷
            rp.PrevX = rp.X; rp.PrevY = rp.Y;
            rp.X = m.X; rp.Y = m.Y;
            rp.Tick = m.Tick;
            rp.Dead = (m.Value & 1) != 0;
            rp.Finished = (m.Value & 2) != 0;
            rp.ReceivedAt = Time.unscaledTime;
        }

        private void OnRemoteDeath(NetMessage m)
        {
            RemotePlayer rp = Remote(m.Sender);
            rp.Deaths = m.Value;
            rp.X = m.X; rp.Y = m.Y;
            rp.Dead = true;
            if (RemoteDied != null) RemoteDied(rp);
        }

        private void OnRemoteFinish(NetMessage m)
        {
            RemotePlayer rp = Remote(m.Sender);
            rp.Finished = true;
            rp.Deaths = m.Value;
        }

        public string NameOf(ulong id)
        {
            if (Session != null && Session.Room != null)
            {
                RoomMember mem = Session.Room.Find(id);
                if (mem != null) return mem.Name;
            }
            return id == LocalId ? Lobby.LocalPlayerName : "?";
        }
    }

    /// <summary>
    /// 디버그 봇 (로컬 로비 전용) — 같은 프로세스의 루프백 전송으로 방에 들어와, 캠페인 맵이면 풀이 대본을 따라 달린다.
    /// 혼자서 멀티방·유령 표시·결과 화면을 시험하는 용도. Steam 빌드에서는 만들 수 없다.
    /// </summary>
    public sealed class MDSRoomBot
    {
        private readonly LoopbackTransport _transport;
        private readonly RoomSession _session;
        private readonly MDSMultiplayer _owner;
        private Simulation _sim;
        private InputRecording _script;
        private int _countdown;
        private float _acc;
        private bool _finished;

        private sealed class Adapter : IRoomTransport
        {
            private readonly LoopbackTransport _t;
            public Adapter(LoopbackTransport t) { _t = t; }
            public void Send(ulong peerId, byte[] data) { _t.Send(peerId, data); }
        }

        public MDSRoomBot(ulong id, string name, ulong hostId, MDSMultiplayer owner)
        {
            _owner = owner;
            _transport = new LoopbackTransport(id);
            _session = RoomSession.CreateClient(new Adapter(_transport), id, name, hostId);
            _session.MatchStarting += OnStart;
        }

        private void OnStart(NetMessage start)
        {
            ResolvedMap m = _owner.SelectedMap;
            if (m == null) return;
            _sim = new Simulation(m.Map);
            string script = m.IsCampaign ? CampaignSolutions.Get(m.World, m.Stage) : null;
            _script = InputRecording.FromScript(m.Map.ContentHash(), script ?? "W600");
            _countdown = start.Tick;
            _acc = 0f;
            _finished = false;
        }

        public void Update(float dt)
        {
            RoomSession s = _session;
            _transport.Poll((from, data) => s.Receive(from, data));
            if (_sim == null || _finished) return;
            _acc += dt;
            const float step = 1f / Fixed.TICKS_PER_SECOND;
            int guard = 0;
            while (_acc >= step && guard++ < 8)
            {
                _acc -= step;
                if (_countdown > 0) { _countdown--; continue; }
                int t = _sim.Tick;
                InputBits input = t < _script.Length ? _script[t] : InputBits.None;
                _sim.Step(input);
                for (int i = 0; i < _sim.EventCount; i++)
                {
                    SimEvent e = _sim.GetEvent(i);
                    if (e.Type == SimEventType.Death) _session.ReportDeath(_sim.Tick, e.X, e.Y, _sim.Deaths);
                    if (e.Type == SimEventType.Coin) _session.ReportCoin(_sim.Tick, e.Index);
                }
                if (_sim.Tick % 3 == 0) _session.SendPosition(_sim.Tick, _sim.PlayerX, _sim.PlayerY, _sim.State == SimState.Dead ? 1 : 0);
                if (_sim.IsCleared)
                {
                    _session.SendPosition(_sim.Tick, _sim.PlayerX, _sim.PlayerY, 2);
                    _session.ReportFinish(_sim.ClearTick, _sim.Deaths);
                    _finished = true;
                    return;
                }
            }
        }

        public void Dispose()
        {
            _session.Leave();
            _transport.Close();
        }
    }
}
