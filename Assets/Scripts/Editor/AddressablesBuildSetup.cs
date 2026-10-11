using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Seunghak.Editor
{
    /// <summary>
    /// 플레이어 빌드용 Addressables 그룹 정리 + 콘텐츠 빌드.
    ///
    /// 배경: 에디터에서는 GameResourceManager / BundleManager가 AssetDatabase로 우회해 프리팹을 찾기 때문에
    /// 문제가 드러나지 않지만, 빌드에서는 Addressables 번들(또는 Resources)에 있는 것만 로드된다.
    ///   - UI 창 프리팹(Assets/BaseSources/Prefabs/UI)이 Addressables에 등록돼 있지 않았다
    ///   - (구) 게임 그룹에 BundledAssetGroupSchema가 없어 번들로 빌드되지 않았다
    ///   - 빌드 파이프라인에 Addressables 콘텐츠 빌드 단계가 없었다
    ///
    /// SetupGroups()는 여러 번 실행해도 결과가 같다 (이미 맞으면 그대로 둔다).
    /// </summary>
    public static class AddressablesBuildSetup
    {
        private const string UiPrefabFolder = "Assets/BaseSources/Prefabs/UI";
        private const string UiGroupName = "UI";
        /// <summary>BundleManager가 앱 시작 시 자동 로드하는 라벨.</summary>
        private const string MainLabel = "main";

        [MenuItem("Tools/Addressables/Setup Groups For Build")]
        public static void SetupGroupsMenu() => SetupGroups();

        [MenuItem("Tools/Addressables/Build Player Content")]
        public static void BuildContentMenu() => BuildContent();

        /// <summary>그룹 스키마 보정 + UI 프리팹 등록. 성공 여부 반환.</summary>
        public static bool SetupGroups()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[AddressablesBuildSetup] AddressableAssetSettings 를 찾을 수 없습니다.");
                return false;
            }

            // 1) 번들 스키마가 없는 그룹 보정 (Built In Data 같은 PlayerData 그룹은 제외)
            foreach (var group in settings.groups)
            {
                if (group == null || group.HasSchema<PlayerDataGroupSchema>()) continue;
                EnsureLocalBundleSchema(settings, group);
            }

            // 2) UI 그룹 + UI 프리팹 등록 (주소 = 파일명, 라벨 main)
            var uiGroup = settings.FindGroup(UiGroupName);
            if (uiGroup == null)
            {
                uiGroup = settings.CreateGroup(UiGroupName, false, false, false, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                Debug.Log($"[AddressablesBuildSetup] 그룹 생성: {UiGroupName}");
            }
            EnsureLocalBundleSchema(settings, uiGroup);

            settings.AddLabel(MainLabel, false);

            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { UiPrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var entry = settings.CreateOrMoveEntry(guid, uiGroup, false, false);
                entry.address = Path.GetFileNameWithoutExtension(path);
                entry.SetLabel(MainLabel, true, false, false);
                count++;
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AddressablesBuildSetup] 그룹 정리 완료 — UI 프리팹 {count}개 등록 (라벨 '{MainLabel}')");
            return true;
        }

        /// <summary>그룹 정리 후 현재 빌드 타겟용 Addressables 콘텐츠를 빌드. 성공 여부 반환.</summary>
        public static bool BuildContent()
        {
            if (!SetupGroups()) return false;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Debug.Log($"[AddressablesBuildSetup] 콘텐츠 빌드 시작 — 타겟: {EditorUserBuildSettings.activeBuildTarget}");

            AddressableAssetSettings.CleanPlayerContent(settings.ActivePlayerDataBuilder);
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);

            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError($"[AddressablesBuildSetup] 콘텐츠 빌드 실패: {result.Error}");
                return false;
            }

            Debug.Log($"[AddressablesBuildSetup] 콘텐츠 빌드 완료 ({result.Duration:F1}s) — {result.OutputPath}");
            return true;
        }

        private static void EnsureLocalBundleSchema(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            var bundle = group.GetSchema<BundledAssetGroupSchema>();
            if (bundle == null)
            {
                bundle = group.AddSchema<BundledAssetGroupSchema>();
                bundle.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                bundle.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                Debug.Log($"[AddressablesBuildSetup] {group.Name}: BundledAssetGroupSchema 추가 (Local 경로)");
            }

            if (!group.HasSchema<ContentUpdateGroupSchema>())
                group.AddSchema<ContentUpdateGroupSchema>();
        }
    }
}
