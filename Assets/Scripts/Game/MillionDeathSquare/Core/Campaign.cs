using System;
using System.Collections.Generic;
using System.Text;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>에디터에서 쓸 수 있는 장치 (월드를 깨면 해금, 기획서 4.1). 기본 지형·아이템은 처음부터 가능.</summary>
    [Flags]
    public enum EditorDevice
    {
        None = 0,
        /// <summary>월드 1: 경로 장애물 (왕복 / 반복 순환)</summary>
        PathObstacle = 1,
        /// <summary>월드 2: 원 회전 장애물</summary>
        CircleObstacle = 2,
        /// <summary>월드 3: 열쇠와 문</summary>
        KeysDoors = 4,
        /// <summary>월드 4: 얼음 바닥, 컨베이어</summary>
        IceConveyor = 8,
        /// <summary>월드 5: 사라지는 바닥</summary>
        Vanishing = 16,
        All = PathObstacle | CircleObstacle | KeysDoors | IceConveyor | Vanishing,
    }

    public sealed class CampaignStage
    {
        public int World;
        public int Stage;
        public MapData Map;
        /// <summary>임시 맵 (레벨 기획 전) 여부</summary>
        public bool IsPlaceholder;

        public string Id { get { return Campaign.StageId(World, Stage); } }
        public string Name { get { return Map.Name; } }
    }

    /// <summary>
    /// 캠페인 (월드 5 × 스테이지 10, 기획서 4장). 월드 1은 정식 데이터, 월드 2~5는 임시 맵(TODO).
    /// Unity 쪽 캠페인 에셋(MDSCampaignAsset)이 공유 코드로 스테이지를 덮어쓸 수 있다.
    /// </summary>
    public static class Campaign
    {
        private static readonly CampaignStage[] Stages = new CampaignStage[Rules.WORLD_COUNT * Rules.STAGES_PER_WORLD];

        public static string StageId(int world, int stage) { return world + "-" + stage; }

        public static bool TryParseStageId(string id, out int world, out int stage)
        {
            world = 0; stage = 0;
            if (string.IsNullOrEmpty(id)) return false;
            int dash = id.IndexOf('-');
            if (dash <= 0) return false;
            if (!int.TryParse(id.Substring(0, dash), out world) || !int.TryParse(id.Substring(dash + 1), out stage)) return false;
            return IsValidStage(world, stage);
        }

        public static bool IsValidStage(int world, int stage)
        {
            return world >= 1 && world <= Rules.WORLD_COUNT && stage >= 1 && stage <= Rules.STAGES_PER_WORLD;
        }

        /// <summary>월드가 소개하는 장치</summary>
        public static EditorDevice DeviceOfWorld(int world)
        {
            switch (world)
            {
                case 1: return EditorDevice.PathObstacle;
                case 2: return EditorDevice.CircleObstacle;
                case 3: return EditorDevice.KeysDoors;
                case 4: return EditorDevice.IceConveyor;
                case 5: return EditorDevice.Vanishing;
                default: return EditorDevice.None;
            }
        }

        /// <summary>월드 팔레트 (월드 1~5 = 팔레트 0~4, 5~7은 에디터 전용)</summary>
        public static int PaletteOfWorld(int world) { return Fixed.Clamp(world - 1, 0, Rules.WORLD_COUNT - 1); }

        /// <summary>스테이지 데이터 (캐시된 같은 인스턴스 — 수정하려면 Map.Clone())</summary>
        public static CampaignStage Get(int world, int stage)
        {
            if (!IsValidStage(world, stage)) throw new ArgumentOutOfRangeException("stage", StageId(world, stage));
            int index = (world - 1) * Rules.STAGES_PER_WORLD + (stage - 1);
            CampaignStage s = Stages[index];
            if (s == null)
            {
                s = new CampaignStage { World = world, Stage = stage };
                if (world == 1)
                {
                    s.Map = CampaignWorld1.Build(stage);
                }
                else
                {
                    s.Map = CampaignPlaceholders.Build(world, stage);
                    s.IsPlaceholder = true;
                }
                Stages[index] = s;
            }
            return s;
        }

        public static IEnumerable<CampaignStage> All()
        {
            for (int w = 1; w <= Rules.WORLD_COUNT; w++)
                for (int s = 1; s <= Rules.STAGES_PER_WORLD; s++)
                    yield return Get(w, s);
        }
    }

    /// <summary>스테이지별 기록 (기획서 4장: 데스 수, 클리어 시간, 0데스 별도 표시)</summary>
    public sealed class StageRecord
    {
        public bool Cleared;
        /// <summary>최소 데스 (클리어 전 -1)</summary>
        public int BestDeaths = -1;
        /// <summary>최단 클리어 틱 (클리어 전 -1)</summary>
        public int BestTicks = -1;
        public bool ZeroDeath;
        public int Clears;
        /// <summary>이 스테이지에서 죽은 총 횟수 (통계)</summary>
        public int TotalDeaths;

        public StageRecord Clone() { return (StageRecord)MemberwiseClone(); }
    }

    public struct ClearOutcome
    {
        public bool FirstClear;
        public bool NewBestDeaths;
        public bool NewBestTime;
        public bool FirstZeroDeath;
        /// <summary>이번 클리어로 새로 열린 월드 (없으면 0)</summary>
        public int UnlockedWorld;
        /// <summary>이번 클리어로 새로 해금된 에디터 장치</summary>
        public EditorDevice UnlockedDevice;
    }

    /// <summary>
    /// 캠페인 진행 저장 모델. 문자열로 저장/복원 (Unity 쪽은 PlayerPrefs "MDSCampaignSave" → PlatformSaveSync).
    /// 해금 규칙: 월드 1은 처음부터, 월드 n+1은 월드 n에서 7개 클리어 시 (막힌 스테이지가 있어도 진행 가능).
    /// 열린 월드 안의 스테이지는 모두 바로 도전할 수 있다. 월드를 "깬다"(7개 클리어)면 그 월드의 장치가 에디터에 해금된다.
    /// </summary>
    public sealed class CampaignProgress
    {
        public const string FORMAT = "MDSC1";
        private readonly Dictionary<string, StageRecord> _records = new Dictionary<string, StageRecord>();

        /// <summary>개발용: 모든 월드·장치 해금</summary>
        public bool UnlockAll;

        public StageRecord Get(int world, int stage)
        {
            StageRecord r;
            return _records.TryGetValue(Campaign.StageId(world, stage), out r) ? r : null;
        }

        public int ClearedCount(int world)
        {
            int n = 0;
            for (int s = 1; s <= Rules.STAGES_PER_WORLD; s++)
            {
                StageRecord r = Get(world, s);
                if (r != null && r.Cleared) n++;
            }
            return n;
        }

        public bool IsWorldCleared(int world) { return ClearedCount(world) >= Rules.STAGES_TO_UNLOCK_NEXT_WORLD; }

        public bool IsWorldUnlocked(int world)
        {
            if (world < 1 || world > Rules.WORLD_COUNT) return false;
            if (UnlockAll || world == 1) return true;
            return IsWorldCleared(world - 1);
        }

        public bool IsStageUnlocked(int world, int stage)
        {
            return Campaign.IsValidStage(world, stage) && IsWorldUnlocked(world);
        }

        public EditorDevice UnlockedDevices
        {
            get
            {
                if (UnlockAll) return EditorDevice.All;
                EditorDevice d = EditorDevice.None;
                for (int w = 1; w <= Rules.WORLD_COUNT; w++) if (IsWorldCleared(w)) d |= Campaign.DeviceOfWorld(w);
                return d;
            }
        }

        public int TotalDeaths
        {
            get
            {
                int n = 0;
                foreach (StageRecord r in _records.Values) n += r.TotalDeaths;
                return n;
            }
        }

        public int TotalCleared
        {
            get
            {
                int n = 0;
                foreach (StageRecord r in _records.Values) if (r.Cleared) n++;
                return n;
            }
        }

        private StageRecord GetOrCreate(int world, int stage)
        {
            string id = Campaign.StageId(world, stage);
            StageRecord r;
            if (!_records.TryGetValue(id, out r))
            {
                r = new StageRecord();
                _records[id] = r;
            }
            return r;
        }

        /// <summary>클리어하지 못하고 나간 판의 데스 (통계용)</summary>
        public void RecordDeaths(int world, int stage, int deaths)
        {
            if (!Campaign.IsValidStage(world, stage) || deaths <= 0) return;
            GetOrCreate(world, stage).TotalDeaths += deaths;
        }

        /// <summary>클리어 기록. 최소 데스·최단 시간·0데스 갱신, 월드/장치 해금 결과 반환.</summary>
        public ClearOutcome RecordClear(int world, int stage, int deaths, int ticks)
        {
            var o = new ClearOutcome();
            if (!Campaign.IsValidStage(world, stage)) return o;
            EditorDevice devicesBefore = UnlockedDevices;
            bool[] worldsBefore = new bool[Rules.WORLD_COUNT + 2];
            for (int w = 1; w <= Rules.WORLD_COUNT; w++) worldsBefore[w] = IsWorldUnlocked(w);

            StageRecord r = GetOrCreate(world, stage);
            o.FirstClear = !r.Cleared;
            r.Cleared = true;
            r.Clears++;
            r.TotalDeaths += deaths;
            if (r.BestDeaths < 0 || deaths < r.BestDeaths) { r.BestDeaths = deaths; o.NewBestDeaths = true; }
            if (r.BestTicks < 0 || ticks < r.BestTicks) { r.BestTicks = ticks; o.NewBestTime = true; }
            if (deaths == 0 && !r.ZeroDeath) { r.ZeroDeath = true; o.FirstZeroDeath = true; }

            for (int w = 1; w <= Rules.WORLD_COUNT; w++)
                if (!worldsBefore[w] && IsWorldUnlocked(w)) { o.UnlockedWorld = w; break; }
            o.UnlockedDevice = UnlockedDevices & ~devicesBefore;
            return o;
        }

        // ── 저장 ──
        // 형식: MDSC1|u=0|1-1=클리어,최소데스,최단틱,0데스,클리어수,총데스|...

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append(FORMAT).Append("|u=").Append(UnlockAll ? 1 : 0);
            var keys = new List<string>(_records.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string k in keys)
            {
                StageRecord r = _records[k];
                sb.Append('|').Append(k).Append('=')
                  .Append(r.Cleared ? 1 : 0).Append(',')
                  .Append(r.BestDeaths).Append(',')
                  .Append(r.BestTicks).Append(',')
                  .Append(r.ZeroDeath ? 1 : 0).Append(',')
                  .Append(r.Clears).Append(',')
                  .Append(r.TotalDeaths);
            }
            return sb.ToString();
        }

        /// <summary>저장 문자열 복원. 비었거나 형식이 다르면 빈 진행으로 (깨진 항목은 건너뜀).</summary>
        public static CampaignProgress Parse(string s)
        {
            var p = new CampaignProgress();
            if (string.IsNullOrEmpty(s)) return p;
            string[] parts = s.Split('|');
            if (parts.Length == 0 || parts[0] != FORMAT) return p;
            for (int i = 1; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                if (eq <= 0) continue;
                string key = parts[i].Substring(0, eq);
                string val = parts[i].Substring(eq + 1);
                if (key == "u") { p.UnlockAll = val == "1"; continue; }
                int w, st;
                if (!Campaign.TryParseStageId(key, out w, out st)) continue;
                string[] f = val.Split(',');
                if (f.Length < 6) continue;
                int[] n = new int[6];
                bool ok = true;
                for (int k = 0; k < 6; k++) if (!int.TryParse(f[k], out n[k])) ok = false;
                if (!ok) continue;
                p._records[key] = new StageRecord
                {
                    Cleared = n[0] == 1,
                    BestDeaths = n[1],
                    BestTicks = n[2],
                    ZeroDeath = n[3] == 1,
                    Clears = n[4],
                    TotalDeaths = n[5],
                };
            }
            return p;
        }
    }
}
