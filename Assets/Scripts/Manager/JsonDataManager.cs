using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Seunghak.Common
{
    public class JsonDataManager : UnitySingleton<JsonDataManager>
    {
        private static Dictionary<string, Dictionary<string,JBaseData>> dicJsonDic = new Dictionary<string, Dictionary<string, JBaseData>>();
        private static void SetDicData<T>(string saveKey,Dictionary<string,T> dicData) where T : JBaseData
        {
            Dictionary<string, JBaseData> convertedData = new Dictionary<string, JBaseData>();
            foreach (var kvp in dicData)
            {
                if (kvp.Value is JBaseData)
                {
                    convertedData[kvp.Key] = (T)kvp.Value;
                }
            }
            dicJsonDic[saveKey] = convertedData;
        }
        private static Dictionary<string, T> GetDicData<T>(string saveKey) where T : JBaseData
        {
            Dictionary<string, T> convertedData = new Dictionary<string, T>();
            foreach (var kvp in dicJsonDic[saveKey])
            {
                if (kvp.Value is T)
                {
                    convertedData[kvp.Key] = (T)kvp.Value;
                }
            }
            return convertedData;
        }
        public static Dictionary<string, T> LoadJsonDatas<T>(E_JSON_TYPE loadType) where T : JBaseData
        {
            String loadPath = "";
            String loadTypeString = loadType.ToString();

            if (loadTypeString.Contains("Data"))
            {
                loadTypeString = loadTypeString.Replace("Data", "");
            }
            loadTypeString = loadTypeString.ToLower();
            if (dicJsonDic.ContainsKey(loadTypeString))
            {
                return GetDicData<T>(loadTypeString);
            }

            Dictionary<string, T> loadedObject = new Dictionary<string, T>();
#if UNITY_EDITOR
            string loadTypeStringPath = loadTypeString + ".json";
            loadPath = FileUtils.JSONFILE_LOAD_PATH + loadTypeStringPath.ToLower();
            object loadData = FileUtils.LoadFile<object>(loadPath);

            List<T> objectData = JsonConvert.DeserializeObject<List<T>>(loadData.ToString());
            loadedObject = objectData.ToDictionary(objectData => objectData.Id);
            SetDicData<T>(loadTypeString, loadedObject);
            //loadedObject = JsonConvert.DeserializeObject<Dictionary<string, T>>(loadData.ToString());

            if (!dicJsonDic.ContainsKey(loadTypeString))
            {
                SetDicData<T>(loadTypeString, loadedObject);
            }
#else
            UnityEngine.Object loadObject = GameResourceManager.Instance.LoadObject(loadTypeString.ToLower());
            List<T> objectData = JsonConvert.DeserializeObject<List<T>>(loadObject.ToString());
            loadedObject = objectData.ToDictionary(objectData => objectData.Id);
            SetDicData<T>(loadTypeString, loadedObject);

            if (!dicJsonDic.ContainsKey(loadTypeString))
            {
                SetDicData<T>(loadTypeString, loadedObject);
            }
#endif
            return loadedObject;
        }

        public T GetSingleData<T>(int singleId, E_JSON_TYPE type) where T : JBaseData
        {
            string findKey = singleId.ToString();

            return GetSingleData<T>(findKey, type);
        }
        public T GetSingleData<T>(string singleKey, E_JSON_TYPE type) where T : JBaseData
        {
            Dictionary<string, T> dicDatas = LoadJsonDatas<T>(type);

            if(dicDatas.ContainsKey(singleKey))
            {
                return dicDatas[singleKey];
            }

            return null;
        }
        public List<T> GetMultiDatas<T>(E_JSON_TYPE type) where T : JBaseData
        {
            return new List<T>(LoadJsonDatas<T>(type).Values);
        }
    }
}