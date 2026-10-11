using System.Collections.Generic;
using System.IO;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class CustomEditorTool :MonoBehaviour
{
    private static string JSON_FILE_PATH = "\\Scripts\\JClass\\";
#if UNITY_EDITOR
    [MenuItem("GameObject/UI/AttachLocalizeScript", false, 1003)]
    public static void AttachLocalizeScript()
    {
        if (Selection.activeGameObject != null)
        {
            GameObject targetObject = Selection.activeGameObject;

            TextMeshProUGUI[] textArray = targetObject.GetComponentsInChildren<TextMeshProUGUI>();

            if (textArray.Length <= 0)
            {
                return;
            }

            List<TextMeshProUGUI> targetTextLists = new List<TextMeshProUGUI>(textArray);

            for (int i = 0; i < targetTextLists.Count; i++)
            {
                //로컬라이즈 텍스트 관련 스크립트가 없다면 추가
                LocalizeText textComponents = targetTextLists[i].gameObject.GetComponent<LocalizeText>();
                if (textComponents == null)
                {
                    targetTextLists[i].gameObject.AddComponent<LocalizeText>();
                }
            }

        }
        //해당 함수는 선택한 오브젝트에 있는 모든 텍스트에 로컬라이즈 스크립트를 붙이는 툴
    }

    [MenuItem("Tools/RefreshJsonClass", false,1004)]
    public static void MakeJsonClassText()
    {
        //특정 폴더 밑에 있는 모든 오브젝트 찾기
        string loadPath = Application.dataPath + JSON_FILE_PATH;
        string savePath = Application.dataPath + "/Scripts/Common/";
        string fileName = "UserDataJsonEnum.cs";
        if (Directory.Exists(savePath) == false)
        {
            Directory.CreateDirectory(savePath);
        }
        if (Directory.Exists(loadPath) == false)
        {
            Directory.CreateDirectory(loadPath);
        }

        DirectoryInfo di = new DirectoryInfo(loadPath);
        List<string> jsonFileLists = new List<string>();
        foreach (FileInfo file in di.GetFiles())
        {
            if (file.Name.Contains(".cs")&& !file.Name.Contains("meta"))
            {
                string addObjectName = file.Name;
                int objectNameDotPos = addObjectName.LastIndexOf('.');

                if (objectNameDotPos >= 0)
                {
                    addObjectName = addObjectName.Substring(0, objectNameDotPos);
                }
                jsonFileLists.Add(addObjectName);
            }
        }
        string text = "public enum E_JSON_TYPE\n";
        text += "{\n";
        for(int i = 0; i < jsonFileLists.Count; i++)
        {
            text += $"\t{jsonFileLists[i]},\n";
        }
        text += "}\n";
        File.WriteAllText($"{savePath}{fileName}", text);
    }
#endif
}
