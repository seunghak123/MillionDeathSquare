using System;
using System.Collections.Generic;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 맵 데이터 (PC·모바일 공통 형식, 기획서 8.2). 직렬화는 MapSerializer, 검증은 MapValidator.
    /// 좌표: x = 0..Width-1 (왼→오), y = 0..Height-1 (위→아래).
    /// </summary>
    [Serializable]
    public class MapData
    {
        public string Name = "";
        public int Width;
        public int Height;
        /// <summary>색 팔레트 0~7 (Palettes)</summary>
        public int PaletteId;
        /// <summary>칸 지형 (y * Width + x)</summary>
        public TileType[] Tiles;
        /// <summary>칸 파라미터 (사라지는 바닥의 위상 0~3). 다른 지형은 0</summary>
        public byte[] TileParams;

        public CellPos Start;
        public List<CellPos> Goals = new List<CellPos>();
        public List<CellPos> Coins = new List<CellPos>();
        public List<KeyItem> Keys = new List<KeyItem>();
        public List<DoorItem> Doors = new List<DoorItem>();
        public List<CellPos> Checkpoints = new List<CellPos>();
        public List<ObstacleDef> Obstacles = new List<ObstacleDef>();

        public MapData() { }

        public MapData(int width, int height, TileType fill)
        {
            Resize(width, height, fill);
        }

        /// <summary>테두리 벽 + 안쪽 바닥으로 된 새 맵 (에디터 기본값)</summary>
        public static MapData CreateBlank(int width, int height)
        {
            var map = new MapData(width, height, TileType.Floor);
            for (int x = 0; x < width; x++)
            {
                map.SetTile(x, 0, TileType.Wall);
                map.SetTile(x, height - 1, TileType.Wall);
            }
            for (int y = 0; y < height; y++)
            {
                map.SetTile(0, y, TileType.Wall);
                map.SetTile(width - 1, y, TileType.Wall);
            }
            map.Start = new CellPos(1, height / 2);
            map.Goals.Add(new CellPos(width - 2, height / 2));
            return map;
        }

        public void Resize(int width, int height, TileType fill)
        {
            Width = width;
            Height = height;
            Tiles = new TileType[width * height];
            TileParams = new byte[width * height];
            for (int i = 0; i < Tiles.Length; i++) Tiles[i] = fill;
        }

        public bool InBounds(int x, int y) { return x >= 0 && y >= 0 && x < Width && y < Height; }
        public bool InBounds(CellPos c) { return InBounds(c.X, c.Y); }

        public TileType GetTile(int x, int y) { return InBounds(x, y) ? Tiles[y * Width + x] : TileType.Empty; }
        public TileType GetTile(CellPos c) { return GetTile(c.X, c.Y); }

        public void SetTile(int x, int y, TileType t)
        {
            if (!InBounds(x, y)) return;
            Tiles[y * Width + x] = t;
            if (t != TileType.Vanishing) TileParams[y * Width + x] = 0;
        }

        public int GetParam(int x, int y) { return InBounds(x, y) ? TileParams[y * Width + x] : 0; }

        public void SetParam(int x, int y, int value)
        {
            if (!InBounds(x, y)) return;
            TileParams[y * Width + x] = (byte)(value & 0x0F);
        }

        /// <summary>네모가 설 수 있는 지형인지 (벽/빈칸이 아님)</summary>
        public static bool IsWalkable(TileType t)
        {
            return t != TileType.Empty && t != TileType.Wall;
        }

        public static bool IsConveyor(TileType t)
        {
            return t == TileType.ConveyorUp || t == TileType.ConveyorDown || t == TileType.ConveyorLeft || t == TileType.ConveyorRight;
        }

        public int CountTiles(TileType t)
        {
            int n = 0;
            for (int i = 0; i < Tiles.Length; i++) if (Tiles[i] == t) n++;
            return n;
        }

        public int IndexOfDoorAt(int x, int y)
        {
            for (int i = 0; i < Doors.Count; i++) if (Doors[i].Cell.X == x && Doors[i].Cell.Y == y) return i;
            return -1;
        }

        public MapData Clone()
        {
            var m = new MapData();
            m.Name = Name;
            m.Width = Width;
            m.Height = Height;
            m.PaletteId = PaletteId;
            m.Tiles = Tiles != null ? (TileType[])Tiles.Clone() : null;
            m.TileParams = TileParams != null ? (byte[])TileParams.Clone() : null;
            m.Start = Start;
            m.Goals = new List<CellPos>(Goals);
            m.Coins = new List<CellPos>(Coins);
            m.Keys = new List<KeyItem>(Keys);
            m.Doors = new List<DoorItem>(Doors);
            m.Checkpoints = new List<CellPos>(Checkpoints);
            m.Obstacles = new List<ObstacleDef>(Obstacles.Count);
            for (int i = 0; i < Obstacles.Count; i++) m.Obstacles.Add(Obstacles[i].Clone());
            return m;
        }

        /// <summary>내용 해시 (이름 제외). 리플레이·업로드 게이트·멀티 맵 일치 확인에 쓴다.</summary>
        public uint ContentHash()
        {
            return MapSerializer.ComputeContentHash(this);
        }
    }
}
