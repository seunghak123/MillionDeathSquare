using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.EditorTools
{
    /// <summary>
    /// 모든 UI를 기기 해상도/비율에 맞게 보정한다.
    /// - 기준: CanvasScaler ScaleWithScreenSize 720x1280 + ScreenMatchMode.Expand
    ///   (기준 영역 720x1280이 항상 화면 안에 다 들어오고, 남는 방향으로 캔버스가 늘어난다.
    ///    세로로 긴 폰 → 폭 720 고정·높이 증가 / 태블릿 → 높이 1280 고정·폭 증가)
    /// - 배경 그림: stretch + AspectRatioFitter(EnvelopeParent)로 비율 유지한 채 화면을 덮음
    /// - 가장자리 요소: 해당 가장자리 앵커로 옮기되 기준 해상도에서 위치가 그대로이도록 좌표 환산
    /// - 720x1280 절대 좌표(좌하단 앵커)로 배치된 요소: 중앙 앵커로 환산
    /// 재실행해도 결과가 같다(멱등). 버튼 리스너/SerializeField 연결은 건드리지 않는다.
    /// </summary>
    public static class UiResolutionFixer
    {
        private const string ScenePath = "Assets/Scenes/Base.unity";
        private const string PrefabDir = "Assets/BaseSources/Prefabs/UI/";
        private const string ReportPath = "Library/UiResolutionReport.txt";
        private static readonly Vector2 RefRes = new Vector2(720f, 1280f);

        private static readonly string[] PrefabNames =
        {
            "GameStartWindow", "InGameHUDWindow", "CharacterShopWindow", "GameOverPopup", "SettingPopup",
            "InGamePause", "LoadingPopup", "BaseAwnserPopup", "AdGachaBox",
        };

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 Bottom = new Vector2(0.5f, 0f);

        [MenuItem("Tools/UI/Fix Resolution Layout")]
        public static void Fix()
        {
            FixScene();
            foreach (var name in PrefabNames)
                FixPrefab(name);
            AssetDatabase.SaveAssets();
            Debug.Log("[UiResolutionFixer] 해상도 대응 보정 완료");
        }

        #region Scene
        private static void FixScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int count = 0;
            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var scaler in go.GetComponentsInChildren<CanvasScaler>(true))
                {
                    ApplyScaler(scaler);
                    count++;
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[UiResolutionFixer] 씬 CanvasScaler {count}개 Expand 적용, 씬 저장");
        }

        private static void ApplyScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = RefRes;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0f;
            EditorUtility.SetDirty(scaler);
        }
        #endregion

        #region Prefab
        private static void FixPrefab(string prefabName)
        {
            string path = PrefabDir + prefabName + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogError($"[UiResolutionFixer] 프리팹 로드 실패: {path}");
                return;
            }

            try
            {
                foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true))
                    ApplyScaler(scaler);

                var r = (RectTransform)root.transform;
                switch (prefabName)
                {
                    case "GameStartWindow":
                        FixLobbyFrame(r);
                        // 캐릭터 선택 줄: 가로 stretch라 넓은 화면에서 좌우 캐릭터/화살표가 벌어짐 → 720 폭 고정, 중앙 정렬
                        Reanchor(r, "Character card", Center, Center, RefRes);
                        break;

                    case "CharacterShopWindow":
                    case "SettingPopup":
                        FixLobbyFrame(r);
                        if (prefabName == "SettingPopup")
                        {
                            // 720x1280 절대 좌표(좌하단 앵커)로 놓인 패널 → 중앙 앵커
                            foreach (var n in new[] { "panel icon", "Card_setting", "Card_setting (1)", "Card_setting (2)",
                                         "Card_setting (3)", "ok icon", "setting logo icon" })
                                Reanchor(r, n, Center, Center, RefRes);
                        }
                        break;

                    case "AdGachaBox":
                        Envelope(r, "Background");
                        foreach (var n in new[] { "panel icon", "setting logo icon", "box_under", "box up" })
                            Reanchor(r, n, Center, Center, RefRes);
                        break;

                    case "InGameHUDWindow":
                        // 아이템 슬롯: 중앙 기준 (-176,-398) → 하단 앵커 (화면 아래에 붙음)
                        Reanchor(r, "Buttons Object", Bottom, Bottom, RefRes);
                        break;

                    case "GameOverPopup":
                        // 루트 정규화 (런타임 FitToParentLayer와 동일, 에디터 프리뷰용)
                        NormalizeRoot(r);
                        Stretch(r, "Background");
                        // SCORE 라벨만 상단 앵커라 세로로 긴 화면에서 점수/버튼 묶음과 벌어짐 → 중앙 앵커
                        Reanchor(r, "Background/Score Image", Center, Center, RefRes);
                        break;

                    case "InGamePause":
                        Stretch(r, "background image");
                        Reanchor(r, "ScoreText", Center, Center, RefRes);
                        Reanchor(r, "score label", Center, Center, RefRes);
                        break;

                    case "LoadingPopup":
                        Stretch(r, "Background");
                        break;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[UiResolutionFixer] {prefabName} 저장");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>로비 계열 창 공통 프레임: 배경 그림 cover, 하단 바 가로 stretch, 하단 아이콘 중앙 기준.</summary>
        private static void FixLobbyFrame(RectTransform r)
        {
            Envelope(r, "Backrgound Group/background image");

            // 하단 바: 720 고정 폭 → 가로 stretch (넓은 화면에서 양옆이 비지 않게)
            Reanchor(r, "Backrgound Group/background bottom group", new Vector2(0f, 0f), new Vector2(1f, 0f), RefRes);
            // 하단 아이콘 묶음: 하단 바 좌하단 기준 → 하단 중앙 기준 (넓은 화면에서도 중앙 콘텐츠와 정렬 유지)
            Reanchor(r, "Backrgound Group/background bottom group/background icon bottom group", Bottom, Bottom,
                new Vector2(RefRes.x, 166f));
        }
        #endregion

        #region RectTransform helpers
        private static RectTransform FindRect(RectTransform root, string path)
        {
            var t = root.Find(path) as RectTransform;
            if (t == null)
                Debug.LogWarning($"[UiResolutionFixer] {root.name}/{path} 없음 — 건너뜀");
            return t;
        }

        /// <summary>
        /// 앵커를 바꾸되 부모 크기가 parentRefSize일 때 사각형 위치/크기가 그대로이도록 오프셋을 환산한다.
        /// 이미 목표 앵커면 값이 변하지 않으므로 멱등.
        /// </summary>
        private static void Reanchor(RectTransform root, string path, Vector2 newMin, Vector2 newMax, Vector2 parentRefSize)
        {
            var rt = FindRect(root, path);
            if (rt == null) return;

            // 이미 목표 앵커면 재계산하지 않음 (float 왕복 오차 누적 방지)
            if (rt.anchorMin != newMin || rt.anchorMax != newMax)
            {
                Vector2 rectMin = Vector2.Scale(rt.anchorMin, parentRefSize) + rt.offsetMin;
                Vector2 rectMax = Vector2.Scale(rt.anchorMax, parentRefSize) + rt.offsetMax;

                rt.anchorMin = newMin;
                rt.anchorMax = newMax;
                rt.offsetMin = rectMin - Vector2.Scale(newMin, parentRefSize);
                rt.offsetMax = rectMax - Vector2.Scale(newMax, parentRefSize);
            }

            // 환산 오차(예: -1.00002)를 정수로 정리. 정수 근처가 아니면 원래 값 유지
            rt.anchoredPosition = SnapNearInt(rt.anchoredPosition);
            rt.sizeDelta = SnapNearInt(rt.sizeDelta);
        }

        private static Vector2 SnapNearInt(Vector2 v)
        {
            float x = Mathf.Round(v.x), y = Mathf.Round(v.y);
            return new Vector2(Mathf.Abs(v.x - x) < 0.001f ? x : v.x, Mathf.Abs(v.y - y) < 0.001f ? y : v.y);
        }

        private static void SetStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform root, string path)
        {
            var rt = FindRect(root, path);
            if (rt != null) SetStretch(rt);
        }

        private static void NormalizeRoot(RectTransform rt)
        {
            rt.localScale = Vector3.one;
            rt.pivot = Center;
            SetStretch(rt);
        }

        /// <summary>배경 그림: 비율 유지하며 부모를 완전히 덮도록(cover) AspectRatioFitter.EnvelopeParent 적용.</summary>
        private static void Envelope(RectTransform root, string path)
        {
            var rt = FindRect(root, path);
            if (rt == null) return;

            SetStretch(rt);
            rt.pivot = Center;

            var image = rt.GetComponent<Image>();
            if (image == null || image.sprite == null)
            {
                Debug.LogWarning($"[UiResolutionFixer] {path}: 스프라이트 없음 — stretch만 적용");
                return;
            }

            var fitter = rt.GetComponent<AspectRatioFitter>();
            if (fitter == null)
                fitter = rt.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
        }
        #endregion

        #region Verify
        private struct Res
        {
            public string Name;
            public int W, H;
            public Res(string name, int w, int h) { Name = name; W = w; H = h; }
        }

        private static readonly Res[] VerifyResolutions =
        {
            new Res("Phone 1080x2340", 1080, 2340),
            new Res("Ref 720x1280", 720, 1280),
            new Res("Tablet 1536x2048", 1536, 2048),
        };

        /// <summary>CanvasScaler 설정으로 화면 크기 → 캔버스(논리) 크기 계산. (CanvasScaler.HandleScaleWithScreenSize와 동일 공식)</summary>
        private static Vector2 CanvasSizeFor(CanvasScaler.ScreenMatchMode mode, float match, int w, int h)
        {
            float scale;
            switch (mode)
            {
                case CanvasScaler.ScreenMatchMode.Expand:
                    scale = Mathf.Min(w / RefRes.x, h / RefRes.y);
                    break;
                case CanvasScaler.ScreenMatchMode.Shrink:
                    scale = Mathf.Max(w / RefRes.x, h / RefRes.y);
                    break;
                default:
                    float lw = Mathf.Log(w / RefRes.x, 2f);
                    float lh = Mathf.Log(h / RefRes.y, 2f);
                    scale = Mathf.Pow(2f, Mathf.Lerp(lw, lh, match));
                    break;
            }
            return new Vector2(w / scale, h / scale);
        }

        [MenuItem("Tools/UI/Verify Resolution Layout")]
        public static void Verify()
        {
            var sb = new StringBuilder();
            var preview = EditorSceneManager.NewPreviewScene();
            var updateRect = typeof(AspectRatioFitter).GetMethod("UpdateRect", BindingFlags.Instance | BindingFlags.NonPublic);

            // 씬의 실제 루트 스케일러 설정 사용
            var mode = CanvasScaler.ScreenMatchMode.Expand;
            float match = 0f;
            var baseScene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
            GameObject bootSource = null;
            if (baseScene.isLoaded)
            {
                foreach (var go in baseScene.GetRootGameObjects())
                {
                    // BaseCanvas는 씬 루트가 아니라 매니저 오브젝트 하위에 있으므로 하위까지 탐색
                    foreach (var s in go.GetComponentsInChildren<CanvasScaler>(true))
                    {
                        if (s.gameObject.name == "BaseCanvas") { mode = s.screenMatchMode; match = s.matchWidthOrHeight; }
                        if (s.gameObject.name == "BootLoadingCanvas") bootSource = s.gameObject;
                    }
                }
            }
            sb.AppendLine($"BaseCanvas scaler: {mode} match={match}");

            var sources = new List<KeyValuePair<string, GameObject>>();
            foreach (var n in PrefabNames)
                sources.Add(new KeyValuePair<string, GameObject>(n, AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + n + ".prefab")));
            if (bootSource != null)
                sources.Add(new KeyValuePair<string, GameObject>("BootLoadingCanvas", bootSource));

            int totalOut = 0, totalBgFail = 0;
            try
            {
                foreach (var res in VerifyResolutions)
                {
                    Vector2 canvas = CanvasSizeFor(mode, match, res.W, res.H);
                    sb.AppendLine();
                    sb.AppendLine($"=== {res.Name} → canvas {canvas.x:0.#}x{canvas.y:0.#} ===");
                    sb.AppendLine("UI | 배경 덮음 | 검사 요소 | 화면 밖 요소 | 중앙 어긋남");

                    foreach (var kv in sources)
                    {
                        if (kv.Value == null) { sb.AppendLine($"{kv.Key} | 로드 실패"); continue; }

                        var layer = new GameObject("VerifyLayer", typeof(RectTransform));
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(layer, preview);
                        var layerRt = (RectTransform)layer.transform;
                        layerRt.anchorMin = layerRt.anchorMax = Vector2.zero;
                        layerRt.pivot = Vector2.zero;
                        layerRt.sizeDelta = canvas;
                        layerRt.position = Vector3.zero;

                        var inst = Object.Instantiate(kv.Value, layerRt, false);
                        // 복제본 루트 Canvas가 화면 크기로 강제되지 않도록 제거 (검증용 임시 객체)
                        foreach (var c in inst.GetComponents<CanvasScaler>()) Object.DestroyImmediate(c);
                        foreach (var c in inst.GetComponents<GraphicRaycaster>()) Object.DestroyImmediate(c);
                        foreach (var c in inst.GetComponents<Canvas>()) Object.DestroyImmediate(c);
                        inst.SetActive(true);

                        // 런타임 UIManager.FitToParentLayer와 동일 정규화
                        var instRt = (RectTransform)inst.transform;
                        instRt.localPosition = Vector3.zero;
                        instRt.localScale = Vector3.one;
                        SetStretch(instRt);

                        foreach (var fitter in inst.GetComponentsInChildren<AspectRatioFitter>(true))
                            updateRect?.Invoke(fitter, null);
                        LayoutRebuilder.ForceRebuildLayoutImmediate(instRt);
                        Canvas.ForceUpdateCanvases();

                        VerifyOne(kv.Key, inst, canvas, sb, ref totalOut, ref totalBgFail);
                        Object.DestroyImmediate(layer);
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }

            sb.AppendLine();
            sb.AppendLine($"TOTAL: 화면 밖 요소 {totalOut}, 배경 미덮음 {totalBgFail}");
            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log("[UiResolutionFixer] 검증 결과 (" + ReportPath + ")\n" + sb);
        }

        private static readonly string[] CenteredKeys =
        {
            "panel icon", "setting logo icon", "pop up image", "title logo", "start icon", "stage icon", "box up",
            "TitleText", "ProgressBarBG",
        };

        private static void VerifyOne(string name, GameObject inst, Vector2 canvas, StringBuilder sb, ref int totalOut, ref int totalBgFail)
        {
            const float eps = 1f;
            var corners = new Vector3[4];
            var outList = new List<string>();
            var bgList = new List<string>();
            var offCenter = new List<string>();
            int checkedCount = 0;

            foreach (var rt in inst.GetComponentsInChildren<RectTransform>(false))
            {
                if (rt == inst.transform) continue;

                rt.GetWorldCorners(corners); // 임시 레이어가 원점·스케일1이므로 월드 = 캔버스 좌표
                Vector2 min = corners[0], max = corners[2];

                // 배경 = cover 피터가 붙은 것, 또는 화면 전체 크기 부모에 stretch된 그래픽(딤/배경 그림)
                var parentRt = rt.parent as RectTransform;
                bool parentIsFull = parentRt != null && Mathf.Abs(parentRt.rect.width - canvas.x) < eps &&
                                    Mathf.Abs(parentRt.rect.height - canvas.y) < eps;
                bool isBackground = rt.GetComponent<AspectRatioFitter>() != null ||
                                    (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one && parentIsFull &&
                                     rt.GetComponent<Graphic>() != null);
                if (isBackground)
                {
                    bool covers = min.x <= eps && min.y <= eps && max.x >= canvas.x - eps && max.y >= canvas.y - eps;
                    bgList.Add($"{rt.name}{(covers ? "" : "(X)")}");
                    if (!covers) totalBgFail++;
                    continue;
                }

                // 핵심 요소 = 보이는 그래픽 또는 버튼 (빈 컨테이너 제외)
                if (rt.GetComponent<Graphic>() == null && rt.GetComponent<Selectable>() == null) continue;
                if (rt.rect.width <= 0.01f || rt.rect.height <= 0.01f) continue;
                checkedCount++;

                if (min.x < -eps || min.y < -eps || max.x > canvas.x + eps || max.y > canvas.y + eps)
                    outList.Add($"{rt.name}[{min.x:0},{min.y:0}~{max.x:0},{max.y:0}]");

                // 화면 가로 중앙에 있어야 하는 핵심 패널이 중앙에서 벗어났는지 (좌하단 절대좌표 배치 검출용)
                if (System.Array.IndexOf(CenteredKeys, rt.name) >= 0)
                {
                    float dx = (min.x + max.x) * 0.5f - canvas.x * 0.5f;
                    if (Mathf.Abs(dx) > 8f) offCenter.Add($"{rt.name}(dx={dx:0})");
                }
            }

            totalOut += outList.Count;
            sb.AppendLine($"{name} | {(bgList.Count == 0 ? "-" : string.Join(",", bgList))} | {checkedCount} | " +
                          $"{(outList.Count == 0 ? "0" : outList.Count + ": " + string.Join(", ", outList))} | " +
                          $"{(offCenter.Count == 0 ? "0" : string.Join(", ", offCenter))}");
        }
        #endregion
    }
}
