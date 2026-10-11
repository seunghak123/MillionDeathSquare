using Seunghak.Game.MillionDeathSquare;

namespace Seunghak.Game.MillionDeathSquare.Tests
{
    /// <summary>테스트 공용 도우미 (NUnit / tools/run-core-tests.sh 양쪽에서 컴파일됨 — UnityEngine 참조 금지)</summary>
    internal static class TestUtil
    {
        public const int C = Fixed.CELL;

        public static MapData Map(params string[] rows) { return MapAscii.Parse("test", 0, rows); }

        /// <summary>같은 입력으로 n틱 진행</summary>
        public static void Run(Simulation sim, InputBits input, int ticks)
        {
            for (int i = 0; i < ticks; i++) sim.Step(input);
        }

        public static bool HadEvent(Simulation sim, SimEventType type)
        {
            for (int i = 0; i < sim.EventCount; i++) if (sim.GetEvent(i).Type == type) return true;
            return false;
        }

        /// <summary>n틱 진행하는 동안 이벤트가 한 번이라도 있었는지</summary>
        public static bool RunUntilEvent(Simulation sim, InputBits input, int maxTicks, SimEventType type)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                sim.Step(input);
                if (HadEvent(sim, type)) return true;
            }
            return false;
        }

        public static PathPoint P(int cx, int cy) { return PathPoint.CellCenter(cx, cy); }
    }
}
