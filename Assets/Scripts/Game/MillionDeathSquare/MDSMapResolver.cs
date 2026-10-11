using Cysharp.Threading.Tasks;
using Seunghak.Platform;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>맵 참조를 풀어 낸 결과</summary>
    public sealed class ResolvedMap
    {
        public MapData Map;
        public string Title = "";
        /// <summary>원래 참조 ("campaign:1-3" / 맵 코드 / 공유 코드)</summary>
        public string MapRef = "";
        /// <summary>순위표 키 (RecordBoards)</summary>
        public string RecordKey = "";
        public ulong WorkshopId;
        public WorkshopItem Item;
        public int World;
        public int Stage;
        public string Error;
        public bool Ok { get { return Map != null; } }
        public bool IsCampaign { get { return World > 0; } }
    }

    /// <summary>
    /// 맵 찾기 (기획서 11장 맵 찾기 화면): 맵 코드(창작마당) / 공유 코드 붙여넣기(모바일판 맵) / 캠페인 ID.
    /// </summary>
    public static class MDSMapResolver
    {
        public const string CAMPAIGN_PREFIX = "campaign:";

        public static string CampaignRef(int world, int stage) { return CAMPAIGN_PREFIX + Campaign.StageId(world, stage); }

        public static ResolvedMap ResolveCampaign(int world, int stage, MDSCampaignAsset asset)
        {
            CampaignStage s = asset != null ? asset.GetStage(world, stage) : Campaign.Get(world, stage);
            return new ResolvedMap
            {
                Map = s.Map,
                Title = s.Id + " " + s.Name,
                MapRef = CampaignRef(world, stage),
                RecordKey = RecordBoards.CampaignKey(world, stage),
                World = world,
                Stage = stage,
            };
        }

        public static async UniTask<ResolvedMap> ResolveAsync(string mapRef, MDSCampaignAsset asset = null)
        {
            string s = (mapRef ?? "").Trim();
            if (s.Length == 0) return new ResolvedMap { Error = "맵 코드를 입력하세요" };

            if (s.StartsWith(CAMPAIGN_PREFIX))
            {
                int w, st;
                if (!Campaign.TryParseStageId(s.Substring(CAMPAIGN_PREFIX.Length), out w, out st)) return new ResolvedMap { Error = "잘못된 캠페인 스테이지" };
                return ResolveCampaign(w, st, asset);
            }

            if (s.StartsWith(MapSerializer.SHARE_PREFIX))
            {
                MapData map;
                string error;
                if (!MapSerializer.TryParseShareCode(s, out map, out error)) return new ResolvedMap { Error = error };
                ValidationResult v = MapValidator.Validate(map);
                if (!v.IsValid) return new ResolvedMap { Error = "규칙에 맞지 않는 맵입니다: " + v };
                return new ResolvedMap { Map = map, Title = map.Name, MapRef = s, RecordKey = RecordBoards.HashKey(map.ContentHash()) };
            }

            ulong id;
            if (!MapCode.TryDecode(s, out id)) return new ResolvedMap { Error = "맵 코드가 올바르지 않습니다 (오타를 확인하세요)" };
            WorkshopItem item = await MDSServices.Workshop.ResolveMapCodeAsync(s);
            if (item == null || item.Map == null) return new ResolvedMap { Error = "맵을 찾을 수 없습니다" };
            if (!MapValidator.Validate(item.Map).IsValid) return new ResolvedMap { Error = "규칙에 맞지 않는 맵입니다" };
            return new ResolvedMap
            {
                Map = item.Map,
                Title = item.Title,
                MapRef = MapCode.Encode(id),
                RecordKey = RecordBoards.WorkshopKey(id),
                WorkshopId = id,
                Item = item,
            };
        }
    }
}
