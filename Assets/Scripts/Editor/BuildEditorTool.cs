using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using UnityEditor.Build.Reporting;
using Seunghak;
#if UNITY_IOS
using System.Collections.Generic;
using UnityEditor.iOS.Xcode;
#endif
using Seunghak.Common;
public class BuildEditorTool : MonoBehaviour
{
#if UNITY_ANDROID
    private static string buildPath = "AOS";
#elif UNITY_IOS
    private static string buildPath = "IOS";
#elif UNITY_STANDALONE_WIN
    private static string buildPath = "WINDOW";
#endif
    static private bool isAABBundle = false;

    const string isApkBuild = "Tools/IsAABBuild";
    [MenuItem(isApkBuild)]
    public static void ToggleSimulationMode()
    {
        isAABBundle = !isAABBundle;
    }
    [MenuItem(isApkBuild, true)]
    public static bool ToggleSimulationModeValidate()
    {
        Menu.SetChecked(isApkBuild, BuildEditorTool.isAABBundle);
        return true;
    }
#if UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_WIN
    [MenuItem("Build/BuildAssetBundles", false, 1002)]
    public static void BuildBundles()
    {
#if UNITY_ANDROID
        BuildPipeline.BuildAssetBundles($"Assets/{Utility.GetPlatformName()}", BuildAssetBundleOptions.None, BuildTarget.Android);
#elif UNITY_IOS
        BuildPipeline.BuildAssetBundles($"Assets/{Utility.GetPlatformName()}", BuildAssetBundleOptions.None, BuildTarget.iOS);
#elif UNITY_STANDALONE_WIN
        BuildPipeline.BuildAssetBundles($"Assets/{Utility.GetPlatformName()}", BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);
#endif
    }

    [MenuItem("Build/PlatformBuild", false, 1001)]
    public static void BuildPlatform()
    {
        BuildPlayerOptions buildOption = new BuildPlayerOptions();
        buildOption.locationPathName = $"./Build/{buildPath}/{System.DateTime.UtcNow.ToString("yy_MM_dd_hh_mm")}";
#if UNITY_ANDROID
        PlayerSettings.bundleVersion = "1.0.1";
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        EditorUserBuildSettings.buildAppBundle = isAABBundle;
        BuildForAndroid();
        buildOption.target = BuildTarget.Android;
        buildOption.locationPathName = buildOption.locationPathName;
#elif UNITY_IOS
        PlayerSettings.bundleVersion = "1.0.1";
        buildOption.target = BuildTarget.iOS;
        buildOption.locationPathName = buildOption.locationPathName;
#elif UNITY_STANDALONE_WIN
        buildOption.target = BuildTarget.StandaloneWindows64;
        buildOption.targetGroup = BuildTargetGroup.Standalone;
        buildOption.locationPathName = buildOption.locationPathName  + "/SirenLive.exe";
        buildOption.options = BuildOptions.None;
#endif
        buildOption.scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);
        BuildReport report = BuildPipeline.BuildPlayer(buildOption);
        MakeVersionAndCDNFile();
        //if (Directory.Exists($"../../Build/{buildPath}"))
        //{
        //    string argument = $"../../Build/{buildPath}";
        //    try
        //    {
        //        System.Diagnostics.Process.Start("open", argument);
        //    }
        //    catch (Exception e)
        //    {
        //        Debug.Log($"Fail open file {e.ToString()}");
        //    }
        //}
    }
