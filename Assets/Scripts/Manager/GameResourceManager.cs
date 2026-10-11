using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Seunghak.Common
{
    public class GameResourceManager : UnitySingleton<GameResourceManager>
    {
        private Dictionary<string, ObjectPool> prefabObjectpools = new Dictionary<string, ObjectPool>();

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.B))
            {
                CommonUtil.SavePlayerPref<int>(PlayerPrefKey.SaveTest, 5);
            }
            if (Input.GetKeyDown(KeyCode.V))
            {
                int a = CommonUtil.GetPlayerPref<int>(PlayerPrefKey.SaveTest);
            }
        }

        public GameObject GetPoolObject(string type)
        {
            if (!prefabObjectpools.ContainsKey(type))
            {
                prefabObjectpools.Add(type, new ObjectPool());
            }
            return null;
        }

        public void PushObjectPool(string type, Object targetObject)
        {
            if (targetObject == null)
            {
                return;
            }
            if (!prefabObjectpools.ContainsKey(type))
            {
                prefabObjectpools.Add(type, new ObjectPool());
            }

            prefabObjectpools[type].PushPool(targetObject);
        }

        public void RemovePoolObject(GameObject targetObject)
        {
            Destroy(targetObject);
        }

        public GameObject SpawnObject(string objectName)
        {
            if (!prefabObjectpools.ContainsKey(objectName))
            {
                // BundleManager Addressables 캐시에서 프리팹 가져오기
                GameObject bundlePrefab = Seunghak.Manager.BundleManager.Instance != null
                    ? Seunghak.Manager.BundleManager.Instance.GetAsset(objectName)
                    : null;

                if (bundlePrefab != null)
                {
                    GameObject bundleInstance = GameObject.Instantiate(bundlePrefab);
                    UpdateAssetBundleObjectMatrial(bundleInstance);
                    PushObjectPool(objectName, bundlePrefab);
                    return bundleInstance;
                }

                Debug.LogWarning($"PrefabLists have not {objectName}");
                Object useritem = Resources.Load(objectName);
                if (useritem == null)
                    useritem = Resources.Load("UI/" + objectName);

#if UNITY_EDITOR
                // 에디터: Resources에 없으면 프로젝트 전체에서 프리팹 검색 (AssetDatabase)
                if (useritem == null)
                {
                    string[] guids = UnityEditor.AssetDatabase.FindAssets($"{objectName} t:Prefab");
                    foreach (var guid in guids)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                        if (System.IO.Path.GetFileNameWithoutExtension(path) == objectName)
                        {
                            useritem = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                            if (useritem != null) break;
                        }
                    }
                }
#endif

                if (useritem != null)
                {
                    GameObject instance = GameObject.Instantiate(useritem as GameObject);
                    UpdateAssetBundleObjectMatrial(instance);
                    return instance;
                }

                return null;
            }

            GameObject poolObject = null;
            if (prefabObjectpools.ContainsKey(objectName))
            {
                poolObject = prefabObjectpools[objectName].GetPoolObject();
                poolObject.transform.SetParent(null);
            }

            UpdateAssetBundleObjectMatrial(poolObject);

            return poolObject;
        }

        public void UpdateAssetBundleObjectMatrial(GameObject inGameObject)
        {
#if UNITY_EDITOR
            {
                if (inGameObject == null) return;
            }
            {
                {
                    var lComList = new List<Renderer>(inGameObject.GetComponentsInChildren<Renderer>(true));
                    if (lComList != null && lComList.Count > 0)
                    {
                        lComList.ForEach((fCom) =>
                        {
                            if (fCom != null && fCom.sharedMaterial != null && fCom.sharedMaterial.shader != null)
                            {
                                fCom.sharedMaterial.shader = Shader.Find(fCom.sharedMaterial.shader.name);
                            }
                        });
                    }
                }
                {
                    var lComList = new List<SpriteRenderer>(inGameObject.GetComponentsInChildren<SpriteRenderer>(true));
                    if (lComList != null && lComList.Count > 0)
                    {
                        lComList.ForEach((fCom) =>
                        {
                            if (fCom != null && fCom.sharedMaterial != null && fCom.sharedMaterial.shader != null)
                            {
                                fCom.sharedMaterial.shader = Shader.Find(fCom.sharedMaterial.shader.name);
                            }
                        });
                    }
                }
                {
                    var lComList = new List<SkinnedMeshRenderer>(inGameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true));
                    if (lComList != null && lComList.Count > 0)
                    {
                        lComList.ForEach((fCom) =>
                        {
                            if (fCom != null && fCom.sharedMaterial != null && fCom.sharedMaterial.shader != null)
                            {
                                fCom.sharedMaterial.shader = Shader.Find(fCom.sharedMaterial.shader.name);
                            }
                        });
                    }
                }
                {
                    var lComList = new List<MeshRenderer>(inGameObject.GetComponentsInChildren<MeshRenderer>(true));
                    if (lComList != null && lComList.Count > 0)
                    {
                        lComList.ForEach((fCom) =>
                        {
                            if (fCom != null && fCom.sharedMaterial != null && fCom.sharedMaterial.shader != null)
                            {
                                fCom.sharedMaterial.shader = Shader.Find(fCom.sharedMaterial.shader.name);
                            }
                        });
                    }
                }
                {
                    var lComList = new List<ParticleSystemRenderer>(inGameObject.GetComponentsInChildren<ParticleSystemRenderer>());
                    if (lComList != null && lComList.Count > 0)
                    {
                        lComList.ForEach((fCom) =>
                        {
                            if (fCom != null && fCom.sharedMaterial != null && fCom.sharedMaterial.shader != null)
                            {
                                fCom.sharedMaterial.shader = Shader.Find(fCom.sharedMaterial.shader.name);
                            }
                        });
                    }
                }
            }
