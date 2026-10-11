using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using Seunghak.UIManager;

/// <summary>
/// 빈 팝업 프리팹에 기본 UI 요소를 자동 구성합니다.
/// </summary>
public class BuildPopupUIs
{
    const string PREFAB_ROOT = "Assets/BaseSources/Prefabs/UI/";

    public static void Execute()
    {
        BuildSettingPopup();
        BuildUserInfoPopup();
        BuildRewardPopup();
        BuildCharacterShopWindow();
        AssetDatabase.SaveAssets();
        Debug.Log("[BuildPopupUIs] 팝업 UI 구축 완료");
    }

    // ── 공용 헬퍼 ─────────────────────────────────────────────────────

    static GameObject LoadPrefab(string name, out GameObject root)
    {
        string path = PREFAB_ROOT + name + ".prefab";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        root = PrefabUtility.LoadPrefabContents(path);
        return asset;
    }

    static void SavePrefab(string name, GameObject root)
    {
        string path = PREFAB_ROOT + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static GameObject CreatePanel(Transform parent, string panelName, Color color)
    {
        var go = new GameObject(panelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.15f);
        rt.anchorMax = new Vector2(0.9f, 0.85f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return go;
    }

    static TextMeshProUGUI CreateTMP(Transform parent, string name, string text, float fontSize,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offset = default)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(10, 0);
        rt.offsetMax = new Vector2(-10, 0);
        rt.anchoredPosition = offset;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        return tmp;
    }

    static Button CreateButton(Transform parent, string name, string label, float fontSize,
        Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(10, 5);
        rt.offsetMax = new Vector2(-10, -5);
        go.GetComponent<Image>().color = bgColor;

        var txt = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txt.transform.SetParent(go.transform, false);
        var txtRt = txt.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
        var tmp = txt.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return go.GetComponent<Button>();
    }

    static Slider CreateSlider(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(10, 0);
        rt.offsetMax = new Vector2(-10, 0);

        // Background
        var bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0, 0.25f);
        bgRt.anchorMax = new Vector2(1, 0.75f);
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);

