using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Seunghak.Editor
{
    /// <summary>
    /// Facebook Instant Games WebGL 빌드 자동화 도구.
    ///
    /// 메뉴: Tools/Facebook Instant Games/...
    ///
    /// Facebook 5MB 초기 로딩 제한 대응:
    ///   - IL2CPP + Strip Engine Code + Managed Stripping: High
    ///   - Brotli 압축 (서버 Content-Encoding 필요)
    ///   - Exception Support: ExplicitlyThrownExceptionsOnly
    ///   - Initial Memory: 32MB (최소값, 런타임 동적 확장)
    ///   - 씬별 번들 분리: main(초기) / lobby / ingame (지연 로드)
    /// </summary>
    public static class FacebookBuildProfile
    {
        private const string BUILD_OUTPUT_DIR = "Builds/FacebookInstantGames";
        private const string WEBGL_TEMPLATE   = "FacebookTemplate";

        // ── 메뉴 진입점 ───────────────────────────────────────────────────

        [MenuItem("Tools/Facebook Instant Games/빌드 설정 적용")]
        public static void ApplyFacebookBuildSettings()
        {
            ApplySettings();
            Debug.Log("[FacebookBuildProfile] Facebook Instant Games 빌드 설정 적용 완료.");
            EditorUtility.DisplayDialog(
                "설정 완료",
                "Facebook Instant Games 빌드 설정이 적용되었습니다.\n\n" +
                "- 플랫폼: WebGL\n" +
                "- 템플릿: FacebookTemplate\n" +
                "- 압축: Brotli\n" +
                "- IL2CPP 코드 스트리핑: High\n\n" +
                "'빌드 실행'으로 빌드를 시작하세요.",
                "확인");
        }

        [MenuItem("Tools/Facebook Instant Games/빌드 실행")]
        public static void BuildFacebookWebGL()
        {
            if (!ValidateBuildEnvironment())
                return;

            ApplySettings();

            if (!Directory.Exists(BUILD_OUTPUT_DIR))
                Directory.CreateDirectory(BUILD_OUTPUT_DIR);

            var buildOptions = new BuildPlayerOptions
            {
                scenes      = GetEnabledScenes(),
                locationPathName = BUILD_OUTPUT_DIR,
                target      = BuildTarget.WebGL,
                options     = BuildOptions.None,
            };

            Debug.Log($"[FacebookBuildProfile] 빌드 시작 → {BUILD_OUTPUT_DIR}");
            var report = BuildPipeline.BuildPlayer(buildOptions);

            if (report.summary.result == BuildResult.Succeeded)
            {
                long size = GetDirectorySize(BUILD_OUTPUT_DIR);
                Debug.Log($"[FacebookBuildProfile] 빌드 성공 — 총 크기: {FormatBytes(size)}");

                // fbapp-config.json 복사 (빌드 출력 루트에 배치)
                CopyFbAppConfig();

                EditorUtility.DisplayDialog(
                    "빌드 성공",
                    $"빌드 완료!\n\n출력 경로: {BUILD_OUTPUT_DIR}\n총 크기: {FormatBytes(size)}\n\n" +
                    "Facebook 개발자 콘솔에 업로드하세요.\n(Hosting 탭 → Upload)\n\n" +
                    "주의: 초기 로딩 파일이 5MB를 초과하면 업로드가 거부됩니다.",
                    "확인");

                // 빌드 폴더 열기
                EditorUtility.RevealInFinder(BUILD_OUTPUT_DIR);
            }
            else
            {
                Debug.LogError($"[FacebookBuildProfile] 빌드 실패: {report.summary.result}");
                EditorUtility.DisplayDialog("빌드 실패",
                    $"빌드가 실패했습니다.\nConsole 로그를 확인하세요.\n결과: {report.summary.result}",
                    "확인");
            }
        }

        [MenuItem("Tools/Facebook Instant Games/빌드 사이즈 리포트")]
        public static void ShowBuildSizeReport()
        {
            if (!Directory.Exists(BUILD_OUTPUT_DIR))
            {
                EditorUtility.DisplayDialog("알림", $"빌드 폴더가 없습니다.\n먼저 빌드를 실행하세요.\n경로: {BUILD_OUTPUT_DIR}", "확인");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== Facebook Instant Games 빌드 사이즈 리포트 ===");
            sb.AppendLine($"출력 경로: {BUILD_OUTPUT_DIR}");
            sb.AppendLine();

            // Build/ 폴더 파일별 사이즈 출력
            string buildSubDir = Path.Combine(BUILD_OUTPUT_DIR, "Build");
            if (Directory.Exists(buildSubDir))
            {
                sb.AppendLine("[Build/ 파일]");
                foreach (var file in Directory.GetFiles(buildSubDir))
                {
                    var fi    = new FileInfo(file);
                    string mark = fi.Length > 5 * 1024 * 1024 ? " ⚠ 5MB 초과" : "";
                    sb.AppendLine($"  {fi.Name}: {FormatBytes(fi.Length)}{mark}");
                }
                sb.AppendLine();
            }

            // StreamingAssets 번들 사이즈
            string streamingDir = Path.Combine(BUILD_OUTPUT_DIR, "StreamingAssets", "Bundles");
            if (Directory.Exists(streamingDir))
            {
                sb.AppendLine("[StreamingAssets/Bundles/ — 지연 로드 번들]");
                foreach (var file in Directory.GetFiles(streamingDir, "*", SearchOption.AllDirectories))
                {
                    var fi = new FileInfo(file);
                    sb.AppendLine($"  {fi.Name}: {FormatBytes(fi.Length)}");
                }
                sb.AppendLine();
            }

            long totalSize = GetDirectorySize(BUILD_OUTPUT_DIR);
            sb.AppendLine($"전체 크기: {FormatBytes(totalSize)}");
            sb.AppendLine();
            sb.AppendLine("Facebook 5MB 제한: 초기 로딩 파일(index.html + Build/*.br)만 해당.");
            sb.AppendLine("StreamingAssets 번들은 런타임 지연 로드 — 제한 제외.");

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("빌드 사이즈 리포트", sb.ToString(), "확인");
        }

        [MenuItem("Tools/Facebook Instant Games/설정 검증")]
        public static bool ValidateBuildEnvironment()
        {
            var issues = new StringBuilder();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                issues.AppendLine("- 빌드 타겟이 WebGL이 아닙니다.");

            if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.WebGL) != ScriptingImplementation.IL2CPP)
                issues.AppendLine("- IL2CPP가 아닙니다. Player Settings → Scripting Backend: IL2CPP 필요.");

            string templatePath = Path.Combine("Assets", "WebGLTemplates", WEBGL_TEMPLATE, "index.html");
            if (!File.Exists(templatePath))
                issues.AppendLine($"- WebGL 템플릿 없음: {templatePath}");

            string fbConfigPath = Path.Combine("Assets", "WebGLTemplates", WEBGL_TEMPLATE, "fbapp-config.json");
            if (!File.Exists(fbConfigPath))
                issues.AppendLine($"- fbapp-config.json 없음: {fbConfigPath}");

            if (issues.Length > 0)
            {
                string msg = "다음 문제를 해결 후 빌드하세요:\n\n" + issues;
                Debug.LogWarning("[FacebookBuildProfile] " + msg);
                EditorUtility.DisplayDialog("빌드 설정 문제", msg, "확인");
                return false;
            }

            Debug.Log("[FacebookBuildProfile] 빌드 환경 검증 통과.");
            return true;
        }

        // ── 내부 ──────────────────────────────────────────────────────────

        private static void ApplySettings()
        {
            // WebGL 플랫폼 전환 (이미 WebGL이면 no-op)
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            // 압축: Brotli (5MB 제한 대응 — 서버에서 Content-Encoding: br 필요)
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;

            // IL2CPP — 코드 크기 최소화
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.WebGL, ScriptingImplementation.IL2CPP);

            // 코드 스트리핑 (High: 미사용 엔진 모듈 제거)
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.High);

            // 예외 처리: 퍼포먼스 우선 (명시적 throw만)
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            // 메모리: 32MB 초기값 (Facebook 권장 최솟값)
            PlayerSettings.WebGL.memorySize = 32;

            // WebGL 템플릿 설정
            PlayerSettings.WebGL.template = $"PROJECT:{WEBGL_TEMPLATE}";

            // 데이터 캐싱 활성화 (재방문 시 빠른 로드)
            PlayerSettings.WebGL.dataCaching = true;

            // 선형 색공간 (그래픽 품질)
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // 토스 환경 심볼(TOSS_DEV/TOSS_PROD)은 토스 빌드 전용
            WebGLBuildTool.ClearTossEnvDefines();
        }

        private static void CopyFbAppConfig()
        {
            string src = Path.Combine("Assets", "WebGLTemplates", WEBGL_TEMPLATE, "fbapp-config.json");
            string dst = Path.Combine(BUILD_OUTPUT_DIR, "fbapp-config.json");

            if (File.Exists(src))
            {
                File.Copy(src, dst, overwrite: true);
                Debug.Log($"[FacebookBuildProfile] fbapp-config.json → {dst}");
            }
            else
            {
                Debug.LogWarning("[FacebookBuildProfile] fbapp-config.json 없음 — 수동 배치 필요.");
            }
        }

        private static string[] GetEnabledScenes()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                    scenes.Add(scene.path);
            }
            return scenes.ToArray();
        }

        private static long GetDirectorySize(string path)
        {
            if (!Directory.Exists(path)) return 0;
            long size = 0;
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                size += new FileInfo(file).Length;
            return size;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }
    }
}
