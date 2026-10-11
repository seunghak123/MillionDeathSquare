using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;

namespace Seunghak.Common
{
    class CommonConst
    {

    }
    class CommonUtil
    {
        public static long PoolRemoveSecTime = 600;
        public static string StringToMD5(string str)
        {
            StringBuilder MD5Str = new StringBuilder();
            byte[] byteArr = Encoding.ASCII.GetBytes(str);
            byte[] resultArr = (new MD5CryptoServiceProvider()).ComputeHash(byteArr);

            for (int cnti = 0; cnti < resultArr.Length; cnti++)
            {
                MD5Str.Append(resultArr[cnti].ToString("X2"));
            }
            return MD5Str.ToString();
        }
        public static T RandomIndexByList<T>(List<T> targetList)
        {
            System.Random random = new System.Random();

            // 리스트에서 랜덤한 요소 선택
            int index = random.Next(targetList.Count); // 0 ~ items.Count - 1 범위의 랜덤 인덱스
            return targetList[index];
        }

        public static T TargetIndexByList<T>(List<T> targetList,int targetIndex)
        {
            return targetList[targetIndex% targetList.Count];
        }
        public static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
        public static void SetLayerRecursively(GameObject obj, string layerName)
        {
            obj.layer = LayerMask.NameToLayer(layerName);
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layerName);
            }
        }
        public static string MD5ToString(string md5str)
        {
            byte[] bytePassword = Encoding.UTF8.GetBytes(md5str);
            string emptymd5;

            using (MD5 md5 = MD5.Create())
            {
                byte[] byteHashedPassword = md5.ComputeHash(bytePassword);
                emptymd5 = byteHashedPassword.ToString();
            }
            return emptymd5;
        }
        public static Type GetTypeFromAssemblies(string TypeName)
        {
            var type = Type.GetType(TypeName);
            if (type != null)
                return type;

            var currentAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            var referencedAssemblies = currentAssembly.GetReferencedAssemblies();
            foreach (var assemblyName in referencedAssemblies)
            {
                var assembly = System.Reflection.Assembly.Load(assemblyName);
                if (assembly != null)
                {
                    type = assembly.GetType(TypeName);
                    if (type != null)
                        return type;
                }
            }

            return null;
        }
        #region PlayerPref
        // int/float/bool/string처럼 ToString()/Convert.ChangeType만으로 왕복 가능한 단순 타입인지.
        // JsonUtility는 이런 원시 타입을 루트로 직렬화하지 못하므로 이 경로를 별도로 유지해야 한다.
        private static bool IsSimplePrefType(Type t) => t.IsPrimitive || t == typeof(string) || t == typeof(decimal);

        public static void SavePlayerPref<T>(PlayerPrefKey saveKey, T saveData)
        {
            // 예전엔 복합 클래스(UserItemDatas 등)도 그냥 .ToString()으로 저장했는데, 그러면
            // "Namespace.ClassName" 같은 타입 이름 문자열만 저장되고 실제 필드 내용(골드/아이템
            // 수량 등)은 전혀 저장되지 않는 버그였다. 복합 타입은 JsonUtility로 직렬화한다.
            string value = IsSimplePrefType(typeof(T))
                ? saveData?.ToString() ?? string.Empty
                : JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString(saveKey.ToString(), value);
            // 플랫폼 저장소(원본)에 반영되도록 변경 표시 — 디바운스 후 스냅샷 푸시
            Seunghak.Platform.PlatformSaveSync.MarkDirty();
            PlayerPrefs.Save();
        }
        public static T GetPlayerPref<T>(PlayerPrefKey saveKey)
        {
            string getValue = PlayerPrefs.GetString(saveKey.ToString());
            if (string.IsNullOrEmpty(getValue))
                return default(T);

            if (IsSimplePrefType(typeof(T)))
            {
                try
                {
                    return (T)Convert.ChangeType(getValue, typeof(T));
                }
                catch (Exception e)
                {
                    Debug.Log($"ConvertValue Error {e.Message}");
                    return default(T);
                }
            }

            try
            {
                T parsed = JsonUtility.FromJson<T>(getValue);
                // JsonUtility가 예외 없이 null을 돌려주는 경우도 있어 아래 catch 폴백과
                // 동일하게 처리한다 — 호출부는 항상 유효한 인스턴스를 기대한다.
                return parsed != null ? parsed : CreateDefaultInstance<T>();
            }
            catch (Exception e)
            {
                // 이전 버전(.ToString() 저장 방식)이 남긴 "Namespace.ClassName" 같은 문자열이거나
                // 그 밖의 잘못된 JSON이면 여기로 온다. default(T)(참조 타입은 null)를 그대로
                // 돌려주면 호출부가 널 참조로 죽으므로, 필드 기본값이 채워진 빈 인스턴스로
                // 대체한다 — 저장된 값을 못 읽었을 뿐 "데이터가 없다"와 동등하게 취급.
                Debug.Log($"JsonFromValue Error {e.Message}");
                return CreateDefaultInstance<T>();
            }
        }

        private static T CreateDefaultInstance<T>()
        {
            try
            {
                return Activator.CreateInstance<T>();
            }
            catch
            {
                return default(T);
            }
        }
        private static string attendString = "SaveBundleHash";
        public static void SaveAssetBundleHash(string assetName, long saveValue)
        {
            string saveKey = $"{attendString}_{assetName}";
            PlayerPrefs.SetString(saveKey, saveValue.ToString());
        }
        public static long GetAssetBundleLocalHash(string assetName)
        {
            long returnValue = 0;
            string loadKey = $"{attendString}_{assetName}";
            string getValue = PlayerPrefs.GetString(loadKey);
            try
            {
                returnValue = (long)Convert.ChangeType(getValue, typeof(long));
            }
            catch (Exception e)
            {
                Debug.Log($"Convert HashValue Error {e.Message}");
            }
            return returnValue;
        }
        public static string GetTimerString(float timer)
        {
            string timerString = string.Empty;
            int hour = (int)timer / 3600;
            int mins = ((int)timer % 3600) / 60;
            int seconds = (int)timer % 60 ;
            timer = timer <= 0 ? 0 : timer;
            if (timer < 60.0f)
            {
                timerString = $"{timer.ToString("F2")}초" ;
            }
            else if(timer <3600.0f)
            {
                timerString = $"{mins}분{seconds}초";
            }
            else
            {
                timerString = $"{hour}시{mins}분";
            }

            return timerString;
        }
        #endregion PlayerPref
        public static List<T> ShuffleData<T>(List<T> originDeck)
        {
            List<T> shuffleDeck = new List<T>(originDeck);
            for (int i = 0; i < shuffleDeck.Count; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, shuffleDeck.Count);
                T temp = shuffleDeck[i];
                shuffleDeck[i] = shuffleDeck[randomIndex];
                shuffleDeck[randomIndex] = temp;
            }
            return shuffleDeck;
        }
        public static T getRandomData<T>(List<T> originDatas)
        {
            int randomIndex = UnityEngine.Random.Range(0, originDatas.Count);
            if (originDatas.Count > 0)
            {
                return originDatas[randomIndex];
            }
            else
            {
                return default(T);
            }
        }
        public static void UpdateStringWithTextMeshPro(TextMeshProUGUI text,string targetValue)
        {
            text.text = targetValue;
        }
    }
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<TKey> keys = new List<TKey>();
        [SerializeField]
        private List<TValue> values = new List<TValue>();

        // save the dictionary to lists
        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (KeyValuePair<TKey, TValue> pair in this)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }

        // load dictionary from lists
        public void OnAfterDeserialize()
        {
            this.Clear();

            //if (keys.Count != values.Count)
                //throw new System.Exception(string.Format("there are {0} keys and {1} values after deserialization. Make sure that both key and value types are serializable."));

            for (int i = 0; i < keys.Count; i++)
                this.Add(keys[i], values[i]);
        }
    }
}
