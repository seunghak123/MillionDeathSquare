using NUnit.Framework;
using Seunghak.Game.MillionDeathSquare;

namespace Seunghak.Game.MillionDeathSquare.Tests
{
    public class SimulationTests
    {
        private static readonly string[] Open =
        {
            "##########",
            "#........#",
            "#.@......#",
            "#........#",
            "#.......E#",
            "##########",
        };

        [Test]
        public void Player_MovesFourCellsPerSecond_AndStopsOnRelease()
        {
            var sim = new Simulation(TestUtil.Map(Open));
            int x0 = sim.PlayerX;
            TestUtil.Run(sim, InputBits.Right, 15);
            Assert.AreEqual(x0 + TestUtil.C, sim.PlayerX, "15틱(0.25초) = 1칸");
            TestUtil.Run(sim, InputBits.None, 10);
            Assert.AreEqual(x0 + TestUtil.C, sim.PlayerX, "키를 떼면 즉시 정지");
            Assert.AreEqual(0, sim.VelocityX);
            Assert.AreEqual(Rules.PLAYER_SPEED * Fixed.TICKS_PER_SECOND, 4 * TestUtil.C);
        }

        [Test]
        public void Walls_BlockMovement_AndEmitBumpOnce()
        {
            var sim = new Simulation(TestUtil.Map(Open));
            TestUtil.Run(sim, InputBits.Left, 30);
            // 왼쪽 벽(x=0칸)에 붙음: 네모 왼쪽 끝 = 1칸 경계
            Assert.AreEqual(TestUtil.C + Rules.PLAYER_HALF, sim.PlayerX);
            var sim2 = new Simulation(TestUtil.Map(Open));
            int bumps = 0;
            for (int i = 0; i < 40; i++)
            {
                sim2.Step(InputBits.Up);
                for (int e = 0; e < sim2.EventCount; e++) if (sim2.GetEvent(e).Type == SimEventType.Bump) bumps++;
            }
            Assert.AreEqual(1, bumps, "벽에 계속 밀어도 부딪힘 이벤트는 한 번");
            Assert.AreEqual(TestUtil.C + Rules.PLAYER_HALF, sim2.PlayerY);
        }

        [Test]
        public void Empty_Tiles_Block_LikeWalls()
        {
            var sim = new Simulation(TestUtil.Map(
                "#####",
                "#@. #",
                "#####"));
            TestUtil.Run(sim, InputBits.Right, 60);
            Assert.AreEqual(3 * TestUtil.C - Rules.PLAYER_HALF, sim.PlayerX);
        }

        [Test]
        public void Ice_KeepsSlidingAfterRelease_UntilWall()
        {
            var sim = new Simulation(TestUtil.Map(
                "##########",
                "#@~~~~~~~#",
                "##########"));
            TestUtil.Run(sim, InputBits.Right, 15); // 얼음 위로 올라섬
            Assert.IsTrue(sim.OnIce);
            int x = sim.PlayerX;
            TestUtil.Run(sim, InputBits.None, 10);
            Assert.Greater(sim.PlayerX, x, "얼음 위에서는 키를 떼도 미끄러짐");
            // 미끄러지는 중에는 방향을 바꿀 수 없음
            int before = sim.PlayerX;
            TestUtil.Run(sim, InputBits.Left, 5);
            Assert.Greater(sim.PlayerX, before);
            TestUtil.Run(sim, InputBits.None, 120);
            Assert.AreEqual(9 * TestUtil.C - Rules.PLAYER_HALF, sim.PlayerX, "오른쪽 벽까지 미끄러짐");
        }

        [Test]
        public void Ice_StopsAtWall_ThenAcceptsNewDirection()
        {
            var sim = new Simulation(TestUtil.Map(
                "#####",
                "#~~~#",
                "#~@~#",
                "#~~~#",
                "#####"));
            // 시작 칸은 바닥, 주변이 얼음
            TestUtil.Run(sim, InputBits.Right, 8); // 중심이 얼음 칸으로 넘어감
            TestUtil.Run(sim, InputBits.None, 60);
            Assert.AreEqual(4 * TestUtil.C - Rules.PLAYER_HALF, sim.PlayerX);
            Assert.AreEqual(0, sim.VelocityX, "벽에 막히면 그 축 속도는 0");
            TestUtil.Run(sim, InputBits.Up, 2);
            TestUtil.Run(sim, InputBits.None, 60);
            Assert.AreEqual(TestUtil.C + Rules.PLAYER_HALF, sim.PlayerY, "멈춘 뒤에는 새 방향으로 출발");
        }

