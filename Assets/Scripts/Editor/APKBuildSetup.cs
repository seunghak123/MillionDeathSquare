#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

using System.IO;

/// <summary>
/// APK 빌드 원스톱 셋업 + 빌드 도구.
/// Unity 메뉴 → Build/Android 에서 실행.
///
/// [Setup Android Settings] : 패키지명·SDK·아키텍처 등 PlayerSettings 일괄 설정
/// [Build APK (Debug)]      : Debug 빌드 — Unity 자동 서명 키스토어 사용
/// [Build APK (Release)]    : Release 빌드 — 커스텀 키스토어 사용 (설정 필요)
/// [Open Build Folder]      : 빌드 출력 폴더 열기
/// </summary>
public static class APKBuildSetup
{
    // ── 수정 가능한 설정 ─────────────────────────────────────────────
    private const string PackageName      = "com.seunghak.milliondeathsquare";
    private const string AppVersion       = "1.0.0";
    private const int    BundleVersionCode = 1;
    private const string CompanyName      = "Seunghak";
    private const string ProductName      = "백만 번 죽는 네모";

    private const string KeystorePath     = "keystore/milliondeathsquare.keystore";
    private const string KeystorePass     = "asdf1234";
    private const string KeyAliasName     = "milliondeathsquare";
    private const string KeyAliasPass     = "asdf1234";

    private static readonly string BuildOutputDir =
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build", "Android"));

    private static readonly string[] ScenePaths = { "Assets/Scenes/Base.unity" };

    // ── 메뉴 아이템 ─────────────────────────────────────────────────

