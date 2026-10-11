using System;
using AppsInToss;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>
    /// 토스 네이티브 Storage 구현 (AIT.Storage* 직접 호출).
    /// 예외/타임아웃은 여기서 삼키고 null / false 로 돌려줍니다.
    /// </summary>
    public sealed class TossStorage : IPlatformStorage
    {
        /// <summary>브릿지 응답 대기 한도(ms). 앱 시작이 무한 대기하지 않게 합니다.</summary>
        private const int TimeoutMs = 5000;

        public async UniTask<string> GetItemAsync(string key)
        {
            var (_, value) = await TryGetItemAsync(key);
            return value;
        }

        public async UniTask<(bool ok, string value)> TryGetItemAsync(string key)
        {
            try
            {
                string value = await AIT.StorageGetItem(key, TimeoutMs);
                // 키가 없으면 JS null이 JSON "null" 그대로 넘어온다 (SDK가 따옴표 없는 값은 원문 그대로 반환).
                // 에디터 mock은 ""를 반환한다. 둘 다 "값 없음"으로 취급.
                if (string.IsNullOrEmpty(value) || value == "null")
                    return (true, null);
                return (true, value);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TossStorage] GetItem 실패 (key={key}): {e.Message}");
                return (false, null);
            }
        }

        public async UniTask<bool> SetItemAsync(string key, string value)
        {
            try
            {
                await AIT.StorageSetItem(key, value ?? string.Empty, TimeoutMs);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TossStorage] SetItem 실패 (key={key}): {e.Message}");
                return false;
            }
        }

        public async UniTask<bool> RemoveItemAsync(string key)
        {
            try
            {
                await AIT.StorageRemoveItem(key, TimeoutMs);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TossStorage] RemoveItem 실패 (key={key}): {e.Message}");
                return false;
            }
        }
    }
}
