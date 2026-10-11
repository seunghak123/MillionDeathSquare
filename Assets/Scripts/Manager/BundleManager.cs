using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Seunghak.Manager
{
    /// <summary>
    /// Addressables 기반 에셋 관리자.
    /// - Main label: 앱 시작 시 InitializeAsync()로 자동 로드.
    /// - ingame / lobby / character_xxx label: 수동 LoadBundleAsync / UnloadBundle.
    /// - 에디터에서는 Addressables 미사용 시 AssetDatabase fallback 사용.
    /// </summary>
    public class BundleManager : UnitySingleton<BundleManager>
    {
        // ── label 이름 상수 ───────────────────────────────────────────────
        private const string LABEL_MAIN        = "main";
        private const string LABEL_INGAME      = "ingame";
        private const string LABEL_LOBBY       = "lobby";
        private const string CHARACTER_LABEL_PREFIX = "character_";

        // ── 상태 ──────────────────────────────────────────────────────────
        /// <summary>label → 해당 label로 로드된 AsyncOperationHandle 목록.</summary>
        private readonly Dictionary<string, List<AsyncOperationHandle>> _labelHandles
            = new Dictionary<string, List<AsyncOperationHandle>>();

        /// <summary>assetName → 로드된 프리팹 캐시.</summary>
        private readonly Dictionary<string, GameObject> _assetCache
            = new Dictionary<string, GameObject>();

        public bool IsMainBundleLoaded => _labelHandles.ContainsKey(LABEL_MAIN);

        // ── 초기화 ────────────────────────────────────────────────────────

        /// <summary>
        /// 앱 시작 시 호출. Addressables 런타임을 초기화하고 Main label 에셋을 로드.
        /// </summary>
        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
            var initHandle = Addressables.InitializeAsync();
            await initHandle.ToUniTask(cancellationToken: ct);
            await LoadBundleAsync(E_BUNDLE_TYPE.MAIN, null, ct);
            Debug.Log("[BundleManager] Addressables initialized and main label loaded.");
        }

        // ── 공개 로드 API ─────────────────────────────────────────────────

        /// <summary>
        /// E_BUNDLE_TYPE 기반 label의 모든 GameObject 에셋을 로드해 _assetCache에 저장.
        /// CHARACTER 타입은 suffix에 E_CHARACTER_ID 소문자(ex: "cat") 전달.
        /// </summary>
        public async UniTask LoadBundleAsync(E_BUNDLE_TYPE bundleType,
                                             string suffix = null,
                                             CancellationToken ct = default)
        {
            string label = GetLabelName(bundleType, suffix);
            await LoadLabelAsync(label, ct);
        }

        /// <summary>번들 이름 직접 지정 로드 (CharacterManager 등 레거시 호환용).</summary>
        public UniTask LoadBundleAsync(string bundleName, CancellationToken ct = default)
            => LoadLabelAsync(bundleName, ct);

        // ── 공개 언로드 API ───────────────────────────────────────────────

        /// <summary>번들 이름 직접 지정 언로드 (CharacterManager 등 레거시 호환용).</summary>
        public void UnloadBundle(string bundleName, bool unloadAllObjects = true)
        {
            if (bundleName == LABEL_MAIN)
            {
                Debug.LogWarning($"[BundleManager] Main label은 언로드 불가.");
                return;
            }
            if (_labelHandles.TryGetValue(bundleName, out var handles))
            {
                foreach (var handle in handles)
                    if (handle.IsValid()) Addressables.Release(handle);
                _labelHandles.Remove(bundleName);
                Debug.Log($"[BundleManager] label 언로드: {bundleName}");
            }
        }

        /// <summary>
        /// label에 해당하는 모든 핸들을 Release하고 캐시에서 제거.
        /// Main label은 언로드 불가 (경고만 출력).
        /// </summary>
        public void UnloadBundle(E_BUNDLE_TYPE bundleType,
                                 string suffix = null,
                                 bool unloadAll = true)
        {
            string label = GetLabelName(bundleType, suffix);

            if (bundleType == E_BUNDLE_TYPE.MAIN)
            {
                Debug.LogWarning($"[BundleManager] Main label({label})은 언로드 불가.");
                return;
            }

            if (_labelHandles.TryGetValue(label, out var handles))
            {
                foreach (var handle in handles)
                {
                    if (handle.IsValid())
                        Addressables.Release(handle);
                }
                _labelHandles.Remove(label);
                Debug.Log($"[BundleManager] label 언로드: {label}");
            }
        }

        // ── 공개 에셋 API ─────────────────────────────────────────────────

        /// <summary>
        /// _assetCache에서 assetName에 해당하는 GameObject를 반환.
        /// 없으면 null 반환.
        /// </summary>
        public GameObject GetAsset(string assetName)
        {
            if (_assetCache.TryGetValue(assetName, out var go))
                return go;

#if UNITY_EDITOR
            return LoadAssetFromAssetDatabaseFallback(assetName);
#else
            Debug.LogWarning($"[BundleManager] 캐시에 없음: {assetName}");
            return null;
#endif
        }

        /// <summary>번들 이름 직접 지정 에셋 로드 (CharacterManager 등 레거시 호환용).</summary>
        public T LoadAsset<T>(string bundleName, string assetName) where T : Object
        {
            if (_assetCache.TryGetValue(assetName, out var go))
                return go as T;
#if UNITY_EDITOR
            var fallback = LoadAssetFromAssetDatabaseFallback(assetName);
            return fallback as T;
#else
            Debug.LogWarning($"[BundleManager] 캐시에 없음: {assetName} (bundle: {bundleName})");
            return null;
#endif
        }

        /// <summary>
        /// 캐시에서 에셋 반환. 번들이 로드되어 있지 않으면 null (LogError 출력).
        /// </summary>
        public T LoadAsset<T>(E_BUNDLE_TYPE bundleType,
                              string assetName,
                              string suffix = null) where T : Object
        {
            string label = GetLabelName(bundleType, suffix);
            if (!_labelHandles.ContainsKey(label))
            {
                Debug.LogError($"[BundleManager] label 미로드: {label}. LoadBundleAsync 먼저 호출 필요.");
                return null;
            }

            if (typeof(T) == typeof(GameObject) || typeof(T).IsSubclassOf(typeof(GameObject)))
            {
                if (_assetCache.TryGetValue(assetName, out var go))
                    return go as T;
            }

            Debug.LogWarning($"[BundleManager] 캐시에 없음: {assetName}");
            return null;
        }

        // ── 상태 조회 ─────────────────────────────────────────────────────

        public bool IsBundleLoaded(E_BUNDLE_TYPE bundleType, string suffix = null)
        {
            string label = GetLabelName(bundleType, suffix);
            return _labelHandles.ContainsKey(label);
        }

        // ── 유틸리티 ──────────────────────────────────────────────────────

        /// <summary>E_BUNDLE_TYPE → Addressables label 이름 변환 (내부 전용).</summary>
        private static string GetLabelName(E_BUNDLE_TYPE type, string suffix = null)
        {
            return type switch
            {
                E_BUNDLE_TYPE.MAIN      => LABEL_MAIN,
                E_BUNDLE_TYPE.INGAME    => LABEL_INGAME,
                E_BUNDLE_TYPE.LOBBY     => LABEL_LOBBY,
                E_BUNDLE_TYPE.CHARACTER => CHARACTER_LABEL_PREFIX + (suffix ?? ""),
                _                       => string.Empty,
            };
        }

        /// <summary>캐릭터 ID → label suffix (소문자 변환).</summary>
        public static string GetCharacterSuffix(E_CHARACTER_ID characterId)
            => characterId.ToString().ToLower();

        // ── 내부 구현 ─────────────────────────────────────────────────────

        private async UniTask LoadLabelAsync(string label, CancellationToken ct)
        {
            if (_labelHandles.ContainsKey(label))
            {
                Debug.Log($"[BundleManager] 이미 로드됨: {label}");
                return;
            }

            var handles = new List<AsyncOperationHandle>();

            // label에 해당하는 에셋이 없으면 건너뜀
            var locHandle = Addressables.LoadResourceLocationsAsync(label, typeof(GameObject));
            await locHandle.ToUniTask(cancellationToken: ct);
            int locCount = locHandle.Status == AsyncOperationStatus.Succeeded ? locHandle.Result.Count : 0;
            Addressables.Release(locHandle);

            if (locCount == 0)
            {
                Debug.LogWarning($"[BundleManager] label '{label}'에 등록된 에셋 없음. 건너뜀.");
                _labelHandles[label] = handles;
                return;
            }

            var loadHandle = Addressables.LoadAssetsAsync<GameObject>(
                label,
                asset => { _assetCache[asset.name] = asset; });

            await loadHandle.ToUniTask(cancellationToken: ct);

            if (loadHandle.Status == AsyncOperationStatus.Succeeded)
            {
                handles.Add(loadHandle);
                _labelHandles[label] = handles;
                Debug.Log($"[BundleManager] label 로드 완료: {label} ({loadHandle.Result.Count}개)");
            }
            else
            {
#if UNITY_EDITOR
                Debug.Log($"[BundleManager] 에디터 fallback 모드: {label}");
                _labelHandles[label] = handles;
#else
                Debug.LogError($"[BundleManager] label 로드 실패: {label}");
#endif
            }
        }

#if UNITY_EDITOR
        private static GameObject LoadAssetFromAssetDatabaseFallback(string assetName)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"{assetName} t:Prefab");
            foreach (var guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fileName == assetName)
                {
                    return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }
            Debug.LogWarning($"[BundleManager][Editor] 프리팹 없음: {assetName}");
            return null;
        }
#endif

        private void OnDestroy()
        {
            foreach (var handles in _labelHandles.Values)
                foreach (var handle in handles)
                    if (handle.IsValid()) Addressables.Release(handle);
            _labelHandles.Clear();
            _assetCache.Clear();
        }
    }
}
