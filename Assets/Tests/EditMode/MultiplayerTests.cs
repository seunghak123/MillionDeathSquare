using System.Collections.Generic;
using NUnit.Framework;
using Seunghak.Game.MillionDeathSquare;

namespace Seunghak.Game.MillionDeathSquare.Tests
{
    public class MultiplayerTests
    {
        private static MapData Map() { return Campaign.Get(1, 2).Map; }

        [Test]
        public void Room_Allows2To4_PrivateJoin_HostOnlyStart()
        {
            var room = new Room("ABCDEF", 1, "방장");
            Assert.IsFalse(room.CanStart(1), "혼자서는 시작 불가");
            Assert.AreEqual(JoinResult.Ok, room.TryJoin(2, "둘"));
            Assert.AreEqual(JoinResult.AlreadyIn, room.TryJoin(2, "둘"));
            Assert.IsFalse(room.CanStart(1), "맵을 골라야 시작");
            Assert.IsFalse(room.SelectMap(2, "campaign:1-2", 1), "방장만 맵 선택");
            Assert.IsTrue(room.SelectMap(1, "campaign:1-2", Map().ContentHash()));
            Assert.IsFalse(room.CanStart(2), "방장만 시작");
            Assert.IsTrue(room.CanStart(1));
            Assert.AreEqual(JoinResult.Ok, room.TryJoin(3, "셋"));
            Assert.AreEqual(JoinResult.Ok, room.TryJoin(4, "넷"));
            Assert.AreEqual(JoinResult.Full, room.TryJoin(5, "다섯"));
            Assert.IsTrue(room.Start(1));
            Assert.AreEqual(JoinResult.InProgress, room.TryJoin(6, "늦음"));
            // 방장이 나가면 다음 사람이 방장
            room.Leave(1);
            Assert.AreEqual(2UL, room.HostId);
            Assert.AreEqual(1, room.Find(2).Slot, "자리 번호는 입장 순서");
        }

        [Test]
        public void RoomCode_IsSixUnambiguousChars_AndNormalizes()
        {
            var rng = new DeterministicRandom(42);
            var seen = new HashSet<string>();
            for (int i = 0; i < 200; i++)
            {
                string code = Room.GenerateCode(rng);
                Assert.AreEqual(Room.CODE_LENGTH, code.Length);
                foreach (char c in code) Assert.IsFalse(c == 'O' || c == 'I' || c == '0' || c == '1', code);
                seen.Add(code);
            }
            Assert.Greater(seen.Count, 195);
            Assert.AreEqual("ABC234", Room.NormalizeCode(" abc-234 "));
            Assert.AreEqual(Room.GenerateCode(new DeterministicRandom(7)), Room.GenerateCode(new DeterministicRandom(7)), "같은 시드 = 같은 코드");
        }

        [Test]
        public void NetCodec_RoundTripsEveryMessage()
        {
            var room = new Room("QWERTY", 77, "호스트");
            room.TryJoin(88, "게스트");
            room.SelectMap(77, "MDS1-abc", 0xDEADBEEF);
            room.Mode = MatchMode.Survival;
            var msgs = new List<NetMessage>
            {
                new NetMessage { Type = NetMessageType.Hello, Sender = 88, Text = "게스트" },
                new NetMessage { Type = NetMessageType.RoomState, Sender = 77, RoomSnapshot = room },
                new NetMessage { Type = NetMessageType.SelectMap, Sender = 77, Text = "campaign:1-3", MapHash = 5 },
                new NetMessage { Type = NetMessageType.SelectMode, Sender = 77, Mode = MatchMode.Coop },
                NetMessage.StartMatch(77, 0xCAFEBABE, MatchMode.Race, 180),
                NetMessage.Position(88, 1234, 5600, -40, 1),
                NetMessage.Death(88, 1300, 7000, 8000, 12),
                new NetMessage { Type = NetMessageType.Coin, Sender = 88, Tick = 99, Value = 7 },
                NetMessage.Finish(88, 4000, 3),
                new NetMessage { Type = NetMessageType.Results, Sender = 77, Results = new List<PlayerResult> { new PlayerResult { PlayerId = 88, Rank = 1, Deaths = 3, Finished = true, FinishTick = 4000, MostDeathsCellX = 5, MostDeathsCellY = 2, MostDeathsCount = 2, CoinsCollected = 1 } } },
                new NetMessage { Type = NetMessageType.Leave, Sender = 88 },
                new NetMessage { Type = NetMessageType.Restart, Sender = 88, Tick = 5, X = 1, Y = 2 },
            };
            foreach (NetMessage m in msgs)
            {
                byte[] bytes = NetCodec.Encode(m);
                NetMessage d = NetCodec.Decode(bytes);
                Assert.AreEqual(m.Type, d.Type);
                Assert.AreEqual(m.Sender, d.Sender);
                Assert.AreEqual(bytes, NetCodec.Encode(d), m.Type + " 재인코딩 일치");
            }
            Assert.LessOrEqual(NetCodec.Encode(NetMessage.Position(88, 1234, 5600, 9000, 0)).Length, 18, "위치 메시지는 작음");
            Assert.Throws<System.FormatException>(() => NetCodec.Decode(new byte[] { 9, 9, 9 }));
        }