        [Test]
        public void Conveyor_PushesPlayer_AddedToInput()
        {
            var sim = new Simulation(TestUtil.Map(
                "##########",
                "#@>>>>>>.#",
                "##########"));
            TestUtil.Run(sim, InputBits.Right, 15);
            int x = sim.PlayerX;
            TestUtil.Run(sim, InputBits.None, 15);
            Assert.AreEqual(x + Rules.CONVEYOR_SPEED * 15, sim.PlayerX, "가만히 있어도 초당 2칸 밀림");
            int x2 = sim.PlayerX;
            TestUtil.Run(sim, InputBits.Left, 10);
            Assert.AreEqual(x2 - (Rules.PLAYER_SPEED - Rules.CONVEYOR_SPEED) * 10, sim.PlayerX, "거슬러 가면 느려짐");
        }

        [Test]
        public void VanishingFloor_KillsWhenGone_ByPurePhaseFunction()
        {
            Assert.AreEqual(Simulation.VanishState.Solid, Simulation.VanishPhaseState(0, 0));
            Assert.AreEqual(Simulation.VanishState.Warning, Simulation.VanishPhaseState(Rules.VANISH_SOLID_TICKS - 1, 0));
            Assert.AreEqual(Simulation.VanishState.Gone, Simulation.VanishPhaseState(Rules.VANISH_SOLID_TICKS, 0));
            Assert.AreEqual(Simulation.VanishState.Solid, Simulation.VanishPhaseState(Rules.VANISH_PERIOD, 0));
            // 위상 1은 45틱 먼저 사라짐
            Assert.AreEqual(Simulation.VanishState.Gone, Simulation.VanishPhaseState(Rules.VANISH_SOLID_TICKS - Rules.VANISH_PHASE_STEP, 1));

            var sim = new Simulation(TestUtil.Map(
                "######",
                "#@0.E#",
                "######"));
            TestUtil.Run(sim, InputBits.Right, 15); // 사라지는 바닥 위
            Assert.AreEqual(SimState.Playing, sim.State);
            bool died = TestUtil.RunUntilEvent(sim, InputBits.None, Rules.VANISH_PERIOD, SimEventType.Death);
            Assert.IsTrue(died);
            Assert.AreEqual(Rules.VANISH_SOLID_TICKS, sim.Tick, "사라지는 순간 죽음");
        }

        [Test]
        public void Obstacle_Touch_Kills_AndRespawnsAfterPoint3Seconds()
        {
            MapData map = TestUtil.Map(
                "########",
                "#@.....#",
                "#.....E#",
                "########");
            map.Obstacles.Add(ObstacleDef.PingPong(1, 0, TestUtil.P(4, 1)));
            var sim = new Simulation(map);
            bool died = TestUtil.RunUntilEvent(sim, InputBits.Right, 60, SimEventType.Death);
            Assert.IsTrue(died);
            Assert.AreEqual(1, sim.Deaths);
            Assert.AreEqual(SimState.Dead, sim.State);
            int deathTick = sim.Tick;
            bool respawned = TestUtil.RunUntilEvent(sim, InputBits.Right, 60, SimEventType.Respawn);
            Assert.IsTrue(respawned);
            Assert.AreEqual(deathTick + Rules.RESPAWN_TICKS, sim.Tick, "0.3초 = 18틱 뒤 재시작");
            Assert.AreEqual(map.Start.CenterX, sim.PlayerX);
            Assert.AreEqual(map.Start.CenterY, sim.PlayerY);
            Assert.AreEqual(18, Rules.RESPAWN_TICKS);
        }

        [Test]
        public void Coins_ResetOnDeath_UnlessSavedByCheckpoint()
        {
            MapData map = TestUtil.Map(
                "#########",
                "#@$P$...#",
                "#......E#",
                "#########");
            map.Obstacles.Add(ObstacleDef.PingPong(1, 0, TestUtil.P(6, 1)));
            var sim = new Simulation(map);
            TestUtil.Run(sim, InputBits.Right, 30); // 동전 0 먹고 체크포인트(3,1)까지
            Assert.IsTrue(sim.IsCoinCollected(0));
            Assert.AreEqual(0, sim.ActiveCheckpoint);
            bool died = TestUtil.RunUntilEvent(sim, InputBits.Right, 60, SimEventType.Death);
            Assert.IsTrue(died);
            Assert.IsTrue(sim.IsCoinCollected(1), "죽기 전 먹은 동전 1");
            TestUtil.Run(sim, InputBits.None, Rules.RESPAWN_TICKS);
            Assert.AreEqual(SimState.Playing, sim.State);
            Assert.IsTrue(sim.IsCoinCollected(0), "체크포인트 전에 먹은 동전은 유지");
            Assert.IsFalse(sim.IsCoinCollected(1), "체크포인트 뒤에 먹은 동전은 되돌림");
            Assert.AreEqual(map.Checkpoints[0].CenterX, sim.PlayerX, "체크포인트에서 재시작");
        }