#endif
        }

        public Object LoadObject(string assetName)
        {
            var result = Resources.Load(assetName);
#if UNITY_EDITOR
            if (result == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets($"{assetName}");
                foreach (var guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) == assetName)
                    {
                        result = UnityEditor.AssetDatabase.LoadAssetAtPath<Object>(path);
                        if (result != null) break;
                    }
                }
            }
#endif
            return result;
        }

        public Object LoadObject(string assetName, bool preload)
        {
            return LoadObject(assetName);
        }

        public void DestroyObject(GameObject targetObject)
        {
            targetObject.SetActive(false);
            targetObject.transform.parent = this.transform;
        }

        public void ReleasePools()
        {
            foreach (var pool in prefabObjectpools)
            {
                pool.Value.DestoryPool();
            }
        }

        public void ReleasePool(string key)
        {
            if (prefabObjectpools.ContainsKey(key))
            {
                prefabObjectpools[key].DestoryPool();
            }
        }
    }

    public class ObjectPool
    {
        private List<GameObject> poolObjects = new List<GameObject>();
        private Object poolObject;
        private System.DateTime currentAccessTime = new System.DateTime();

        private void RemoveLifeTimeSpanObjectPool()
        {
            if (System.DateTime.Now > currentAccessTime.AddSeconds(CommonUtil.PoolRemoveSecTime))
            {
                DestoryPool();
            }
        }

        public void ReleaseUnUsedObject()
        {
            for (int i = 0; i < poolObjects.Count; i++)
            {
                if (!poolObjects[i].activeInHierarchy)
                {

                }
            }
        }

        public void PushPool(Object targetObject)
        {
            currentAccessTime = System.DateTime.Now;
            poolObject = targetObject;
        }

        public GameObject GetPoolObject(bool isSound = false)
        {
            currentAccessTime = System.DateTime.Now;
            for (int i = 0; i < poolObjects.Count; i++)
            {
                if (isSound)
                {
                    return poolObjects[i];
                }
                else
                {
                    if (poolObjects[i] == null)
                    {
                        poolObjects.RemoveAt(i);
                        i--;
                    }
                    else
                    {
                        if (!poolObjects[i].activeInHierarchy)
                        {
                            poolObjects[i].SetActive(true);
                            return poolObjects[i];
                        }
                    }
                }
            }
            GameObject createdObject = GameObject.Instantiate(poolObject) as GameObject;
            poolObjects.Add(createdObject);
            return createdObject;
        }

        public void DestoryPool()
        {
            for (int i = 0; i < poolObjects.Count; i++)
            {
                GameResourceManager.Instance.RemovePoolObject(poolObjects[i]);
            }

            poolObjects.Clear();
        }
    }
}
