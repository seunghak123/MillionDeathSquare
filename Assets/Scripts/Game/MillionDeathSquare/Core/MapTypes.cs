using System;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>칸 지형. 값은 저장 형식에 쓰이므로 바꾸지 말 것 (하위 4비트).</summary>
    public enum TileType : byte
    {
        /// <summary>빈 공간 (밖). 지나갈 수 없음</summary>
        Empty = 0,
        Wall = 1,
        Floor = 2,
        /// <summary>얼음: 들어온 방향으로 계속 미끄러짐</summary>
        Ice = 3,
        ConveyorUp = 4,
        ConveyorDown = 5,
        ConveyorLeft = 6,
        ConveyorRight = 7,
        /// <summary>사라졌다 나타나는 바닥 (위상은 칸 파라미터 0~3). 사라졌을 때 위에 있으면 죽음</summary>
        Vanishing = 8,
    }

    /// <summary>장애물 움직임 방식</summary>
    public enum PathMode : byte
    {
        /// <summary>왕복: 마지막 지점에서 되돌아옴</summary>
        PingPong = 0,
        /// <summary>반복 순환: 마지막 지점에서 첫 지점으로 이어짐</summary>
        Loop = 1,
        /// <summary>원 회전: Points[0]이 중심</summary>
        Circle = 2,
    }

    /// <summary>열쇠/문 색 (3가지)</summary>
    public enum KeyColor : byte
    {
        Red = 0,
        Green = 1,
        Blue = 2,
    }

    /// <summary>칸 좌표 (x: 0..Width-1, y: 0..Height-1, y=0이 위쪽 행)</summary>
    [Serializable]
    public struct CellPos : IEquatable<CellPos>
    {
        public int X;
        public int Y;

        public CellPos(int x, int y) { X = x; Y = y; }

        public bool Equals(CellPos other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return obj is CellPos && Equals((CellPos)obj); }
        public override int GetHashCode() { return (X * 397) ^ Y; }
        public override string ToString() { return "(" + X + "," + Y + ")"; }
        public static bool operator ==(CellPos a, CellPos b) { return a.Equals(b); }
        public static bool operator !=(CellPos a, CellPos b) { return !a.Equals(b); }

        public int CenterX { get { return Fixed.CellCenter(X); } }
        public int CenterY { get { return Fixed.CellCenter(Y); } }
    }

    /// <summary>장애물 경로 점 (반 칸 단위: 칸 c의 중심 = 2c+1, 칸 경계 = 2c)</summary>
    [Serializable]
    public struct PathPoint : IEquatable<PathPoint>
    {
        public int HX;
        public int HY;

        public PathPoint(int hx, int hy) { HX = hx; HY = hy; }

        public static PathPoint CellCenter(int cx, int cy) { return new PathPoint(cx * 2 + 1, cy * 2 + 1); }

        public int UnitX { get { return HX * Fixed.HALF_CELL; } }
        public int UnitY { get { return HY * Fixed.HALF_CELL; } }

        public bool Equals(PathPoint other) { return HX == other.HX && HY == other.HY; }
        public override bool Equals(object obj) { return obj is PathPoint && Equals((PathPoint)obj); }
        public override int GetHashCode() { return (HX * 397) ^ HY; }
        public override string ToString() { return "h(" + HX + "," + HY + ")"; }
    }

    [Serializable]
    public struct KeyItem
    {
        public CellPos Cell;
        public KeyColor Color;
        public KeyItem(CellPos cell, KeyColor color) { Cell = cell; Color = color; }
    }

    [Serializable]
    public struct DoorItem
    {
        public CellPos Cell;
        public KeyColor Color;
        public DoorItem(CellPos cell, KeyColor color) { Cell = cell; Color = color; }
    }

    /// <summary>
    /// 동그라미 장애물 정의. 위치는 ObstacleMotion.Evaluate(정의, 틱)의 순수 함수다.
    ///   - PingPong/Loop: Points 1~8개 (1개면 제자리)
    ///   - Circle: Points[0] = 중심, RadiusHalf = 반지름(반 칸 단위), StartAngle 0~15 (×22.5°, 0 = 오른쪽), Clockwise
    /// </summary>
    [Serializable]
    public class ObstacleDef
    {
        public PathMode Mode = PathMode.PingPong;
        public PathPoint[] Points = new PathPoint[0];
        /// <summary>속도 단계 1~3 (초당 2/4/6칸)</summary>
        public int SpeedTier = 2;
        /// <summary>출발 지연 (0.1초 단위, 0~255). 지연 동안은 첫 지점(원은 시작 각도)에 멈춰 있음</summary>
        public int DelayTenths;
        public int RadiusHalf = 2;
        public int StartAngle;
        public bool Clockwise = true;

        public ObstacleDef Clone()
        {
            var o = (ObstacleDef)MemberwiseClone();
            o.Points = (PathPoint[])Points.Clone();
            return o;
        }

        public static ObstacleDef PingPong(int speedTier, int delayTenths, params PathPoint[] points)
        {
            return new ObstacleDef { Mode = PathMode.PingPong, SpeedTier = speedTier, DelayTenths = delayTenths, Points = points };
        }

        public static ObstacleDef Loop(int speedTier, int delayTenths, params PathPoint[] points)
        {
            return new ObstacleDef { Mode = PathMode.Loop, SpeedTier = speedTier, DelayTenths = delayTenths, Points = points };
        }

        public static ObstacleDef Circle(PathPoint center, int radiusHalf, int startAngle, bool clockwise, int speedTier, int delayTenths)
        {
            return new ObstacleDef
            {
                Mode = PathMode.Circle,
                Points = new[] { center },
                RadiusHalf = radiusHalf,
                StartAngle = startAngle,
                Clockwise = clockwise,
                SpeedTier = speedTier,
                DelayTenths = delayTenths,
            };
        }
    }
}
