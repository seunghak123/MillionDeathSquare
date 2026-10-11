using UnityEngine;
using UnityEditor;
using System.IO;

namespace Seunghak.Editor
{
    /// <summary>
    /// 토스 빌드 헬퍼 에디터 도구
    /// </summary>
    public class TossBuildHelper : EditorWindow
    {
        private string appId = "";
        private string iconUrl = "";
        private bool showBuildSettings = false;

        [MenuItem("Tools/토스 빌드 설정")]
        public static void ShowWindow()
        {
            TossBuildHelper window = GetWindow<TossBuildHelper>("토스 빌드 설정");
            window.Show();
        }

        private void OnEnable()
        {
            // 저장된 설정 불러오기
            appId = EditorPrefs.GetString("TossAppId", "");
            iconUrl = EditorPrefs.GetString("TossIconUrl", "");
        }

        private void OnGUI()
        {
            GUILayout.Label("토스(Apps In Toss) 빌드 설정", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "이 도구는 토스 빌드를 위한 기본 설정을 확인하는 도구입니다.\n" +
                "실제 빌드는 'Apps in Toss > Build & Deploy Window'를 사용하세요.",
                MessageType.Info);

            EditorGUILayout.Space();

            // App ID 설정
            EditorGUILayout.LabelField("앱 ID (Apps In Toss 콘솔에서 발급)", EditorStyles.boldLabel);
            appId = EditorGUILayout.TextField("App ID", appId);
            if (string.IsNullOrEmpty(appId))
            {
                EditorGUILayout.HelpBox("앱 ID를 입력하세요. 앱인토스 콘솔에서 발급받을 수 있습니다.", MessageType.Warning);
            }

            EditorGUILayout.Space();

            // Icon URL 설정
            EditorGUILayout.LabelField("아이콘 URL", EditorStyles.boldLabel);
            iconUrl = EditorGUILayout.TextField("Icon URL", iconUrl);
            if (string.IsNullOrEmpty(iconUrl))
            {
                EditorGUILayout.HelpBox("아이콘 URL을 입력하세요. 토스 앱에서 미니앱 아이콘으로 표시됩니다.", MessageType.Warning);
            }

            EditorGUILayout.Space();

            // 설정 저장
            if (GUILayout.Button("설정 저장", GUILayout.Height(30)))
            {
                EditorPrefs.SetString("TossAppId", appId);
                EditorPrefs.SetString("TossIconUrl", iconUrl);
                EditorUtility.DisplayDialog("저장 완료", "설정이 저장되었습니다.\n실제 빌드는 'Apps in Toss > Build & Deploy Window'에서 진행하세요.", "확인");
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // 빌드 설정 확인
            showBuildSettings = EditorGUILayout.Foldout(showBuildSettings, "빌드 설정 확인");
            if (showBuildSettings)
            {
                EditorGUI.indentLevel++;

                // 플랫폼 확인
                bool isWebGL = EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL;
                EditorGUILayout.LabelField("현재 빌드 타겟:", EditorUserBuildSettings.activeBuildTarget.ToString());
                if (!isWebGL)
                {
                    EditorGUILayout.HelpBox("토스 빌드는 WebGL 플랫폼이 필요합니다.\nFile > Build Settings에서 WebGL로 변경하세요.", MessageType.Warning);
                }

                EditorGUILayout.Space();

                // SDK 확인
                bool sdkInstalled = File.Exists("Packages/manifest.json");
                if (sdkInstalled)
                {
                    string manifestContent = File.ReadAllText("Packages/manifest.json");
                    bool hasTossSDK = manifestContent.Contains("apps-in-toss-unity-sdk");
                    EditorGUILayout.LabelField("SDK 설치:", hasTossSDK ? "✓ 설치됨" : "✗ 미설치");
                    if (!hasTossSDK)
                    {
                        EditorGUILayout.HelpBox("앱인토스 Unity SDK가 설치되지 않았습니다.\nPackages/manifest.json에 패키지를 추가하세요.", MessageType.Error);
                    }
                }

                EditorGUILayout.Space();

                // 로그인 코드 확인
                EditorGUILayout.LabelField("코드 설정:", "✓ 완료");
                EditorGUILayout.HelpBox(
                    "ApplicationManager.cs에서 WebGL 빌드 시 자동으로 토스 로그인을 사용하도록 설정되어 있습니다.",
                    MessageType.Info);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // 빠른 링크
            EditorGUILayout.LabelField("빠른 링크", EditorStyles.boldLabel);
            if (GUILayout.Button("앱인토스 개발자센터 열기"))
            {
                Application.OpenURL("https://developers-apps-in-toss.toss.im/");
            }
            if (GUILayout.Button("토스 Unity SDK GitHub 열기"))
            {
                Application.OpenURL("https://github.com/toss/apps-in-toss-unity-sdk");
            }
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "참고: Vite Warp 방식(커뮤니티)으로 브릿지를 쓰려면 Hoongcha/Unity-App-in-TossAPI를 참고할 수 있습니다. " +
                "현재 프로젝트는 공식 SDK만 사용하며, 두 브릿지를 동시에 쓰면 안 됩니다. 자세한 비교는 Assets/AppsInToss/Docs/Hoongcha_Unity_App_in_TossAPI_참고.md 를 보세요.",
                MessageType.None);
            if (GUILayout.Button("Hoongcha Unity-App-in-TossAPI (Vite Warp 참고)"))
            {
                Application.OpenURL("https://github.com/Hoongcha/Unity-App-in-TossAPI");
            }
        }
    }
}
