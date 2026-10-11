using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using Newtonsoft.Json;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Seunghak.Common
{
    public class SpriteManager : UnitySingleton<SpriteManager>
    {
        private List<SpriteAtlas> spriteAtlasLists = new List<SpriteAtlas>();
        private Dictionary<string, string> spriteAtlasPathDic = new Dictionary<string, string>();
        protected override void InitSingleton()
        {
            InitAtlasLists();
        }
        public Sprite LoadSprite(string spriteName)
        {
            if (spriteAtlasPathDic.ContainsKey(spriteName))
            {
                SpriteAtlas targetAtlas = spriteAtlasLists.Find(find => find.name == spriteAtlasPathDic[spriteName]);
                if (targetAtlas == null)
                {
                    Debug.Log($"{spriteAtlasPathDic[spriteName]} atlas no there");
                    return null;
                }
                return targetAtlas.GetSprite(spriteName);
            }
            else
            {
                Object target = GameResourceManager.Instance.LoadObject(spriteName);
                if (target != null)
                {
                    Texture2D targetTexture = target as Texture2D;
                    return Sprite.Create(targetTexture, new Rect(0, 0, targetTexture.width, targetTexture.height), new Vector2(0.5f, 0.5f));
                }
                else
                {
                    Debug.Log($"{spriteName} sprite nothing");          
                    return null;
                }
            }
        }
        private void InitAtlasLists()
        {
            SpriteAtlasManager.atlasRequested += RequestAtlasCallback;

            spriteAtlasLists.Clear();
            spriteAtlasPathDic = new Dictionary<string, string>();

            AtlasLists atlasLists = new AtlasLists();

            string atlasfilePath = Application.dataPath;

#if UNITY_EDITOR
            atlasfilePath = $"{FileUtils.ATLAS_SAVE_PATH}/{FileUtils.ATLAS_LIST_FILE_NAME}";
                     
            string bundleFilePath = $"{FileUtils.GetStreamingAssetsPath()}{FileUtils.GetPlatformString()}{ FileUtils.BUNDLE_LIST_FILE_NAME}";
                 
            BundleListsDic bundleLists = FileUtils.LoadFile<BundleListsDic>(bundleFilePath);
            atlasLists = FileUtils.LoadFile<AtlasLists>(atlasfilePath);

            if (atlasLists == null || atlasLists.atlaseLists == null || bundleLists == null)
            {
                Debug.LogWarning("[SpriteManager] atlas/bundle 리스트 파일 없음. 에디터 fallback 생략.");
                return;
            }

            for (int i = 0; i < atlasLists.atlaseLists.Count; i++)
            {
                BundleListInfo listInfo = bundleLists.bundleNameLists.Find(find => find.bundleName == "atlas");
                if (!string.IsNullOrEmpty(listInfo.bundleName))
                {
                    List<BundleFileInfo> infos = bundleLists.bundleObjectLists[listInfo.bundleName];
                    for(int j=0;j< infos.Count; j++)
                    {
                        SpriteAtlas atlasSprits = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(infos[j].filePath);

                        if (atlasSprits != null)
                        {
                            spriteAtlasLists.Add(atlasSprits);
                        }
                    }
                }
                for(int j = 0; j < atlasLists.atlaseLists[i].spriteLists.Count; j++)
                {
                    spriteAtlasPathDic[atlasLists.atlaseLists[i].spriteLists[j]] = atlasLists.atlaseLists[i].atlasName;
                }
            }
#else
            atlasLists = JsonConvert.DeserializeObject<AtlasLists>(GameResourceManager.Instance.LoadObject("AtlasList").ToString());

            for (int i = 0; i < atlasLists.atlaseLists.Count; i++)
            {
                SpriteAtlas atlas =  GameResourceManager.Instance.LoadObject(atlasLists.atlaseLists[i].atlasName) as SpriteAtlas;
                if (atlas != null)
                {
                    spriteAtlasLists.Add(atlas);

                    for (int j = 0; j < atlasLists.atlaseLists[i].spriteLists.Count; j++)
                    {
                        spriteAtlasPathDic[atlasLists.atlaseLists[i].spriteLists[j]] = atlasLists.atlaseLists[i].atlasName;
                    }
                }
            }
#endif
        }
        private void RequestAtlasCallback(string tag, System.Action<SpriteAtlas> callback)
        {
            //추후 Atlas 개발 시 변경
        }
    }
}