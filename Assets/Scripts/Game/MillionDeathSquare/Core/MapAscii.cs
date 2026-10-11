using System;
using System.Text;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 글자 그림 ↔ 맵 (캠페인 데이터 작성·테스트·디버그용). 장애물은 따로 추가한다.
    ///
    ///   '#' 벽   ' ' 빈칸   '.' 바닥   '~' 얼음   '^' 'v' '&lt;' '&gt;' 컨베이어   '0'~'3' 사라지는 바닥(위상)
    ///   아래 기호는 바닥 위에 놓이는 것: '@' 시작  'E' 골인  '$' 동전  'P' 체크포인트
    ///   'r' 'g' 'b' 열쇠(빨강/초록/파랑)  'R' 'G' 'B' 문
    /// </summary>
    public static class MapAscii
    {
        public static MapData Parse(string name, int paletteId, params string[] rows)
        {
            if (rows == null || rows.Length == 0) throw new ArgumentException("rows");
            int h = rows.Length;
            int w = 0;
            for (int i = 0; i < h; i++) if (rows[i].Length > w) w = rows[i].Length;

            var map = new MapData(w, h, TileType.Empty);
            map.Name = name;
            map.PaletteId = paletteId;
            bool hasStart = false;
            for (int y = 0; y < h; y++)
            {
                string row = rows[y];
                for (int x = 0; x < w; x++)
                {
                    char c = x < row.Length ? row[x] : ' ';
                    var cell = new CellPos(x, y);
                    switch (c)
                    {
                        case ' ': map.SetTile(x, y, TileType.Empty); break;
                        case '#': map.SetTile(x, y, TileType.Wall); break;
                        case '.': map.SetTile(x, y, TileType.Floor); break;
                        case '~': map.SetTile(x, y, TileType.Ice); break;
                        case '^': map.SetTile(x, y, TileType.ConveyorUp); break;
                        case 'v': map.SetTile(x, y, TileType.ConveyorDown); break;
                        case '<': map.SetTile(x, y, TileType.ConveyorLeft); break;
                        case '>': map.SetTile(x, y, TileType.ConveyorRight); break;
                        case '0': case '1': case '2': case '3':
                            map.SetTile(x, y, TileType.Vanishing);
                            map.SetParam(x, y, c - '0');
                            break;
                        case '@': map.SetTile(x, y, TileType.Floor); map.Start = cell; hasStart = true; break;
                        case 'E': map.SetTile(x, y, TileType.Floor); map.Goals.Add(cell); break;
                        case '$': map.SetTile(x, y, TileType.Floor); map.Coins.Add(cell); break;
                        case 'P': map.SetTile(x, y, TileType.Floor); map.Checkpoints.Add(cell); break;
                        case 'r': map.SetTile(x, y, TileType.Floor); map.Keys.Add(new KeyItem(cell, KeyColor.Red)); break;
                        case 'g': map.SetTile(x, y, TileType.Floor); map.Keys.Add(new KeyItem(cell, KeyColor.Green)); break;
                        case 'b': map.SetTile(x, y, TileType.Floor); map.Keys.Add(new KeyItem(cell, KeyColor.Blue)); break;
                        case 'R': map.SetTile(x, y, TileType.Floor); map.Doors.Add(new DoorItem(cell, KeyColor.Red)); break;
                        case 'G': map.SetTile(x, y, TileType.Floor); map.Doors.Add(new DoorItem(cell, KeyColor.Green)); break;
                        case 'B': map.SetTile(x, y, TileType.Floor); map.Doors.Add(new DoorItem(cell, KeyColor.Blue)); break;
                        default: throw new FormatException("알 수 없는 맵 글자 '" + c + "' (" + x + "," + y + ")");
                    }
                }
            }
            if (!hasStart) throw new FormatException("시작점 '@'이 없습니다");
            return map;
        }

        /// <summary>맵 → 글자 그림 (장애물 제외)</summary>
        public static string ToAscii(MapData map)
        {
            var grid = new char[map.Height][];
            for (int y = 0; y < map.Height; y++)
            {
                grid[y] = new char[map.Width];
                for (int x = 0; x < map.Width; x++)
                {
                    TileType t = map.GetTile(x, y);
                    char c;
                    switch (t)
                    {
                        case TileType.Wall: c = '#'; break;
                        case TileType.Floor: c = '.'; break;
                        case TileType.Ice: c = '~'; break;
                        case TileType.ConveyorUp: c = '^'; break;
                        case TileType.ConveyorDown: c = 'v'; break;
                        case TileType.ConveyorLeft: c = '<'; break;
                        case TileType.ConveyorRight: c = '>'; break;
                        case TileType.Vanishing: c = (char)('0' + (map.GetParam(x, y) & 3)); break;
                        default: c = ' '; break;
                    }
                    grid[y][x] = c;
                }
            }
            Put(grid, map.Start, '@');
            foreach (CellPos c in map.Goals) Put(grid, c, 'E');
            foreach (CellPos c in map.Coins) Put(grid, c, '$');
            foreach (CellPos c in map.Checkpoints) Put(grid, c, 'P');
            foreach (KeyItem k in map.Keys) Put(grid, k.Cell, "rgb"[(int)k.Color]);
            foreach (DoorItem d in map.Doors) Put(grid, d.Cell, "RGB"[(int)d.Color]);
            var sb = new StringBuilder();
            for (int y = 0; y < map.Height; y++)
            {
                if (y > 0) sb.Append('\n');
                sb.Append(new string(grid[y]));
            }
            return sb.ToString();
        }

        private static void Put(char[][] grid, CellPos c, char ch)
        {
            if (c.Y >= 0 && c.Y < grid.Length && c.X >= 0 && c.X < grid[c.Y].Length) grid[c.Y][c.X] = ch;
        }

        /// <summary>칸 중심 경로 점 (캠페인 데이터 작성 편의)</summary>
        public static PathPoint C(int cx, int cy) { return PathPoint.CellCenter(cx, cy); }
    }
}
