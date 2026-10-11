#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// Addressables 빌드 도구.
/// 메뉴: Tools/Addressables/...
/// </summary>
public static class AddressablesBuilderTool
{
    private const string MENU_ROOT = "Tools/Addressables/";

    [MenuItem(MENU_ROOT + "Build (Clean)", false, 100)]
    public static void BuildClean()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[Addressables] Settings를 찾을 수 없습니다. Window > Asset Management > Addressables > Groups에서 초기화하세요.");
            return;
        }
        AddressableAssetSettings.CleanPlayerContent();
        AddressableAssetSettings.BuildPlayerContent(out var result);
        if (!string.IsNullOrEmpty(result.Error))
            Debug.LogError($"[Addressables] 빌드 실패: {result.Error}");
        else
            Debug.Log($"[Addressables] 빌드 완료. 시간: {result.Duration:F2}s");
    }

    [MenuItem(MENU_ROOT + "Build (Incremental Update)", false, 101)]
    public static void BuildUpdate()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[Addressables] Settings를 찾을 수 없습니다.");
            return;
        }
        string contentStatePath = ContentUpdateScript.GetContentStateDataPath(false);
        if (string.IsNullOrEmpty(contentStatePath))
        {
            Debug.LogError("[Addressables] 이전 빌드 상태 파일을 찾을 수 없습니다. 먼저 Clean 빌드를 실행하세요.");
            return;
        }
        ContentUpdateScript.BuildContentUpdate(settings, contentStatePath);
        Debug.Log("[Addressables] 증분 업데이트 빌드 완료.");
    }

    [MenuItem(MENU_ROOT + "Open Groups Window", false, 200)]
    public static void OpenGroupsWindow()
    {
        EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
    }

    [MenuItem(MENU_ROOT + "Check For Update", false, 201)]
    public static void CheckForUpdate()
    {
        string statePath = ContentUpdateScript.GetContentStateDataPath(false);
        if (string.IsNullOrEmpty(statePath))
        {
            Debug.LogWarning("[Addressables] 이전 빌드 상태 없음. Clean 빌드 먼저 실행하세요.");
            return;
        }
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var entries = ContentUpdateScript.GatherModifiedEntries(settings, statePath);
        if (entries.Count == 0)
            Debug.Log("[Addressables] 변경된 에셋 없음.");
        else
        {
            Debug.Log($"[Addressables] 변경된 에셋 {entries.Count}개:");
            foreach (var e in entries)
                Debug.Log($"  - {e.address}");
        }
    }
}
#endif
