using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Seunghak
{
    /// <summary>
    /// AssetBundle 로드 결과를 감싸는 컨테이너.
    /// </summary>
    public class LoadedAssetBundle
    {
        public AssetBundle assetBundle;
        public int referencedCount;

        internal event Action unload;

        internal void OnUnload()
        {
            assetBundle.Unload(false);
            if (unload != null)
                unload();
        }

        public LoadedAssetBundle(AssetBundle assetBundle)
        {
            this.assetBundle = assetBundle;
            referencedCount = 1;
        }
    }

    /// <summary>
    /// 레거시 AssetBundle 매니저 (AssetBundleLoadOperation 호환용).
    /// 신규 코드는 Seunghak.Manager.BundleManager를 사용하세요.
    /// </summary>
    public class AssetBundleManager : UnitySingleton<AssetBundleManager>
    {
        // ── 에디터 시뮬레이션 ─────────────────────────────────────────────
#if UNITY_EDITOR
        static bool _simulateAssetBundleInEditor = true;
        const string kSimulateAssetBundles = "SimulateAssetBundles";

        public static bool SimulateAssetBundleInEditor
        {
            get { return UnityEditor.EditorPrefs.GetBool(kSimulateAssetBundles, true); }
            set { UnityEditor.EditorPrefs.SetBool(kSimulateAssetBundles, value); }
        }
#else
        public static bool SimulateAssetBundleInEditor { get { return false; } }
#endif

        // ── 매니페스트 ────────────────────────────────────────────────────
        static AssetBundleManifest _assetBundleManifest;
        public static AssetBundleManifest AssetBundleManifestObject
        {
            get { return _assetBundleManifest; }
            set { _assetBundleManifest = value; }
        }

        // ── 로드된 번들 캐시 ──────────────────────────────────────────────
        static readonly Dictionary<string, LoadedAssetBundle> _loadedAssetBundles
            = new Dictionary<string, LoadedAssetBundle>();

        static readonly Dictionary<string, string> _downloadingErrors
            = new Dictionary<string, string>();

        // ── 공개 API ──────────────────────────────────────────────────────

        /// <summary>
        /// 초기화 완료 여부를 코루틴으로 대기합니다.
        /// GameResourceManager.SetDownloadDatas() 에서 사용.
        /// </summary>
        public IEnumerator GetReadyStatus()
        {
            // 매니페스트가 설정될 때까지 대기
            while (_assetBundleManifest == null)
            {
#if UNITY_EDITOR
                if (SimulateAssetBundleInEditor)
                    break; // 에디터 시뮬레이션 모드에서는 즉시 통과
#endif
                yield return null;
            }
        }

        /// <summary>
        /// 이름으로 로드된 번들을 조회합니다.
        /// </summary>
        public static LoadedAssetBundle GetLoadedAssetBundle(string assetBundleName, out string error)
        {
            if (_downloadingErrors.TryGetValue(assetBundleName, out error))
                return null;

            LoadedAssetBundle bundle;
            _loadedAssetBundles.TryGetValue(assetBundleName, out bundle);

            if (bundle == null)
                return null;

            // 의존성 확인
            if (_assetBundleManifest != null)
            {
                string[] dependencies = _assetBundleManifest.GetAllDependencies(assetBundleName);
                foreach (string dependency in dependencies)
                {
                    if (_downloadingErrors.TryGetValue(dependency, out error))
                        return null;

                    LoadedAssetBundle dependentBundle;
                    _loadedAssetBundles.TryGetValue(dependency, out dependentBundle);
                    if (dependentBundle == null)
                        return null;
                }
            }

            error = null;
            return bundle;
        }

        /// <summary>
        /// 번들을 캐시에 등록합니다 (다운로드 완료 후 호출).
        /// </summary>
        public static void RegisterLoadedBundle(string assetBundleName, LoadedAssetBundle bundle)
        {
            if (_loadedAssetBundles.ContainsKey(assetBundleName))
            {
                _loadedAssetBundles[assetBundleName].referencedCount++;
            }
            else
            {
                _loadedAssetBundles[assetBundleName] = bundle;
            }
        }

        /// <summary>
        /// 번들을 언로드합니다.
        /// </summary>
        public static void UnloadAssetBundle(string assetBundleName)
        {
            LoadedAssetBundle bundle;
            if (!_loadedAssetBundles.TryGetValue(assetBundleName, out bundle))
                return;

            bundle.referencedCount--;
            if (bundle.referencedCount <= 0)
            {
                bundle.OnUnload();
                _loadedAssetBundles.Remove(assetBundleName);
            }
        }

        /// <summary>
        /// 다운로드 에러를 등록합니다.
        /// </summary>
        public static void RegisterDownloadError(string assetBundleName, string error)
        {
            if (!_downloadingErrors.ContainsKey(assetBundleName))
                _downloadingErrors[assetBundleName] = error;
        }

        private void OnDestroy()
        {
            foreach (var bundle in _loadedAssetBundles.Values)
            {
                bundle?.OnUnload();
            }
            _loadedAssetBundles.Clear();
            _downloadingErrors.Clear();
        }
    }
}
