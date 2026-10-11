using NUnit.Framework;
using Seunghak.Game.MillionDeathSquare;

namespace Seunghak.Game.MillionDeathSquare.Tests
{
    public class ObstacleTests
    {
        [Test]
        public void PingPong_ReachesEndpoints_AndReturns()
        {
            // 4칸 거리, 속도 2단계(초당 4칸) → 1초에 끝, 2초에 원위치
            ObstacleDef o = ObstacleDef.PingPong(2, 0, TestUtil.P(1, 1), TestUtil.P(5, 1));
            var m = new ObstacleMotion(o);
            int x, y;
            m.Evaluate(0, out x, out y);
            Assert.AreEqual(TestUtil.P(1, 1).UnitX, x);
            m.Evaluate(60, out x, out y);
            Assert.AreEqual(TestUtil.P(5, 1).UnitX, x);
            m.Evaluate(120, out x, out y);
            Assert.AreEqual(TestUtil.P(1, 1).UnitX, x);
            m.Evaluate(90, out x, out y);
            Assert.AreEqual(TestUtil.P(3, 1).UnitX, x);
            Assert.AreEqual(TestUtil.P(3, 1).UnitY, y);
            Assert.AreEqual(120, m.PeriodTicks);
        }

        [Test]
        public void SpeedTiers_Are_2_4_6_CellsPerSecond()
        {
            Assert.AreEqual(2 * Fixed.CELL, Rules.ObstacleSpeedPerTick(1) * Fixed.TICKS_PER_SECOND);
            Assert.AreEqual(4 * Fixed.CELL, Rules.ObstacleSpeedPerTick(2) * Fixed.TICKS_PER_SECOND);
            Assert.AreEqual(6 * Fixed.CELL, Rules.ObstacleSpeedPerTick(3) * Fixed.TICKS_PER_SECOND);
        }

        [Test]
        public void Loop_ContinuesFromLastPointToFirst()
        {
            // 정사각형 2×2칸 둘레 = 8칸, 속도 2단계 → 2초에 한 바퀴
            ObstacleDef o = ObstacleDef.Loop(2, 0, TestUtil.P(1, 1), TestUtil.P(3, 1), TestUtil.P(3, 3), TestUtil.P(1, 3));
            var m = new ObstacleMotion(o);
            int x, y;
            m.Evaluate(90, out x, out y); // 6칸 → 마지막 점 (1,3)
            Assert.AreEqual(TestUtil.P(1, 3).UnitX, x);
            Assert.AreEqual(TestUtil.P(1, 3).UnitY, y);
            m.Evaluate(105, out x, out y); // 7칸 → (1,2) 닫는 구간 위
            Assert.AreEqual(TestUtil.P(1, 2).UnitY, y);
            m.Evaluate(120, out x, out y);
            Assert.AreEqual(TestUtil.P(1, 1).UnitX, x);
            Assert.AreEqual(TestUtil.P(1, 1).UnitY, y);
        }

        [Test]
        public void Delay_HoldsAtFirstPoint_ThenMoves()
        {
            ObstacleDef o = ObstacleDef.PingPong(2, 10, TestUtil.P(1, 1), TestUtil.P(5, 1)); // 1초 지연
            var m = new ObstacleMotion(o);
            int x, y;
            m.Evaluate(59, out x, out y);
            Assert.AreEqual(TestUtil.P(1, 1).UnitX, x);
            m.Evaluate(120, out x, out y);
            Assert.AreEqual(TestUtil.P(5, 1).UnitX, x);
        }

        [Test]
        public void Circle_StartsAtAngle_AndKeepsRadius()
        {
            // 중심 (5,5), 반지름 2칸, 시작 각도 0 (오른쪽), 시계방향
            ObstacleDef o = ObstacleDef.Circle(TestUtil.P(5, 5), 4, 0, true, 2, 0);
            var m = new ObstacleMotion(o);
            int cx = TestUtil.P(5, 5).UnitX, cy = TestUtil.P(5, 5).UnitY;
            int x, y;
            m.Evaluate(0, out x, out y);
            Assert.AreEqual(cx + 2 * Fixed.CELL, x);
            Assert.AreEqual(cy, y);
            int quarter = m.PeriodTicks / 4;
            m.Evaluate(quarter, out x, out y);
            Assert.LessOrEqual(System.Math.Abs(x - cx), 30, "90° 뒤 x ≈ 중심");
            Assert.LessOrEqual(System.Math.Abs(y - (cy + 2 * Fixed.CELL)), 30, "시계방향(화면 y 아래) → 아래쪽");
            for (int t = 0; t < m.PeriodTicks; t += 7)
            {
                m.Evaluate(t, out x, out y);
                long dx = x - cx, dy = y - cy;
                long r = Fixed.ISqrt(dx * dx + dy * dy);
                Assert.LessOrEqual(System.Math.Abs(r - 2 * Fixed.CELL), 4, "반지름 유지 (t=" + t + ")");
            }
        }

        [Test]
        public void Position_IsPureFunctionOfTick_AcrossInstancesAndOrder()
        {
            MapData map = Campaign.Get(2, 9).Map; // 원 + 여러 장애물
            var a = new Simulation(map);
            var motions = new ObstacleMotion[map.Obstacles.Count];
            for (int i = 0; i < motions.Length; i++) motions[i] = new ObstacleMotion(map.Obstacles[i]);
            for (int t = 1; t <= 600; t++)
            {
                a.Step(InputBits.None);
                for (int i = 0; i < motions.Length; i++)
                {
                    int x, y;
                    // 역순·임의 틱 조회가 상태를 바꾸지 않음을 같이 확인
                    motions[i].Evaluate(1000 - t, out x, out y);
                    motions[i].Evaluate(t, out x, out y);
                    Assert.AreEqual(x, a.ObstacleX(i));
                    Assert.AreEqual(y, a.ObstacleY(i));
                }
            }
        }

        [Test]
        public void SinTable_IsSymmetric_AndExactAtQuadrants()
        {
            Assert.AreEqual(0, Fixed.Sin(0));
            Assert.AreEqual(Fixed.ONE, Fixed.Sin(16384));
            Assert.AreEqual(0, Fixed.Sin(32768));
            Assert.AreEqual(-Fixed.ONE, Fixed.Sin(49152));
            Assert.AreEqual(Fixed.ONE, Fixed.Cos(0));
            for (int a = 0; a < 16384; a += 97) Assert.AreEqual(Fixed.Sin(a), Fixed.Sin(32768 - a));
            Assert.AreEqual(46341, Fixed.Sin(8192), "sin 45°");
        }

        [Test]
        public void ISqrt_IsExactFloor()
        {
            Assert.AreEqual(0, Fixed.ISqrt(0));
            Assert.AreEqual(1, Fixed.ISqrt(3));
            Assert.AreEqual(2, Fixed.ISqrt(4));
            Assert.AreEqual(1697, Fixed.ISqrt(2880000)); // √(1200²+1200²) 내림
            Assert.AreEqual(3037000499L, Fixed.ISqrt(long.MaxValue));
        }
    }
}
