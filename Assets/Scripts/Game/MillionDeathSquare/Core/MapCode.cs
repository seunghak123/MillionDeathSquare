using System.Text;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 창작마당 항목 번호(ulong) ↔ 짧은 맵 코드 (기획서 6.2).
    /// Crockford Base32(헷갈리는 I/L/O/U 없음) + 가중 합 검사 글자 1개, 4글자마다 '-'.
    /// 예) 3141592653 → "2XJ-FKTD" 같은 형태. 읽을 때 대소문자·하이픈·공백 무시, I/L→1, O→0.
    /// </summary>
    public static class MapCode
    {
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        /// <summary>가중 합 검사 글자 (인접 글자 뒤바뀜·대부분의 오타 검출)</summary>
        private static int CheckDigit(StringBuilder digits, int count)
        {
            int sum = 0;
            for (int i = 0; i < count; i++) sum += (i + 1) * Alphabet.IndexOf(digits[i]);
            return sum & 31;
        }

        public static string Encode(ulong id)
        {
            var digits = new StringBuilder();
            ulong v = id;
            do
            {
                digits.Insert(0, Alphabet[(int)(v & 31)]);
                v >>= 5;
            } while (v != 0);
            digits.Append(Alphabet[CheckDigit(digits, digits.Length)]);

            // 뒤에서부터 4글자씩 묶기 (앞 그룹이 짧음)
            var sb = new StringBuilder();
            int len = digits.Length;
            int first = len % 4 == 0 ? 4 : len % 4;
            for (int i = 0; i < len; i++)
            {
                if (i > 0 && (i - first) % 4 == 0) sb.Append('-');
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }

        public static bool TryDecode(string code, out ulong id)
        {
            id = 0;
            if (string.IsNullOrEmpty(code)) return false;
            var clean = new StringBuilder();
            for (int i = 0; i < code.Length; i++)
            {
                char c = char.ToUpperInvariant(code[i]);
                if (c == '-' || c == ' ' || c == '\t' || c == '\r' || c == '\n') continue;
                if (c == 'I' || c == 'L') c = '1';
                else if (c == 'O') c = '0';
                clean.Append(c);
            }
            if (clean.Length < 2 || clean.Length > 15) return false;

            ulong v = 0;
            for (int i = 0; i < clean.Length - 1; i++)
            {
                int d = Alphabet.IndexOf(clean[i]);
                if (d < 0) return false;
                if (v > (ulong.MaxValue >> 5)) return false; // 넘침
                v = (v << 5) | (uint)d;
            }
            int check = Alphabet.IndexOf(clean[clean.Length - 1]);
            if (check < 0 || check != CheckDigit(clean, clean.Length - 1)) return false;
            id = v;
            return true;
        }
    }
}