        [Test]
        public void Goal_RequiresAllCoins()
        {
            var sim = new Simulation(TestUtil.Map(
                "#######",
                "#@..E.#",
                "#....$#",
                "#######"));
            TestUtil.Run(sim, InputBits.Right, 45);
            Assert.IsFalse(sim.IsCleared, "동전이 남아 있으면 골인 안 됨");
            TestUtil.Run(sim, InputBits.Right, 15);
            TestUtil.Run(sim, InputBits.Down, 15);
            Assert.IsTrue(sim.AllCoinsCollected);
            TestUtil.Run(sim, InputBits.Up, 15);
            TestUtil.Run(sim, InputBits.Left, 15);
            Assert.IsTrue(sim.IsCleared);
            Assert.IsTrue(sim.ZeroDeath);
            Assert.AreEqual(98, sim.ClearTick, "중심이 골인 칸에 들어선 틱");
            int t = sim.Tick;
            sim.Step(InputBits.Left);
            Assert.AreEqual(t, sim.Tick, "클리어 후에는 진행하지 않음");
        }

        [Test]
        public void Keys_OpenDoors_AndCloseAgainAfterDeathWithoutCheckpoint()
        {
            MapData map = TestUtil.Map(
                "#########",
                "#@r#....#",
                "#..R..E.#",
                "#########");
            map.Obstacles.Add(ObstacleDef.PingPong(1, 0, TestUtil.P(5, 1)));
            var sim = new Simulation(map);
            Assert.IsTrue(sim.IsSolid(3, 2), "닫힌 문은 막힘");
            TestUtil.Run(sim, InputBits.Right, 15);
            Assert.IsTrue(sim.IsKeyCollected(0));
            Assert.IsTrue(sim.IsDoorOpen(KeyColor.Red));
            Assert.IsFalse(sim.IsSolid(3, 2));
            TestUtil.Run(sim, InputBits.Down, 15);
            bool died = TestUtil.RunUntilEvent(sim, InputBits.Right, 120, SimEventType.Death);
            Assert.IsFalse(died, "아래 줄은 장애물과 겹치지 않음");
            Assert.IsTrue(sim.IsCleared, "문을 지나 골인");

            // 열쇠를 먹고 죽으면 문이 다시 닫힘
            MapData map2 = TestUtil.Map(
                "#######",
                "#@r...#",
                "#..R.E#",
                "#######");
            map2.Obstacles.Add(ObstacleDef.PingPong(1, 0, TestUtil.P(4, 1)));
            var s2 = new Simulation(map2);
            TestUtil.Run(s2, InputBits.Right, 15);
            Assert.IsTrue(s2.IsDoorOpen(KeyColor.Red));
            Assert.IsTrue(TestUtil.RunUntilEvent(s2, InputBits.Right, 60, SimEventType.Death));
            TestUtil.Run(s2, InputBits.None, Rules.RESPAWN_TICKS);
            Assert.IsFalse(s2.IsDoorOpen(KeyColor.Red));
            Assert.IsFalse(s2.IsKeyCollected(0));
        }

        [Test]
        public void RestartInput_ReturnsToStart_KeepsDeathsAndTime()
        {
            MapData map = TestUtil.Map(
                "#######",
                "#@$...#",
                "#....E#",
                "#######");
            var sim = new Simulation(map);
            TestUtil.Run(sim, InputBits.Right, 30);
            Assert.IsTrue(sim.IsCoinCollected(0));
            sim.Step(InputBits.Restart);
            Assert.IsTrue(TestUtil.HadEvent(sim, SimEventType.Restart));
            Assert.AreEqual(map.Start.CenterX, sim.PlayerX);
            Assert.IsFalse(sim.IsCoinCollected(0));
            Assert.AreEqual(31, sim.Tick);
        }

        [Test]
        public void CopyStateFrom_ProducesIdenticalFuture()
        {
            MapData map = CampaignSolutionsTestsData.Stage(1, 2);
            var a = new Simulation(map);
            TestUtil.Run(a, InputBits.Up, 20);
            var b = new Simulation(map);
            b.CopyStateFrom(a);
            for (int i = 0; i < 200; i++)
            {
                InputBits input = (i / 15) % 2 == 0 ? InputBits.Right : InputBits.Down;
                a.Step(input);
                b.Step(input);
                Assert.AreEqual(a.PlayerX, b.PlayerX);
                Assert.AreEqual(a.PlayerY, b.PlayerY);
                Assert.AreEqual(a.Deaths, b.Deaths);
            }
        }
    }

    internal static class CampaignSolutionsTestsData
    {
        public static MapData Stage(int w, int s) { return Campaign.Get(w, s).Map; }
    }
}
