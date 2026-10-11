using System;
using UnityEngine;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>캠페인 스테이지 덮어쓰기 (에디터로 만든 맵의 공유 코드를 넣으면 코어 기본 데이터 대신 사용)</summary>
    [CreateAssetMenu(menuName = "Million Death Square/Campaign", fileName = "MDSCampaign")]
    public sealed class MDSCampaignAsset : ScriptableObject
    {
        [Serializable]
        public struct StageOverride
        {
            public string stageId;     // "2-3"
            [TextArea(2, 6)] public string shareCode;
        }

        [Tooltip("비어 있으면 코어(Campaign) 기본 스테이지를 쓴다")]
        public StageOverride[] overrides = new StageOverride[0];

        public CampaignStage GetStage(int world, int stage)
        {
            CampaignStage s = Campaign.Get(world, stage);
            string id = Campaign.StageId(world, stage);
            if (overrides != null)
            {
                for (int i = 0; i < overrides.Length; i++)
                {
                    if (overrides[i].stageId != id || string.IsNullOrEmpty(overrides[i].shareCode)) continue;
                    MapData map;
                    string error;
                    if (MapSerializer.TryParseShareCode(overrides[i].shareCode, out map, out error) && MapValidator.Validate(map).IsValid)
                        return new CampaignStage { World = world, Stage = stage, Map = map, IsPlaceholder = false };
                    Debug.LogWarning("[MDSCampaignAsset] " + id + " 덮어쓰기 공유 코드가 잘못되어 기본 맵을 씁니다: " + error);
                }
            }
            return s;
        }
    }
}
