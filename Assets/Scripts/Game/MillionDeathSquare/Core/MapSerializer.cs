using System;
using System.Collections.Generic;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 맵 바이너리 직렬화 (버전 있음) + 공유 코드 문자열 (기획서 6.2 / 8.2 / 14.3).
    ///
    /// 형식 v1 (리틀 엔디언):
    ///   "MDS" + 버전(1) + 규칙 버전(1) + 너비(1) + 높이(1) + 팔레트(1)
    ///   이름(가변 길이 UTF-8)
    ///   지형 RLE: (반복 수 1~255, 칸 바이트[하위 4비트 지형 | 상위 4비트 파라미터]) … 칸 수를 채울 때까지
    ///   시작(x,y) / 골인 n×(x,y) / 동전 n×(x,y) / 열쇠 n×(x,y,색) / 문 n×(x,y,색) / 체크포인트 n×(x,y)
    ///   장애물 n × [방식, 속도, 지연, 반지름, 시작각, 시계방향, 점 수, 점 n×(hx,hy)]
    ///   CRC32 (앞 전체)
    /// 공유 코드 = "MDS1-" + Base64Url(바이너리). 모바일판에서 만든 맵도 같은 형식이다.
    /// </summary>
    public static class MapSerializer
    {
        public const int FORMAT_VERSION = 1;
        public const string SHARE_PREFIX = "MDS1-";
        private const int MAX_NAME_BYTES = 128;

        public static byte[] Serialize(MapData map)
        {
            var w = new ByteWriter(256);
            Write(map, w, true);
            byte[] body = w.ToArray();
            w.WriteUInt(Crc32.Compute(body)); // 끝에 CRC (앞 전체)
            return w.ToArray();
        }

        /// <summary>이름을 뺀 내용의 CRC32 (리플레이·업로드 게이트·멀티 맵 일치 확인용)</summary>
        public static uint ComputeContentHash(MapData map)
        {
            var w = new ByteWriter(256);
            Write(map, w, false);
            return Crc32.Compute(w.ToArray()); // 주의: CRC를 덧붙인 데이터의 CRC는 상수이므로 본문만 계산
        }

        private static void Write(MapData map, ByteWriter w, bool includeName)
        {
            w.WriteByte('M'); w.WriteByte('D'); w.WriteByte('S');
            w.WriteByte(FORMAT_VERSION);
            w.WriteByte(Rules.RULES_VERSION);
            w.WriteByte(map.Width);
            w.WriteByte(map.Height);
            w.WriteByte(map.PaletteId);
            w.WriteString(includeName ? (map.Name ?? "") : "");

            // 지형 RLE
            int count = map.Width * map.Height;
            int i = 0;
            while (i < count)
            {
                int b = TileByte(map, i);
                int run = 1;
                while (i + run < count && run < 255 && TileByte(map, i + run) == b) run++;
                w.WriteByte(run);
                w.WriteByte(b);
                i += run;
            }

            WriteCell(w, map.Start);
            WriteCells(w, map.Goals);
            WriteCells(w, map.Coins);
            w.WriteByte(map.Keys.Count);
            for (int k = 0; k < map.Keys.Count; k++) { WriteCell(w, map.Keys[k].Cell); w.WriteByte((int)map.Keys[k].Color); }
            w.WriteByte(map.Doors.Count);
            for (int k = 0; k < map.Doors.Count; k++) { WriteCell(w, map.Doors[k].Cell); w.WriteByte((int)map.Doors[k].Color); }
            WriteCells(w, map.Checkpoints);

            w.WriteByte(map.Obstacles.Count);
            for (int k = 0; k < map.Obstacles.Count; k++)
            {
                ObstacleDef o = map.Obstacles[k];
                w.WriteByte((int)o.Mode);
                w.WriteByte(o.SpeedTier);
                w.WriteByte(o.DelayTenths);
                w.WriteByte(o.RadiusHalf);
                w.WriteByte(o.StartAngle);
                w.WriteBool(o.Clockwise);
                int n = o.Points != null ? o.Points.Length : 0;
                w.WriteByte(n);
                for (int p = 0; p < n; p++) { w.WriteByte(o.Points[p].HX); w.WriteByte(o.Points[p].HY); }
            }
        }

        private static int TileByte(MapData map, int index)
        {
            int t = (int)map.Tiles[index] & 0x0F;
            int p = map.TileParams != null ? map.TileParams[index] & 0x0F : 0;
            return t | (p << 4);
        }

        private static void WriteCell(ByteWriter w, CellPos c) { w.WriteByte(c.X); w.WriteByte(c.Y); }

        private static void WriteCells(ByteWriter w, List<CellPos> cells)
        {
            w.WriteByte(cells.Count);
            for (int i = 0; i < cells.Count; i++) WriteCell(w, cells[i]);
        }

        /// <summary>바이너리 → 맵. 형식이 깨졌으면 FormatException. (규칙 검증은 MapValidator로 따로)</summary>
        public static MapData Deserialize(byte[] data)
        {
            if (data == null || data.Length < 16) throw new FormatException("맵 데이터가 너무 짧습니다");
            uint stored = (uint)(data[data.Length - 4] | (data[data.Length - 3] << 8) | (data[data.Length - 2] << 16) | (data[data.Length - 1] << 24));
            if (Crc32.Compute(data, 0, data.Length - 4) != stored) throw new FormatException("맵 데이터가 손상되었습니다 (CRC)");

            var r = new ByteReader(data, 0, data.Length - 4);
            if (r.ReadByte() != 'M' || r.ReadByte() != 'D' || r.ReadByte() != 'S') throw new FormatException("맵 데이터가 아닙니다");
            int version = r.ReadByte();
            if (version != FORMAT_VERSION) throw new FormatException("지원하지 않는 맵 형식 버전: " + version);
            int rules = r.ReadByte();
            if (rules > Rules.RULES_VERSION) throw new FormatException("더 새로운 게임 버전에서 만든 맵입니다");

            var map = new MapData();
            map.Width = r.ReadByte();
            map.Height = r.ReadByte();
            if (map.Width < 1 || map.Height < 1 || map.Width > Rules.MAX_WIDTH || map.Height > Rules.MAX_HEIGHT)
                throw new FormatException("맵 크기가 잘못되었습니다");
            map.PaletteId = r.ReadByte();
            map.Name = r.ReadString(MAX_NAME_BYTES);

            int count = map.Width * map.Height;
            map.Tiles = new TileType[count];
            map.TileParams = new byte[count];
            int i = 0;
            while (i < count)
            {
                int run = r.ReadByte();
                int b = r.ReadByte();
                if (run < 1 || i + run > count) throw new FormatException("지형 데이터가 잘못되었습니다");
                int t = b & 0x0F;
                if (t > (int)TileType.Vanishing) throw new FormatException("알 수 없는 지형: " + t);
                for (int k = 0; k < run; k++)
                {
                    map.Tiles[i + k] = (TileType)t;
                    map.TileParams[i + k] = (byte)((b >> 4) & 0x0F);
                }
                i += run;
            }

            map.Start = ReadCell(r);
            ReadCells(r, map.Goals);
            ReadCells(r, map.Coins);
            int nk = r.ReadByte();
            for (int k = 0; k < nk; k++) { CellPos c = ReadCell(r); map.Keys.Add(new KeyItem(c, ReadColor(r))); }
            int nd = r.ReadByte();
            for (int k = 0; k < nd; k++) { CellPos c = ReadCell(r); map.Doors.Add(new DoorItem(c, ReadColor(r))); }
            ReadCells(r, map.Checkpoints);

            int no = r.ReadByte();
            for (int k = 0; k < no; k++)
            {
                var o = new ObstacleDef();
                int mode = r.ReadByte();
                if (mode > (int)PathMode.Circle) throw new FormatException("알 수 없는 장애물 방식: " + mode);
                o.Mode = (PathMode)mode;
                o.SpeedTier = r.ReadByte();
                o.DelayTenths = r.ReadByte();
                o.RadiusHalf = r.ReadByte();
                o.StartAngle = r.ReadByte();
                o.Clockwise = r.ReadBool();
                int n = r.ReadByte();
                o.Points = new PathPoint[n];
                for (int p = 0; p < n; p++) o.Points[p] = new PathPoint(r.ReadByte(), r.ReadByte());
                map.Obstacles.Add(o);
            }
            if (r.Remaining != 0) throw new FormatException("맵 데이터 끝에 알 수 없는 내용이 있습니다");
            return map;
        }

        private static KeyColor ReadColor(ByteReader r)
        {
            int c = r.ReadByte();
            if (c >= Rules.KEY_COLORS) throw new FormatException("알 수 없는 열쇠 색: " + c);
            return (KeyColor)c;
        }

        private static CellPos ReadCell(ByteReader r) { int x = r.ReadByte(); int y = r.ReadByte(); return new CellPos(x, y); }

        private static void ReadCells(ByteReader r, List<CellPos> list)
        {
            int n = r.ReadByte();
            for (int i = 0; i < n; i++) list.Add(ReadCell(r));
        }

        // ── 공유 코드 ──

        public static string ToShareCode(MapData map)
        {
            return SHARE_PREFIX + Base64Url.Encode(Serialize(map));
        }

        /// <summary>공유 코드 → 맵. 앞뒤 공백/줄바꿈은 무시. 실패 시 false + 이유.</summary>
        public static bool TryParseShareCode(string code, out MapData map, out string error)
        {
            map = null;
            error = null;
            if (string.IsNullOrEmpty(code)) { error = "공유 코드가 비어 있습니다"; return false; }
            string s = code.Trim();
            if (!s.StartsWith(SHARE_PREFIX, StringComparison.Ordinal)) { error = "공유 코드는 " + SHARE_PREFIX + " 로 시작해야 합니다"; return false; }
            try
            {
                map = Deserialize(Base64Url.Decode(s.Substring(SHARE_PREFIX.Length)));
                return true;
            }
            catch (FormatException e)
            {
                error = e.Message;
                map = null;
                return false;
            }
        }

        public static MapData FromShareCode(string code)
        {
            MapData map;
            string error;
            if (!TryParseShareCode(code, out map, out error)) throw new FormatException(error);
            return map;
        }
    }
}
