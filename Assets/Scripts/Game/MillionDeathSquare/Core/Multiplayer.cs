using System;
using System.Collections.Generic;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>시드 기반 정수 난수 (xorshift64*). 결정적이어야 하는 곳에서만 사용.</summary>
    public sealed class DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed) { _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }

        public ulong NextULong()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return _state * 2685821657736338717UL;
        }

        /// <summary>[0, max) 정수</summary>
        public int Next(int max) { return max <= 0 ? 0 : (int)(NextULong() % (ulong)max); }
    }

    /// <summary>멀티방 방식 (기획서 7.2)</summary>
    public enum MatchMode : byte
    {
        /// <summary>먼저 골인한 사람이 승리</summary>
        Race = 0,
        /// <summary>동전을 나눠 모으고 한 명이라도 골인하면 모두 클리어</summary>
        Coop = 1,
        /// <summary>제한 시간(3분) 안에 덜 죽은 사람이 승리</summary>
        Survival = 2,
    }

    public enum RoomState : byte
    {
        Lobby = 0,
        /// <summary>시작 신호 후 카운트다운</summary>
        Countdown = 1,
        Playing = 2,
        Results = 3,
    }

    public enum JoinResult
    {
        Ok,
        AlreadyIn,
        Full,
        InProgress,
    }

    public sealed class RoomMember
    {
        public ulong PlayerId;
        public string Name;
        /// <summary>색 슬롯 0~3 (입장 순서, 빈자리 재사용)</summary>
        public int Slot;
    }

    /// <summary>
    /// 비공개 멀티방 모델 (기획서 7.1). 방장이 원본을 갖고 RoomState 메시지로 모두에게 알린다.
    /// 2~4명, 방 코드 또는 친구 초대로만 입장, 맵(공유 코드/맵 코드/캠페인 ID)과 방식은 방장이 고르고 방장만 시작.
    /// </summary>
    public sealed class Room
    {
        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public const int CODE_LENGTH = 6;

        public string Code;
        public ulong HostId;
        public MatchMode Mode = MatchMode.Race;
        /// <summary>고른 맵: 공유 코드("MDS1-…"), 맵 코드, 또는 "campaign:1-3"</summary>
        public string MapRef = "";
        /// <summary>고른 맵의 내용 해시 (모두 같은 맵인지 확인)</summary>
        public uint MapHash;
        public RoomState State = RoomState.Lobby;
        /// <summary>시작 메시지의 카운트다운 틱 (받은 순간부터 이만큼 뒤에 틱 0)</summary>
        public int CountdownTicks = Rules.START_COUNTDOWN_TICKS;
        public readonly List<RoomMember> Members = new List<RoomMember>();

        public Room(string code, ulong hostId, string hostName)
        {
            Code = code;
            HostId = hostId;
            Members.Add(new RoomMember { PlayerId = hostId, Name = hostName ?? "", Slot = 0 });
        }

        /// <summary>방 코드 6글자 (헷갈리는 글자 I/O/0/1 없음)</summary>
        public static string GenerateCode(DeterministicRandom rng)
        {
            var chars = new char[CODE_LENGTH];
            for (int i = 0; i < CODE_LENGTH; i++) chars[i] = CodeAlphabet[rng.Next(CodeAlphabet.Length)];
            return new string(chars);
        }

        public static string NormalizeCode(string code)
        {
            if (code == null) return "";
            var chars = new List<char>();
            foreach (char c in code)
            {
                char u = char.ToUpperInvariant(c);
                if (u == 'O') u = '0';
                if (u == 'I') u = '1';
                if (CodeAlphabet.IndexOf(u) >= 0) chars.Add(u);
            }
            return new string(chars.ToArray());
        }

        public int Count { get { return Members.Count; } }
        public bool IsFull { get { return Members.Count >= Rules.ROOM_MAX_PLAYERS; } }
        public bool IsHost(ulong playerId) { return playerId == HostId; }

        public RoomMember Find(ulong playerId)
        {
            for (int i = 0; i < Members.Count; i++) if (Members[i].PlayerId == playerId) return Members[i];
            return null;
        }

        public JoinResult TryJoin(ulong playerId, string name)
        {
            if (Find(playerId) != null) return JoinResult.AlreadyIn;
            if (State != RoomState.Lobby && State != RoomState.Results) return JoinResult.InProgress;
            if (IsFull) return JoinResult.Full;
            int slot = 0;
            while (SlotUsed(slot)) slot++;
            Members.Add(new RoomMember { PlayerId = playerId, Name = name ?? "", Slot = slot });
            return JoinResult.Ok;
        }

        private bool SlotUsed(int slot)
        {
            for (int i = 0; i < Members.Count; i++) if (Members[i].Slot == slot) return true;
            return false;
        }

        /// <summary>나가기. 방장이 나가면 가장 먼저 들어온 사람이 방장. 방이 비면 true.</summary>
        public bool Leave(ulong playerId)
        {
            for (int i = 0; i < Members.Count; i++)
            {
                if (Members[i].PlayerId != playerId) continue;
                Members.RemoveAt(i);
                if (Members.Count == 0) return true;
                if (HostId == playerId) HostId = Members[0].PlayerId;
                return false;
            }
            return Members.Count == 0;
        }

        public bool SelectMap(ulong requester, string mapRef, uint mapHash)
        {
            if (!IsHost(requester) || State != RoomState.Lobby && State != RoomState.Results) return false;
            MapRef = mapRef ?? "";
            MapHash = mapHash;
            return true;
        }

        public bool SelectMode(ulong requester, MatchMode mode)
        {
            if (!IsHost(requester) || State != RoomState.Lobby && State != RoomState.Results) return false;
            Mode = mode;
            return true;
        }

        /// <summary>방장만, 2명 이상, 맵을 골랐을 때</summary>
        public bool CanStart(ulong requester)
        {
            return IsHost(requester)
                && (State == RoomState.Lobby || State == RoomState.Results)
                && Members.Count >= Rules.ROOM_MIN_PLAYERS
                && !string.IsNullOrEmpty(MapRef);
        }

        public bool Start(ulong requester)
        {
            if (!CanStart(requester)) return false;
            State = RoomState.Countdown;
            return true;
        }
    }

    // ── 메시지 ──

    public enum NetMessageType : byte
    {
        /// <summary>참가 요청 (클라 → 방장). Text = 이름</summary>
        Hello = 1,
        /// <summary>방 상태 전체 (방장 → 모두)</summary>
        RoomState = 2,
        /// <summary>맵/방식 선택 (방장 → 모두, RoomState로도 반영)</summary>
        SelectMap = 3,
        SelectMode = 4,
        /// <summary>시작: 맵 해시 + 방식 + 카운트다운 틱. 받은 순간 + 카운트다운 = 모두의 틱 0 (기획서 7.3)</summary>
        Start = 5,
        /// <summary>내 위치 (틱, x, y, 플래그). 방장이 다른 모두에게 중계</summary>
        Position = 6,
        /// <summary>죽음 (틱, 위치, 누적 데스)</summary>
        Death = 7,
        /// <summary>협동: 동전 먹음 (번호)</summary>
        Coin = 8,
        /// <summary>골인 (내 PC 기준 클리어 틱, 데스) — 판정은 각자 PC의 플레이 시간 (기획서 10장)</summary>
        Finish = 9,
        /// <summary>판 결과 (방장 → 모두)</summary>
        Results = 10,
        Leave = 11,
        /// <summary>멀티 중 R: 시작점으로 (위치 동기화용 알림)</summary>
        Restart = 12,
    }

    /// <summary>
    /// 방장 중계용 메시지 하나. 필드는 종류에 따라 쓰는 것만 쓴다.
    /// 위치 메시지는 12바이트 안팎이라 실시간 멀티치고 데이터가 적다 (기획서 7.3).
    /// </summary>
    public sealed class NetMessage
    {
        public NetMessageType Type;
        public ulong Sender;
        public int Tick;
        public int X;
        public int Y;
        /// <summary>Position: 1 = 죽음 연출 중, 2 = 클리어 / Death: 누적 데스 / Coin: 번호 / Finish: 데스</summary>
        public int Value;
        public uint MapHash;
        public MatchMode Mode;
        public string Text = "";
        /// <summary>RoomState 전용</summary>
        public Room RoomSnapshot;
        /// <summary>Results 전용</summary>
        public List<PlayerResult> Results;

        public static NetMessage Position(ulong sender, int tick, int x, int y, int flags)
        {
            return new NetMessage { Type = NetMessageType.Position, Sender = sender, Tick = tick, X = x, Y = y, Value = flags };
        }

        public static NetMessage Death(ulong sender, int tick, int x, int y, int deaths)
        {
            return new NetMessage { Type = NetMessageType.Death, Sender = sender, Tick = tick, X = x, Y = y, Value = deaths };
        }

        public static NetMessage Finish(ulong sender, int clearTick, int deaths)
        {
            return new NetMessage { Type = NetMessageType.Finish, Sender = sender, Tick = clearTick, Value = deaths };
        }

        public static NetMessage StartMatch(ulong host, uint mapHash, MatchMode mode, int countdownTicks)
        {
            return new NetMessage { Type = NetMessageType.Start, Sender = host, MapHash = mapHash, Mode = mode, Tick = countdownTicks };
        }
    }

    /// <summary>NetMessage ↔ 바이트 (리틀 엔디언, 가변 길이 정수)</summary>
    public static class NetCodec
    {
        public const int PROTOCOL_VERSION = 1;

        public static byte[] Encode(NetMessage m)
        {
            var w = new ByteWriter(32);
            w.WriteByte(PROTOCOL_VERSION);
            w.WriteByte((int)m.Type);
            w.WriteULong(m.Sender);
            switch (m.Type)
            {
                case NetMessageType.Hello:
                case NetMessageType.Leave:
                    w.WriteString(m.Text);
                    break;
                case NetMessageType.SelectMap:
                    w.WriteString(m.Text);
                    w.WriteUInt(m.MapHash);
                    break;
                case NetMessageType.SelectMode:
                    w.WriteByte((int)m.Mode);
                    break;
                case NetMessageType.Start:
                    w.WriteUInt(m.MapHash);
                    w.WriteByte((int)m.Mode);
                    w.WriteVarUInt((uint)m.Tick);
                    break;
                case NetMessageType.Position:
                case NetMessageType.Death:
                case NetMessageType.Restart:
                    w.WriteVarUInt((uint)m.Tick);
                    w.WriteVarInt(m.X);
                    w.WriteVarInt(m.Y);
                    w.WriteVarInt(m.Value);
                    break;
                case NetMessageType.Coin:
                    w.WriteVarUInt((uint)m.Tick);
                    w.WriteVarInt(m.Value);
                    break;
                case NetMessageType.Finish:
                    w.WriteVarUInt((uint)m.Tick);
                    w.WriteVarInt(m.Value);
                    break;
                case NetMessageType.RoomState:
                    WriteRoom(w, m.RoomSnapshot);
                    break;
                case NetMessageType.Results:
                    int n = m.Results != null ? m.Results.Count : 0;
                    w.WriteByte(n);
                    for (int i = 0; i < n; i++) WriteResult(w, m.Results[i]);
                    break;
            }
            return w.ToArray();
        }

        public static NetMessage Decode(byte[] data, int offset, int count)
        {
            var r = new ByteReader(data, offset, count);
            int ver = r.ReadByte();
            if (ver != PROTOCOL_VERSION) throw new FormatException("프로토콜 버전이 다릅니다: " + ver);
            var m = new NetMessage();
            m.Type = (NetMessageType)r.ReadByte();
            m.Sender = r.ReadULong();
            switch (m.Type)
            {
                case NetMessageType.Hello:
                case NetMessageType.Leave:
                    m.Text = r.ReadString(256);
                    break;
                case NetMessageType.SelectMap:
                    m.Text = r.ReadString(8192);
                    m.MapHash = r.ReadUInt();
                    break;
                case NetMessageType.SelectMode:
                    m.Mode = ReadMode(r);
                    break;
                case NetMessageType.Start:
                    m.MapHash = r.ReadUInt();
                    m.Mode = ReadMode(r);
                    m.Tick = (int)r.ReadVarUInt();
                    break;
                case NetMessageType.Position:
                case NetMessageType.Death:
                case NetMessageType.Restart:
                    m.Tick = (int)r.ReadVarUInt();
                    m.X = r.ReadVarInt();
                    m.Y = r.ReadVarInt();
                    m.Value = r.ReadVarInt();
                    break;
                case NetMessageType.Coin:
                    m.Tick = (int)r.ReadVarUInt();
                    m.Value = r.ReadVarInt();
                    break;
                case NetMessageType.Finish:
                    m.Tick = (int)r.ReadVarUInt();
                    m.Value = r.ReadVarInt();
                    break;
                case NetMessageType.RoomState:
                    m.RoomSnapshot = ReadRoom(r);
                    break;
                case NetMessageType.Results:
                    int n = r.ReadByte();
                    m.Results = new List<PlayerResult>(n);
                    for (int i = 0; i < n; i++) m.Results.Add(ReadResult(r));
                    break;
                default:
                    throw new FormatException("알 수 없는 메시지: " + (int)m.Type);
            }
            if (r.Remaining != 0) throw new FormatException("메시지 끝에 알 수 없는 내용이 있습니다");
            return m;
        }

        public static NetMessage Decode(byte[] data) { return Decode(data, 0, data.Length); }

        private static MatchMode ReadMode(ByteReader r)
        {
            int v = r.ReadByte();
            if (v > (int)MatchMode.Survival) throw new FormatException("알 수 없는 방식: " + v);
            return (MatchMode)v;
        }

        private static void WriteRoom(ByteWriter w, Room room)
        {
            w.WriteString(room.Code);
            w.WriteULong(room.HostId);
            w.WriteByte((int)room.Mode);
            w.WriteString(room.MapRef);
            w.WriteUInt(room.MapHash);
            w.WriteByte((int)room.State);
            w.WriteByte(room.Members.Count);
            for (int i = 0; i < room.Members.Count; i++)
            {
                w.WriteULong(room.Members[i].PlayerId);
                w.WriteString(room.Members[i].Name);
                w.WriteByte(room.Members[i].Slot);
            }
        }

        private static Room ReadRoom(ByteReader r)
        {
            string code = r.ReadString(32);
            ulong host = r.ReadULong();
            var room = new Room(code, host, "");
            room.Members.Clear();
            room.Mode = ReadMode(r);
            room.MapRef = r.ReadString(8192);
            room.MapHash = r.ReadUInt();
            room.State = (RoomState)r.ReadByte();
            int n = r.ReadByte();
            if (n > Rules.ROOM_MAX_PLAYERS) throw new FormatException("방 인원이 너무 많습니다");
            for (int i = 0; i < n; i++)
            {
                var mem = new RoomMember();
                mem.PlayerId = r.ReadULong();
                mem.Name = r.ReadString(256);
                mem.Slot = r.ReadByte();
                room.Members.Add(mem);
            }
            return room;
        }

        private static void WriteResult(ByteWriter w, PlayerResult p)
        {
            w.WriteULong(p.PlayerId);
            w.WriteByte(p.Rank);
            w.WriteVarInt(p.Deaths);
            w.WriteBool(p.Finished);
            w.WriteVarInt(p.FinishTick);
            w.WriteVarInt(p.MostDeathsCellX);
            w.WriteVarInt(p.MostDeathsCellY);
            w.WriteVarInt(p.MostDeathsCount);
            w.WriteVarInt(p.CoinsCollected);
        }

        private static PlayerResult ReadResult(ByteReader r)
        {
            var p = new PlayerResult();
            p.PlayerId = r.ReadULong();
            p.Rank = r.ReadByte();
            p.Deaths = r.ReadVarInt();
            p.Finished = r.ReadBool();
            p.FinishTick = r.ReadVarInt();
            p.MostDeathsCellX = r.ReadVarInt();
            p.MostDeathsCellY = r.ReadVarInt();
            p.MostDeathsCount = r.ReadVarInt();
            p.CoinsCollected = r.ReadVarInt();
            return p;
        }
    }

    /// <summary>
    /// 방장 중계 규칙 (기획서 7.3: 방장은 플레이어 위치만 주고받으면 된다).
    /// 클라이언트끼리는 직접 연결하지 않고, 방장이 받은 메시지를 누구에게 다시 보낼지 정한다.
    /// </summary>
    public static class RelayRouter
    {
        public enum Route
        {
            /// <summary>보낸 사람 빼고 모두에게</summary>
            OthersExceptSender,
            /// <summary>방장만 처리 (중계 안 함)</summary>
            HostOnly,
            /// <summary>모두에게 (보낸 사람 포함)</summary>
            Everyone,
            /// <summary>무시 (방장이 아닌 사람이 보낸 방장 전용 메시지 등)</summary>
            Drop,
        }

        public static Route RouteFor(NetMessage m, Room room)
        {
            switch (m.Type)
            {
                case NetMessageType.Position:
                case NetMessageType.Death:
                case NetMessageType.Coin:
                case NetMessageType.Finish:
                case NetMessageType.Restart:
                    return room.Find(m.Sender) != null ? Route.OthersExceptSender : Route.Drop;
                case NetMessageType.Hello:
                case NetMessageType.Leave:
                    return Route.HostOnly;
                case NetMessageType.SelectMap:
                case NetMessageType.SelectMode:
                case NetMessageType.Start:
                case NetMessageType.RoomState:
                case NetMessageType.Results:
                    return room.IsHost(m.Sender) ? Route.Everyone : Route.Drop;
                default:
                    return Route.Drop;
            }
        }
    }

    // ── 판 결과 ──

    public sealed class PlayerResult
    {
        public ulong PlayerId;
        /// <summary>1부터. 같은 순위 가능</summary>
        public int Rank;
        public int Deaths;
        public bool Finished;
        /// <summary>골인 틱 (못 했으면 -1)</summary>
        public int FinishTick = -1;
        /// <summary>가장 많이 죽은 칸 (안 죽었으면 -1) — 결과 화면 "가장 많이 죽은 장소" (기획서 7.2)</summary>
        public int MostDeathsCellX = -1;
        public int MostDeathsCellY = -1;
        public int MostDeathsCount;
        public int CoinsCollected;
    }

    /// <summary>
    /// 한 판의 진행 집계 (방장이 원본, 결과는 Results 메시지로 공유).
    ///   - 레이스: 먼저 골인한 사람 승리. 첫 골인 후 30초가 지나거나 모두 골인하면 끝
    ///   - 협동: 동전은 팀 공유, 누구든 골인하면 모두 클리어 (각자 Simulation.ApplyTeamCoin으로 동전 반영)
    ///   - 생존: 3분 동안 덜 죽은 사람 승리 (같으면 골인한 사람, 더 빨리 골인한 사람 우선)
    /// </summary>
    public sealed class MatchTracker
    {
        private sealed class PlayerState
        {
            public ulong Id;
            public int Deaths;
            public int FinishTick = -1;
            public int Coins;
            public readonly Dictionary<int, int> DeathCells = new Dictionary<int, int>();
            public int MostCell = -1;
            public int MostCount;
        }

        private readonly MatchMode _mode;
        private readonly int _mapWidth;
        private readonly List<PlayerState> _players = new List<PlayerState>();
        private readonly bool[] _teamCoins;
        private int _firstFinishTick = -1;
        private int _currentTick;

        public MatchTracker(MatchMode mode, MapData map, IEnumerable<ulong> playerIds)
        {
            _mode = mode;
            _mapWidth = map.Width;
            _teamCoins = new bool[map.Coins.Count];
            foreach (ulong id in playerIds) _players.Add(new PlayerState { Id = id });
        }

        public MatchMode Mode { get { return _mode; } }
        public int CurrentTick { get { return _currentTick; } }
        public int PlayerCount { get { return _players.Count; } }

        private PlayerState Find(ulong id)
        {
            for (int i = 0; i < _players.Count; i++) if (_players[i].Id == id) return _players[i];
            return null;
        }

        public void RemovePlayer(ulong id)
        {
            PlayerState p = Find(id);
            if (p != null) _players.Remove(p);
        }

        public void OnDeath(ulong id, int tick, int x, int y)
        {
            PlayerState p = Find(id);
            if (p == null || p.FinishTick >= 0) return;
            p.Deaths++;
            int cell = Fixed.CellOf(y) * _mapWidth + Fixed.CellOf(x);
            int c;
            p.DeathCells.TryGetValue(cell, out c);
            c++;
            p.DeathCells[cell] = c;
            if (c > p.MostCount || (c == p.MostCount && cell < p.MostCell)) { p.MostCount = c; p.MostCell = cell; }
        }

        /// <summary>협동: 동전 먹음 → 팀 공유. 새로 먹은 거면 true (다른 사람에게 반영할 것)</summary>
        public bool OnCoin(ulong id, int index)
        {
            PlayerState p = Find(id);
            if (p == null || index < 0 || index >= _teamCoins.Length) return false;
            if (_mode == MatchMode.Coop && _teamCoins[index]) return false;
            _teamCoins[index] = true;
            p.Coins++;
            return true;
        }

        public bool TeamHasCoin(int index) { return index >= 0 && index < _teamCoins.Length && _teamCoins[index]; }

        public void OnFinish(ulong id, int clearTick)
        {
            PlayerState p = Find(id);
            if (p == null || p.FinishTick >= 0) return;
            p.FinishTick = clearTick;
            if (_firstFinishTick < 0 || clearTick < _firstFinishTick) _firstFinishTick = clearTick;
        }

        /// <summary>방장의 현재 틱 (시간 제한 판정)</summary>
        public void Advance(int tick) { _currentTick = tick; }

        public bool IsOver
        {
            get
            {
                if (_players.Count == 0) return true;
                switch (_mode)
                {
                    case MatchMode.Coop:
                        return _firstFinishTick >= 0;
                    case MatchMode.Survival:
                        return _currentTick >= Rules.SURVIVAL_TICKS || AllFinished;
                    default:
                        return AllFinished || (_firstFinishTick >= 0 && _currentTick >= _firstFinishTick + Rules.RACE_GRACE_TICKS);
                }
            }
        }

        private bool AllFinished
        {
            get
            {
                for (int i = 0; i < _players.Count; i++) if (_players[i].FinishTick < 0) return false;
                return true;
            }
        }

        /// <summary>협동에서 팀이 클리어했는지</summary>
        public bool TeamCleared { get { return _mode == MatchMode.Coop && _firstFinishTick >= 0; } }

        public List<PlayerResult> ComputeResults()
        {
            var list = new List<PlayerResult>(_players.Count);
            foreach (PlayerState p in _players)
            {
                var r = new PlayerResult
                {
                    PlayerId = p.Id,
                    Deaths = p.Deaths,
                    Finished = p.FinishTick >= 0 || TeamCleared,
                    FinishTick = p.FinishTick,
                    MostDeathsCount = p.MostCount,
                    CoinsCollected = p.Coins,
                };
                if (p.MostCell >= 0)
                {
                    r.MostDeathsCellX = p.MostCell % _mapWidth;
                    r.MostDeathsCellY = p.MostCell / _mapWidth;
                }
                list.Add(r);
            }

            Comparison<PlayerResult> cmp;
            switch (_mode)
            {
                case MatchMode.Survival: cmp = CompareSurvival; break;
                case MatchMode.Coop: cmp = CompareCoop; break;
                default: cmp = CompareRace; break;
            }
            // 안정 정렬 (입장 순서 유지)
            var indexed = new List<KeyValuePair<int, PlayerResult>>();
            for (int i = 0; i < list.Count; i++) indexed.Add(new KeyValuePair<int, PlayerResult>(i, list[i]));
            indexed.Sort((a, b) => { int c = cmp(a.Value, b.Value); return c != 0 ? c : a.Key.CompareTo(b.Key); });
            list.Clear();
            for (int i = 0; i < indexed.Count; i++) list.Add(indexed[i].Value);

            for (int i = 0; i < list.Count; i++)
            {
                if (_mode == MatchMode.Coop) list[i].Rank = 1; // 협동은 팀 전체가 같은 결과
                else if (i > 0 && cmp(list[i], list[i - 1]) == 0) list[i].Rank = list[i - 1].Rank;
                else list[i].Rank = i + 1;
            }
            return list;
        }

        private static int CompareRace(PlayerResult a, PlayerResult b)
        {
            bool fa = a.FinishTick >= 0, fb = b.FinishTick >= 0;
            if (fa != fb) return fa ? -1 : 1;
            if (fa && a.FinishTick != b.FinishTick) return a.FinishTick.CompareTo(b.FinishTick);
            return a.Deaths.CompareTo(b.Deaths);
        }

        private static int CompareSurvival(PlayerResult a, PlayerResult b)
        {
            if (a.Deaths != b.Deaths) return a.Deaths.CompareTo(b.Deaths);
            bool fa = a.FinishTick >= 0, fb = b.FinishTick >= 0;
            if (fa != fb) return fa ? -1 : 1;
            if (fa && a.FinishTick != b.FinishTick) return a.FinishTick.CompareTo(b.FinishTick);
            return 0;
        }

        /// <summary>협동은 모두 같은 결과 — 개인 기여(동전)·데스로만 표시 순서</summary>
        private static int CompareCoop(PlayerResult a, PlayerResult b)
        {
            if (a.CoinsCollected != b.CoinsCollected) return b.CoinsCollected.CompareTo(a.CoinsCollected);
            return a.Deaths.CompareTo(b.Deaths);
        }
    }
}
