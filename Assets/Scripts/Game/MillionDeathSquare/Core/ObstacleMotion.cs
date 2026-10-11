namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 장애물 경로를 미리 계산해 둔 형태. 위치는 Evaluate(틱)의 순수 함수다 (기획서 7.3, 12.4).
    /// 같은 맵 + 같은 틱이면 어느 기기에서든 같은 정수 좌표가 나온다 → 멀티 동기화 / 리플레이 검증의 기반.
    /// </summary>
    public sealed class ObstacleMotion
    {
        private readonly PathMode _mode;
        private readonly int _speed;          // 틱당 단위
        private readonly int _delayTicks;
        private readonly int[] _xs;           // 경로 점 (단위). Loop는 닫는 점을 끝에 덧붙임
        private readonly int[] _ys;
        private readonly long[] _segStart;    // 각 구간 시작까지 누적 거리
        private readonly long[] _segLen;
        private readonly long _length;        // 전체 길이 (Loop는 닫힌 둘레)
        // 원
        private readonly int _cx, _cy, _radius, _startAngle;
        private readonly bool _clockwise;
        private readonly long _circumference;

        public ObstacleMotion(ObstacleDef def)
        {
            _mode = def.Mode;
            _speed = Rules.ObstacleSpeedPerTick(def.SpeedTier);
            _delayTicks = def.DelayTenths * Rules.DELAY_UNIT_TICKS;

            PathPoint[] pts = def.Points ?? new PathPoint[0];
            if (_mode == PathMode.Circle)
            {
                PathPoint c = pts.Length > 0 ? pts[0] : new PathPoint(1, 1);
                _cx = c.UnitX;
                _cy = c.UnitY;
                _radius = def.RadiusHalf * Fixed.HALF_CELL;
                _startAngle = (def.StartAngle & 15) * (Fixed.FULL_TURN / 16);
                _clockwise = def.Clockwise;
                _circumference = (_radius * Fixed.TWO_PI_FIXED) >> 16;
                _xs = new int[0];
                _ys = new int[0];
                _segStart = new long[0];
                _segLen = new long[0];
                return;
            }

            int n = pts.Length;
            bool closed = _mode == PathMode.Loop && n > 1;
            int count = closed ? n + 1 : n;
            _xs = new int[count];
            _ys = new int[count];
            for (int i = 0; i < n; i++) { _xs[i] = pts[i].UnitX; _ys[i] = pts[i].UnitY; }
            if (closed) { _xs[n] = _xs[0]; _ys[n] = _ys[0]; }

            int segs = count > 1 ? count - 1 : 0;
            _segStart = new long[segs];
            _segLen = new long[segs];
            long acc = 0;
            for (int i = 0; i < segs; i++)
            {
                long dx = _xs[i + 1] - _xs[i];
                long dy = _ys[i + 1] - _ys[i];
                long len = Fixed.ISqrt(dx * dx + dy * dy);
                _segStart[i] = acc;
                _segLen[i] = len;
                acc += len;
            }
            _length = acc;
        }

        public PathMode Mode { get { return _mode; } }

        /// <summary>틱 t의 장애물 중심 위치 (단위).</summary>
        public void Evaluate(int tick, out int x, out int y)
        {
            long moving = tick - _delayTicks;
            if (moving < 0) moving = 0;
            long s = moving * _speed;

            if (_mode == PathMode.Circle)
            {
                int angle = _startAngle;
                if (_circumference > 0)
                {
                    // 이동 거리 → 각도 (원 한 바퀴 = 65536)
                    long turn = Fixed.PosMod(s, _circumference);
                    int delta = (int)((turn * Fixed.FULL_TURN) / _circumference);
                    // 화면 좌표는 y가 아래로 증가 → 시계방향 = 각도 증가
                    angle = _clockwise ? angle + delta : angle - delta;
                }
                x = _cx + (int)(((long)_radius * Fixed.Cos(angle)) / Fixed.ONE);
                y = _cy + (int)(((long)_radius * Fixed.Sin(angle)) / Fixed.ONE);
                return;
            }

            if (_xs.Length == 0) { x = 0; y = 0; return; }
            if (_length <= 0) { x = _xs[0]; y = _ys[0]; return; }

            long d;
            if (_mode == PathMode.Loop)
            {
                d = Fixed.PosMod(s, _length);
            }
            else
            {
                long m = Fixed.PosMod(s, _length * 2);
                d = m <= _length ? m : _length * 2 - m;
            }

            // 구간 찾기 (점 ≤ 9개라 선형 탐색)
            int seg = _segLen.Length - 1;
            for (int i = 0; i < _segLen.Length; i++)
            {
                if (d < _segStart[i] + _segLen[i]) { seg = i; break; }
            }
            long into = d - _segStart[seg];
            long len = _segLen[seg];
            if (len <= 0) { x = _xs[seg]; y = _ys[seg]; return; }
            if (into > len) into = len;
            x = _xs[seg] + (int)(((long)(_xs[seg + 1] - _xs[seg]) * into) / len);
            y = _ys[seg] + (int)(((long)(_ys[seg + 1] - _ys[seg]) * into) / len);
        }

        /// <summary>한 주기 길이(틱). 제자리면 1. 표시/분석용.</summary>
        public int PeriodTicks
        {
            get
            {
                if (_speed <= 0) return 1;
                long dist = _mode == PathMode.Circle ? _circumference : (_mode == PathMode.Loop ? _length : _length * 2);
                if (dist <= 0) return 1;
                return (int)((dist + _speed - 1) / _speed);
            }
        }

        /// <summary>정의에서 바로 계산 (캐시 없이). 테스트·도구용.</summary>
        public static void Evaluate(ObstacleDef def, int tick, out int x, out int y)
        {
            new ObstacleMotion(def).Evaluate(tick, out x, out y);
        }
    }
}
