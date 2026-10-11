using System.Collections.Generic;
using System.Text;

namespace Seunghak.Game.MillionDeathSquare
{
    public enum ValidationCode
    {
        SizeOutOfRange,
        TileDataMismatch,
        PaletteOutOfRange,
        NameTooLong,
        NameForbidden,
        StartOutOfBounds,
        StartNotWalkable,
        NoGoal,
        TooManyGoals,
        TooManyCoins,
        TooManyObstacles,
        TooManyCheckpoints,
        TooManyKeys,
        TooManyDoors,
        ItemOutOfBounds,
        ItemNotWalkable,
        ItemOverlap,
        DoorWithoutKey,
        BadKeyColor,
        ObstacleNoPoints,
        ObstacleTooManyPoints,
        ObstaclePointOutOfBounds,
        ObstacleBadSpeed,
        ObstacleBadDelay,
        ObstacleBadRadius,
        ObstacleBadAngle,
        StartTouchesObstacle,
    }

    public struct ValidationIssue
    {
        public ValidationCode Code;
        public string Message;
        /// <summary>문제 위치 칸 (없으면 -1)</summary>
        public int X;
        public int Y;
        /// <summary>장애물 번호 등 (없으면 -1)</summary>
        public int Index;

        public override string ToString() { return Code + ": " + Message; }
    }

    public sealed class ValidationResult
    {
        public readonly List<ValidationIssue> Issues = new List<ValidationIssue>();
        public bool IsValid { get { return Issues.Count == 0; } }

        public bool Has(ValidationCode code)
        {
            for (int i = 0; i < Issues.Count; i++) if (Issues[i].Code == code) return true;
            return false;
        }

        internal void Add(ValidationCode code, string message, int x = -1, int y = -1, int index = -1)
        {
            Issues.Add(new ValidationIssue { Code = code, Message = message, X = x, Y = y, Index = index });
        }

        public override string ToString()
        {
            if (IsValid) return "OK";
            var sb = new StringBuilder();
            for (int i = 0; i < Issues.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(Issues[i].Message);
            }
            return sb.ToString();
        }
    }

