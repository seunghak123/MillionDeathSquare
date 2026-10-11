using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

public class AttachScriptTool : EditorWindow
{
    string m_DefaultPath = "Assets";
    string m_strInputScriptsName = string.Empty;
    string m_strPreScriptsName = string.Empty;
    string m_strPrefabsRootname = string.Empty;
    List<UnityEngine.GameObject> findedassets = new List<UnityEngine.GameObject>();
    [MenuItem("Tools/AttachScript Tool")]
    static void Init()
    {
        EditorWindow.GetWindow<AttachScriptTool>();
    }

    // 어셈블리로부터 클래스 이름 문자열을 보내 System.Type을 얻는다.
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
    void FindAssets(string[] assetroot,string root)
    {
        for(int i = 0; i < assetroot.Length; i++)
        {
            string[] subfolder = AssetDatabase.GetSubFolders(assetroot[i]);
            if (subfolder.Length > 0)
            {
                FindAssets(subfolder, assetroot[i]);
            }

            //UnityEngine.Object[] addedlist = AssetDatabase.LoadAllAssetsAtPath(assetroot[i]);
            System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(assetroot[i]);
            FileInfo[]  filelists = di.GetFiles();
            for (int j = 0; j < filelists.Length; j++)
            {
                if (filelists[j].Name.Contains("prefab"))
                {
                    string data = assetroot[i] + '/'+  filelists[j].Name;//\\
                    data = data.Replace('/', '\\');
                    UnityEngine.GameObject file = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(data);

                    if (file!=null)
                        findedassets.Add(file);
                }
            }
        }

    }
    void finditems(string path)
    {
        System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(path);
        FileInfo[] filelists = di.GetFiles();
        for (int j = 0; j < filelists.Length; j++)
        {
            if (filelists[j].Name.Contains("prefab"))
            {
                string data = path + '/' + filelists[j].Name;//\\
                data = data.Replace('/', '\\');
                UnityEngine.GameObject file = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(data);

                if (file != null)
                    findedassets.Add(file);
            }
        }
    }
    int data = 0;
    private void OnGUI()
    {
        findedassets = new List<UnityEngine.GameObject>();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Add Attach Scripts");
        m_strInputScriptsName = EditorGUILayout.TextField(m_strInputScriptsName);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Needed Scripts");
        m_strPreScriptsName = EditorGUILayout.TextField(m_strPreScriptsName);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(" Default Path:  "+ m_DefaultPath);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Prefabs Root");
        m_strPrefabsRootname = EditorGUILayout.TextField(m_strPrefabsRootname);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.EndHorizontal();

        int count = 0;
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Finded Root :" + m_DefaultPath + m_strPrefabsRootname);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        string[] prefabObject = AssetDatabase.GetSubFolders(m_DefaultPath + m_strPrefabsRootname);
        FindAssets(prefabObject, m_DefaultPath + m_strPrefabsRootname);
        finditems(m_DefaultPath + m_strPrefabsRootname);
        count = findedassets.Count;

        GUILayout.Label("Finded Objects :" + count.ToString());
        EditorGUILayout.EndHorizontal();


        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Add Button");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("ADD Scripts") == true)
        {
            Type prescripts = null;
            if (!string.IsNullOrEmpty(m_strPreScriptsName))
            {
                prescripts = GetTypeFromAssemblies(m_strPreScriptsName);

                if (prescripts != null)
                    return;
            }

            Type scripts = GetTypeFromAssemblies(m_strInputScriptsName);
            if (scripts == null)
                return;
            for (int i=0;i< findedassets.Count; i++)
            {
                if (findedassets.Count <= data)
                    return;
                bool isChange = false;

                data++;
                if(isChange)
                    PrefabUtility.SavePrefabAsset(findedassets[i]);
            }
        }
        else
        {
            data = 0;
        }
        EditorGUILayout.EndHorizontal();

    }
}
