using UnityEngine;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// 화석 단계 테이블 (기획서 4장: 모래알 → … → 공룡 골격, 11단계).
    /// 인덱스가 곧 단계(tier, 0부터)이며 같은 단계 2개가 닿으면 다음 단계로 합체한다.
    /// 마지막 단계(공룡 골격)가 완성되면 공룡 부활 이벤트가 일어난다.
    /// 합체 점수는 SuikaRules.MergeScore(단계)로 계산한다 (mergeScore 필드는 쓰지 않음).
    ///
    /// 프로토타입 그래픽: Tools/Fossil Merge/1. 화석 프리팹 + 테이블 생성 (단계별 색/크기 노듈 구체)
    /// </summary>
    [CreateAssetMenu(fileName = "FossilTable", menuName = "Seunghak/Fossil Merge/Fossil Table")]
    public class SuikaFruitTable : ScriptableObject
    {
        [System.Serializable]
        public class Tier
        {
            public string displayName;
            [Tooltip("루트에 SuikaFruit + Rigidbody + SphereCollider(radius 0.5)가 붙은 프리팹. 지름 1 기준.")]
            public GameObject prefab;
            [Tooltip("실제 반지름 (월드 단위)")]
            public float radius = 0.5f;
            public float mass = 1f;
            [Tooltip("(미사용 — SuikaRules.MergeScore로 계산)")]
            public int mergeScore = 1;
            [Tooltip("드롭 후보 가중치 (0이면 드롭으로는 나오지 않음)")]
            public float spawnWeight = 0f;
        }

        public Tier[] tiers;

        [Tooltip("(예외 상황) 마지막 단계 2개가 닿았을 때 보너스 점수 — 보통은 공룡 부활로 사라져 일어나지 않음")]
        public int maxTierClearBonus = 100;

        public int LastTier => Count - 1;

        public int Count => tiers != null ? tiers.Length : 0;

        public Tier Get(int tier) => tiers[tier];

        /// <summary>spawnWeight 기반 랜덤 단계 선택 (할당 없음).</summary>
        public int PickSpawnTier()
        {
            float total = 0f;
            for (int i = 0; i < tiers.Length; i++) total += tiers[i].spawnWeight;
            if (total <= 0f) return 0;

            float r = Random.value * total;
            for (int i = 0; i < tiers.Length; i++)
            {
                r -= tiers[i].spawnWeight;
                if (r <= 0f) return i;
            }
            return 0;
        }
    }
}