#endif

    /// <summary>
    /// WebGL 빌드를 프로젝트 루트의 Build/WebGL 폴더에 추출합니다. (토스 AIT 패키징 시 해당 경로 사용)
    /// </summary>
    [MenuItem("Build/WebGL to Build Folder", false, 1000)]
    public static void BuildWebGLToBuildFolder()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            if (!EditorUtility.DisplayDialog("WebGL 빌드", "현재 플랫폼이 WebGL이 아닙니다. WebGL로 전환한 뒤 빌드할까요?", "전환 후 빌드", "취소"))
                return;
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError("WebGL 플랫폼 전환에 실패했습니다.");
                return;
            }
        }

        string buildOutputPath = Path.Combine(Application.dataPath, "..", "Build", "WebGL");
        buildOutputPath = Path.GetFullPath(buildOutputPath);

        var buildOption = new BuildPlayerOptions
        {
            scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
            locationPathName = buildOutputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(buildOption);
        if (report.summary.result == BuildResult.Succeeded)
            Debug.Log($"[Build] WebGL 빌드 완료 → {buildOutputPath}");
        else
            Debug.LogError($"[Build] WebGL 빌드 실패: {report.summary.result}");
    }

    [MenuItem("Build/InfoFiles", false, 1002)]
    private static void MakeVersionAndCDNFile()
    {
        UpdateVersionInfo userUpdateVersion = new UpdateVersionInfo();
        userUpdateVersion.currentVersion = PlayerSettings.bundleVersion;
        userUpdateVersion.forcedUpdateVersion = userUpdateVersion.currentVersion;
        userUpdateVersion.cdnAddressInfoPath = $"{ApplicationManager.cdnAddressPath}{FileUtils.GetPlatformString()}";
        string dataSavePath = $"{FileUtils.DATA_SAVE_PATH}";
        FileUtils.SaveFile<UpdateVersionInfo>(dataSavePath, FileUtils.VERSION_INFO_FILE_NAME, userUpdateVersion);

        CDNUpdateAddressInfo cdnUpdateInfo = new CDNUpdateAddressInfo();
        cdnUpdateInfo.updateCDNPath = $"{userUpdateVersion.cdnAddressInfoPath}/{userUpdateVersion.currentVersion}/";
        FileUtils.SaveFile<CDNUpdateAddressInfo>(dataSavePath, FileUtils.CDN_ADDRESS_FILE_NAME, cdnUpdateInfo);
    }
#if UNITY_ANDROID
    private static void BuildForAndroid()
    {
        PlayerSettings.Android.keystorePass = "asdf1234";
        PlayerSettings.Android.keyaliasPass = "asdf1234";
        PlayerSettings.Android.targetArchitectures = isAABBundle ? AndroidArchitecture.ARM64 : PlayerSettings.Android.targetArchitectures;

        //build aab
        PlayerSettings.Android.buildApkPerCpuArchitecture = true;
    }
#endif
#if UNITY_IOS
    private static string PHOTO_USAGE_STRING = "해당 앱을 사용하시려면 사진첩 접근 권한이 필요합니다.";
    [UnityEditor.Callbacks.PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        string pbxProjectPath = PBXProject.GetPBXProjectPath(buildPath);
        string plistPath = Path.Combine(buildPath, "Info.plist");

        PBXProject pbxProject = new PBXProject();
        pbxProject.ReadFromFile(pbxProjectPath);

#if UNITY_2019_3_OR_NEWER
        string targetGUID = pbxProject.GetUnityFrameworkTargetGuid();
#else
        string targetGUID = pbxProject.TargetGuidByName(pbxProject.GetUnityMainTargetGuid());
#endif
        //프로퍼티 추가 방식
        //pbxProject.AddBuildProperty(targetGUID, "OTHER_LDFLAGS", "-weak_framework PhotosUI");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromString(File.ReadAllText(plistPath));

        PlistElementDict rootDic = plist.root;

        rootDic.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        rootDic.SetString("NSPhotoLibraryUsageDescription", PHOTO_USAGE_STRING);
        //사진 추가 권한
        rootDic.SetString("NSPhotoLibraryAddUsageDescription", PHOTO_USAGE_STRING);
        //마이크 사용 권한
        rootDic.SetString("NSMicrophonUsageDescription", PHOTO_USAGE_STRING);
        File.WriteAllText(plistPath, plist.WriteToString());
    }
#endif
}
