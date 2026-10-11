using System.Collections.Generic;
using System.Text;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>체감 난이도 (기획서 6.3)</summary>
    public enum PerceivedDifficulty : byte
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Hell = 3,
    }

    /// <summary>
    /// 맵 후기 (기획서 6.3). 좋아요만(싫어요 없음), 체감 난이도 4단계, 한 줄 후기는 정해진 문구 중 선택만.
    /// 자유 입력은 운영 인력이 생긴 뒤에 연다 — 그래서 문구 번호만 저장·전송한다.
    /// </summary>
    public sealed class Review
    {
        public const int NO_PHRASE = -1;

        public bool Liked;
        public PerceivedDifficulty Difficulty = PerceivedDifficulty.Normal;
        /// <summary>정해진 문구 번호 (ReviewPhrases), 없으면 -1</summary>
        public int PhraseId = NO_PHRASE;
        /// <summary>클리어 후 후기인지 (포기 후 후기도 가능)</summary>
        public bool Cleared;

        public static string DifficultyLabel(PerceivedDifficulty d)
        {
            switch (d)
            {
                case PerceivedDifficulty.Easy: return "쉬움";
                case PerceivedDifficulty.Normal: return "보통";
                case PerceivedDifficulty.Hard: return "어려움";
                default: return "지옥";
            }
        }

        public bool IsValid
        {
            get
            {
                return (int)Difficulty >= 0 && (int)Difficulty <= (int)PerceivedDifficulty.Hell
                    && (PhraseId == NO_PHRASE || (PhraseId >= 0 && PhraseId < ReviewPhrases.Count));
            }
        }

        /// <summary>짧은 문자열 (창작마당 키-값 태그 등): "R1;l1;d3;p2;c1"</summary>
        public string Encode()
        {
            return "R1;l" + (Liked ? 1 : 0) + ";d" + (int)Difficulty + ";p" + PhraseId + ";c" + (Cleared ? 1 : 0);
        }

        public static bool TryDecode(string s, out Review review)
        {
            review = null;
            if (string.IsNullOrEmpty(s)) return false;
            string[] parts = s.Split(';');
            if (parts.Length < 5 || parts[0] != "R1") return false;
            var r = new Review();
            int v;
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].Length < 2 || !int.TryParse(parts[i].Substring(1), out v)) return false;
                switch (parts[i][0])
                {
                    case 'l': r.Liked = v == 1; break;
                    case 'd': r.Difficulty = (PerceivedDifficulty)v; break;
                    case 'p': r.PhraseId = v; break;
                    case 'c': r.Cleared = v == 1; break;
                    default: return false;
                }
            }
            if (!r.IsValid) return false;
            review = r;
            return true;
        }
    }

    /// <summary>한 줄 후기 정해진 문구 (기획서 6.3 예시 + 추가). 순서를 바꾸면 저장된 번호가 어긋나므로 뒤에만 추가할 것.</summary>
    public static class ReviewPhrases
    {
        private static readonly string[] Phrases =
        {
            "천재적이다",
            "악마가 만든 맵",
            "끝까지 했다",
            "타이밍이 예술",
            "손이 떨린다",
            "한 번만 더…",
            "친구한테 보내야지",
            "다신 안 해",
        };

        public static int Count { get { return Phrases.Length; } }
        public static string Get(int id) { return id >= 0 && id < Phrases.Length ? Phrases[id] : ""; }
    }

    /// <summary>맵 하나의 후기·기록 요약 (맵 상세 화면, 기획서 6.4 / 11장)</summary>
    public sealed class ReviewSummary
    {
        public int Likes;
        public readonly int[] DifficultyCounts = new int[4];
        public readonly int[] PhraseCounts = new int[ReviewPhrases.Count];
        public int ReviewCount;
        public int Attempts;
        public int Clears;

        public void Add(Review r)
        {
            if (r == null || !r.IsValid) return;
            ReviewCount++;
            if (r.Liked) Likes++;
            DifficultyCounts[(int)r.Difficulty]++;
            if (r.PhraseId != Review.NO_PHRASE) PhraseCounts[r.PhraseId]++;
        }

        /// <summary>클리어율 0~1 (시도 0이면 0)</summary>
        public double ClearRate { get { return Attempts > 0 ? (double)Clears / Attempts : 0.0; } }

        /// <summary>"클리어율 0.8%" 같은 표시 (소수 첫째 자리, 정수 연산)</summary>
        public string ClearRateText
        {
            get
            {
                if (Attempts <= 0) return "기록 없음";
                long permille = ((long)Clears * 1000 + Attempts / 2) / Attempts;
                return (permille / 10) + "." + (permille % 10) + "%";
            }
        }

        /// <summary>가장 많이 고른 체감 난이도 (후기 없으면 Normal)</summary>
        public PerceivedDifficulty MostVotedDifficulty
        {
            get
            {
                int best = 1;
                for (int i = 0; i < 4; i++) if (DifficultyCounts[i] > DifficultyCounts[best]) best = i;
                return (PerceivedDifficulty)best;
            }
        }

        /// <summary>많이 고른 문구 상위 n개</summary>
        public List<int> TopPhrases(int n)
        {
            var ids = new List<int>();
            for (int i = 0; i < PhraseCounts.Length; i++) if (PhraseCounts[i] > 0) ids.Add(i);
            ids.Sort((a, b) => PhraseCounts[b] != PhraseCounts[a] ? PhraseCounts[b].CompareTo(PhraseCounts[a]) : a.CompareTo(b));
            if (ids.Count > n) ids.RemoveRange(n, ids.Count - n);
            return ids;
        }
    }
}
