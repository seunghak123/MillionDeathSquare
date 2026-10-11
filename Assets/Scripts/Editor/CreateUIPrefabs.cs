using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using System.IO;
using Seunghak.UIManager;

/// <summary>
/// Resources/UI/ 폴더에 필요한 UI 프리팹을 자동 생성합니다.
/// Tools > Create UI Prefabs 메뉴에서 실행하세요.
/// </summary>
public class CreateUIPrefabs
{
    private const string SAVE_PATH = "Assets/Resources/UI";

    [MenuItem("Tools/Create UI Prefabs", false, 900)]
    public static void CreateAll()
    {
        if (!Directory.Exists(SAVE_PATH))
            Directory.CreateDirectory(SAVE_PATH);

        CreateGameStartWindow();
        CreateLoadingPopup();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CreateUIPrefabs] UI 프리팹 생성 완료.");
    }

    // ----------------------------------------------------------------
    // GameStartWindow
    // ----------------------------------------------------------------
    private static void CreateGameStartWindow()
    {
        string path = $"{SAVE_PATH}/GameStartWindow.prefab";
        if (File.Exists(path))
        {
            Debug.Log($"[CreateUIPrefabs] {path} 이미 존재합니다. 스킵.");
            return;
        }

        // Root: Canvas
        GameObject root = CreateCanvasRoot("GameStartWindow");

        // Background panel
        GameObject bg = CreatePanel(root.transform, "Background", Color.black * 0.5f);
        SetStretch(bg.GetComponent<RectTransform>());

        // Title text
        GameObject titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(root.transform, false);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "Fossil Merge";
        titleTMP.fontSize = 60;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = Color.white;
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.1f, 0.65f);
        titleRT.anchorMax = new Vector2(0.9f, 0.85f);
        titleRT.offsetMin = titleRT.offsetMax = Vector2.zero;

        // Start button
        GameObject startBtn = CreateButton(root.transform, "StartButton", "시작하기",
            new Vector2(0.3f, 0.40f), new Vector2(0.7f, 0.52f));

        // Settings button
        GameObject settingsBtn = CreateButton(root.transform, "SettingsButton", "설정",
            new Vector2(0.3f, 0.26f), new Vector2(0.7f, 0.38f));

        // GameStartWindow component
        var gsw = root.AddComponent<GameStartWindow>();
        // SerializedObject로 필드 주입
        var so = new SerializedObject(gsw);
        so.FindProperty("titleText").objectReferenceValue = titleTMP;
        so.FindProperty("startButton").objectReferenceValue = startBtn.GetComponent<Button>();
        so.FindProperty("settingsButton").objectReferenceValue = settingsBtn.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();

        SavePrefab(root, path);
        GameObject.DestroyImmediate(root);
    }

    // ----------------------------------------------------------------
    // LoadingPopup
    // ----------------------------------------------------------------
    private static void CreateLoadingPopup()
    {
        string path = $"{SAVE_PATH}/LoadingPopup.prefab";
        if (File.Exists(path))
        {
            Debug.Log($"[CreateUIPrefabs] {path} 이미 존재합니다. 스킵.");
            return;
        }

        // Root: Canvas
        GameObject root = CreateCanvasRoot("LoadingPopup");

        // Background panel
        GameObject bg = CreatePanel(root.transform, "Background", new Color(0, 0, 0, 0.75f));
        SetStretch(bg.GetComponent<RectTransform>());

        // Loading text
        GameObject labelGO = new GameObject("LoadingPercentLabel");
        labelGO.transform.SetParent(root.transform, false);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = "0%";
        labelTMP.fontSize = 36;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.color = Color.white;
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.2f, 0.52f);
        labelRT.anchorMax = new Vector2(0.8f, 0.62f);
        labelRT.offsetMin = labelRT.offsetMax = Vector2.zero;

        // Progress Slider
        GameObject sliderGO = new GameObject("ProgressSlider");
        sliderGO.transform.SetParent(root.transform, false);
        var slider = sliderGO.AddComponent<Slider>();
        var sliderRT = sliderGO.GetComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0.1f, 0.44f);
        sliderRT.anchorMax = new Vector2(0.9f, 0.50f);
        sliderRT.offsetMin = sliderRT.offsetMax = Vector2.zero;

        // Slider background image
        GameObject sliderBg = new GameObject("Background");
        sliderBg.transform.SetParent(sliderGO.transform, false);
        var sliderBgImg = sliderBg.AddComponent<Image>();
        sliderBgImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        SetStretch(sliderBg.GetComponent<RectTransform>());

        // Slider fill area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGO.transform, false);
        var fillAreaRT = fillArea.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = Vector2.zero;
        fillAreaRT.anchorMax = Vector2.one;
        fillAreaRT.offsetMin = fillAreaRT.offsetMax = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.8f, 0.2f, 1f);
        var fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;

        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.interactable = false;

        // LoadingPopup component
        var lp = root.AddComponent<LoadingPopup>();
        var so = new SerializedObject(lp);
        so.FindProperty("loadingPercentLabel").objectReferenceValue = labelTMP;
        so.FindProperty("progressSlider").objectReferenceValue = slider;
        so.FindProperty("fixedWaitingTime").floatValue = 10f;
        so.ApplyModifiedPropertiesWithoutUndo();

        SavePrefab(root, path);
        GameObject.DestroyImmediate(root);
    }

    // ----------------------------------------------------------------
    // 헬퍼
    // ----------------------------------------------------------------
    private static GameObject CreateCanvasRoot(string name)
    {
        var go = new GameObject(name);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static void SetStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static GameObject CreateButton(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.6f, 1f, 1f);

        var btn = go.AddComponent<Button>();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // Label child
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 36;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        SetStretch(labelGO.GetComponent<RectTransform>());

        return go;
    }

    private static void SavePrefab(GameObject go, string path)
    {
        bool success;
        PrefabUtility.SaveAsPrefabAsset(go, path, out success);
        if (success)
            Debug.Log($"[CreateUIPrefabs] 생성됨: {path}");
        else
            Debug.LogError($"[CreateUIPrefabs] 실패: {path}");
    }
}
