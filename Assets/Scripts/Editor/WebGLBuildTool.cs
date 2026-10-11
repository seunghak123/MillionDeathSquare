using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Seunghak.Editor
{
    /// <summary>
    /// itch.io / Newgrounds WebGL 빌드 자동화 도구.
    ///
    /// 메뉴: Tools/WebGL Portals/...
    ///
    /// itch.io 빌드 특성:
    ///   - GuestLogin만 사용 (소셜 로그인 없음)
    ///   - 광고 없음 (NullAds)
    ///   - ItchTemplate — 미니멀 로딩 화면
    ///   - 최대 500MB 업로드 허용 (압축 사용 시 여유로움)
    ///
    /// Newgrounds 빌드 특성:
    ///   - GuestLogin + NGIO 메달/스코어보드
    ///   - NewgroundsTemplate — ngio.js 자동 로드
    ///   - NEWGROUNDS 빌드 심볼로 조건부 컴파일
    ///   - 파일 크기 제한 없음
    /// </summary>
    public static class WebGLBuildTool
    {
        private const string ITCH_OUTPUT_DIR     = "Builds/ItchIO";
        private const string NG_OUTPUT_DIR       = "Builds/Newgrounds";
        private const string TOSS_OUTPUT_DIR     = "Builds/Toss";
        private const string GD_OUTPUT_DIR       = "Builds/GameDistribution";
        private const string CG_OUTPUT_DIR       = "Builds/CrazyGames";
        private const string ITCH_TEMPLATE       = "ItchTemplate";
        private const string NG_TEMPLATE         = "NewgroundsTemplate";

        // ── Toss 미니게임 ──────────────────────────────────────────────────

        [MenuItem("Tools/WebGL Portals/Toss 미니게임/빌드 설정 적용")]
        public static void ApplyTossSettings()
        {
            ApplyBaseWebGLSettings();

            // AITTemplate이 있으면 자동 적용, 없으면 기본 Unity 템플릿 사용
            string aitTemplatePath = Path.Combine("Assets", "WebGLTemplates", "AITTemplate");
            if (Directory.Exists(aitTemplatePath))
                PlayerSettings.WebGL.template = "PROJECT:AITTemplate";

            // Toss는 소셜 플랫폼 심볼 제거 (AIT 로그인은 런타임 IsInsideTossApp()으로 감지)
            RemoveScriptingDefine(BuildTargetGroup.WebGL, "NEWGROUNDS");
            RemoveScriptingDefine(BuildTargetGroup.WebGL, "GAME_DISTRIBUTION");
            RemoveScriptingDefine(BuildTargetGroup.WebGL, "CRAZY_GAMES");

            // Toss는 초기 메모리 32MB (모바일 웹뷰 최적화)
            PlayerSettings.WebGL.memorySize = 32;

            Debug.Log("[WebGLBuildTool] Toss 미니게임 빌드 설정 적용 완료.");
            if (Application.isBatchMode) return; // 배치모드에서는 다이얼로그 금지
            EditorUtility.DisplayDialog("Toss 설정",
                "Toss 미니게임 빌드 설정이 적용되었습니다.\n\n" +
                "- 로그인: 런타임 IsInsideTossApp() 감지 → AIT or GuestLogin\n" +
                "- 메모리: 32MB (모바일 웹뷰 최적화)\n" +
                "- 광고: TossBridge postMessage 기반\n\n" +
                "AITTemplate이 Assets/WebGLTemplates/AITTemplate에 있으면 자동 적용됩니다.",
                "확인");
        }

        [MenuItem("Tools/WebGL Portals/Toss 미니게임/빌드 실행 (dev)")]
        public static void BuildTossDev() => BuildTossEnv(TOSS_ENV_DEV);

        [MenuItem("Tools/WebGL Portals/Toss 미니게임/빌드 실행 (prod)")]
        public static void BuildTossProd() => BuildTossEnv(TOSS_ENV_PROD);

        /// <summary>전체 포털 빌드 등 환경 지정이 없는 호출용 — dev로 빌드.</summary>
        public static void BuildToss() => BuildTossEnv(TOSS_ENV_DEV);

        private static void BuildTossEnv(string env)
        {
            if (!ValidateWebGLTarget()) return;

            if (!ValidateTossBuild(env)) { FailTossBuild(env, "빌드 전 검증 실패 (Console 로그 확인)"); return; }

            ApplyTossSettings();
            ApplyTossEnvDefines(env);

            // 심볼 변경은 재컴파일 후에야 에디터 어셈블리에 반영된다. 반영 전이면 잘못된 환경으로
            // 빌드될 수 있으므로 중단 (배치모드는 deploy-toss.ps1이 "심볼 설정 → 빌드" 2회 실행으로 처리).
            if (!IsTossEnvCompiled(env))
            {
                FailTossBuild(env, "스크립팅 심볼이 아직 컴파일에 반영되지 않았습니다. 재컴파일이 끝난 뒤 같은 메뉴를 다시 실행하세요.");
                return;
            }

            if (!Directory.Exists(TOSS_OUTPUT_DIR))
                Directory.CreateDirectory(TOSS_OUTPUT_DIR);

            var report = Build(TOSS_OUTPUT_DIR);

            if (report.summary.result == BuildResult.Succeeded)
            {
                long size = GetDirSize(TOSS_OUTPUT_DIR);
                Debug.Log($"[WebGLBuildTool] Toss 미니게임({env}) 빌드 성공 — {FormatBytes(size)}");
                EditorUtility.DisplayDialog($"Toss 빌드 완료 ({env})",
                    $"빌드 완료!\n출력: {TOSS_OUTPUT_DIR}\n크기: {FormatBytes(size)}\n\n" +
                    "Toss 미니게임 배포:\n1. Toss 개발자 포털 → 미니앱 관리\n2. 빌드 폴더를 업로드\n3. 테스트 링크로 웹뷰 동작 확인",
                    "확인");
                EditorUtility.RevealInFinder(TOSS_OUTPUT_DIR);
            }
            else
            {
                LogBuildFailed("Toss 미니게임", report);
            }
        }

        // ── Toss 환경(dev/prod) ───────────────────────────────────────────
        //
        // 환경은 스크립팅 심볼로 빌드 시점에 고정된다 (TossConfig 참고).
        //   dev  : TOSS_DEV 추가, TOSS_PROD 제거 → 테스트 광고 / 테스트 프로모션
        //   prod : TOSS_PROD 추가, TOSS_DEV 제거 → 실제 광고 / 실제 프로모션
        //   토스 외 포털 : 둘 다 제거
        // AITConvertCore.DoExport는 extraScriptingDefines를 SDK 고정값으로 채우므로(외부 주입 불가)
        // 심볼은 PlayerSettings에 쓰고, 배치모드에서는 별도 프로세스(-platform toss-env)로 먼저
        // 설정·저장 → 다음 프로세스가 시작 시 해당 심볼로 컴파일한 뒤 빌드한다.

        private const string TOSS_ENV_DEV  = "dev";
        private const string TOSS_ENV_PROD = "prod";
        private const string TOSS_CONFIG_ASSET = "Assets/Resources/TossConfig.asset";
        private const string AIT_CONFIG_ASSET  = "Assets/AppsInToss/Editor/AITConfig.asset";
        private const string TOSS_TEST_AD_PREFIX = "ait-ad-test-";

        /// <summary>토스 외 포털 빌드에서 토스 환경 심볼 제거.</summary>
        public static void ClearTossEnvDefines()
        {
            RemoveScriptingDefine(BuildTargetGroup.WebGL, "TOSS_DEV");
            RemoveScriptingDefine(BuildTargetGroup.WebGL, "TOSS_PROD");
        }

        private static void ApplyTossEnvDefines(string env)
        {
            if (env == TOSS_ENV_PROD)
            {
                AddScriptingDefine(BuildTargetGroup.WebGL, "TOSS_PROD");
                RemoveScriptingDefine(BuildTargetGroup.WebGL, "TOSS_DEV");
            }
            else
            {
                AddScriptingDefine(BuildTargetGroup.WebGL, "TOSS_DEV");
                RemoveScriptingDefine(BuildTargetGroup.WebGL, "TOSS_PROD");
            }
            Debug.Log($"[WebGLBuildTool] Toss 환경 심볼 적용: {env} → {PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL)}");
        }

        /// <summary>현재 컴파일된 TossConfig 상수가 요청 환경과 일치하는지 (심볼이 실제 반영됐는지).</summary>
        private static bool IsTossEnvCompiled(string env)
        {
            bool wantProd = env == TOSS_ENV_PROD;
            return Seunghak.Platform.TossConfig.IsProd == wantProd
                && Seunghak.Platform.TossConfig.IsDevBuild == !wantProd;
        }

        private static string ParseTossEnv()
        {
            string env = GetCLIArg("-tossEnv", TOSS_ENV_DEV).ToLowerInvariant();
            return env == TOSS_ENV_PROD ? TOSS_ENV_PROD : (env == TOSS_ENV_DEV ? TOSS_ENV_DEV : null);
        }

        private static void FailTossBuild(string env, string reason)
        {
            Debug.LogError($"[WebGLBuildTool] Toss({env}) 빌드 중단: {reason}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else EditorUtility.DisplayDialog("Toss 빌드 중단", reason, "확인");
        }

        /// <summary>빌드 전 검증. 에러가 있으면 false (원인을 Console에 명확히 기록).</summary>
        private static bool ValidateTossBuild(string env)
        {
            bool ok = true;

            // 양쪽 환경 공통: 아이콘 URL (brand.icon)
            var ait = AssetDatabase.LoadAssetAtPath<AppsInToss.AITEditorScriptObject>(AIT_CONFIG_ASSET);
            if (ait == null)
            {
                Debug.LogError($"[WebGLBuildTool] 검증 실패: {AIT_CONFIG_ASSET} 를 로드할 수 없습니다.");
                ok = false;
            }
            else if (string.IsNullOrWhiteSpace(ait.iconUrl))
            {
                // SDK 3.5.0은 빈 iconUrl로도 빌드/배포가 된다 (값이 있을 때만 형식 검사). 경고만 남긴다.
                Debug.LogWarning("[WebGLBuildTool] AITConfig.iconUrl 이 비어 있습니다 — 미니앱 브랜드 아이콘 없이 빌드합니다.");
            }

            if (env != TOSS_ENV_PROD) return ok;

            var cfg = AssetDatabase.LoadAssetAtPath<Seunghak.Platform.TossConfig>(TOSS_CONFIG_ASSET);
            if (cfg == null)
            {
                Debug.LogError($"[WebGLBuildTool] prod 검증 실패: {TOSS_CONFIG_ASSET} 가 없습니다. " +
                               "Create > Seunghak > Toss Config 로 만들고 prod 값을 입력하세요.");
                return false;
            }

            ok &= CheckProdAdId("prod.rewardedAdGroupId", cfg.prod.rewardedAdGroupId);
            ok &= CheckProdAdId("prod.interstitialAdGroupId", cfg.prod.interstitialAdGroupId);

            // 프로모션/초대는 비어 있어도 빌드는 진행 (해당 기능만 비활성)
            if (string.IsNullOrWhiteSpace(cfg.prod.promotionCode))
                Debug.LogWarning("[WebGLBuildTool] prod 경고: promotionCode 가 비어 있습니다 (프로모션 지급 비활성).");
            if (string.IsNullOrWhiteSpace(cfg.prod.contactsViralModuleId))
                Debug.LogWarning("[WebGLBuildTool] prod 경고: contactsViralModuleId 가 비어 있습니다 (친구 초대 비활성).");

            return ok;
        }

        private static bool CheckProdAdId(string field, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Debug.LogError($"[WebGLBuildTool] prod 검증 실패: TossConfig.{field} 가 비어 있습니다 (앱인토스 콘솔 발급 광고 그룹 ID 필요).");
                return false;
            }
            if (value.StartsWith(TOSS_TEST_AD_PREFIX))
            {
                Debug.LogError($"[WebGLBuildTool] prod 검증 실패: TossConfig.{field} 가 테스트 ID({TOSS_TEST_AD_PREFIX}…)입니다. 실제 광고 그룹 ID로 교체하세요.");
                return false;
            }
            return true;
        }

        // ── itch.io ───────────────────────────────────────────────────────

        [MenuItem("Tools/WebGL Portals/itch.io/빌드 설정 적용")]
        public static void ApplyItchSettings()
        {
            ApplyBaseWebGLSettings();
            PlayerSettings.WebGL.template = $"PROJECT:{ITCH_TEMPLATE}";
            ClearTossEnvDefines();

            // itch.io는 GuestLogin 전용 — NEWGROUNDS 심볼 제거
            RemoveScriptingDefine(BuildTargetGroup.WebGL, "NEWGROUNDS");
            RemoveScriptingDefine(BuildTargetGroup.WebGL, "GAME_DISTRIBUTION");
            RemoveScriptingDefine(BuildTargetGroup.WebGL, "CRAZY_GAMES");

            Debug.Log("[WebGLBuildTool] itch.io 빌드 설정 적용 완료.");
            EditorUtility.DisplayDialog("itch.io 설정", "itch.io 빌드 설정이 적용되었습니다.\n\n- 템플릿: ItchTemplate\n- 광고: 없음 (NullAds)\n- 로그인: GuestLogin (IndexedDB 영속화)", "확인");
        }

        [MenuItem("Tools/WebGL Portals/itch.io/빌드 실행")]
        public static void BuildItch()
        {
            if (!ValidateWebGLTarget()) return;

            ApplyItchSettings();

            if (!Directory.Exists(ITCH_OUTPUT_DIR))
                Directory.CreateDirectory(ITCH_OUTPUT_DIR);

            var report = Build(ITCH_OUTPUT_DIR);

            if (report.summary.result == BuildResult.Succeeded)
            {
                long size = GetDirSize(ITCH_OUTPUT_DIR);
                Debug.Log($"[WebGLBuildTool] itch.io 빌드 성공 — {FormatBytes(size)}");
                EditorUtility.DisplayDialog("itch.io 빌드 완료",
                    $"빌드 완료!\n출력: {ITCH_OUTPUT_DIR}\n크기: {FormatBytes(size)}\n\n" +
                    "itch.io 업로드:\n1. itch.io 프로젝트 페이지 → Edit Game\n2. Uploads → Upload files\n3. 빌드 폴더를 ZIP으로 압축 후 업로드\n4. Kind: HTML → Embed in page",
                    "확인");
                EditorUtility.RevealInFinder(ITCH_OUTPUT_DIR);
            }
            else
            {
                LogBuildFailed("itch.io", report);
            }
        }

        // ── Newgrounds ────────────────────────────────────────────────────

        [MenuItem("Tools/WebGL Portals/Newgrounds/빌드 설정 적용")]
        public static void ApplyNewgroundsSettings()
        {
            ApplyBaseWebGLSettings();
            PlayerSettings.WebGL.template = $"PROJECT:{NG_TEMPLATE}";
            ClearTossEnvDefines();

            // NEWGROUNDS 심볼 추가 → 조건부 NG API 코드 활성화
            AddScriptingDefine(BuildTargetGroup.WebGL, "NEWGROUNDS");
            RemoveScriptingDefine(BuildTargetGroup.WebGL, "GAME_DISTRIBUTION");
            RemoveScriptingDefine(BuildTargetGroup.WebGL, "CRAZY_GAMES");

            Debug.Log("[WebGLBuildTool] Newgrounds 빌드 설정 적용 완료.");
            EditorUtility.DisplayDialog("Newgrounds 설정",
                "Newgrounds 빌드 설정이 적용되었습니다.\n\n- 템플릿: NewgroundsTemplate\n- ngio.js: 자동 로드\n- 심볼: NEWGROUNDS\n\n" +
                "주의: NewgroundsTemplate/index.html의\nNG_API_ID와 NG_ENC_KEY를 실제 값으로 교체하세요.",
                "확인");
        }

        [MenuItem("Tools/WebGL Portals/Newgrounds/빌드 실행")]
        public static void BuildNewgrounds()
        {
            if (!ValidateWebGLTarget()) return;

            // index.html에 플레이스홀더가 있는지 경고 (CI 배치 모드에서는 경고만 출력)
            string templatePath = Path.Combine("Assets", "WebGLTemplates", NG_TEMPLATE, "index.html");
            if (File.Exists(templatePath))
            {
                string content = File.ReadAllText(templatePath);
                if (content.Contains("YOUR_NG_API_ID"))
                {
                    if (Application.isBatchMode)
                    {
                        Debug.LogWarning("[WebGLBuildTool] Newgrounds: 플레이스홀더(YOUR_NG_API_ID)가 교체되지 않았습니다. 빌드를 계속합니다.");
                    }
                    else
                    {
                        bool proceed = EditorUtility.DisplayDialog("경고",
                            "NewgroundsTemplate/index.html에 아직 플레이스홀더(YOUR_NG_API_ID)가 있습니다.\n\n" +
                            "실제 API ID와 암호화 키를 입력하지 않으면 메달/스코어보드가 동작하지 않습니다.\n\n" +
                            "그래도 빌드하시겠습니까?",
                            "계속", "취소");
                        if (!proceed) return;
                    }
                }
            }

            ApplyNewgroundsSettings();

            if (!Directory.Exists(NG_OUTPUT_DIR))
                Directory.CreateDirectory(NG_OUTPUT_DIR);

            var report = Build(NG_OUTPUT_DIR);

            if (report.summary.result == BuildResult.Succeeded)
            {
                long size = GetDirSize(NG_OUTPUT_DIR);
                Debug.Log($"[WebGLBuildTool] Newgrounds 빌드 성공 — {FormatBytes(size)}");
                EditorUtility.DisplayDialog("Newgrounds 빌드 완료",
                    $"빌드 완료!\n출력: {NG_OUTPUT_DIR}\n크기: {FormatBytes(size)}\n\n" +
                    "Newgrounds 업로드:\n1. newgrounds.com → 계정 → Game Dev Sandbox\n2. Create New Project → HTML5\n3. 빌드 폴더를 ZIP으로 압축 후 업로드\n4. Entry File: index.html",
                    "확인");
                EditorUtility.RevealInFinder(NG_OUTPUT_DIR);
            }
            else
            {
                LogBuildFailed("Newgrounds", report);
            }
        }

        // ── GameDistribution ──────────────────────────────────────────────

        [MenuItem("Tools/WebGL Portals/GameDistribution/빌드 설정 적용")]
        public static void ApplyGameDistributionSettings()
        {
            ApplyBaseWebGLSettings();

            // GD SDK는 jslib로 자동 로드 — 기본 Unity 템플릿 사용
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            ClearTossEnvDefines();

            AddScriptingDefine(BuildTargetGroup.WebGL, "GAME_DISTRIBUTION");
            RemoveScriptingDefine(BuildTargetGroup.WebGL, "CRAZY_GAMES");
            RemoveScriptingDefine(BuildTargetGroup.WebGL, "NEWGROUNDS");

            Debug.Log("[WebGLBuildTool] GameDistribution 빌드 설정 적용 완료.");
            EditorUtility.DisplayDialog("GD 설정",
                "GameDistribution 빌드 설정이 적용되었습니다.\n\n" +
                "- 심볼: GAME_DISTRIBUTION\n" +
                "- 광고: GameDistributionAds (gdsdk.js 동적 로드)\n" +
                "- 로그인: GuestLogin\n\n" +
                "GD 개발자 포털에 빌드 폴더를 ZIP으로 업로드하세요.",
                "확인");
        }

        [MenuItem("Tools/WebGL Portals/GameDistribution/빌드 실행")]
        public static void BuildGameDistribution()
        {
            if (!ValidateWebGLTarget()) return;
            ApplyGameDistributionSettings();

            if (!Directory.Exists(GD_OUTPUT_DIR))
                Directory.CreateDirectory(GD_OUTPUT_DIR);

            var report = Build(GD_OUTPUT_DIR);
            if (report.summary.result == BuildResult.Succeeded)
            {
                long size = GetDirSize(GD_OUTPUT_DIR);
                Debug.Log($"[WebGLBuildTool] GameDistribution 빌드 성공 — {FormatBytes(size)}");
                EditorUtility.DisplayDialog("GD 빌드 완료",
                    $"빌드 완료!\n출력: {GD_OUTPUT_DIR}\n크기: {FormatBytes(size)}\n\n" +
                    "GameDistribution 업로드:\n1. gamedistribution.com → 개발자 포털\n2. 게임 관리 → Upload\n3. 빌드 폴더를 ZIP으로 압축 후 업로드",
                    "확인");
                EditorUtility.RevealInFinder(GD_OUTPUT_DIR);
            }
            else
            {
                LogBuildFailed("GameDistribution", report);
            }
        }

        // ── CrazyGames ────────────────────────────────────────────────────

        [MenuItem("Tools/WebGL Portals/CrazyGames/빌드 설정 적용")]
        public static void ApplyCrazyGamesSettings()
        {
            ApplyBaseWebGLSettings();

            // CrazyGames SDK는 jslib로 자동 로드 — 기본 Unity 템플릿 사용
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            ClearTossEnvDefines();

            AddScriptingDefine(BuildTargetGroup.WebGL, "CRAZY_GAMES");
            RemoveScriptingDefine(BuildTargetGroup.WebGL, "GAME_DISTRIBUTION");
            RemoveScriptingDefine(BuildTargetGroup.WebGL, "NEWGROUNDS");

            Debug.Log("[WebGLBuildTool] CrazyGames 빌드 설정 적용 완료.");
            EditorUtility.DisplayDialog("CG 설정",
                "CrazyGames 빌드 설정이 적용되었습니다.\n\n" +
                "- 심볼: CRAZY_GAMES\n" +
                "- 광고: CrazyGamesAds (midgame/rewarded)\n" +
                "- 로그인: GuestLogin\n\n" +
                "CrazyGames SDK 도메인에서만 광고가 정상 동작합니다.",
                "확인");
        }

        [MenuItem("Tools/WebGL Portals/CrazyGames/빌드 실행")]
        public static void BuildCrazyGames()
        {
            if (!ValidateWebGLTarget()) return;
            ApplyCrazyGamesSettings();

            if (!Directory.Exists(CG_OUTPUT_DIR))
                Directory.CreateDirectory(CG_OUTPUT_DIR);

            var report = Build(CG_OUTPUT_DIR);
            if (report.summary.result == BuildResult.Succeeded)
            {
                long size = GetDirSize(CG_OUTPUT_DIR);
                Debug.Log($"[WebGLBuildTool] CrazyGames 빌드 성공 — {FormatBytes(size)}");
                EditorUtility.DisplayDialog("CG 빌드 완료",
                    $"빌드 완료!\n출력: {CG_OUTPUT_DIR}\n크기: {FormatBytes(size)}\n\n" +
                    "CrazyGames 업로드:\n1. crazygames.com → 개발자 → 게임 관리\n2. Upload → 빌드 폴더 ZIP 업로드\n3. 테스트 후 검수 제출",
                    "확인");
                EditorUtility.RevealInFinder(CG_OUTPUT_DIR);
            }
            else
            {
                LogBuildFailed("CrazyGames", report);
            }
        }

        // ── 전체 포털 일괄 빌드 ───────────────────────────────────────────

        [MenuItem("Tools/WebGL Portals/전체 포털 빌드 실행")]
        public static void BuildAllPortals()
        {
            if (!ValidateWebGLTarget()) return;

            if (!Application.isBatchMode)
            {
                bool proceed = EditorUtility.DisplayDialog("전체 포털 빌드",
                    "Toss / Facebook / itch.io / Newgrounds / GameDistribution / CrazyGames\n" +
                    "6개 플랫폼을 순서대로 빌드합니다.\n\n시간이 오래 걸릴 수 있습니다. 계속하시겠습니까?",
                    "전체 빌드", "취소");
                if (!proceed) return;
            }

            var results = new StringBuilder();
            results.AppendLine("=== 전체 포털 빌드 결과 ===\n");

            BuildAndRecord(results, "Toss",             BuildToss);
            BuildAndRecord(results, "Facebook",         FacebookBuildProfile.BuildFacebookWebGL);
            BuildAndRecord(results, "itch.io",          BuildItch);
            BuildAndRecord(results, "Newgrounds",       BuildNewgrounds);
            BuildAndRecord(results, "GameDistribution", BuildGameDistribution);
            BuildAndRecord(results, "CrazyGames",       BuildCrazyGames);

            Debug.Log(results.ToString());
            EditorUtility.DisplayDialog("전체 빌드 완료", results.ToString(), "확인");
        }

        private static void BuildAndRecord(StringBuilder log, string name, System.Action buildAction)
        {
            try
            {
                buildAction();
                log.AppendLine($"✓ {name}: 성공");
            }
            catch (System.Exception e)
            {
                log.AppendLine($"✗ {name}: 실패 — {e.Message}");
                Debug.LogError($"[WebGLBuildTool] {name} 빌드 오류: {e}");
            }
        }

        // ── CI/CD 커맨드라인 진입점 ───────────────────────────────────────
        //
        //  사용법:
        //    Unity -batchmode -quit -projectPath . \
        //      -executeMethod Seunghak.Editor.WebGLBuildTool.BuildCI \
        //      -platform toss|toss-env|toss-full|facebook|itch|newgrounds|gd|crazygames|all \
        //      [-tossEnv dev|prod]   (toss 계열 전용, 기본 dev)
        //
        //  토스 dev/prod 배포는 ci/toss/deploy-toss.ps1 이 "toss-env → toss-full" 순으로 호출한다.

        public static void BuildCI()
        {
            string platform = GetCLIArg("-platform", "all").ToLowerInvariant();
            Debug.Log($"[WebGLBuildTool] CI 빌드 시작 — 플랫폼: {platform}");

            switch (platform)
            {
                case "toss":            BuildToss();             break;
                case "toss-env":        SetTossEnvCI();          return; // 심볼만 설정·저장 후 종료
                case "toss-full":       BuildAndPackageTossCI(); return; // 자체적으로 배치모드 종료 코드 처리
                case "facebook":        FacebookBuildProfile.BuildFacebookWebGL(); break;
                case "itch":            BuildItch();             break;
                case "newgrounds":      BuildNewgrounds();       break;
                case "gd":              BuildGameDistribution(); break;
                case "crazygames":      BuildCrazyGames();       break;
                case "all":             BuildAllPortals();       break;
                default:
                    Debug.LogError($"[WebGLBuildTool] 알 수 없는 플랫폼: {platform}");
                    EditorApplication.Exit(1);
                    return;
            }

            Debug.Log("[WebGLBuildTool] CI 빌드 완료.");
        }

        /// <summary>
        /// Toss WebGL 빌드 + AIT 패키징(pnpm/granite build)까지 동기적으로 전부 수행.
        /// "toss"(BuildToss)는 Unity WebGL 빌드만 하고 .ait 패키징은 하지 않는다 — 그건
        /// CI에서 .ait를 안정적으로 뽑으려면 앱인토스 SDK의 동기 API(AITConvertCore.DoExport)를
        /// 직접 호출해야 한다. (옛 TossPostBuild 리플렉션 훅은 SDK 3.5.0에서 제거됨)
        /// AppsInTossMenu.BuildAndPackage()는 DoExportAsync 기반이라 배치모드 -quit과
        /// 함께 쓰면 완료 전에 프로세스가 종료되므로 사용하지 않는다.
        /// </summary>
        private static void SetTossEnvCI()
        {
            string env = ParseTossEnv();
            if (env == null)
            {
                Debug.LogError($"[WebGLBuildTool] 알 수 없는 -tossEnv 값: {GetCLIArg("-tossEnv", "")} (dev|prod)");
                EditorApplication.Exit(1);
                return;
            }

            ApplyTossEnvDefines(env);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WebGLBuildTool] Toss 환경 심볼 설정 완료({env}). 다음 프로세스에서 빌드하세요.");
            EditorApplication.Exit(0);
        }

        private static void BuildAndPackageTossCI()
        {
            string env = ParseTossEnv();
            if (env == null)
            {
                Debug.LogError($"[WebGLBuildTool] 알 수 없는 -tossEnv 값: {GetCLIArg("-tossEnv", "")} (dev|prod)");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[WebGLBuildTool] Toss Build & Package(AIT 패키징 포함) 시작 — 환경: {env}");

            if (!ValidateTossBuild(env)) { FailTossBuild(env, "빌드 전 검증 실패 (위 로그 참고)"); return; }

            // 이 프로세스가 컴파일한 심볼이 요청 환경과 다르면 잘못된 ID로 빌드되므로 즉시 중단
            if (!IsTossEnvCompiled(env))
            {
                FailTossBuild(env, "컴파일된 TOSS_DEV/TOSS_PROD 심볼이 -tossEnv 와 다릅니다. 먼저 '-platform toss-env' 를 실행해 심볼을 설정하세요.");
                return;
            }

            // dev: productionProfile 기반 + SDK 디버그 콘솔 활성화 (토스 앱 안에서 로그 확인용)
            // prod: productionProfile 그대로 (null 전달)
            AppsInToss.AITBuildProfile profile = null;
            if (env == TOSS_ENV_DEV)
            {
                var ait = AssetDatabase.LoadAssetAtPath<AppsInToss.AITEditorScriptObject>(AIT_CONFIG_ASSET);
                if (ait != null && ait.productionProfile != null)
                {
                    profile = ait.productionProfile.Clone();
                    profile.enableDebugConsole = true;
                }
            }

            // UI 프리팹 등 런타임 에셋은 Addressables 번들로만 로드되므로 플레이어 빌드 전에 반드시 빌드한다
            if (!AddressablesBuildSetup.BuildContent())
            {
                FailTossBuild(env, "Addressables 콘텐츠 빌드 실패 (위 로그 참고)");
                return;
            }

            var result = AppsInToss.AITConvertCore.DoExport(
                buildWebGL: true,
                doPackaging: true,
                cleanBuild: false,
                profile: profile,
                profileName: env == TOSS_ENV_DEV ? "CI-dev" : "CI-prod");

            if (result == AppsInToss.AITConvertCore.AITExportError.SUCCEED)
            {
                Debug.Log("[WebGLBuildTool] Toss Build & Package 성공 — ait-build/dist/ 확인.");
                Debug.Log("[WebGLBuildTool] CI 빌드 완료.");
                return;
            }

            Debug.LogError($"[WebGLBuildTool] Toss Build & Package 실패: {result}");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        private static string GetCLIArg(string flag, string defaultValue)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag) return args[i + 1];
            return defaultValue;
        }

        // ── 빌드 사이즈 리포트 ────────────────────────────────────────────

        [MenuItem("Tools/WebGL Portals/전체 빌드 사이즈 리포트")]
        public static void ShowAllBuildSizes()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WebGL 포털 빌드 사이즈 리포트 ===\n");

            AppendDirReport(sb, "Toss",             TOSS_OUTPUT_DIR);
            AppendDirReport(sb, "Facebook",         "Builds/FacebookInstantGames");
            AppendDirReport(sb, "itch.io",          ITCH_OUTPUT_DIR);
            AppendDirReport(sb, "Newgrounds",       NG_OUTPUT_DIR);
            AppendDirReport(sb, "GameDistribution", GD_OUTPUT_DIR);
            AppendDirReport(sb, "CrazyGames",       CG_OUTPUT_DIR);

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("빌드 사이즈 리포트", sb.ToString(), "확인");
        }

        // ── 공통 설정 ─────────────────────────────────────────────────────

        private static void ApplyBaseWebGLSettings()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            // Brotli 압축 (itch.io/Newgrounds 모두 지원)
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;

            // IL2CPP
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.WebGL, ScriptingImplementation.IL2CPP);

            // 코드 스트리핑
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.High);

            // 예외 처리 (퍼포먼스 우선)
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            // 메모리: itch.io는 더 넉넉하게 사용 가능 (64MB)
            PlayerSettings.WebGL.memorySize = 64;

            // 데이터 캐싱 (재방문 시 빠른 로드)
            PlayerSettings.WebGL.dataCaching = true;

            // 색공간
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }

        private static BuildReport Build(string outputPath)
        {
            var options = new BuildPlayerOptions
            {
                scenes           = GetEnabledScenes(),
                locationPathName = outputPath,
                target           = BuildTarget.WebGL,
                options          = BuildOptions.None,
            };
            return BuildPipeline.BuildPlayer(options);
        }

        // ── 유틸리티 ──────────────────────────────────────────────────────

        private static bool ValidateWebGLTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL) return true;

            // CI 배치 모드: 다이얼로그 없이 자동 전환
            if (Application.isBatchMode)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
                return true;
            }

            bool ok = EditorUtility.DisplayDialog("빌드 타겟 변경",
                "현재 빌드 타겟이 WebGL이 아닙니다.\nWebGL로 전환하시겠습니까?", "전환", "취소");
            if (ok) EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            return ok;
        }

        private static void AddScriptingDefine(BuildTargetGroup group, string define)
        {
            string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            if (!current.Contains(define))
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group,
                    string.IsNullOrEmpty(current) ? define : current + ";" + define);
        }

        private static void RemoveScriptingDefine(BuildTargetGroup group, string define)
        {
            string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            if (!current.Contains(define)) return;
            string result = current.Replace(";" + define, "").Replace(define + ";", "").Replace(define, "");
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, result.Trim(';'));
        }

        private static string[] GetEnabledScenes()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) list.Add(s.path);
            return list.ToArray();
        }

        private static void AppendDirReport(StringBuilder sb, string name, string path)
        {
            sb.AppendLine($"[{name}] {path}");
            if (!Directory.Exists(path)) { sb.AppendLine("  (빌드 없음)\n"); return; }
            long total = 0;
            foreach (var f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                var fi = new FileInfo(f);
                total += fi.Length;
            }
            sb.AppendLine($"  총 크기: {FormatBytes(total)}\n");
        }

        private static void LogBuildFailed(string platform, BuildReport report)
        {
            Debug.LogError($"[WebGLBuildTool] {platform} 빌드 실패: {report.summary.result}");

            // 배치모드(CI)에서는 다이얼로그가 뜨지 않고 자동 무시되므로, 대신 프로세스 종료 코드로
            // 실패를 알려야 CI 스크립트가 빌드 성공/실패를 감지할 수 있다.
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
                return;
            }

            EditorUtility.DisplayDialog("빌드 실패",
                $"{platform} 빌드가 실패했습니다.\nConsole 로그를 확인하세요.", "확인");
        }

        private static long GetDirSize(string path)
        {
            long size = 0;
            if (!Directory.Exists(path)) return 0;
            foreach (var f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                size += new FileInfo(f).Length;
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
