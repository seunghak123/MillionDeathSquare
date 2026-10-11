namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 결정적 정수 연산 도우미. 코어는 float/double을 쓰지 않는다 (PC·모바일·리플레이 검증에서 결과가 같아야 함).
    ///
    /// 단위: 1칸 = CELL(1200) 단위. 60틱/초에서 초당 2/4/6칸 = 틱당 40/80/120 단위로 정확히 나누어떨어진다.
    /// 각도: 원 한 바퀴 = 65536 (BAM16). 사인값 스케일 = 65536.
    /// </summary>
    public static class Fixed
    {
        /// <summary>1칸의 내부 단위 수</summary>
        public const int CELL = 1200;
        public const int HALF_CELL = CELL / 2;
        /// <summary>시뮬레이션 틱/초</summary>
        public const int TICKS_PER_SECOND = 60;
        /// <summary>원 한 바퀴 각도 단위</summary>
        public const int FULL_TURN = 65536;
        /// <summary>sin/cos 결과 스케일 (1.0 = 65536)</summary>
        public const int ONE = 65536;
        /// <summary>2π × 65536 (반올림)</summary>
        public const long TWO_PI_FIXED = 411775;

        public static int CellOf(int units)
        {
            // 음수 좌표는 바깥(-1)로 내림
            return units >= 0 ? units / CELL : -1 - ((-units - 1) / CELL);
        }

        public static int CellCenter(int cell) { return cell * CELL + HALF_CELL; }

        public static int TicksFromSeconds(int seconds) { return seconds * TICKS_PER_SECOND; }

        /// <summary>정수 제곱근 (내림). 음수는 0.</summary>
        public static long ISqrt(long v)
        {
            if (v <= 0) return 0;
            long r = 0;
            long bit = 1L << 62;
            while (bit > v) bit >>= 2;
            while (bit != 0)
            {
                if (v >= r + bit)
                {
                    v -= r + bit;
                    r = (r >> 1) + bit;
                }
                else
                {
                    r >>= 1;
                }
                bit >>= 2;
            }
            return r;
        }

        public static int Clamp(int v, int min, int max) { return v < min ? min : (v > max ? max : v); }
        public static int Abs(int v) { return v < 0 ? -v : v; }

        /// <summary>양수 나머지 (a mod m, m &gt; 0)</summary>
        public static long PosMod(long a, long m)
        {
            long r = a % m;
            return r < 0 ? r + m : r;
        }

        // 사분면 사인 테이블: sin(i/256 × 90°) × 65536, i = 0..256
        private static readonly int[] QuarterSin =
        {
            0, 402, 804, 1206, 1608, 2010, 2412, 2814, 3216, 3617, 4019, 4420, 4821, 5222, 5623, 6023,
            6424, 6824, 7224, 7623, 8022, 8421, 8820, 9218, 9616, 10014, 10411, 10808, 11204, 11600, 11996, 12391,
            12785, 13180, 13573, 13966, 14359, 14751, 15143, 15534, 15924, 16314, 16703, 17091, 17479, 17867, 18253, 18639,
            19024, 19409, 19792, 20175, 20557, 20939, 21320, 21699, 22078, 22457, 22834, 23210, 23586, 23961, 24335, 24708,
            25080, 25451, 25821, 26190, 26558, 26925, 27291, 27656, 28020, 28383, 28745, 29106, 29466, 29824, 30182, 30538,
            30893, 31248, 31600, 31952, 32303, 32652, 33000, 33347, 33692, 34037, 34380, 34721, 35062, 35401, 35738, 36075,
            36410, 36744, 37076, 37407, 37736, 38064, 38391, 38716, 39040, 39362, 39683, 40002, 40320, 40636, 40951, 41264,
            41576, 41886, 42194, 42501, 42806, 43110, 43412, 43713, 44011, 44308, 44604, 44898, 45190, 45480, 45769, 46056,
            46341, 46624, 46906, 47186, 47464, 47741, 48015, 48288, 48559, 48828, 49095, 49361, 49624, 49886, 50146, 50404,
            50660, 50914, 51166, 51417, 51665, 51911, 52156, 52398, 52639, 52878, 53114, 53349, 53581, 53812, 54040, 54267,
            54491, 54714, 54934, 55152, 55368, 55582, 55794, 56004, 56212, 56418, 56621, 56823, 57022, 57219, 57414, 57607,
            57798, 57986, 58172, 58356, 58538, 58718, 58896, 59071, 59244, 59415, 59583, 59750, 59914, 60075, 60235, 60392,
            60547, 60700, 60851, 60999, 61145, 61288, 61429, 61568, 61705, 61839, 61971, 62101, 62228, 62353, 62476, 62596,
            62714, 62830, 62943, 63054, 63162, 63268, 63372, 63473, 63572, 63668, 63763, 63854, 63944, 64031, 64115, 64197,
            64277, 64354, 64429, 64501, 64571, 64639, 64704, 64766, 64827, 64884, 64940, 64993, 65043, 65091, 65137, 65180,
            65220, 65259, 65294, 65328, 65358, 65387, 65413, 65436, 65457, 65476, 65492, 65505, 65516, 65525, 65531, 65535,
            65536,
        };

        /// <summary>sin(angle). angle은 BAM16 (65536 = 360°). 결과 스케일 65536. 선형 보간.</summary>
        public static int Sin(int angle)
        {
            int a = angle & 0xFFFF;
            int quadrant = a >> 14;          // 0..3
            int within = a & 0x3FFF;         // 0..16383 (90° = 16384)
            int idx;
            int frac;
            int result;
            if ((quadrant & 1) == 0)
            {
                idx = within >> 6;           // 0..255
                frac = within & 63;
                result = QuarterSin[idx] + (((QuarterSin[idx + 1] - QuarterSin[idx]) * frac) >> 6);
            }
            else
            {
                int rev = 16384 - within;    // 1..16384
                idx = rev >> 6;
                frac = rev & 63;
                if (idx >= 256) result = QuarterSin[256];
                else result = QuarterSin[idx] + (((QuarterSin[idx + 1] - QuarterSin[idx]) * frac) >> 6);
            }
            return quadrant >= 2 ? -result : result;
        }

        public static int Cos(int angle) { return Sin(angle + 16384); }
    }
}
