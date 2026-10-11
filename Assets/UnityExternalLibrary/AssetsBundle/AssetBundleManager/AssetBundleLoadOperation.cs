using UnityEngine;
#if UNITY_5_3 || UNITY_5_3_OR_NEWER
using UnityEngine.SceneManagement;
#endif
#if UNITY_5_4_OR_NEWER
using UnityEngine.Networking;
using System.Text;
using System;
using Seunghak.Common;
using Cysharp.Threading.Tasks;
#endif
#if ENABLE_IOS_ON_DEMAND_RESOURCES
using UnityEngine.iOS;
#endif
using System.Collections;

namespace Seunghak
{
    public abstract class AssetBundleLoadOperation : IEnumerator
    {
        public object Current
        {
            get
            {
                return null;
            }
        }
        virtual public long GetBundleSize()
        {
            return 0;
        }
        virtual public long GetDownloadedBundleSize()
        {
            return 0;
        }
        public bool MoveNext()
        {
            return !IsDone();
        }

        public void Reset()
        {
        }

        virtual public float Progress()
        {
            return 0;
        }

        abstract public bool Update();

        abstract public bool IsDone();
    }

    public abstract class AssetBundleDownloadOperation : AssetBundleLoadOperation
    {
        bool done;

        public string assetBundleName { get; private set; }
        public LoadedAssetBundle assetBundle { get; protected set; }
        public string error { get; protected set; }

        protected abstract bool downloadIsDone { get; }
        protected abstract void FinishDownload();
        public override bool Update()
        {
            if (!done && downloadIsDone)
            {
                FinishDownload();
                done = true;
            }

            return !done;
        }

        public override bool IsDone()
        {
            return done;
        }

        public abstract string GetSourceURL();

        public AssetBundleDownloadOperation(string assetBundleName)
        {
            this.assetBundleName = assetBundleName;
        }
    }

#if ENABLE_IOS_ON_DEMAND_RESOURCES
    // Read asset bundle asynchronously from iOS / tvOS asset catalog that is downloaded
    // using on demand resources functionality.
    public class AssetBundleDownloadFromODROperation : AssetBundleDownloadOperation
    {
        OnDemandResourcesRequest request;

        public AssetBundleDownloadFromODROperation(string assetBundleName)
            : base(assetBundleName)
        {
            // Work around Xcode crash when opening Resources tab when a 
            // resource name contains slash character
            request = OnDemandResources.PreloadAsync(new string[] { assetBundleName.Replace('/', '>') });
        }

        protected override bool downloadIsDone { get { return (request == null) || request.isDone; } }

        public override string GetSourceURL()
        {
            return "odr://" + assetBundleName;
        }

        protected override void FinishDownload()
        {
            error = request.error;
            if (error != null)
                return;

            var path = "res://" + assetBundleName;
#if UNITY_5_3 || UNITY_5_3_OR_NEWER
            var bundle = AssetBundle.LoadFromFile(path);
#else
            var bundle = AssetBundle.CreateFromFile(path);
#endif
            if (bundle == null)
            {
                error = string.Format("Failed to load {0}", path);
                request.Dispose();
            }
            else
            {
                assetBundle = new LoadedAssetBundle(bundle);
                // At the time of unload request is already set to null, so capture it to local variable.
                var localRequest = request;
                // Dispose of request only when bundle is unloaded to keep the ODR pin alive.
                assetBundle.unload += () =>
                {
                    localRequest.Dispose();
                };
            }

            request = null;
        }
    }
#endif

#if ENABLE_IOS_APP_SLICING
    // Read asset bundle synchronously from an iOS / tvOS asset catalog
    public class AssetBundleOpenFromAssetCatalogOperation : AssetBundleDownloadOperation
    {
        public AssetBundleOpenFromAssetCatalogOperation(string assetBundleName)
            : base(assetBundleName)
        {
            var path = "res://" + assetBundleName;
#if UNITY_5_3 || UNITY_5_3_OR_NEWER
            var bundle = AssetBundle.LoadFromFile(path);
#else
            var bundle = AssetBundle.CreateFromFile(path);
#endif
            if (bundle == null)
                error = string.Format("Failed to load {0}", path);
            else
                assetBundle = new LoadedAssetBundle(bundle);
        }