        [Test]
        public void RelayRouter_RelaysPlayerDataToOthers_HostControlsRoom()
        {
            var room = new Room("ROOMXX", 1, "a");
            room.TryJoin(2, "b");
            Assert.AreEqual(RelayRouter.Route.OthersExceptSender, RelayRouter.RouteFor(NetMessage.Position(2, 1, 0, 0, 0), room));
            Assert.AreEqual(RelayRouter.Route.Drop, RelayRouter.RouteFor(NetMessage.Position(9, 1, 0, 0, 0), room), "방에 없는 사람");
            Assert.AreEqual(RelayRouter.Route.Drop, RelayRouter.RouteFor(NetMessage.StartMatch(2, 0, MatchMode.Race, 0), room), "방장이 아닌 시작 신호");
            Assert.AreEqual(RelayRouter.Route.Everyone, RelayRouter.RouteFor(NetMessage.StartMatch(1, 0, MatchMode.Race, 0), room));
            Assert.AreEqual(RelayRouter.Route.HostOnly, RelayRouter.RouteFor(new NetMessage { Type = NetMessageType.Hello, Sender = 3 }, room));
        }

        [Test]
        public void Race_FirstFinisherWins_MostDeathLocationReported()
        {
            MapData map = Map();
            var t = new MatchTracker(MatchMode.Race, map, new ulong[] { 1, 2, 3 });
            t.OnDeath(1, 10, Fixed.CellCenter(4), Fixed.CellCenter(2));
            t.OnDeath(1, 20, Fixed.CellCenter(4), Fixed.CellCenter(2) + 100);
            t.OnDeath(1, 30, Fixed.CellCenter(8), Fixed.CellCenter(3));
            t.OnFinish(2, 900);
            t.Advance(900);
            Assert.IsFalse(t.IsOver, "첫 골인 후 유예 시간");
            t.OnFinish(1, 1200);
            t.Advance(900 + Rules.RACE_GRACE_TICKS);
            Assert.IsTrue(t.IsOver);
            List<PlayerResult> r = t.ComputeResults();
            Assert.AreEqual(2UL, r[0].PlayerId);
            Assert.AreEqual(1, r[0].Rank);
            Assert.AreEqual(1UL, r[1].PlayerId);
            Assert.AreEqual(3UL, r[2].PlayerId);
            Assert.IsFalse(r[2].Finished);
            Assert.AreEqual(4, r[1].MostDeathsCellX, "가장 많이 죽은 장소");
            Assert.AreEqual(2, r[1].MostDeathsCellY);
            Assert.AreEqual(2, r[1].MostDeathsCount);
            Assert.AreEqual(-1, r[0].MostDeathsCellX, "안 죽었으면 없음");
        }

        [Test]
        public void Coop_SharedCoins_AnyFinishClearsTeam()
        {
            MapData map = Map();
            var t = new MatchTracker(MatchMode.Coop, map, new ulong[] { 1, 2 });
            Assert.IsTrue(t.OnCoin(1, 0));
            Assert.IsFalse(t.OnCoin(2, 0), "이미 팀이 먹은 동전");
            Assert.IsTrue(t.OnCoin(2, 1));
            Assert.IsFalse(t.IsOver);
            t.OnFinish(2, 700);
            Assert.IsTrue(t.IsOver);
            Assert.IsTrue(t.TeamCleared);
            foreach (PlayerResult p in t.ComputeResults())
            {
                Assert.IsTrue(p.Finished, "한 명이 골인하면 모두 클리어");
                Assert.AreEqual(1, p.Rank);
            }

            // 협동: 팀 동전이 내 시뮬레이션에 반영되면 혼자서 동전을 다 안 먹어도 골인 가능
            var sim = new Simulation(map);
            for (int i = 0; i < map.Coins.Count; i++) sim.ApplyTeamCoin(i);
            Assert.IsTrue(sim.AllCoinsCollected);
        }

