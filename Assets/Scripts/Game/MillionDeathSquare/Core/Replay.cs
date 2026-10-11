using System;
using System.Collections.Generic;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 틱마다의 입력 기록 (기획서 7.3 "입력 기록을 다시 재생해 검증").
    /// 저장 형식: "MDR" + 버전 + 규칙 버전 + 맵 내용 해시 + 틱 수 + RLE(입력, 반복 수) + CRC32.
    /// 키를 누르고 있는 동안 같은 입력이 이어지므로 RLE로 매우 작아진다.
    /// </summary>
    public sealed class InputRecording
    {
        public const int FORMAT_VERSION = 1;

        private readonly List<byte> _inputs;

        public uint MapHash;
        public int RulesVersion = Rules.RULES_VERSION;

        public InputRecording(uint mapHash, int capacity = 1024)
        {
            MapHash = mapHash;
            _inputs = new List<byte>(capacity);
        }

        public int Length { get { return _inputs.Count; } }
        public InputBits this[int tick] { get { return (InputBits)_inputs[tick]; } }

        public void Add(InputBits input) { _inputs.Add((byte)input); }

        public void Add(InputBits input, int ticks)
        {
            for (int i = 0; i < ticks; i++) _inputs.Add((byte)input);
        }

        public void Clear() { _inputs.Clear(); }

        public byte[] Serialize()
        {
            var w = new ByteWriter(64 + _inputs.Count / 4);
            w.WriteByte('M'); w.WriteByte('D'); w.WriteByte('R');
            w.WriteByte(FORMAT_VERSION);
            w.WriteByte(RulesVersion);
            w.WriteUInt(MapHash);
            w.WriteVarUInt((uint)_inputs.Count);
            int i = 0;
            while (i < _inputs.Count)
            {
                byte v = _inputs[i];
                int run = 1;
                while (i + run < _inputs.Count && _inputs[i + run] == v) run++;
                w.WriteByte(v);
                w.WriteVarUInt((uint)run);
                i += run;
            }
            byte[] body = w.ToArray();
            w.WriteUInt(Crc32.Compute(body));
            return w.ToArray();
        }

        public static InputRecording Deserialize(byte[] data)
        {
            if (data == null || data.Length < 14) throw new FormatException("입력 기록이 너무 짧습니다");
            uint stored = (uint)(data[data.Length - 4] | (data[data.Length - 3] << 8) | (data[data.Length - 2] << 16) | (data[data.Length - 1] << 24));
            if (Crc32.Compute(data, 0, data.Length - 4) != stored) throw new FormatException("입력 기록이 손상되었습니다 (CRC)");
            var r = new ByteReader(data, 0, data.Length - 4);
            if (r.ReadByte() != 'M' || r.ReadByte() != 'D' || r.ReadByte() != 'R') throw new FormatException("입력 기록이 아닙니다");
            int version = r.ReadByte();
            if (version != FORMAT_VERSION) throw new FormatException("지원하지 않는 입력 기록 버전: " + version);
            int rules = r.ReadByte();
            uint hash = r.ReadUInt();
            uint total = r.ReadVarUInt();
            if (total > Rules.MAX_REPLAY_TICKS) throw new FormatException("입력 기록이 너무 깁니다");
            var rec = new InputRecording(hash, (int)total);
            rec.RulesVersion = rules;
            while (rec.Length < total)
            {
                int v = r.ReadByte();
                uint run = r.ReadVarUInt();
                if (run == 0 || rec.Length + run > total) throw new FormatException("입력 기록 데이터가 잘못되었습니다");
                rec.Add((InputBits)v, (int)run);
            }
            if (r.Remaining != 0) throw new FormatException("입력 기록 끝에 알 수 없는 내용이 있습니다");
            return rec;
        }

        public string ToBase64() { return Base64Url.Encode(Serialize()); }
        public static InputRecording FromBase64(string s) { return Deserialize(Base64Url.Decode(s)); }

        /// <summary>
        /// 사람이 쓰기 쉬운 입력 대본 → 기록. 공백으로 구분한 "방향+틱수" 토큰.
        /// 방향: U D L R 조합(예: UR), W = 아무것도 안 누름, X = 처음부터(Restart). 예) "R30 W12 DR15 U45"
        /// </summary>
        public static InputRecording FromScript(uint mapHash, string script)
        {
            var rec = new InputRecording(mapHash);
            if (string.IsNullOrEmpty(script)) return rec;
            string[] tokens = script.Split(new[] { ' ', '\n', '\r', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string tok in tokens)
            {
                int i = 0;
                InputBits bits = InputBits.None;
                while (i < tok.Length && !char.IsDigit(tok[i]))
                {
                    switch (char.ToUpperInvariant(tok[i]))
                    {
                        case 'U': bits |= InputBits.Up; break;
                        case 'D': bits |= InputBits.Down; break;
                        case 'L': bits |= InputBits.Left; break;
                        case 'R': bits |= InputBits.Right; break;
                        case 'W': break;
                        case 'X': bits |= InputBits.Restart; break;
                        default: throw new FormatException("입력 대본에 알 수 없는 글자: " + tok);
                    }
                    i++;
                }
                int count = 1;
                if (i < tok.Length && !int.TryParse(tok.Substring(i), out count)) throw new FormatException("입력 대본 틱 수가 잘못되었습니다: " + tok);
                rec.Add(bits, count);
            }
            return rec;
        }
    }

    public enum ReplayVerdict
    {
        /// <summary>재생 결과 클리어했고 (주장한 값이 있으면) 일치함</summary>
        Valid,
        MapMismatch,
        RulesMismatch,
        InvalidMap,
        NotCleared,
        /// <summary>클리어 뒤에도 입력이 남아 있음 (기록 끝 = 클리어 틱이어야 함)</summary>
        TrailingInput,
        DeathsMismatch,
        TimeMismatch,
        TooLong,
    }

    public struct ReplayResult
    {
        public ReplayVerdict Verdict;
        public int Deaths;
        public int ClearTick;
        public bool IsValid { get { return Verdict == ReplayVerdict.Valid; } }
        public override string ToString() { return Verdict + " deaths=" + Deaths + " ticks=" + ClearTick; }
    }

    /// <summary>
    /// 입력 기록 재생 검증 — 조작된 순위 기록 차단 + "직접 깨야 업로드" (기획서 5.4, 7.3).
    /// 같은 맵(내용 해시)·같은 규칙 버전에서 입력을 그대로 재생해, 기록의 마지막 틱에 클리어해야 한다.
    /// </summary>
    public static class ReplayVerifier
    {
        /// <summary>재생만 해서 결과를 돌려준다 (주장값 비교 없음)</summary>
        public static ReplayResult Run(MapData map, InputRecording rec)
        {
            var result = new ReplayResult { Verdict = ReplayVerdict.NotCleared, Deaths = 0, ClearTick = -1 };
            if (rec.RulesVersion != Rules.RULES_VERSION) { result.Verdict = ReplayVerdict.RulesMismatch; return result; }
            if (rec.MapHash != map.ContentHash()) { result.Verdict = ReplayVerdict.MapMismatch; return result; }
            if (rec.Length > Rules.MAX_REPLAY_TICKS) { result.Verdict = ReplayVerdict.TooLong; return result; }
            if (!MapValidator.Validate(map).IsValid) { result.Verdict = ReplayVerdict.InvalidMap; return result; }

            var sim = new Simulation(map);
            for (int t = 0; t < rec.Length; t++)
            {
                sim.Step(rec[t]);
                if (sim.IsCleared)
                {
                    result.Deaths = sim.Deaths;
                    result.ClearTick = sim.ClearTick;
                    result.Verdict = t == rec.Length - 1 ? ReplayVerdict.Valid : ReplayVerdict.TrailingInput;
                    return result;
                }
            }
            result.Deaths = sim.Deaths;
            return result;
        }

        /// <summary>재생 결과가 주장한 데스·시간과 같은지까지 확인 (순위표 제출 검증)</summary>
        public static ReplayResult Verify(MapData map, InputRecording rec, int claimedDeaths, int claimedTicks)
        {
            ReplayResult r = Run(map, rec);
            if (!r.IsValid) return r;
            if (r.Deaths != claimedDeaths) { r.Verdict = ReplayVerdict.DeathsMismatch; return r; }
            if (r.ClearTick != claimedTicks) { r.Verdict = ReplayVerdict.TimeMismatch; return r; }
            return r;
        }
    }
}