    /// <summary>맵 규칙 검사 (기획서 5.1 / 5.3 / 13장). 업로드·공유 코드 읽기·캠페인 데이터 모두 이걸 통과해야 한다.</summary>
    public static class MapValidator
    {
        public static ValidationResult Validate(MapData map)
        {
            var r = new ValidationResult();
            if (map.Width < Rules.MIN_SIZE || map.Height < Rules.MIN_SIZE || map.Width > Rules.MAX_WIDTH || map.Height > Rules.MAX_HEIGHT)
            {
                r.Add(ValidationCode.SizeOutOfRange, "맵 크기는 " + Rules.MIN_SIZE + "×" + Rules.MIN_SIZE + " ~ " + Rules.MAX_WIDTH + "×" + Rules.MAX_HEIGHT + "칸이어야 합니다");
                return r;
            }
            if (map.Tiles == null || map.Tiles.Length != map.Width * map.Height || map.TileParams == null || map.TileParams.Length != map.Tiles.Length)
            {
                r.Add(ValidationCode.TileDataMismatch, "지형 데이터 크기가 맞지 않습니다");
                return r;
            }
            if (map.PaletteId < 0 || map.PaletteId >= Rules.PALETTE_COUNT)
                r.Add(ValidationCode.PaletteOutOfRange, "팔레트는 0~" + (Rules.PALETTE_COUNT - 1) + " 중 하나여야 합니다");

            string name = map.Name ?? "";
            if (name.Length > Rules.MAX_NAME_LENGTH)
                r.Add(ValidationCode.NameTooLong, "맵 이름은 " + Rules.MAX_NAME_LENGTH + "자 이하여야 합니다");
            string reason;
            if (name.Length > 0 && !MapNameFilter.IsAllowed(name, out reason))
                r.Add(ValidationCode.NameForbidden, "맵 이름에 쓸 수 없는 말이 있습니다" + (string.IsNullOrEmpty(reason) ? "" : " (" + reason + ")"));

            // 시작점
            if (!map.InBounds(map.Start)) r.Add(ValidationCode.StartOutOfBounds, "시작점이 맵 밖에 있습니다", map.Start.X, map.Start.Y);
            else if (!MapData.IsWalkable(map.GetTile(map.Start)) || map.GetTile(map.Start) == TileType.Vanishing)
                r.Add(ValidationCode.StartNotWalkable, "시작점은 바닥(사라지는 바닥 제외) 위에 있어야 합니다", map.Start.X, map.Start.Y);

            // 개수 제한
            if (map.Goals.Count < 1) r.Add(ValidationCode.NoGoal, "골인 지점이 1개 이상 있어야 합니다");
            if (map.Goals.Count > Rules.MAX_GOALS) r.Add(ValidationCode.TooManyGoals, "골인 지점은 " + Rules.MAX_GOALS + "개까지입니다");
            if (map.Coins.Count > Rules.MAX_COINS) r.Add(ValidationCode.TooManyCoins, "동전은 " + Rules.MAX_COINS + "개까지입니다");
            if (map.Obstacles.Count > Rules.MAX_OBSTACLES) r.Add(ValidationCode.TooManyObstacles, "장애물은 " + Rules.MAX_OBSTACLES + "개까지입니다");
            if (map.Checkpoints.Count > Rules.MAX_CHECKPOINTS) r.Add(ValidationCode.TooManyCheckpoints, "체크포인트는 " + Rules.MAX_CHECKPOINTS + "개까지입니다");
            if (map.Keys.Count > Rules.MAX_KEYS) r.Add(ValidationCode.TooManyKeys, "열쇠는 " + Rules.MAX_KEYS + "개까지입니다");
            if (map.Doors.Count > Rules.MAX_DOORS) r.Add(ValidationCode.TooManyDoors, "문은 " + Rules.MAX_DOORS + "개까지입니다");

            // 아이템 위치 (한 칸에 아이템 하나)
            var used = new HashSet<int>();
            if (map.InBounds(map.Start)) used.Add(map.Start.Y * map.Width + map.Start.X);
            CheckCells(map, r, map.Goals, "골인 지점", used, false);
            CheckCells(map, r, map.Coins, "동전", used, false);
            CheckCells(map, r, map.Checkpoints, "체크포인트", used, false);
            var keyColors = new bool[Rules.KEY_COLORS];
            for (int i = 0; i < map.Keys.Count; i++)
            {
                KeyItem k = map.Keys[i];
                if ((int)k.Color < 0 || (int)k.Color >= Rules.KEY_COLORS) { r.Add(ValidationCode.BadKeyColor, "열쇠 색이 잘못되었습니다", k.Cell.X, k.Cell.Y); continue; }
                keyColors[(int)k.Color] = true;
                CheckCell(map, r, k.Cell, "열쇠", used, false);
            }
            for (int i = 0; i < map.Doors.Count; i++)
            {
                DoorItem d = map.Doors[i];
                if ((int)d.Color < 0 || (int)d.Color >= Rules.KEY_COLORS) { r.Add(ValidationCode.BadKeyColor, "문 색이 잘못되었습니다", d.Cell.X, d.Cell.Y); continue; }
                if (!keyColors[(int)d.Color]) r.Add(ValidationCode.DoorWithoutKey, "같은 색 열쇠가 없는 문이 있습니다", d.Cell.X, d.Cell.Y);
                CheckCell(map, r, d.Cell, "문", used, false);
            }

            // 장애물
            int maxHX = map.Width * 2;
            int maxHY = map.Height * 2;
            for (int i = 0; i < map.Obstacles.Count; i++)
            {
                ObstacleDef o = map.Obstacles[i];
                int n = o.Points != null ? o.Points.Length : 0;
                if (n == 0) { r.Add(ValidationCode.ObstacleNoPoints, "장애물 " + (i + 1) + "에 경로 지점이 없습니다", -1, -1, i); continue; }
                if (n > Rules.MAX_PATH_POINTS) r.Add(ValidationCode.ObstacleTooManyPoints, "장애물 경로 지점은 " + Rules.MAX_PATH_POINTS + "개까지입니다", -1, -1, i);
                if (o.Mode == PathMode.Circle && n != 1) r.Add(ValidationCode.ObstacleTooManyPoints, "원 회전 장애물은 중심 1개만 가집니다", -1, -1, i);
                for (int p = 0; p < n; p++)
                {
                    PathPoint pt = o.Points[p];
                    if (pt.HX < 0 || pt.HY < 0 || pt.HX > maxHX || pt.HY > maxHY)
                    {
                        r.Add(ValidationCode.ObstaclePointOutOfBounds, "장애물 " + (i + 1) + "의 경로가 맵 밖으로 나갑니다", -1, -1, i);
                        break;
                    }
                }
                if (o.SpeedTier < 1 || o.SpeedTier > 3) r.Add(ValidationCode.ObstacleBadSpeed, "장애물 속도는 1~3단계입니다", -1, -1, i);
                if (o.DelayTenths < 0 || o.DelayTenths > 255) r.Add(ValidationCode.ObstacleBadDelay, "출발 지연은 0~25.5초입니다", -1, -1, i);
                if (o.Mode == PathMode.Circle)
                {
                    if (o.RadiusHalf < 1 || o.RadiusHalf > Rules.MAX_CIRCLE_RADIUS_HALF) r.Add(ValidationCode.ObstacleBadRadius, "원 반지름은 0.5~8칸입니다", -1, -1, i);
                    if (o.StartAngle < 0 || o.StartAngle > 15) r.Add(ValidationCode.ObstacleBadAngle, "시작 각도는 0~15입니다", -1, -1, i);
                    PathPoint c = o.Points[0];
                    int rad = o.RadiusHalf;
                    if (c.HX - rad < 0 || c.HY - rad < 0 || c.HX + rad > maxHX || c.HY + rad > maxHY)
                        r.Add(ValidationCode.ObstaclePointOutOfBounds, "장애물 " + (i + 1) + "의 원이 맵 밖으로 나갑니다", -1, -1, i);
                }
            }

            // 시작 순간 바로 죽는 맵 방지 (틱 0과 1)
            if (r.IsValid)
            {
                for (int i = 0; i < map.Obstacles.Count; i++)
                {
                    var m = new ObstacleMotion(map.Obstacles[i]);
                    for (int t = 0; t <= 1; t++)
                    {
                        int ox, oy;
                        m.Evaluate(t, out ox, out oy);
                        if (Simulation.BoxCircleOverlap(map.Start.CenterX, map.Start.CenterY, Rules.PLAYER_HALF, ox, oy, Rules.OBSTACLE_RADIUS))
                        {
                            r.Add(ValidationCode.StartTouchesObstacle, "시작하자마자 장애물에 닿습니다", map.Start.X, map.Start.Y, i);
                            t = 2;
                            i = map.Obstacles.Count;
                        }
                    }
                }
            }
            return r;
        }

