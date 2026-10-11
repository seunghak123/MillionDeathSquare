using System;
using System.Collections.Generic;
using System.Text;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>작은 바이너리 쓰기 도우미 (리틀 엔디언, 가변 길이 정수)</summary>
    public sealed class ByteWriter
    {
        private byte[] _buf;
        private int _len;

        public ByteWriter(int capacity = 256) { _buf = new byte[capacity < 16 ? 16 : capacity]; }

        public int Length { get { return _len; } }

        public void Clear() { _len = 0; }

        private void Ensure(int extra)
        {
            if (_len + extra <= _buf.Length) return;
            int size = _buf.Length * 2;
            while (size < _len + extra) size *= 2;
            Array.Resize(ref _buf, size);
        }

        public void WriteByte(int v) { Ensure(1); _buf[_len++] = (byte)v; }

        public void WriteBool(bool v) { WriteByte(v ? 1 : 0); }

        public void WriteUShort(int v) { Ensure(2); _buf[_len++] = (byte)v; _buf[_len++] = (byte)(v >> 8); }

        public void WriteUInt(uint v)
        {
            Ensure(4);
            _buf[_len++] = (byte)v; _buf[_len++] = (byte)(v >> 8);
            _buf[_len++] = (byte)(v >> 16); _buf[_len++] = (byte)(v >> 24);
        }

        public void WriteInt(int v) { WriteUInt(unchecked((uint)v)); }

        public void WriteULong(ulong v)
        {
            WriteUInt((uint)(v & 0xFFFFFFFFUL));
            WriteUInt((uint)(v >> 32));
        }

        /// <summary>7비트 가변 길이 부호 없는 정수</summary>
        public void WriteVarUInt(uint v)
        {
            while (v >= 0x80)
            {
                WriteByte((int)(v & 0x7F) | 0x80);
                v >>= 7;
            }
            WriteByte((int)v);
        }

        /// <summary>지그재그 가변 길이 부호 있는 정수</summary>
        public void WriteVarInt(int v) { WriteVarUInt(unchecked((uint)((v << 1) ^ (v >> 31)))); }

        public void WriteBytes(byte[] data, int offset, int count)
        {
            Ensure(count);
            Buffer.BlockCopy(data, offset, _buf, _len, count);
            _len += count;
        }

        /// <summary>UTF-8 문자열 (가변 길이 접두)</summary>
        public void WriteString(string s)
        {
            byte[] b = Encoding.UTF8.GetBytes(s ?? "");
            WriteVarUInt((uint)b.Length);
            WriteBytes(b, 0, b.Length);
        }

        public byte[] ToArray()
        {
            var r = new byte[_len];
            Buffer.BlockCopy(_buf, 0, r, 0, _len);
            return r;
        }
    }

    /// <summary>ByteWriter와 짝을 이루는 읽기 도우미. 범위를 넘으면 FormatException.</summary>
    public sealed class ByteReader
    {
        private readonly byte[] _buf;
        private readonly int _end;
        private int _pos;

        public ByteReader(byte[] data) : this(data, 0, data.Length) { }

        public ByteReader(byte[] data, int offset, int count)
        {
            _buf = data;
            _pos = offset;
            _end = offset + count;
        }

        public int Position { get { return _pos; } }
        public int Remaining { get { return _end - _pos; } }

        private void Need(int n)
        {
            if (_pos + n > _end) throw new FormatException("데이터가 잘렸습니다");
        }

        public int ReadByte() { Need(1); return _buf[_pos++]; }
        public bool ReadBool() { return ReadByte() != 0; }

        public int ReadUShort()
        {
            Need(2);
            int v = _buf[_pos] | (_buf[_pos + 1] << 8);
            _pos += 2;
            return v;
        }

        public uint ReadUInt()
        {
            Need(4);
            uint v = (uint)(_buf[_pos] | (_buf[_pos + 1] << 8) | (_buf[_pos + 2] << 16) | (_buf[_pos + 3] << 24));
            _pos += 4;
            return v;
        }

        public int ReadInt() { return unchecked((int)ReadUInt()); }

        public ulong ReadULong()
        {
            ulong lo = ReadUInt();
            ulong hi = ReadUInt();
            return lo | (hi << 32);
        }

        public uint ReadVarUInt()
        {
            uint result = 0;
            int shift = 0;
            while (true)
            {
                if (shift > 28) throw new FormatException("가변 정수가 너무 깁니다");
                int b = ReadByte();
                result |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return result;
                shift += 7;
            }
        }

        public int ReadVarInt()
        {
            uint z = ReadVarUInt();
            return unchecked((int)(z >> 1) ^ -(int)(z & 1));
        }

        public string ReadString(int maxBytes)
        {
            int n = (int)ReadVarUInt();
            if (n > maxBytes) throw new FormatException("문자열이 너무 깁니다");
            Need(n);
            string s = Encoding.UTF8.GetString(_buf, _pos, n);
            _pos += n;
            return s;
        }

        public byte[] ReadBytes(int n)
        {
            Need(n);
            var r = new byte[n];
            Buffer.BlockCopy(_buf, _pos, r, 0, n);
            _pos += n;
            return r;
        }
    }

    /// <summary>CRC-32 (IEEE 802.3)</summary>
    public static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var t = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[i] = c;
            }
            return t;
        }

        public static uint Compute(byte[] data, int offset, int count)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = offset; i < offset + count; i++) crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }

        public static uint Compute(byte[] data) { return Compute(data, 0, data.Length); }
    }

    /// <summary>URL/채팅에 안전한 Base64 (+ → -, / → _, 패딩 없음)</summary>
    public static class Base64Url
    {
        public static string Encode(byte[] data)
        {
            string s = Convert.ToBase64String(data);
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '=') break;
                sb.Append(c == '+' ? '-' : (c == '/' ? '_' : c));
            }
            return sb.ToString();
        }

        public static byte[] Decode(string s)
        {
            var sb = new StringBuilder(s.Length + 3);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c)) continue;
                if (c == '-') c = '+';
                else if (c == '_') c = '/';
                else if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '+' || c == '/'))
                    throw new FormatException("공유 코드에 쓸 수 없는 글자가 있습니다");
                sb.Append(c);
            }
            while (sb.Length % 4 != 0) sb.Append('=');
            return Convert.FromBase64String(sb.ToString());
        }
    }
}
