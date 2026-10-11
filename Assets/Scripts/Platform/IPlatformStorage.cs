using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Seunghak.Platform
{
    /// <summary>
    /// 플랫폼별 영속 저장소 추상화 (키-값 문자열).
    /// PlayerPrefs는 동기 로컬 캐시로 쓰고, 이 저장소가 플랫폼 측 원본 역할을 합니다.
    ///
    /// 구현체:
    ///   - PlayerPrefsStorage — 기본 (에디터 / 전용 저장소가 없는 플랫폼)
    ///   - TossStorage        — 토스 네이티브 Storage (AIT.Storage*)
    /// </summary>
    public interface IPlatformStorage
    {
        /// <summary>값 조회. 키가 없거나 실패하면 null.</summary>
        UniTask<string> GetItemAsync(string key);

        /// <summary>
        /// 값 조회 — "키 없음"과 "읽기 실패"를 구분합니다.
        /// ok=true, value=null 이면 키 없음. ok=false 이면 읽기 실패(저장소 상태를 알 수 없음).
        /// </summary>
        UniTask<(bool ok, string value)> TryGetItemAsync(string key);

        /// <summary>값 저장. 성공 여부 반환.</summary>
        UniTask<bool> SetItemAsync(string key, string value);

        /// <summary>키 삭제. 성공 여부 반환.</summary>
        UniTask<bool> RemoveItemAsync(string key);
    }

    /// <summary>PlayerPrefs 기반 기본 저장소.</summary>
    public sealed class PlayerPrefsStorage : IPlatformStorage
    {
        public static readonly PlayerPrefsStorage Instance = new PlayerPrefsStorage();

        public UniTask<string> GetItemAsync(string key)
            => UniTask.FromResult(PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null);

        public UniTask<(bool ok, string value)> TryGetItemAsync(string key)
            => UniTask.FromResult((true, PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null));

        public UniTask<bool> SetItemAsync(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
            return UniTask.FromResult(true);
        }

        public UniTask<bool> RemoveItemAsync(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return UniTask.FromResult(true);
        }
    }
}