        protected override bool downloadIsDone { get { return true; } }

        protected override void FinishDownload() {}

        public override string GetSourceURL()
        {
            return "res://" + assetBundleName;
        }
    }
#endif

#if UNITY_5_4_OR_NEWER
    public class AssetBundleDownloadFromWebOperation : AssetBundleDownloadOperation
    {
        private UnityWebRequest webRequest;
        private AsyncOperation webOperation;
        private string weburl;

        public AssetBundleDownloadFromWebOperation(string assetBundleName, UnityWebRequest request)
            : base(assetBundleName)
        {
            if (request == null)
                throw new System.ArgumentNullException("request");

            weburl = request.url;
            webRequest = request;
            webOperation = request.SendWebRequest();
        }

        protected override bool downloadIsDone { get { return (webOperation == null) || webOperation.isDone; } }

        protected override void FinishDownload()
        {
            error = webRequest?.error;
            if (!string.IsNullOrEmpty(error))
            {
                return;
            }

            var handler = webRequest.downloadHandler;
            AssetBundle bundle = AssetBundle.LoadFromMemory(handler.data);
            if (bundle == null)
            {
                error = string.Format("{0} is not a valid asset bundle.", assetBundleName);
            }
            else
            {
                assetBundle = new LoadedAssetBundle(bundle);
            }

            webRequest?.Dispose();
            webRequest = null;
            webOperation = null;
        }

        public override string GetSourceURL()
        {
            return weburl;
        }
    }
#else
    public class AssetBundleDownloadFromWebOperation : AssetBundleDownloadOperation
    {
        WWW wwwUrl;
        string weburl;

        public AssetBundleDownloadFromWebOperation(string assetBundleName, WWW www)
            : base(assetBundleName)
        {
            if (www == null)
                throw new System.ArgumentNullException("www");
            weburl = www.url;
            this.wwwUrl = www;
        }

        protected override bool downloadIsDone { get { return (wwwUrl == null) || wwwUrl.isDone; } }

        protected override void FinishDownload()
        {
            error = wwwUrl.error;
            if (!string.IsNullOrEmpty(error))
            {
                return;
            }

            AssetBundle bundle = wwwUrl.assetBundle;
            if (bundle == null)
            {
                error = string.Format("{0} is not a valid asset bundle.", assetBundleName);
            }
            else
            {
                assetBundle = new LoadedAssetBundle(wwwUrl.assetBundle);
            }

            wwwUrl.Dispose();
            wwwUrl = null;
        }

        public override string GetSourceURL()
        {
            return weburl;
        }
    }
#endif

#if UNITY_5_4_OR_NEWER
    public class AssetBundleDownloadWebRequestOperation : AssetBundleDownloadOperation
    {
        private UnityWebRequest webRequest;
        private AsyncOperation webOperation;
        private string webUrl;

        public AssetBundleDownloadWebRequestOperation(string assetBundleName, UnityWebRequest request)
            : base(assetBundleName)
        {
            if (request == null)
                throw new System.ArgumentNullException("request");
            webUrl = request.url;
            webRequest = request;
            webOperation = request.SendWebRequest();
        }
        public override float Progress()
        {
            float fValue = 0;
            if (webOperation != null)
                fValue = webOperation.progress;

            return fValue;
        }
        protected override bool downloadIsDone { get { return (webOperation == null) || webOperation.isDone; } }

        protected override void FinishDownload()
        {
            error = webRequest.error;
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError(error);
                return;
            }

            var handler = webRequest.downloadHandler ;

            AssetBundle bundle = AssetBundle.LoadFromMemory(handler.data);
            if (bundle == null)
            {
                error = string.Format("{0} is not a valid asset bundle.", assetBundleName);
                Debug.Log(assetBundleName + "is not valid assets");
            }
            else
            {
                CommonUtil.SaveAssetBundleHash(assetBundleName, bundle.GetHashCode());
                FileUtils.SaveFile<byte[]>(Application.persistentDataPath, assetBundleName, webRequest.downloadHandler.data);
            }
            assetBundle = new LoadedAssetBundle(bundle);
            webRequest.Dispose();
            webRequest = null;
            webOperation = null;
        }

