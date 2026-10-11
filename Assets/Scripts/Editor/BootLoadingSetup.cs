using Seunghak.Common;
using Seunghak.UIManager;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.EditorTools
{
    /// <summary>
    /// Base.unity 씬에 부트 로딩 화면(BootLoadingCanvas)을 생성하고 ApplicationManager에 연결한다.
    /// 여러 번 실행해도 기존 오브젝트를 이름으로 찾아 갱신한다.
    /// </summary>
    public static class BootLoadingSetup
    {
        private const string ScenePath = "Assets/Scenes/Base.unity";
        private const string FontPath = "Assets/BaseSources/UI/Font/ONE Mobile POP OTF SDF.asset";
        private const string RootName = "BootLoadingCanvas";
        private const string RoundBarPath = "Assets/BaseSources/UI/BootLoading/UI_RoundBar.png"; // 64x64 원, border 31 (9-slice)
        private const float RoundBarSize = 64f;
        private const float BarInset = 4f;

        private static readonly Color BgColor = new Color(0.36f, 0.72f, 0.95f, 1f);
        private static readonly Color BarBgColor = new Color(1f, 1f, 1f, 0.35f);

        [MenuItem("Tools/UI/Setup Boot Loading Screen")]
        public static void Setup()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            // 루트 Canvas (재실행 시 기존 것 재사용)
            GameObject root = null;
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == RootName) { root = go; break; }
            if (root == null)
            {
                root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(BootLoadingScreen));
                Undo.RegisterCreatedObjectUndo(root, "Create BootLoadingCanvas");
            }
            root.layer = LayerMask.NameToLayer("UI");

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            // 해상도 대응 기준은 UiResolutionFixer와 동일하게 Expand (기준 영역이 항상 화면 안에 들어옴)
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0f;

            var rootRt = (RectTransform)root.transform;

            // 배경
            var bg = GetOrCreate(rootRt, "Background", typeof(Image));
            Stretch(bg);
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = BgColor;
            bgImg.raycastTarget = true; // 로딩 중 아래 입력 차단

            // 타이틀 (스프라이트 로고 대신 텍스트 — 번들 의존 없음)
            var title = GetOrCreate(rootRt, "TitleText", typeof(TextMeshProUGUI));
            Anchor(title, new Vector2(0.5f, 0.62f), new Vector2(640, 140), Vector2.zero);
            var titleTmp = title.GetComponent<TextMeshProUGUI>();
            SetText(titleTmp, font, "Fossil Merge", 80, Color.white, TextAlignmentOptions.Center);

            // 스피너
            var spinner = GetOrCreate(rootRt, "Spinner", typeof(Image));
            Anchor(spinner, new Vector2(0.5f, 0.5f), new Vector2(80, 80), Vector2.zero);
            var spinImg = spinner.GetComponent<Image>();
            spinImg.sprite = GetSpinnerSprite();
            spinImg.type = Image.Type.Filled;
            spinImg.fillMethod = Image.FillMethod.Radial360;
            spinImg.fillAmount = 0.75f;
            spinImg.color = Color.white;
            spinImg.raycastTarget = false;

            // 진행 바
            var barBg = GetOrCreate(rootRt, "ProgressBarBG", typeof(Image));
            Anchor(barBg, new Vector2(0.5f, 0.3f), new Vector2(520, 24), Vector2.zero);
            var roundBar = AssetDatabase.LoadAssetAtPath<Sprite>(RoundBarPath);
            var barBgImg = barBg.GetComponent<Image>();
            barBgImg.sprite = roundBar;
            barBgImg.type = Image.Type.Sliced;
            barBgImg.pixelsPerUnitMultiplier = RoundBarSize / 24f; // 끝 반지름 = 바 높이의 절반
            barBgImg.color = BarBgColor;
            barBgImg.raycastTarget = false;

            // 채움: Filled는 9-slice가 안 되므로 Sliced + 폭 조절 (BootLoadingScreen.ApplyFill)
            var fill = GetOrCreate(barBg, "ProgressFill", typeof(Image));
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = new Vector2(BarInset, 0f);
            fill.sizeDelta = new Vector2(24f - BarInset * 2f, -BarInset * 2f);
            var fillImg = fill.GetComponent<Image>();
            fillImg.sprite = roundBar;
            fillImg.type = Image.Type.Sliced;
            fillImg.pixelsPerUnitMultiplier = RoundBarSize / (24f - BarInset * 2f);
            fillImg.fillAmount = 1f;
            fillImg.color = Color.white;
            fillImg.raycastTarget = false;

            // 상태 텍스트
            var status = GetOrCreate(rootRt, "StatusText", typeof(TextMeshProUGUI));
            Anchor(status, new Vector2(0.5f, 0.3f), new Vector2(600, 50), new Vector2(0, -50));
            var statusTmp = status.GetComponent<TextMeshProUGUI>();
            SetText(statusTmp, font, "게임을 준비하고 있어요", 30, Color.white, TextAlignmentOptions.Center);

            // BootLoadingScreen 필드 연결
            var screen = root.GetComponent<BootLoadingScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            so.FindProperty("progressFill").objectReferenceValue = fillImg;
            so.FindProperty("statusText").objectReferenceValue = statusTmp;
            so.FindProperty("spinner").objectReferenceValue = (RectTransform)spinner.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 앱 시작 즉시 보여야 하므로 활성 상태로 저장
            root.SetActive(true);
            root.transform.SetAsLastSibling();

            // ApplicationManager 연결
            var app = Object.FindFirstObjectByType<ApplicationManager>(FindObjectsInactive.Include);
            if (app != null)
            {
                var appSo = new SerializedObject(app);
                appSo.FindProperty("bootLoadingScreen").objectReferenceValue = screen;
                appSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(app);
            }
            else
                Debug.LogError("[BootLoadingSetup] 씬에서 ApplicationManager를 찾지 못했습니다.");

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BootLoadingSetup] done (ApplicationManager 연결: " + (app != null) + ")");
        }

        private static RectTransform GetOrCreate(Transform parent, string name, System.Type comp)
        {
            var t = parent.Find(name);
            if (t == null)
            {
                var go = new GameObject(name, typeof(RectTransform), comp);
                go.layer = LayerMask.NameToLayer("UI");
                t = go.transform;
                t.SetParent(parent, false);
            }
            else if (t.GetComponent(comp) == null)
                t.gameObject.AddComponent(comp);
            return (RectTransform)t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        private static void SetText(TextMeshProUGUI tmp, TMP_FontAsset font, string text, float size,
            Color color, TextAlignmentOptions align)
        {
            if (font != null)
            {
                tmp.font = font;
                tmp.fontSharedMaterial = font.material;
            }
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
        }

        // 내장 스프라이트: 스피너용 (Fill 타입은 sprite 필수)
        private static Sprite GetSpinnerSprite()
            => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
    }
}
