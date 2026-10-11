using UnityEngine;
using UnityEditor;

public class CreateCustomPrefab
{
    // MenuItem 속성을 사용하여 컨텍스트 메뉴에 항목을 추가합니다.
    [MenuItem("Assets/Create/Custom Prefab")]
    static void CreatePrefab()
    {
        //리스트를 가져온다
        string prefabPath = "Assets/BaseSources/Prefabs/Effect/BaseParticle.prefab";

        // 프리팹을 로드합니다.
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        // 프리팹이 존재하는지 확인합니다.
        if (prefab == null)
        {
            Debug.LogError("프리팹을 찾을 수 없습니다: " + prefabPath);
            return;
        }

        // 선택한 폴더의 경로를 가져옵니다.
        string folderPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(folderPath))
        {
            folderPath = "Assets";
        }
        else if (!AssetDatabase.IsValidFolder(folderPath))
        {
            folderPath = System.IO.Path.GetDirectoryName(folderPath);
        }

        // 새로운 프리팹 인스턴스의 이름을 지정합니다.
        string newPrefabPath = AssetDatabase.GenerateUniqueAssetPath(folderPath + "/New" + prefab.name + ".prefab");

        // 프리팹을 해당 경로에 복사합니다.
        GameObject newPrefabInstance = PrefabUtility.SaveAsPrefabAsset(prefab, newPrefabPath);

        // 프로젝트 뷰를 갱신합니다.
        AssetDatabase.Refresh();

        // 새로운 프리팹을 선택합니다.
        Selection.activeObject = newPrefabInstance;

        Debug.Log("프리팹이 생성되었습니다: " + newPrefabPath);
    }

    // 프리팹을 생성할 수 있는 조건을 설정합니다.
    [MenuItem("Assets/Create/Custom Prefab", true)]
    static bool ValidateCreateCustomPrefab()
    {
        // 폴더가 선택된 경우에만 메뉴 항목을 활성화합니다.
        return Selection.activeObject != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(Selection.activeObject));
    }
}