        private static void CheckCells(MapData map, ValidationResult r, List<CellPos> cells, string what, HashSet<int> used, bool allowVanishing)
        {
            for (int i = 0; i < cells.Count; i++) CheckCell(map, r, cells[i], what, used, allowVanishing);
        }

        private static void CheckCell(MapData map, ValidationResult r, CellPos c, string what, HashSet<int> used, bool allowVanishing)
        {
            if (!map.InBounds(c)) { r.Add(ValidationCode.ItemOutOfBounds, what + "이(가) 맵 밖에 있습니다", c.X, c.Y); return; }
            TileType t = map.GetTile(c);
            if (!MapData.IsWalkable(t) || (!allowVanishing && t == TileType.Vanishing))
                r.Add(ValidationCode.ItemNotWalkable, what + "은(는) 바닥 위에 있어야 합니다", c.X, c.Y);
            int key = c.Y * map.Width + c.X;
            if (!used.Add(key)) r.Add(ValidationCode.ItemOverlap, "한 칸에 아이템을 두 개 놓을 수 없습니다", c.X, c.Y);
        }
    }

    /// <summary>
    /// 맵 이름 금지어 필터 연결 지점 (기획서 10장). 기본 구현은 단어 목록 포함 검사.
    /// 실제 금지어 목록은 운영 쪽에서 채운다 — 게임 시작 시 MapNameFilter.Current를 바꾸거나 AddWords로 추가.
    /// </summary>
    public interface IMapNameFilter
    {
        bool IsAllowed(string name, out string reason);
    }

    public sealed class WordListNameFilter : IMapNameFilter
    {
        private readonly List<string> _words = new List<string>();

        public WordListNameFilter(IEnumerable<string> words)
        {
            if (words != null) foreach (string w in words) Add(w);
        }

        public int Count { get { return _words.Count; } }

        public void Add(string word)
        {
            string n = Normalize(word);
            if (n.Length > 0 && !_words.Contains(n)) _words.Add(n);
        }

        /// <summary>소문자화 + 공백·기호·숫자 제거 (띄어쓰기/특수문자로 우회하는 것 방지)</summary>
        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetter(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        public bool IsAllowed(string name, out string reason)
        {
            reason = null;
            string n = Normalize(name);
            for (int i = 0; i < _words.Count; i++)
            {
                if (n.Contains(_words[i]))
                {
                    reason = "금지어";
                    return false;
                }
            }
            return true;
        }
    }

    public static class MapNameFilter
    {
        /// <summary>
        /// 기본 금지어 (자리표시 — 운영 정책에 맞는 목록으로 교체 필요, Docs/TODO.md 참고).
        /// </summary>
        public static readonly string[] PlaceholderWords = { "fuck", "shit", "씨발", "시발", "병신", "좆", "개새끼", "nigger" };

        private static IMapNameFilter _current = new WordListNameFilter(PlaceholderWords);

        public static IMapNameFilter Current
        {
            get { return _current; }
            set { _current = value ?? new WordListNameFilter(null); }
        }

        public static bool IsAllowed(string name, out string reason)
        {
            return _current.IsAllowed(name, out reason);
        }
    }
}
