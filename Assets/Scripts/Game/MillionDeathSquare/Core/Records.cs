using System;
using System.Collections.Generic;
using System.Text;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>기록 집계 플랫폼 — 조작 방식이 달라 공정하지 않으므로 따로 집계 (기획서 8.2)</summary>
    public enum RecordPlatform : byte
    {
        PC = 0,
        Mobile = 1,
    }

    public enum RecordKind : byte
    {
        /// <summary>최소 데스</summary>
        Deaths = 0,
        /// <summary>최단 시간 (틱)</summary>
        Time = 1,
    }

    public sealed class RecordEntry
    {
        public string PlayerId;
        public string PlayerName;
        /// <summary>낮을수록 좋음 (데스 수 또는 클리어 틱)</summary>
        public int Score;
        /// <summary>보조 값 (데스 기록이면 그때 시간, 시간 기록이면 그때 데스)</summary>
        public int Detail;
    }

    /// <summary>맵별 순위표 이름 규칙 + 로컬 순위표 (Steam 순위표 이름 최대 128자)</summary>
    public static class RecordBoards
    {
        /// <summary>예) "mds_w_2XJFKTD_pc_deaths", 캠페인 "mds_c_1-3_pc_time", 해시 기반 "mds_h_1a2b3c4d_mo_deaths"</summary>
        public static string BoardName(string mapKey, RecordPlatform platform, RecordKind kind)
        {
            return "mds_" + mapKey + "_" + (platform == RecordPlatform.PC ? "pc" : "mo") + "_" + (kind == RecordKind.Deaths ? "deaths" : "time");
        }

        /// <summary>창작마당 맵의 순위표 키 (맵 코드에서 하이픈 제거)</summary>
        public static string WorkshopKey(ulong workshopId) { return "w_" + MapCode.Encode(workshopId).Replace("-", ""); }

        public static string CampaignKey(int world, int stage) { return "c_" + Campaign.StageId(world, stage); }

        /// <summary>공유 코드로만 받은 맵 (창작마당 번호 없음) — 내용 해시</summary>
        public static string HashKey(uint mapHash) { return "h_" + mapHash.ToString("x8"); }
    }

    /// <summary>
    /// 순위표 한 개 (낮은 점수가 위). 한 사람당 최고 기록 하나만. 로컬 순위표·테스트·Steam 결과 캐시에 공통으로 쓴다.
    /// </summary>
    public sealed class RecordTable
    {
        private readonly List<RecordEntry> _entries = new List<RecordEntry>();
        public readonly int Capacity;

        public RecordTable(int capacity = 100) { Capacity = capacity; }

        public int Count { get { return _entries.Count; } }
        public RecordEntry this[int i] { get { return _entries[i]; } }

        /// <summary>기록 제출. 개인 최고가 갱신되면 true (Steam의 KeepBest와 같은 동작)</summary>
        public bool Submit(string playerId, string playerName, int score, int detail)
        {
            int existing = IndexOf(playerId);
            if (existing >= 0)
            {
                RecordEntry e = _entries[existing];
                if (score >= e.Score) return false;
                _entries.RemoveAt(existing);
            }
            var entry = new RecordEntry { PlayerId = playerId, PlayerName = playerName ?? "", Score = score, Detail = detail };
            int at = 0;
            while (at < _entries.Count && _entries[at].Score <= score) at++;
            _entries.Insert(at, entry);
            if (_entries.Count > Capacity) _entries.RemoveAt(_entries.Count - 1);
            return IndexOf(playerId) >= 0;
        }

        public int IndexOf(string playerId)
        {
            for (int i = 0; i < _entries.Count; i++) if (_entries[i].PlayerId == playerId) return i;
            return -1;
        }

        /// <summary>1부터 순위 (없으면 0)</summary>
        public int RankOf(string playerId) { return IndexOf(playerId) + 1; }

        public RecordEntry Best { get { return _entries.Count > 0 ? _entries[0] : null; } }

        public string Serialize()
        {
            var sb = new StringBuilder("T1");
            foreach (RecordEntry e in _entries)
                sb.Append('\n').Append(Escape(e.PlayerId)).Append('\t').Append(Escape(e.PlayerName)).Append('\t').Append(e.Score).Append('\t').Append(e.Detail);
            return sb.ToString();
        }

        public static RecordTable Parse(string s, int capacity = 100)
        {
            var t = new RecordTable(capacity);
            if (string.IsNullOrEmpty(s)) return t;
            string[] lines = s.Split('\n');
            if (lines[0] != "T1") return t;
            for (int i = 1; i < lines.Length; i++)
            {
                string[] f = lines[i].Split('\t');
                int score, detail;
                if (f.Length < 4 || !int.TryParse(f[2], out score) || !int.TryParse(f[3], out detail)) continue;
                t.Submit(Unescape(f[0]), Unescape(f[1]), score, detail);
            }
            return t;
        }

        private static string Escape(string s) { return (s ?? "").Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n"); }

        private static string Unescape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    sb.Append(n == 't' ? '\t' : (n == 'n' ? '\n' : n));
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }

    /// <summary>제작자 프로필 (기획서 6.4) — 내 맵 목록, 받은 좋아요 합계, 내 맵에서 사람들이 죽은 총 횟수</summary>
    public sealed class CreatorProfile
    {
        public readonly List<ulong> MapIds = new List<ulong>();
        public long TotalLikes;
        public long TotalDeathsOnMyMaps;
    }
}