        // Fill Area
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        var fillAreaRt = fillArea.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0, 0.25f);
        fillAreaRt.anchorMax = new Vector2(1, 0.75f);
        fillAreaRt.offsetMin = new Vector2(5, 0);
        fillAreaRt.offsetMax = new Vector2(-15, 0);

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(0.5f, 1f);
        fill.GetComponent<Image>().color = new Color(0.2f, 0.7f, 1f);

        // Handle
        var handleSlide = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleSlide.transform.SetParent(go.transform, false);
        var hsRt = handleSlide.GetComponent<RectTransform>();
        hsRt.anchorMin = Vector2.zero;
        hsRt.anchorMax = Vector2.one;
        hsRt.offsetMin = new Vector2(10, 0);
        hsRt.offsetMax = new Vector2(-10, 0);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handle.transform.SetParent(handleSlide.transform, false);
        var handleRt = handle.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(20, 0);
        handle.GetComponent<Image>().color = Color.white;

        var slider = go.GetComponent<Slider>();
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.minValue = 0;
        slider.maxValue = 100;
        slider.value = 80;
        return slider;
    }

    // ── SettingPopup ──────────────────────────────────────────────────

    static void BuildSettingPopup()
    {
        GameObject root;
        LoadPrefab("SettingPopup", out root);

        // 이미 패널이 있으면 스킵
        if (root.transform.Find("Panel") != null)
        {
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        var panel = CreatePanel(root.transform, "Panel", new Color(0.1f, 0.1f, 0.15f, 0.95f));
        var pt = panel.transform;

        // 타이틀
        CreateTMP(pt, "TitleText", "설정", 50, new Vector2(0, 0.85f), new Vector2(1, 1f));

        // 슬라이더 행 (master, bgm, character, sfx)
        string[] labels = { "전체 볼륨", "음악", "캐릭터", "효과음" };
        string[] sliderNames = { "MasterSlider", "BgmSlider", "CharacterSlider", "SfxSlider" };
        string[] valueNames = { "MasterValueText", "BgmValueText", "CharacterValueText", "SfxValueText" };
        float rowH = 0.12f;
        float startY = 0.73f;

        var sliders = new Slider[4];
        var valueTexts = new TextMeshProUGUI[4];

        for (int i = 0; i < 4; i++)
        {
            float top = startY - i * rowH;
            float bot = top - rowH + 0.02f;
            float mid = (top + bot) * 0.5f;

            CreateTMP(pt, labels[i] + "Label", labels[i], 28,
                new Vector2(0.02f, bot), new Vector2(0.3f, top));

            sliders[i] = CreateSlider(pt, sliderNames[i],
                new Vector2(0.3f, bot), new Vector2(0.78f, top));

            valueTexts[i] = CreateTMP(pt, valueNames[i], "80", 28,
                new Vector2(0.78f, bot), new Vector2(0.98f, top));
        }

        // 버튼
        var inquiryBtn = CreateButton(pt, "InquiryButton", "문의하기", 28,
            new Vector2(0.05f, 0.05f), new Vector2(0.45f, 0.18f), new Color(0.4f, 0.4f, 0.6f));
        var closeBtn = CreateButton(pt, "CloseButton", "닫기", 30,
            new Vector2(0.55f, 0.05f), new Vector2(0.95f, 0.18f), new Color(0.6f, 0.3f, 0.3f));

        // 컴포넌트 연결
        var script = root.GetComponent<SettingPopup>();
        SetPrivateField(script, "masterSlider",    sliders[0]);
        SetPrivateField(script, "bgmSlider",       sliders[1]);
        SetPrivateField(script, "characterSlider", sliders[2]);
        SetPrivateField(script, "sfxSlider",       sliders[3]);
        SetPrivateField(script, "masterValueText",    valueTexts[0]);
        SetPrivateField(script, "bgmValueText",       valueTexts[1]);
        SetPrivateField(script, "characterValueText", valueTexts[2]);
        SetPrivateField(script, "sfxValueText",       valueTexts[3]);
        SetPrivateField(script, "inquiryButton", inquiryBtn);
        SetPrivateField(script, "closeButton",   closeBtn);

        SavePrefab("SettingPopup", root);
        Debug.Log("[BuildPopupUIs] SettingPopup 구축 완료");
    }

    // ── UserInfoPopup ─────────────────────────────────────────────────

    static void BuildUserInfoPopup()
    {
        GameObject root;
        LoadPrefab("UserInfoPopup", out root);

        if (root.transform.Find("Panel") != null)
        {
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        var panel = CreatePanel(root.transform, "Panel", new Color(0.1f, 0.1f, 0.15f, 0.95f));
        var pt = panel.transform;

        CreateTMP(pt, "TitleText", "내 정보", 50, new Vector2(0, 0.82f), new Vector2(1, 0.97f));

        var bestScore   = CreateTMP(pt, "BestScoreText",   "Best Score : 0", 36, new Vector2(0.05f, 0.6f),  new Vector2(0.95f, 0.72f));
        var playCount   = CreateTMP(pt, "PlayCountText",   "Play Count : 0", 36, new Vector2(0.05f, 0.46f), new Vector2(0.95f, 0.58f));
        var topChar     = CreateTMP(pt, "TopCharacterText", "대표 캐릭터 : -", 36, new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.44f));

        var closeBtn = CreateButton(pt, "CloseButton", "닫기", 32,
            new Vector2(0.25f, 0.07f), new Vector2(0.75f, 0.2f), new Color(0.6f, 0.3f, 0.3f));

        var script = root.GetComponent<UserInfoPopup>();
        SetPrivateField(script, "bestScoreText",   bestScore);
        SetPrivateField(script, "playCountText",   playCount);
        SetPrivateField(script, "topCharacterText", topChar);
        SetPrivateField(script, "closeButton",     closeBtn);

        SavePrefab("UserInfoPopup", root);
        Debug.Log("[BuildPopupUIs] UserInfoPopup 구축 완료");
    }

    // ── RewardPopup ───────────────────────────────────────────────────

    static void BuildRewardPopup()
    {
        GameObject root;
        LoadPrefab("RewardPopup", out root);

        if (root.transform.Find("Panel") != null)
        {
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        var panel = CreatePanel(root.transform, "Panel", new Color(0.1f, 0.12f, 0.1f, 0.95f));
        var pt = panel.transform;

        var title  = CreateTMP(pt, "RewardTitleText",  "보상 획득!", 48, new Vector2(0, 0.78f), new Vector2(1, 0.96f));
        var amount = CreateTMP(pt, "RewardAmountText", "Gold +100", 40, new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.72f));

        var claimBtn   = CreateButton(pt, "ClaimButton",    "수령",         32, new Vector2(0.05f, 0.3f),  new Vector2(0.45f, 0.45f), new Color(0.3f, 0.6f, 0.3f));
        var watchAdBtn = CreateButton(pt, "WatchAdButton",  "광고 보고 2배", 26, new Vector2(0.55f, 0.3f), new Vector2(0.95f, 0.45f), new Color(0.5f, 0.5f, 0.2f));
        var closeBtn   = CreateButton(pt, "CloseButton",    "닫기",          28, new Vector2(0.25f, 0.1f),  new Vector2(0.75f, 0.23f), new Color(0.5f, 0.3f, 0.3f));

        var script = root.GetComponent<RewardPopup>();
        SetPrivateField(script, "rewardTitleText",  title);
        SetPrivateField(script, "rewardAmountText", amount);
        SetPrivateField(script, "claimButton",      claimBtn);
        SetPrivateField(script, "watchAdButton",    watchAdBtn);
        SetPrivateField(script, "closeButton",      closeBtn);

        SavePrefab("RewardPopup", root);
        Debug.Log("[BuildPopupUIs] RewardPopup 구축 완료");
    }

    // ── CharacterShopWindow ───────────────────────────────────────────

    static void BuildCharacterShopWindow()
    {
        GameObject root;
        LoadPrefab("CharacterShopWindow", out root);

        if (root.transform.Find("Panel") != null)
        {
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        var panel = CreatePanel(root.transform, "Panel", new Color(0.08f, 0.08f, 0.12f, 0.97f));
        var pt = panel.transform;

        // 타이틀
        CreateTMP(pt, "TitleText", "상점", 50, new Vector2(0, 0.9f), new Vector2(1, 1f));

        // 닫기 버튼
        var closeBtn = CreateButton(pt, "CloseButton", "X", 30,
            new Vector2(0.82f, 0.91f), new Vector2(0.97f, 0.99f), new Color(0.6f, 0.3f, 0.3f));

        // 일일 선물
        CreateTMP(pt, "DailyGiftLabel", "1 ~ 50 C", 26,
            new Vector2(0.05f, 0.08f), new Vector2(0.45f, 0.16f));
        var dailyBtn = CreateButton(pt, "DailyGiftButton", "일일 선물", 24,
            new Vector2(0.05f, 0.02f), new Vector2(0.45f, 0.09f), new Color(0.3f, 0.5f, 0.4f));

        // 광고 보상
        CreateTMP(pt, "AdRewardLabel", "10 ~ 100 C", 26,
            new Vector2(0.55f, 0.08f), new Vector2(0.95f, 0.16f));
        var adBtn = CreateButton(pt, "AdRewardButton", "광고 보상", 24,
            new Vector2(0.55f, 0.02f), new Vector2(0.95f, 0.09f), new Color(0.4f, 0.4f, 0.2f));

        // 스크립트 연결
        var script = root.GetComponent<CharacterShopWindow>();
        SetPrivateField(script, "closeButton",      closeBtn);
        SetPrivateField(script, "dailyGiftButton",  dailyBtn);
        SetPrivateField(script, "adRewardButton",   adBtn);
        SetPrivateField(script, "dailyGiftLabel",   pt.Find("DailyGiftLabel").GetComponent<TextMeshProUGUI>());
        SetPrivateField(script, "adRewardLabel",    pt.Find("AdRewardLabel").GetComponent<TextMeshProUGUI>());

        SavePrefab("CharacterShopWindow", root);
        Debug.Log("[BuildPopupUIs] CharacterShopWindow 구축 완료");
    }

    // ── Reflection 헬퍼 ──────────────────────────────────────────────

    static void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
            field.SetValue(obj, value);
        else
            Debug.LogWarning($"[BuildPopupUIs] 필드 없음: {fieldName}");
    }
}