        public override string GetSourceURL()
        {
            return webUrl;
        }
    }

    public class AssetBundleDownloadFileOperation : AssetBundleDownloadOperation
    {
        AssetBundleCreateRequest assetBundleRequest;
        string webUrl;

        public AssetBundleDownloadFileOperation(string assetBundleName, string url, uint crc = 0, ulong offset = 0)
            : base(assetBundleName)
        {
            Debug.Log("CreateDonwloadFile");
            assetBundleRequest = AssetBundle.LoadFromFileAsync(url, crc, offset);
            webUrl = url;
        }
        public override float Progress()
        {
            float fValue = 0;
            if (assetBundleRequest != null)
                fValue = assetBundleRequest.progress;

            return fValue;
        }
        protected override bool downloadIsDone { get { return (assetBundleRequest == null) || assetBundleRequest.isDone; } }

        protected override void FinishDownload()
        {
            AssetBundle bundle = assetBundleRequest.assetBundle;
            if (bundle == null)
            {
                error = string.Format("failed to load assetBundle {0}.", assetBundleName);
                return;
            }

            if (bundle == null)
            {
                error = string.Format("{0} is not a valid asset bundle.", assetBundleName);
            }
            else
            {
                assetBundle = new LoadedAssetBundle(bundle);
            }
            assetBundleRequest = null;
        }

        public override string GetSourceURL()
        {
            return webUrl;
        }
    }
#endif

#if UNITY_EDITOR
    public class AssetBundleLoadLevelSimulationOperation : AssetBundleLoadOperation
    {
        AsyncOperation webOperation = null;

        public AssetBundleLoadLevelSimulationOperation(string assetBundleName, string levelName, bool isAdditive)
        {
            string[] levelPaths = UnityEditor.AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(assetBundleName, levelName);
            if (levelPaths.Length == 0)
            {
                ///@TODO: The error needs to differentiate that an asset bundle name doesn't exist
                //        from that there right scene does not exist in the asset bundle...

                Debug.LogError("There is no scene with name \"" + levelName + "\" in " + assetBundleName);
                return;
            }

            if (isAdditive)
            {
                webOperation = UnityEditor.EditorApplication.LoadLevelAdditiveAsyncInPlayMode(levelPaths[0]);
            }
            else
            {
                webOperation = UnityEditor.EditorApplication.LoadLevelAsyncInPlayMode(levelPaths[0]);
            }
        }
        public override float Progress()
        {
            float fValue = 0;
            if (webOperation != null)
                fValue = webOperation.progress;

            return fValue;
        }
        public override bool Update()
        {
            return false;
        }

        public override bool IsDone()
        {
            return webOperation == null || webOperation.isDone;
        }
    }