    [MenuItem("Build/Android/① Setup Android Settings", false, 100)]
    public static void SetupAndroidSettings()
    {
        // 플랫폼 전환
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            bool ok = EditorUtility.DisplayDialog("플랫폼 전환",
                "Android 플랫폼으로 전환합니다. 시간이 걸릴 수 있습니다.", "전환", "취소");
            if (!ok) return;

            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);
            if (!switched)
            {
                Debug.LogError("[APKBuild] Android 플랫폼 전환 실패. Android Build Support가 설치되어 있는지 확인하세요.");
                EditorUtility.DisplayDialog("오류", "Android Build Support가 설치되어 있지 않습니다.\nUnity Hub → Installs → 모듈 추가 → Android Build Support 설치 후 다시 시도하세요.", "확인");
                return;
            }
        }

        // 기본 앱 정보
        PlayerSettings.companyName        = CompanyName;
        PlayerSettings.productName        = ProductName;
        PlayerSettings.bundleVersion      = AppVersion;
        PlayerSettings.SetApplicationIdentifier(
            UnityEditor.Build.NamedBuildTarget.Android, PackageName);

        // Android 전용
        PlayerSettings.Android.bundleVersionCode     = BundleVersionCode;
        PlayerSettings.Android.minSdkVersion         = AndroidSdkVersions.AndroidApiLevel23;   // Android 6.0+
        PlayerSettings.Android.targetSdkVersion      = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.targetArchitectures   = AndroidArchitecture.ARM64;
        PlayerSettings.Android.buildApkPerCpuArchitecture = false;

        // 렌더링
        PlayerSettings.colorSpace                    = ColorSpace.Linear;
        PlayerSettings.gpuSkinning                   = true;

        // APK (AAB 아님)
        EditorUserBuildSettings.buildAppBundle       = false;

        // 씬 목록 등록
        ApplyScenesToBuildSettings();

        AssetDatabase.SaveAssets();
        Debug.Log("[APKBuild] Android 세팅 완료.");
        EditorUtility.DisplayDialog("세팅 완료",
            $"Android 세팅이 완료되었습니다.\n\n" +
            $"패키지명: {PackageName}\n" +
            $"버전: {AppVersion} ({BundleVersionCode})\n" +
            $"Min SDK: 22 (Android 5.1)\n" +
            $"Architecture: ARM64\n\n" +
            "이제 Build APK (Debug) 를 실행하세요.",
            "확인");
    }

    [MenuItem("Build/Android/② Build APK (Debug — 자동 서명)", false, 101)]
    public static void BuildAPKDebug()
    {
        if (!CheckAndroidPlatform()) return;

        // Debug 빌드 — Unity 자동 서명 (커스텀 키스토어 불필요)
        PlayerSettings.Android.useCustomKeystore = false;
        EditorUserBuildSettings.buildAppBundle   = false;

        string outputDir  = Path.Combine(BuildOutputDir, "Debug");
        string outputPath = Path.Combine(outputDir, $"{ProductName}_v{AppVersion}_debug.apk");
        Directory.CreateDirectory(outputDir);

        ApplyScenesToBuildSettings();

        var opt = new BuildPlayerOptions
        {
            scenes             = ScenePaths,
            locationPathName   = outputPath,
            target             = BuildTarget.Android,
            targetGroup        = BuildTargetGroup.Android,
            options            = BuildOptions.Development | BuildOptions.AllowDebugging,
        };

        Debug.Log($"[APKBuild] Debug APK 빌드 시작 → {outputPath}");
        var report = BuildPipeline.BuildPlayer(opt);
        HandleBuildResult(report, outputPath);
    }

    [MenuItem("Build/Android/③ Build APK (Release — 커스텀 키스토어)", false, 102)]
    public static void BuildAPKRelease()
    {
        if (!CheckAndroidPlatform()) return;

        // 키스토어 확인 / 생성
        if (!EnsureKeystore())
        {
            EditorUtility.DisplayDialog("키스토어 없음",
                $"키스토어 파일이 없습니다.\n경로: {KeystorePath}\n\n" +
                "Debug 빌드 또는 수동으로 키스토어를 생성한 뒤 다시 시도하세요.", "확인");
            return;
        }

        string absKeystore = Path.GetFullPath(Path.Combine(Application.dataPath, "..", KeystorePath));
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName      = absKeystore;
        PlayerSettings.Android.keystorePass      = KeystorePass;
        PlayerSettings.Android.keyaliasName      = KeyAliasName;
        PlayerSettings.Android.keyaliasPass      = KeyAliasPass;
        EditorUserBuildSettings.buildAppBundle   = false;

        string outputDir  = Path.Combine(BuildOutputDir, "Release");
        string outputPath = Path.Combine(outputDir, $"{ProductName}_v{AppVersion}.apk");
        Directory.CreateDirectory(outputDir);

        ApplyScenesToBuildSettings();

        var opt = new BuildPlayerOptions
        {
            scenes           = ScenePaths,
            locationPathName = outputPath,
            target           = BuildTarget.Android,
            targetGroup      = BuildTargetGroup.Android,
            options          = BuildOptions.None,
        };

        Debug.Log($"[APKBuild] Release APK 빌드 시작 → {outputPath}");
        var report = BuildPipeline.BuildPlayer(opt);
        HandleBuildResult(report, outputPath);
    }

    [MenuItem("Build/Android/④ Generate Keystore (keytool 필요)", false, 103)]
    public static void GenerateKeystore()
    {
        string keystoreAbsPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", KeystorePath));
        string keystoreDir = Path.GetDirectoryName(keystoreAbsPath);

        if (File.Exists(keystoreAbsPath))
        {
            EditorUtility.DisplayDialog("키스토어 존재",
                $"이미 존재합니다:\n{keystoreAbsPath}", "확인");
            return;
        }

        Directory.CreateDirectory(keystoreDir);

        // keytool 명령 실행
        string keytoolArgs =
            $"-genkey -v -keystore \"{keystoreAbsPath}\"" +
            $" -alias {KeyAliasName}" +
            $" -keyalg RSA -keysize 2048 -validity 10000" +
            $" -storepass {KeystorePass} -keypass {KeyAliasPass}" +
            $" -dname \"CN=Seunghak, OU=Dev, O=Seunghak, L=Seoul, S=Seoul, C=KR\"";

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("keytool", keytoolArgs)
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };
            var proc = System.Diagnostics.Process.Start(psi);
            proc.WaitForExit(30000);

            if (File.Exists(keystoreAbsPath))
            {
                Debug.Log($"[APKBuild] 키스토어 생성 완료: {keystoreAbsPath}");
                EditorUtility.DisplayDialog("키스토어 생성 완료",
                    $"키스토어 생성 완료!\n경로: {keystoreAbsPath}\n\n이제 Build APK (Release) 를 실행하세요.", "확인");
            }
            else
            {
                Debug.LogError("[APKBuild] keytool 실행 실패. Java JDK PATH를 확인하세요.");
                EditorUtility.DisplayDialog("키스토어 생성 실패",
                    "keytool 실행에 실패했습니다.\n\nJava JDK가 PATH에 등록되어 있는지 확인하세요.\n또는 Debug 빌드를 사용하세요.", "확인");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[APKBuild] keytool 오류: {e.Message}");
            EditorUtility.DisplayDialog("keytool 없음",
                $"keytool을 찾을 수 없습니다.\n\nJava JDK를 설치하고 PATH에 등록하세요.\n또는 Debug 빌드를 사용하세요.\n\n오류: {e.Message}", "확인");
        }
    }

    [MenuItem("Build/Android/⑤ Open Build Folder", false, 110)]
    public static void OpenBuildFolder()
    {
        Directory.CreateDirectory(BuildOutputDir);
        EditorUtility.RevealInFinder(BuildOutputDir);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────

    private static bool CheckAndroidPlatform()
    {
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) return true;
        EditorUtility.DisplayDialog("플랫폼 오류",
            "현재 플랫폼이 Android가 아닙니다.\n'① Setup Android Settings' 를 먼저 실행하세요.", "확인");
        return false;
    }

    private static void ApplyScenesToBuildSettings()
    {
        var scenes = new EditorBuildSettingsScene[ScenePaths.Length];
        for (int i = 0; i < ScenePaths.Length; i++)
            scenes[i] = new EditorBuildSettingsScene(ScenePaths[i], true);
        EditorBuildSettings.scenes = scenes;
    }

    private static bool EnsureKeystore()
    {
        string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", KeystorePath));
        return File.Exists(abs);
    }

    private static void HandleBuildResult(BuildReport report, string outputPath)
    {
        if (report.summary.result == BuildResult.Succeeded)
        {
            long size = new FileInfo(outputPath).Length / (1024 * 1024);
            Debug.Log($"[APKBuild] 빌드 성공! 크기: {size} MB → {outputPath}");
            bool open = EditorUtility.DisplayDialog("APK 빌드 성공!",
                $"APK 빌드가 완료되었습니다.\n크기: {size} MB\n경로: {outputPath}\n\n폴더를 여시겠습니까?",
                "폴더 열기", "닫기");
            if (open)
                EditorUtility.RevealInFinder(outputPath);
        }
        else
        {
            Debug.LogError($"[APKBuild] 빌드 실패: {report.summary.result}  " +
                           $"오류: {report.summary.totalErrors}개");
            EditorUtility.DisplayDialog("빌드 실패",
                $"빌드가 실패했습니다.\nConsole 창에서 오류를 확인하세요.\n결과: {report.summary.result}", "확인");
        }
    }
}
#endif
