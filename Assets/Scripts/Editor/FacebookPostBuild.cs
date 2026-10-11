using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Seunghak.Editor
{
    /// <summary>
    /// Facebook Instant Games 빌드 후 처리기.
    ///
    /// 빌드 완료 시 자동으로:
    ///   1. 초기 로딩 파일 크기 검증 (Facebook 5MB 제한)
    ///   2. fbapp-config.json 빌드 출력에 복사
    ///   3. 크기 리포트를 Console에 출력
    ///
    /// Facebook 5MB 제한 규칙:
    ///   - index.html + Build/ 폴더의 압축 파일(.br/.gz)만 해당
    ///   - StreamingAssets/Bundles/ 는 런타임 지연 로드 → 제외
    /// </summary>
    public class FacebookPostBuild : IPostprocessBuildWithReport
    {
        // 낮은 callbackOrder = 먼저 실행 (TossPostBuild 이후)
        public int callbackOrder => 20;

        private const long FB_SIZE_LIMIT_BYTES = 5L * 1024 * 1024; // 5MB
        private const string WEBGL_TEMPLATE = "FacebookTemplate";
        private const string FB_CONFIG_FILE  = "fbapp-config.json";

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
                return;

            // FacebookTemplate으로 빌드한 경우에만 실행
            if (!PlayerSettings.WebGL.template.Contains(WEBGL_TEMPLATE))
                return;

            string outputPath = report.summary.outputPath;
            Debug.Log($"[FacebookPostBuild] 빌드 후 처리 시작: {outputPath}");

            CopyFbAppConfig(outputPath);
            AnalyzeBuildSize(outputPath);
        }

        // ── fbapp-config.json 복사 ─────────────────────────────────────

        private static void CopyFbAppConfig(string buildPath)
        {
            string src = Path.Combine("Assets", "WebGLTemplates", WEBGL_TEMPLATE, FB_CONFIG_FILE);
            string dst = Path.Combine(buildPath, FB_CONFIG_FILE);

            if (!File.Exists(src))
            {
                Debug.LogWarning($"[FacebookPostBuild] {FB_CONFIG_FILE} 없음: {src}");
                return;
            }

            File.Copy(src, dst, overwrite: true);
            Debug.Log($"[FacebookPostBuild] {FB_CONFIG_FILE} 복사 완료 → {dst}");
        }

        // ── 빌드 사이즈 분석 ───────────────────────────────────────────

        private static void AnalyzeBuildSize(string buildPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine("  Facebook Instant Games 빌드 사이즈 리포트");
            sb.AppendLine("========================================");

            // 초기 로딩 파일 (5MB 제한 대상)
            var initialFiles  = GetInitialLoadFiles(buildPath);
            long initialTotal = 0;
            bool hasOverLimit  = false;

            sb.AppendLine("\n[초기 로딩 파일 — Facebook 5MB 제한 대상]");
            foreach (var (file, size) in initialFiles)
            {
                initialTotal += size;
                bool over = size > FB_SIZE_LIMIT_BYTES;
                if (over) hasOverLimit = true;
                sb.AppendLine($"  {Path.GetFileName(file),-40} {FormatBytes(size),8}{(over ? " ⚠ 5MB 초과" : "")}");
            }
            sb.AppendLine($"  {"합계",-40} {FormatBytes(initialTotal),8}");

            if (initialTotal > FB_SIZE_LIMIT_BYTES)
            {
                sb.AppendLine($"\n  ⚠ 초기 로딩 합계({FormatBytes(initialTotal)})가 5MB를 초과합니다!");
                sb.AppendLine("  최적화 방법:");
                sb.AppendLine("    - 미사용 엔진 모듈 제거 (Strip Engine Code: High)");
                sb.AppendLine("    - Managed Stripping Level: High");
                sb.AppendLine("    - 텍스처/오디오를 StreamingAssets 번들로 분리");
                sb.AppendLine("    - WebAssembly 코드 스플리팅 검토");
            }
            else
            {
                sb.AppendLine($"\n  초기 로딩 크기 정상 ({FormatBytes(initialTotal)} / 5MB)");
            }

            // 지연 로드 번들 (제한 제외)
            string bundleDir = Path.Combine(buildPath, "StreamingAssets", "Bundles");
            if (Directory.Exists(bundleDir))
            {
                sb.AppendLine("\n[StreamingAssets/Bundles/ — 런타임 지연 로드 (제한 제외)]");
                long bundleTotal = 0;
                foreach (var file in Directory.GetFiles(bundleDir, "*", SearchOption.AllDirectories))
                {
                    var fi = new FileInfo(file);
                    bundleTotal += fi.Length;
                    sb.AppendLine($"  {fi.Name,-40} {FormatBytes(fi.Length),8}");
                }
                sb.AppendLine($"  {"번들 합계",-40} {FormatBytes(bundleTotal),8}");
            }

            sb.AppendLine("\n========================================");

            if (hasOverLimit || initialTotal > FB_SIZE_LIMIT_BYTES)
            {
                Debug.LogWarning("[FacebookPostBuild]\n" + sb);
                EditorUtility.DisplayDialog(
                    "Facebook 빌드 크기 경고",
                    $"초기 로딩 파일 크기가 5MB 제한에 근접하거나 초과했습니다.\n\n" +
                    $"초기 로딩 합계: {FormatBytes(initialTotal)}\n\n" +
                    "Console에서 상세 리포트를 확인하세요.",
                    "확인");
            }
            else
            {
                Debug.Log("[FacebookPostBuild]\n" + sb);
            }
        }

        /// <summary>
        /// Facebook 5MB 제한 대상 파일 수집.
        /// Build/ 폴더의 압축 파일(.br, .gz) + index.html
        /// </summary>
        private static List<(string path, long size)> GetInitialLoadFiles(string buildPath)
        {
            var result  = new List<(string path, long size)>();
            var indexHtml = Path.Combine(buildPath, "index.html");
            if (File.Exists(indexHtml))
                result.Add((indexHtml, new FileInfo(indexHtml).Length));

            string buildDir = Path.Combine(buildPath, "Build");
            if (!Directory.Exists(buildDir))
                return result;

            // 압축 파일 우선 (.br > .gz > 원본)
            var extensions = new[] { ".br", ".gz", ".data", ".js", ".wasm" };
            foreach (var file in Directory.GetFiles(buildDir))
            {
                var fi = new FileInfo(file);
                foreach (var ext in extensions)
                {
                    if (fi.Name.EndsWith(ext))
                    {
                        result.Add((file, fi.Length));
                        break;
                    }
                }
            }

            result.Sort((a, b) => b.size.CompareTo(a.size)); // 큰 파일 먼저
            return result;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }
    }
}