#endif

    public class AssetBundleLoadLevelOperation : AssetBundleLoadOperation
    {
        protected string assetBundleName;
        protected string levelName;
        protected bool isAdditive;
        protected string downloadingError;
        protected AsyncOperation webOperation;

        public AssetBundleLoadLevelOperation(string assetbundleName, string levelName, bool isAdditive)
        {
            assetBundleName = assetbundleName;
            this.levelName = levelName;
            this.isAdditive = isAdditive;
        }

        public override bool Update()
        {
            if (webOperation != null)
                return false;

            LoadedAssetBundle bundle = AssetBundleManager.GetLoadedAssetBundle(assetBundleName, out downloadingError);
            if (bundle != null)
            {
#if UNITY_5_3 || UNITY_5_3_OR_NEWER
                webOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(levelName, isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single);
#else
                if (m_IsAdditive)
                    m_Request = Application.LoadLevelAdditiveAsync(m_LevelName);
                else
                    m_Request = Application.LoadLevelAsync(m_LevelName);
#endif
                return false;
            }
            else
                return true;
        }

        public override bool IsDone()
        {
            // Return if meeting downloading error.
            // m_DownloadingError might come from the dependency downloading.
            if (webOperation == null && downloadingError != null)
            {
                Debug.LogError(downloadingError);
                return true;
            }

            return webOperation != null && webOperation.isDone;
        }
    }

    public abstract class AssetBundleLoadAssetOperation : AssetBundleLoadOperation
    {
        public abstract T GetAsset<T>() where T : UnityEngine.Object;
    }

    public class AssetBundleLoadAssetOperationSimulation : AssetBundleLoadAssetOperation
    {
        UnityEngine.Object simulationObject;

        public AssetBundleLoadAssetOperationSimulation(UnityEngine.Object simulatedObject)
        {
            simulationObject = simulatedObject;
        }

        public override T GetAsset<T>()
        {
            return simulationObject as T;
        }

        public override bool Update()
        {
            return false;
        }

        public override bool IsDone()
        {
            return true;
        }
    }

    public class AssetBundleLoadAssetOperationFull : AssetBundleLoadAssetOperation
    {
        protected string assetBundleName;
        protected string assetName;
        protected string downloadingError;
        protected System.Type systemType;
        protected AssetBundleRequest webRequest = null;

        public AssetBundleLoadAssetOperationFull(string bundleName, string assetName, System.Type type)
        {
            assetBundleName = bundleName;
            this.assetName = assetName;
            systemType = type;
        }

        public override T GetAsset<T>()
        {
            if (webRequest != null && webRequest.isDone)
                return webRequest.asset as T;
            else
                return null;
        }

        // Returns true if more Update calls are required.
        public override bool Update()
        {
            if (webRequest != null)
                return false;

            LoadedAssetBundle bundle = AssetBundleManager.GetLoadedAssetBundle(assetBundleName, out downloadingError);
            if (bundle != null)
            {
                ///@TODO: When asset bundle download fails this throws an exception...
                ///
               
                if (bundle.assetBundle != null)
                {
                    webRequest = bundle.assetBundle.LoadAssetAsync(assetName, systemType);
                }
                return false;
            }
            else
            {
                return true;
            }
        }

        public override bool IsDone()
        {
            // Return if meeting downloading error.
            // m_DownloadingError might come from the dependency downloading.
            if (webRequest == null && downloadingError != null)
            {
                Debug.LogError(downloadingError);
                return true;
            }

            return webRequest != null && webRequest.isDone;
        }
        public async UniTask<bool> IsDoneAsync(Action waitAction = null)
        {
            // Check for errors immediately if the request is not started or an error occurred during setup.
            if (webRequest == null && downloadingError != null)
            {
                Debug.LogError(downloadingError);
                return true;
            }

            // Await the completion of the web request if it exists.
            if (webRequest != null)
            {
                // Wait until the request is done
                await UniTask.WaitUntil(() => webRequest.isDone);
                if (waitAction != null)
                {
                    waitAction();
                }
                Debug.LogError($"WebRequest Error: {downloadingError}");
                
            }

            return webRequest != null && webRequest.isDone;
        }
    }

    public class AssetBundleLoadManifestOperation : AssetBundleLoadAssetOperationFull
    {
        public AssetBundleLoadManifestOperation(string bundleName, string assetName, System.Type type)
            : base(bundleName, assetName, type)
        {
            Debug.Log("Create Operation " + bundleName);
        }

        public override bool Update()
        {
            base.Update();

            if (webRequest != null && webRequest.isDone)
            {
                AssetBundleManager.AssetBundleManifestObject = GetAsset<AssetBundleManifest>();
                return false;
            }
            else
                return true;
        }
    }
    public class AssetBundleLoadPersistentOperation : AssetBundleLoadOperation
    {
        public string assetBundleName;
        protected string downloadPersistentUrl;
        public string err = string.Empty;
        public LoadedAssetBundle loadedAssetBundle;
        public AssetBundleLoadPersistentOperation(string bundleName,string url)
        {
            assetBundleName = bundleName;
            downloadPersistentUrl = url;
        }

        public override bool IsDone()
        {
            return loadedAssetBundle.assetBundle != null;
        }

        public override bool Update()
        {
            byte[] targetData = FileUtils.LoadFile<byte[]>(downloadPersistentUrl);
            AssetBundle bundle = AssetBundle.LoadFromMemory(targetData);
            loadedAssetBundle = new LoadedAssetBundle(bundle);
            if (loadedAssetBundle != null)
            {
                return false;
            }
            else
            {
                err = "SomeError";
                return true;
            }
        }
    }
}
