using System.Collections.Generic;
using UnityEngine;

namespace Seunghak.Game.Suika3D
{
    /// <summary>
    /// 단계별 과일 오브젝트 풀. 게임플레이 중 Instantiate/Destroy를 피하기 위해 시작 시 미리 채운다.
    /// </summary>
    public class SuikaFruitPool
    {
        private readonly SuikaFruitTable _table;
        private readonly Transform _root;
        private readonly Stack<SuikaFruit>[] _pools;

        public SuikaFruitPool(SuikaFruitTable table, Transform root)
        {
            _table = table;
            _root = root;
            _pools = new Stack<SuikaFruit>[table.Count];
            for (int i = 0; i < _pools.Length; i++)
                _pools[i] = new Stack<SuikaFruit>(16);
        }

        public void Prewarm(int countPerTier)
        {
            for (int t = 0; t < _pools.Length; t++)
                for (int i = 0; i < countPerTier; i++)
                    _pools[t].Push(Create(t));
        }

        public SuikaFruit Get(int tier)
        {
            SuikaFruit fruit = _pools[tier].Count > 0 ? _pools[tier].Pop() : Create(tier);
            fruit.gameObject.SetActive(true);
            return fruit;
        }

        public void Release(SuikaFruit fruit)
        {
            fruit.gameObject.SetActive(false);
            _pools[fruit.Tier].Push(fruit);
        }

        private SuikaFruit Create(int tier)
        {
            GameObject prefab = _table.Get(tier).prefab;
            GameObject go = Object.Instantiate(prefab, _root);
            go.name = prefab.name;
            go.SetActive(false);

            var fruit = go.GetComponent<SuikaFruit>();
            if (fruit == null) fruit = go.AddComponent<SuikaFruit>();
            // Release 시 Tier로 풀을 찾으므로, 생성 직후 풀에 들어갈 때를 위해 단계만 기록
            fruit.Setup(null, tier, _table.Get(tier));
            return fruit;
        }
    }
}
