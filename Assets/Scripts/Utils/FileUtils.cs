using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine;
using Seunghak.Common;
using Cysharp.Threading.Tasks;

public static class FileUtils
{
    public const string BUNDLE_LIST_FILE_NAME = "AssetbundleList.json";
    public const string ATLAS_LIST_FILE_NAME = "AtlasList.json";
    public const string VERSION_INFO_FILE_NAME = "UpdateVersionInfo.txt";
    public const string CDN_ADDRESS_FILE_NAME = "CDNAddress.txt";
    public static string JSONFILE_LOAD_PATH = $"{Application.dataPath}/BaseSources/JsonData/";
    public static string ATLAS_SAVE_PATH = $"{Application.dataPath}/BaseSources/Atlas/";
    public static string DATA_SAVE_PATH = $"{Application.dataPath}/BaseSources/BuildInfo/";
    public static string[] FILE_SIZE_COLUMN = new string[] {"Bytes", "KBs", "MBs", "GBs", "TBs" };
    public static IEnumerator RequestTextFile<T>(string url)
    {
#if UNITY_EDITOR
        yield return FileUtils.LoadFile<T>(url);
#else
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (!request.isNetworkError)
            {
                string bundleData = request.downloadHandler.text;

                T loadClass = JsonConvert.DeserializeObject<T>(bundleData);

                yield return loadClass;
            }
        }
#endif
    }
    public static async UniTask<T> RequestTextFileAsync<T>(string url) where T : class
    {
#if UNITY_EDITOR
        // Load from file system in the editor
        if (!File.Exists(url))
        {
            Debug.LogError($"RequestTextFileAsync: File not found at path: {url}");
            return null;
        }

        try
        {
            string fileContent = await UniTask.RunOnThreadPool(() => File.ReadAllText(url));
            return JsonConvert.DeserializeObject<T>(fileContent);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"RequestTextFileAsync: Error reading or deserializing file. Exception: {ex}");
            return null;
        }
#else
        // Use UnityWebRequest for runtime environments
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            var requestOperation = await request.SendWebRequest().ToUniTask();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"RequestTextFileAsync: Network error. URL: {url}, Error: {request.error}");
                return null;
            }

            try
            {
                string fileContent = request.downloadHandler.text;
                return JsonConvert.DeserializeObject<T>(fileContent);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"RequestTextFileAsync: Error deserializing response. Exception: {ex}");
                return null;
            }
        }
#endif
    }
    #region Base64
    public static string Base64Encode(string data)
    {
        try
        {
            byte[] encData_byte = new byte[data.Length];
            encData_byte = System.Text.Encoding.UTF8.GetBytes(data);
            string encodedData = Convert.ToBase64String(encData_byte);
            return encodedData;
        }
        catch (Exception e)
        {
            throw new Exception("Error in Base64Encode: " + e.Message);
        }
    }
    public static string Base64Decode(string data)
    {
        try
        {
            System.Text.UTF8Encoding encoder = new System.Text.UTF8Encoding();

            System.Text.Decoder utf8Decode = encoder.GetDecoder();
            byte[] todecode_byte = Convert.FromBase64String(data);
            int charCount = utf8Decode.GetCharCount(todecode_byte, 0, todecode_byte.Length);
            char[] decoded_char = new char[charCount];
            utf8Decode.GetChars(todecode_byte, 0, todecode_byte.Length, decoded_char, 0);
            string result = new String(decoded_char);
            return result;
        }
        catch (Exception e)
        {
            throw new Exception("Error in Base64Decode: " + e.Message);
        }
    }
    #endregion
    public static T LoadFile<T>(string filePath)
    {
        T loadClass;
        try
        {
            string fildData = File.ReadAllText(filePath);
            //string decodeData = Base64Decode(fildData);
            loadClass = JsonConvert.DeserializeObject<T>(fildData);
        }
        catch(Exception)
        {
            loadClass = default(T);
        }

        return loadClass;
    }
    public static string LoadFile(string filePath)
    {
        string fildData = File.ReadAllText(filePath);

        return fildData;
    }
    public static string GetPlatformString()
    {
#if UNITY_ANDROID
        return "/Android/";
#elif UNITY_IOS
        return "/IOS/";
#else
        return "/StandaloneWindows/";
#endif
    }
    public static void SaveFile<T>(string savePath,string fileName,T saveData)
    {
        string saveStringData = JsonConvert.SerializeObject(saveData);
        if (!savePath.EndsWith("/"))
        {
            savePath = $"{savePath}/";
        }
        if (Directory.Exists(savePath) == false)
        {
            Directory.CreateDirectory(savePath);
        }
        try
        {
            //string encodeData = Base64Encode(saveStringData);
            File.WriteAllText($"{savePath}{fileName}", saveStringData);
        }catch(Exception e)
        {

        }
    }
    public static void SaveToExcel(string savePath,string fileName,string saveData)
    {

    }
    public static T DeSerealString<T>(string target)
    {
        T returnValue = JsonConvert.DeserializeObject<T>(target);
        return returnValue;
    }
    public static bool CompareHash<T>(T userData , string downloadData)
    {
        //downloadData는 해쉬코드를 받아야한다
        //if(userData.GetHashCode().CompareTo()
        return false;
    }
    public static BundleListsDic CompareDicData(BundleListsDic preloadDic, BundleListsDic downloadDic)
    {
        BundleListsDic finalDownloadDic = new BundleListsDic();
        foreach(var downloadAssets in downloadDic.bundleNameLists)
        {
            BundleListInfo bundleInfo = preloadDic.bundleNameLists.Find(find => find.bundleName == downloadAssets.bundleName);
            if(string.IsNullOrEmpty(bundleInfo.bundleName))
            {
                //값을 찾지 못했을떄
                finalDownloadDic.AddBundleName(downloadAssets);
                finalDownloadDic.AddObjectsRejeon(downloadAssets.bundleName, downloadDic.bundleObjectLists[downloadAssets.bundleName]);
            }
            else
            {
                if (bundleInfo.bundleTotalHashCode.Equals(downloadAssets.bundleTotalHashCode))
                {
                    //같다면 ~
                    continue;
                }
                else
                {
                    long compareHash = CommonUtil.GetAssetBundleLocalHash(downloadAssets.bundleName);
                    if (compareHash.Equals(downloadAssets.bundleTotalHashCode))
                    {
                        continue;
                    }
                    else
                    {
                        finalDownloadDic.AddBundleName(downloadAssets);
                        finalDownloadDic.AddObjectsRejeon(downloadAssets.bundleName,
                            downloadDic.bundleObjectLists[downloadAssets.bundleName]);
                    }
                }
            }
        }
        return finalDownloadDic;
    }
    public static string GetFileSizeString(long fileSize)
    {
        int index = 0;
        int fileColumnValue = 1024;
        long remainValue = 0;
        while (fileSize > fileColumnValue)
        {
            remainValue = (fileSize % fileColumnValue) / 100;
            fileSize /= fileColumnValue;
            index++;
        }
        string fileSizeString = $"{fileSize}.{remainValue} {FILE_SIZE_COLUMN[index]}";
        return fileSizeString;
    }
    public static string GetStreamingAssetsPath()
    {
        if (Application.isEditor)
        {
            return Application.dataPath;
        }
        else if (Application.isMobilePlatform || Application.isConsolePlatform)
        {
            return Application.streamingAssetsPath;
        }
        return "file://" + Application.streamingAssetsPath;
    }
    public static int GetPrefabsUnique(GameObject target)
    {
        return target.GetInstanceID();
    }
}