        [Test]
        public void Survival_ThreeMinutes_FewestDeathsWins()
        {
            var t = new MatchTracker(MatchMode.Survival, Map(), new ulong[] { 1, 2, 3 });
            for (int i = 0; i < 5; i++) t.OnDeath(1, i, 0, 0);
            t.OnDeath(2, 1, 0, 0);
            t.OnDeath(3, 1, 0, 0);
            t.OnFinish(3, 5000);
            t.Advance(Rules.SURVIVAL_TICKS - 1);
            Assert.IsFalse(t.IsOver);
            t.Advance(Rules.SURVIVAL_TICKS);
            Assert.IsTrue(t.IsOver);
            Assert.AreEqual(10800, Rules.SURVIVAL_TICKS, "3분");
            List<PlayerResult> r = t.ComputeResults();
            Assert.AreEqual(3UL, r[0].PlayerId, "데스가 같으면 골인한 사람 우선");
            Assert.AreEqual(2UL, r[1].PlayerId);
            Assert.AreEqual(1UL, r[2].PlayerId);
            Assert.AreEqual(3, r[2].Rank);
        }

        [Test]
        public void Session_FullFlowOverLoopback_JoinStartRelayResults()
        {
            var hub = new LoopbackHub();
            RoomSession host = RoomSession.CreateHost(hub.TransportFor(1), 1, "방장", "ROOM42");
            hub.Register(host);
            RoomSession b = RoomSession.CreateClient(hub.TransportFor(2), 2, "비", 1);
            hub.Register(b);
            RoomSession c = RoomSession.CreateClient(hub.TransportFor(3), 3, "씨", 1);
            hub.Register(c);
            hub.Pump();
            Assert.AreEqual(3, host.Room.Count);
            Assert.AreEqual(3, b.Room.Count, "참가자도 방 상태를 받음");
            Assert.AreEqual("ROOM42", c.Room.Code);

            MapData map = Map();
            Assert.IsTrue(host.SelectMap("campaign:1-2", map.ContentHash()));
            Assert.IsTrue(host.SelectMode(MatchMode.Race));
            hub.Pump();
            Assert.AreEqual(MatchMode.Race, b.Room.Mode);

            int starts = 0;
            int countdown = -1;
            b.MatchStarting += m => { starts++; countdown = m.Tick; Assert.AreEqual(map.ContentHash(), m.MapHash); };
            c.MatchStarting += m => starts++;
            var seenByC = new List<NetMessage>();
            c.RemotePosition += m => seenByC.Add(m);
            List<PlayerResult> resultsAtB = null;
            b.ResultsReady += r => resultsAtB = r;

            Assert.IsTrue(host.StartMatch(map));
            hub.Pump();
            Assert.AreEqual(2, starts);
            Assert.AreEqual(Rules.START_COUNTDOWN_TICKS, countdown, "모두 같은 카운트다운 뒤 틱 0");

            b.SendPosition(10, 1000, 2000, 0);
            hub.Pump();
            Assert.AreEqual(1, seenByC.Count, "방장이 b의 위치를 c에게 중계");
            Assert.AreEqual(2UL, seenByC[0].Sender);

            b.ReportDeath(20, Fixed.CellCenter(5), Fixed.CellCenter(3), 1);
            b.ReportFinish(600, 1);
            c.ReportFinish(650, 0);
            host.ReportFinish(700, 0);
            hub.Pump();
            Assert.IsTrue(host.Match.IsOver, "모두 골인");
            Assert.IsNotNull(resultsAtB, "결과가 모두에게 공유됨");
            Assert.AreEqual(2UL, resultsAtB[0].PlayerId);
            Assert.AreEqual(5, resultsAtB[0].MostDeathsCellX);
            Assert.AreEqual(RoomState.Results, b.Room.State);

            // 가짜 발신자 메시지는 무시
            Assert.IsFalse(host.Receive(3, NetCodec.Encode(NetMessage.Finish(2, 1, 0))));
        }
    }
}
