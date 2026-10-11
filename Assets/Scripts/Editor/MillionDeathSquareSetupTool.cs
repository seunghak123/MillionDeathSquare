using System;
using System.Collections.Generic;
using System.IO;
using Seunghak.Game.MillionDeathSquare;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Seunghak.Editor
{
    /// <summary>
    /// 백만 번 죽는 네모 셋업 툴 (메뉴 Tools/Million Death Square/...).
    /// UI 오브젝트는 에디터에서만 만든다는 프로젝트 규칙에 따라 화면 프리팹을 여기서 생성한다.
    ///
    ///   0. 전부 실행 (1 → 2 → 3 → 5)
    ///   1. 스프라이트·팔레트·캠페인 에셋 생성 — 도형 PNG(흰색+알파), MDSSprites / MDSPalettes / MDSCampaign 에셋
    ///   2. 화면 프리팹 생성 (+ Addressables UI 그룹 등록) — MDS* 창/팝업 8종
    ///   3. Base 씬에 연결 — 디렉터·보드 오브젝트, 직교 카메라, 씬 컨트롤러 연결, BaseCanvas 1920×1080
    ///   4. 테스트 씬 생성 — UI 없이 보드만으로 캠페인을 바로 플레이 (MDSTestBootstrap)
    ///   5. 게임패드 방향 패드 축 추가 (Input Manager: MDS_DPadX / MDS_DPadY)
    ///   Steam 켜기/끄기 — STEAMWORKS_NET 정의 (Standalone) + steam_appid.txt
    /// 여러 번 실행해도 된다 (있는 에셋은 갱신, 프리팹은 덮어씀).
    /// </summary>
    public static class MillionDeathSquareSetupTool
    {
        private const string ROOT = "Assets/BaseSources/MillionDeathSquare";
        private const string SPRITE_DIR = ROOT + "/Sprites";
        private const string SPRITES_ASSET = ROOT + "/MDSSprites.asset";
        private const string PALETTE_ASSET = ROOT + "/MDSPalettes.asset";
        private const string CAMPAIGN_ASSET = ROOT + "/MDSCampaign.asset";
        private const string UI_PREFAB_DIR = "Assets/BaseSources/Prefabs/UI";
        private const string BASE_SCENE_PATH = "Assets/Scenes/Base.unity";
        private const string TEST_SCENE_PATH = "Assets/Scenes/MillionDeathSquareTest.unity";
        private const string FONT_PATH = "Assets/BaseSources/UI/Font/ONE Mobile POP OTF SDF.asset";
        private const string STEAM_DEFINE = "STEAMWORKS_NET";
        private const string MENU = "Tools/Million Death Square/";

        private static readonly Vector2 Reference = new Vector2(1920, 1080);
        private static readonly Color PanelColor = new Color(0.10f, 0.10f, 0.16f, 0.92f);
        private static readonly Color ButtonColor = new Color(0.25f, 0.25f, 0.35f, 1f);
        private static readonly Color AccentColor = new Color(1f, 0.45f, 0.45f, 1f);

        private static TMP_FontAsset _font;
        private static Sprite _white;

        // ── 0. 전부 ────────────────────────────────────────────────

        [MenuItem(MENU + "0. 전부 실행 (에셋 → 프리팹 → Base 씬 → 입력 축)", false, 0)]
        public static void RunAll()
        {
            CreateAssets();
            CreateUiPrefabs();
            AddGamepadAxes();
            IntegrateIntoBaseScene();
        }

        // ── 1. 에셋 ────────────────────────────────────────────────

        [MenuItem(MENU + "1. 스프라이트·팔레트·캠페인 에셋 생성", false, 100)]
        public static void CreateAssets()
        {
            EnsureFolder(SPRITE_DIR);
            var sprites = new Dictionary<MDSShapes.Shape, Sprite>();
            foreach (MDSShapes.Shape shape in Enum.GetValues(typeof(MDSShapes.Shape)))
            {
                string path = SPRITE_DIR + "/MDS_" + shape + ".png";
                Texture2D tex = MDSShapes.CreateTexture(shape, 128);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 128;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
                sprites[shape] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            MDSSpriteLibrary lib = LoadOrCreate<MDSSpriteLibrary>(SPRITES_ASSET);
            lib.square = sprites[MDSShapes.Shape.Square];
            lib.roundedSquare = sprites[MDSShapes.Shape.RoundedSquare];
            lib.circle = sprites[MDSShapes.Shape.Circle];
            lib.ring = sprites[MDSShapes.Shape.Ring];
            lib.eyeWhite = sprites[MDSShapes.Shape.Circle];
            lib.pupil = sprites[MDSShapes.Shape.Circle];
            lib.eyeHappy = sprites[MDSShapes.Shape.Arc];
            lib.eyeClosed = sprites[MDSShapes.Shape.Bar];
            lib.brow = sprites[MDSShapes.Shape.Bar];
            lib.arrow = sprites[MDSShapes.Shape.Arrow];
            lib.key = sprites[MDSShapes.Shape.Key];
            lib.lockIcon = sprites[MDSShapes.Shape.Lock];
            lib.flag = sprites[MDSShapes.Shape.Flag];
            lib.glow = sprites[MDSShapes.Shape.Glow];
            lib.sparkle = sprites[MDSShapes.Shape.Sparkle];
            lib.iceShine = sprites[MDSShapes.Shape.IceShine];
            lib.particle = sprites[MDSShapes.Shape.RoundedSquare];
            EditorUtility.SetDirty(lib);

            MDSPaletteAsset pal = LoadOrCreate<MDSPaletteAsset>(PALETTE_ASSET);
            if (pal.palettes == null || pal.palettes.Length != Palettes.Count) pal.ResetToCore();
            EditorUtility.SetDirty(pal);

            MDSCampaignAsset camp = LoadOrCreate<MDSCampaignAsset>(CAMPAIGN_ASSET);
            if (camp.overrides == null || camp.overrides.Length == 0)
            {
                var list = new List<MDSCampaignAsset.StageOverride>();
                for (int w = 1; w <= Rules.WORLD_COUNT; w++)
                    for (int s = 1; s <= Rules.STAGES_PER_WORLD; s++)
                        list.Add(new MDSCampaignAsset.StageOverride { stageId = Campaign.StageId(w, s), shareCode = "" });
                camp.overrides = list.ToArray();
            }
            EditorUtility.SetDirty(camp);

            AssetDatabase.SaveAssets();
            Debug.Log("[MDSSetup] 스프라이트 " + sprites.Count + "개, MDSSprites / MDSPalettes / MDSCampaign 에셋 생성 → " + ROOT);
        }

        // ── 2. 화면 프리팹 ──────────────────────────────────────────

        [MenuItem(MENU + "2. 화면 프리팹 생성 (Addressables 등록)", false, 101)]
        public static void CreateUiPrefabs()
        {
            EnsureFolder(UI_PREFAB_DIR);
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            if (_font == null) Debug.LogWarning("[MDSSetup] 폰트를 찾지 못했습니다: " + FONT_PATH + " (TMP 기본 폰트 사용 — 한글이 안 보일 수 있음)");
            _white = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_DIR + "/MDS_Square.png");

            SavePrefab("MDSMainWindow", BuildMainWindow);
            SavePrefab("MDSCampaignWindow", BuildCampaignWindow);
            SavePrefab("MDSPlayWindow", BuildPlayWindow);
            SavePrefab("MDSEditorWindow", BuildEditorWindow);
            SavePrefab("MDSMapFindWindow", BuildMapFindWindow);
            SavePrefab("MDSRoomWindow", BuildRoomWindow);
            SavePrefab("MDSMapDetailPopup", BuildMapDetailPopup);
            SavePrefab("MDSResultPopup", BuildResultPopup);

            // 빌드에서도 로드되도록 Addressables UI 그룹 등록 (주소 = 파일명, 라벨 main)
            AddressablesBuildSetup.SetupGroups();
            Debug.Log("[MDSSetup] 화면 프리팹 8종 생성 → " + UI_PREFAB_DIR + " (Addressables UI 그룹 등록 완료)");
        }

        private static void SavePrefab(string name, Action<GameObject> build)
        {
            string path = UI_PREFAB_DIR + "/" + name + ".prefab";
            GameObject root = NewCanvasRoot(name);
            try
            {
                build(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject NewCanvasRoot(string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            return root;
        }

        // ── 화면별 배치 (앵커 비율 좌표: 왼쪽 아래 (0,0) ~ 오른쪽 위 (1,1)) ──

        private static void BuildMainWindow(GameObject root)
        {
            var w = root.AddComponent<MDSMainWindow>();
            Image(root.transform, "Background", new Color(0.06f, 0.06f, 0.1f, 0.82f), 0, 0, 1, 1);
            Set(w, "titleText", Text(root.transform, "Title", "백만 번 죽는 네모", 104, 0.1f, 0.72f, 0.9f, 0.9f));
            string[] names = { "campaignButton", "editorButton", "findButton", "roomButton", "settingsButton", "quitButton" };
            string[] labels = { "캠페인", "맵 에디터", "맵 찾기", "멀티방", "설정", "종료" };
            for (int i = 0; i < names.Length; i++)
            {
                float top = 0.64f - i * 0.09f;
                Set(w, names[i], Button(root.transform, names[i], labels[i], 46, 0.36f, top - 0.075f, 0.64f, top, i == 0 ? AccentColor : ButtonColor));
            }
            Set(w, "statsText", Text(root.transform, "Stats", "", 28, 0.03f, 0.02f, 0.7f, 0.08f, TextAlignmentOptions.Left));
            Set(w, "platformText", Text(root.transform, "Platform", "", 24, 0.7f, 0.02f, 0.97f, 0.08f, TextAlignmentOptions.Right));
        }

        private static void BuildCampaignWindow(GameObject root)
        {
            var w = root.AddComponent<MDSCampaignWindow>();
            Image(root.transform, "Background", new Color(0.05f, 0.05f, 0.08f, 0.6f), 0, 0, 1, 1);
            var worlds = new Button[Rules.WORLD_COUNT];
            for (int i = 0; i < worlds.Length; i++)
                worlds[i] = Button(root.transform, "World" + (i + 1), "월드 " + (i + 1), 34, 0.04f + i * 0.165f, 0.87f, 0.19f + i * 0.165f, 0.96f, ButtonColor);
            SetArray(w, "worldButtons", worlds);
            Set(w, "worldTitleText", Text(root.transform, "WorldTitle", "월드 1", 56, 0.04f, 0.77f, 0.7f, 0.86f, TextAlignmentOptions.Left));
            Set(w, "worldInfoText", Text(root.transform, "WorldInfo", "", 28, 0.04f, 0.66f, 0.96f, 0.77f, TextAlignmentOptions.Left));
            var stages = new Button[Rules.STAGES_PER_WORLD];
            var labels = new TMP_Text[Rules.STAGES_PER_WORLD];
            for (int i = 0; i < stages.Length; i++)
            {
                int col = i % 5, row = i / 5;
                float x0 = 0.04f + col * 0.186f;
                float y1 = row == 0 ? 0.63f : 0.33f;
                stages[i] = Button(root.transform, "Stage" + (i + 1), "", 26, x0, y1 - 0.27f, x0 + 0.17f, y1, ButtonColor);
                labels[i] = stages[i].GetComponentInChildren<TMP_Text>();
            }
            SetArray(w, "stageButtons", stages);
            SetArray(w, "stageLabels", labels);
            Set(w, "backButton", Button(root.transform, "Back", "뒤로", 32, 0.88f, 0.87f, 0.97f, 0.96f, ButtonColor));
        }

        private static void BuildPlayWindow(GameObject root)
        {
            var w = root.AddComponent<MDSPlayWindow>();
            Image(root.transform, "TopBar", new Color(0f, 0f, 0f, 0.28f), 0, 0.86f, 1, 1);
            TMP_Text death = Text(root.transform, "Deaths", "0", 120, 0.35f, 0.865f, 0.65f, 1.0f);
            death.fontStyle = FontStyles.Bold;
            Set(w, "deathText", death);
            Set(w, "deathLabel", Text(root.transform, "DeathLabel", "데스", 26, 0.44f, 0.835f, 0.56f, 0.875f));
            Set(w, "stageText", Text(root.transform, "Stage", "", 34, 0.08f, 0.92f, 0.36f, 0.99f, TextAlignmentOptions.Left));
            Set(w, "coinText", Text(root.transform, "Coins", "", 36, 0.66f, 0.92f, 0.98f, 0.99f, TextAlignmentOptions.Right));
            Set(w, "timeText", Text(root.transform, "Time", "", 28, 0.66f, 0.87f, 0.98f, 0.92f, TextAlignmentOptions.Right));
            TMP_Text cd = Text(root.transform, "Countdown", "3", 220, 0.3f, 0.3f, 0.7f, 0.7f);
            cd.gameObject.SetActive(false);
            Set(w, "countdownText", cd);
            TMP_Text roster = Text(root.transform, "Roster", "", 26, 0.78f, 0.45f, 0.99f, 0.84f, TextAlignmentOptions.TopRight);
            roster.gameObject.SetActive(false);
            Set(w, "rosterText", roster);
            Set(w, "hintText", Text(root.transform, "Hint", "", 22, 0.1f, 0.0f, 0.9f, 0.04f));
            Set(w, "pauseButton", Button(root.transform, "Pause", "II", 40, 0.01f, 0.875f, 0.065f, 0.985f, ButtonColor));
        }

        private static void BuildEditorWindow(GameObject root)
        {
            var w = root.AddComponent<MDSEditorWindow>();
            Image(root.transform, "ToolPanel", PanelColor, 0, 0, 0.17f, 1f);
            int n = MDSEditorWindow.ToolNames.Length;
            var tools = new Button[n];
            float h = 0.985f / n;
            for (int i = 0; i < n; i++)
            {
                float top = 0.995f - i * h;
                tools[i] = Button(root.transform, "Tool" + i, MDSEditorWindow.ToolNames[i], 20, 0.005f, top - h + 0.003f, 0.165f, top, ButtonColor);
            }
            SetArray(w, "toolButtons", tools);

            Image(root.transform, "TopBar", PanelColor, 0.17f, 0.925f, 1f, 1f);
            Set(w, "nameInput", InputField(root.transform, "NameInput", "맵 이름", 26, 0.175f, 0.935f, 0.35f, 0.99f));
            Set(w, "paletteButton", Button(root.transform, "Palette", "팔레트", 22, 0.355f, 0.935f, 0.455f, 0.99f, ButtonColor));
            Set(w, "widthMinus", Button(root.transform, "WidthMinus", "가로-", 20, 0.46f, 0.935f, 0.49f, 0.99f, ButtonColor));
            Set(w, "widthPlus", Button(root.transform, "WidthPlus", "가로+", 20, 0.49f, 0.935f, 0.52f, 0.99f, ButtonColor));
            Set(w, "sizeText", Text(root.transform, "Size", "24 × 14", 24, 0.52f, 0.935f, 0.575f, 0.99f));
            Set(w, "heightMinus", Button(root.transform, "HeightMinus", "세로-", 20, 0.575f, 0.935f, 0.605f, 0.99f, ButtonColor));
            Set(w, "heightPlus", Button(root.transform, "HeightPlus", "세로+", 20, 0.605f, 0.935f, 0.635f, 0.99f, ButtonColor));
            Set(w, "undoButton", Button(root.transform, "Undo", "되돌리기", 22, 0.64f, 0.935f, 0.70f, 0.99f, ButtonColor));
            Set(w, "redoButton", Button(root.transform, "Redo", "다시하기", 22, 0.70f, 0.935f, 0.76f, 0.99f, ButtonColor));
            Set(w, "testPlayButton", Button(root.transform, "TestPlay", "시험 플레이", 26, 0.77f, 0.935f, 0.86f, 0.99f, AccentColor));
            Set(w, "uploadButton", Button(root.transform, "Upload", "업로드", 26, 0.865f, 0.935f, 0.935f, 0.99f, new Color(0.3f, 0.6f, 0.95f)));
            Set(w, "backButton", Button(root.transform, "Back", "나가기", 24, 0.94f, 0.935f, 0.995f, 0.99f, ButtonColor));

            Image(root.transform, "BottomBar", PanelColor, 0.17f, 0f, 1f, 0.075f);
            Set(w, "newButton", Button(root.transform, "New", "새 맵", 22, 0.175f, 0.008f, 0.245f, 0.067f, ButtonColor));
            Set(w, "copyCodeButton", Button(root.transform, "CopyCode", "공유 코드 복사", 22, 0.25f, 0.008f, 0.36f, 0.067f, ButtonColor));
            Set(w, "pasteCodeButton", Button(root.transform, "PasteCode", "공유 코드 붙여넣기", 22, 0.365f, 0.008f, 0.49f, 0.067f, ButtonColor));
            Set(w, "statusText", Text(root.transform, "Status", "", 20, 0.5f, 0.0f, 0.995f, 0.075f, TextAlignmentOptions.Left));

            // 장애물 패널 (오른쪽)
            RectTransform panel = Image(root.transform, "ObstaclePanel", PanelColor, 0.84f, 0.09f, 1f, 0.91f);
            Set(w, "obstaclePanel", panel.gameObject);
            Set(w, "obstacleText", Text(panel, "Info", "", 20, 0.04f, 0.78f, 0.96f, 0.99f, TextAlignmentOptions.TopLeft));
            Set(w, "modePingPong", Button(panel, "ModePingPong", "왕복", 20, 0.03f, 0.70f, 0.33f, 0.77f, ButtonColor));
            Set(w, "modeLoop", Button(panel, "ModeLoop", "반복", 20, 0.35f, 0.70f, 0.65f, 0.77f, ButtonColor));
            Set(w, "modeCircle", Button(panel, "ModeCircle", "원", 20, 0.67f, 0.70f, 0.97f, 0.77f, ButtonColor));
            var speeds = new Button[3];
            for (int i = 0; i < 3; i++) speeds[i] = Button(panel, "Speed" + (i + 1), "속도 " + (i + 1), 20, 0.03f + i * 0.32f, 0.61f, 0.33f + i * 0.32f, 0.68f, ButtonColor);
            SetArray(w, "speedButtons", speeds);
            Set(w, "delayMinus", Button(panel, "DelayMinus", "출발 -0.1초", 18, 0.03f, 0.52f, 0.49f, 0.59f, ButtonColor));
            Set(w, "delayPlus", Button(panel, "DelayPlus", "출발 +0.1초", 18, 0.51f, 0.52f, 0.97f, 0.59f, ButtonColor));
            Set(w, "radiusMinus", Button(panel, "RadiusMinus", "반지름 -", 18, 0.03f, 0.43f, 0.49f, 0.50f, ButtonColor));
            Set(w, "radiusPlus", Button(panel, "RadiusPlus", "반지름 +", 18, 0.51f, 0.43f, 0.97f, 0.50f, ButtonColor));
            Set(w, "angleMinus", Button(panel, "AngleMinus", "시작 각도 -", 18, 0.03f, 0.34f, 0.49f, 0.41f, ButtonColor));
            Set(w, "anglePlus", Button(panel, "AnglePlus", "시작 각도 +", 18, 0.51f, 0.34f, 0.97f, 0.41f, ButtonColor));
            Set(w, "directionButton", Button(panel, "Direction", "방향 바꾸기", 18, 0.03f, 0.25f, 0.97f, 0.32f, ButtonColor));
            Set(w, "duplicateButton", Button(panel, "Duplicate", "기차 (복제 +0.5초)", 18, 0.03f, 0.16f, 0.97f, 0.23f, ButtonColor));
            Set(w, "finishPathButton", Button(panel, "FinishPath", "경로 완료 (Enter)", 18, 0.03f, 0.08f, 0.97f, 0.15f, ButtonColor));
            Set(w, "deleteObstacleButton", Button(panel, "DeleteObstacle", "장애물 삭제 (Del)", 18, 0.03f, 0.005f, 0.97f, 0.07f, new Color(0.6f, 0.25f, 0.25f)));
            panel.gameObject.SetActive(false);
        }

        private static void BuildMapFindWindow(GameObject root)
        {
            var w = root.AddComponent<MDSMapFindWindow>();
            Image(root.transform, "Background", new Color(0.06f, 0.06f, 0.1f, 0.9f), 0, 0, 1, 1);
            Text(root.transform, "Title", "맵 찾기", 56, 0.1f, 0.9f, 0.6f, 0.98f, TextAlignmentOptions.Left);
            Set(w, "codeInput", InputField(root.transform, "CodeInput", "맵 코드 또는 공유 코드", 30, 0.1f, 0.81f, 0.58f, 0.89f));
            Set(w, "findButton", Button(root.transform, "Find", "찾기", 30, 0.59f, 0.81f, 0.69f, 0.89f, AccentColor));
            Set(w, "pasteButton", Button(root.transform, "Paste", "붙여넣기", 28, 0.70f, 0.81f, 0.82f, 0.89f, ButtonColor));
            Set(w, "statusText", Text(root.transform, "Status", "", 26, 0.1f, 0.75f, 0.9f, 0.8f, TextAlignmentOptions.Left));
            RectTransform result = Image(root.transform, "Result", PanelColor, 0.1f, 0.45f, 0.9f, 0.74f);
            Set(w, "resultRoot", result.gameObject);
            Set(w, "resultPreview", Raw(result, "Preview", 0.02f, 0.06f, 0.36f, 0.94f));
            Set(w, "resultText", Text(result, "Info", "", 28, 0.38f, 0.32f, 0.98f, 0.94f, TextAlignmentOptions.TopLeft));
            Set(w, "playButton", Button(result, "Play", "플레이", 32, 0.38f, 0.06f, 0.62f, 0.28f, AccentColor));
            Set(w, "detailButton", Button(result, "Detail", "상세·후기", 30, 0.64f, 0.06f, 0.88f, 0.28f, ButtonColor));
            result.gameObject.SetActive(false);
            Set(w, "playedTab", Button(root.transform, "PlayedTab", "플레이한 맵", 26, 0.1f, 0.37f, 0.3f, 0.43f, ButtonColor));
            Set(w, "mineTab", Button(root.transform, "MineTab", "내가 올린 맵", 26, 0.31f, 0.37f, 0.51f, 0.43f, ButtonColor));
            var list = new Button[8];
            for (int i = 0; i < list.Length; i++)
            {
                float top = 0.355f - i * 0.043f;
                list[i] = Button(root.transform, "Item" + i, "", 24, 0.1f, top - 0.038f, 0.9f, top, ButtonColor);
                list[i].GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Left;
            }
            SetArray(w, "listButtons", list);
            Set(w, "backButton", Button(root.transform, "Back", "뒤로", 32, 0.86f, 0.91f, 0.97f, 0.98f, ButtonColor));
        }

        private static void BuildRoomWindow(GameObject root)
        {
            var w = root.AddComponent<MDSRoomWindow>();
            Image(root.transform, "Background", new Color(0.06f, 0.06f, 0.1f, 0.9f), 0, 0, 1, 1);
            Text(root.transform, "Title", "멀티방", 52, 0.75f, 0.0f, 0.99f, 0.06f, TextAlignmentOptions.Right);

            RectTransform lobby = Image(root.transform, "Lobby", PanelColor, 0.25f, 0.3f, 0.75f, 0.75f);
            Set(w, "lobbyRoot", lobby.gameObject);
            Text(lobby, "Info", "비공개 방만 — 방 코드나 친구 초대로 들어와요 (2~4명)", 26, 0.05f, 0.88f, 0.95f, 0.98f);
            Set(w, "createButton", Button(lobby, "Create", "방 만들기", 40, 0.1f, 0.55f, 0.9f, 0.82f, AccentColor));
            Set(w, "joinCodeInput", InputField(lobby, "JoinCode", "방 코드 6자리", 32, 0.1f, 0.2f, 0.6f, 0.42f));
            Set(w, "joinButton", Button(lobby, "Join", "코드로 참가", 30, 0.62f, 0.2f, 0.9f, 0.42f, ButtonColor));

            RectTransform room = Rect(root.transform, "Room", 0, 0.06f, 1, 1);
            Set(w, "roomRoot", room.gameObject);
            Set(w, "roomCodeText", Text(room, "RoomCode", "방 코드", 48, 0.04f, 0.87f, 0.42f, 0.97f, TextAlignmentOptions.Left));
            Set(w, "copyRoomCodeButton", Button(room, "CopyCode", "코드 복사", 24, 0.43f, 0.885f, 0.53f, 0.955f, ButtonColor));
            Set(w, "inviteButton", Button(room, "Invite", "Steam 친구 초대", 24, 0.54f, 0.885f, 0.7f, 0.955f, new Color(0.3f, 0.55f, 0.9f)));
            var slots = new TMP_Text[Rules.ROOM_MAX_PLAYERS];
            for (int i = 0; i < slots.Length; i++)
            {
                float top = 0.82f - i * 0.11f;
                RectTransform slot = Image(room, "Slot" + i, PanelColor, 0.04f, top - 0.095f, 0.36f, top);
                slots[i] = Text(slot, "Name", "빈자리", 30, 0.05f, 0f, 0.95f, 1f, TextAlignmentOptions.Left);
            }
            SetArray(w, "memberSlots", slots);
            Set(w, "countText", Text(room, "Count", "1/4명", 28, 0.04f, 0.31f, 0.36f, 0.37f, TextAlignmentOptions.Left));
            Set(w, "statusText", Text(room, "Status", "", 24, 0.04f, 0.13f, 0.36f, 0.3f, TextAlignmentOptions.TopLeft));
            Set(w, "mapPreview", Raw(room, "MapPreview", 0.4f, 0.42f, 0.96f, 0.84f));
            Set(w, "mapText", Text(room, "MapTitle", "", 28, 0.4f, 0.36f, 0.96f, 0.42f, TextAlignmentOptions.Left));
            Set(w, "mapCodeInput", InputField(room, "MapCode", "맵 코드 / 공유 코드 / 1-3", 26, 0.4f, 0.28f, 0.76f, 0.35f));
            Set(w, "selectMapButton", Button(room, "SelectMap", "맵 고르기", 26, 0.77f, 0.28f, 0.96f, 0.35f, ButtonColor));
            var modes = new Button[3];
            string[] modeLabels = { "레이스", "협동", "생존" };
            for (int i = 0; i < 3; i++) modes[i] = Button(room, "Mode" + i, modeLabels[i], 30, 0.4f + i * 0.19f, 0.18f, 0.57f + i * 0.19f, 0.26f, ButtonColor);
            SetArray(w, "modeButtons", modes);
            Set(w, "startButton", Button(room, "Start", "시작", 40, 0.74f, 0.03f, 0.96f, 0.15f, AccentColor));
            Set(w, "botButton", Button(room, "Bot", "봇 추가 (개발용)", 22, 0.4f, 0.04f, 0.58f, 0.12f, ButtonColor));
            Set(w, "leaveButton", Button(room, "Leave", "방 나가기", 26, 0.04f, 0.03f, 0.2f, 0.11f, new Color(0.6f, 0.25f, 0.25f)));
            room.gameObject.SetActive(false);

            Set(w, "backButton", Button(root.transform, "Back", "뒤로", 30, 0.01f, 0.005f, 0.1f, 0.055f, ButtonColor));
        }

        private static void BuildMapDetailPopup(GameObject root)
        {
            var w = root.AddComponent<MDSMapDetailPopup>();
            Image(root.transform, "Dim", new Color(0f, 0f, 0f, 0.6f), 0, 0, 1, 1);
            RectTransform p = Image(root.transform, "Panel", PanelColor, 0.12f, 0.08f, 0.88f, 0.92f);
            Set(w, "titleText", Text(p, "Title", "", 40, 0.03f, 0.88f, 0.97f, 0.98f, TextAlignmentOptions.Left));
            Set(w, "preview", Raw(p, "Preview", 0.03f, 0.5f, 0.45f, 0.86f));
            Set(w, "statsText", Text(p, "Stats", "", 26, 0.47f, 0.62f, 0.97f, 0.86f, TextAlignmentOptions.TopLeft));
            var bars = new UnityEngine.UI.Image[4];
            string[] diff = { "쉬움", "보통", "어려움", "지옥" };
            for (int i = 0; i < 4; i++)
            {
                float y = 0.585f - i * 0.03f;
                Text(p, "DiffLabel" + i, diff[i], 20, 0.47f, y - 0.025f, 0.56f, y, TextAlignmentOptions.Left);
                RectTransform bar = Image(p, "DiffBar" + i, new Color(1f, 0.55f, 0.35f), 0.57f, y - 0.022f, 0.97f, y - 0.004f);
                var img = bar.GetComponent<UnityEngine.UI.Image>();
                img.sprite = _white;
                img.type = UnityEngine.UI.Image.Type.Filled;
                img.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                img.fillAmount = 0f;
                bars[i] = img;
            }
            SetArray(w, "difficultyBars", bars);
            Set(w, "phrasesText", Text(p, "Phrases", "", 26, 0.03f, 0.2f, 0.45f, 0.48f, TextAlignmentOptions.TopLeft));
            Set(w, "recordsText", Text(p, "Records", "", 26, 0.47f, 0.2f, 0.97f, 0.46f, TextAlignmentOptions.TopLeft));
            Set(w, "playButton", Button(p, "Play", "플레이", 32, 0.04f, 0.04f, 0.29f, 0.15f, AccentColor));
            Set(w, "likeButton", Button(p, "Like", "좋아요", 30, 0.32f, 0.04f, 0.52f, 0.15f, ButtonColor));
            Set(w, "copyCodeButton", Button(p, "CopyCode", "코드 복사", 30, 0.55f, 0.04f, 0.75f, 0.15f, ButtonColor));
            Set(w, "closeButton", Button(p, "Close", "닫기", 30, 0.78f, 0.04f, 0.96f, 0.15f, ButtonColor));
        }

        private static void BuildResultPopup(GameObject root)
        {
            var w = root.AddComponent<MDSResultPopup>();
            Image(root.transform, "Dim", new Color(0f, 0f, 0f, 0.55f), 0, 0, 1, 1);
            RectTransform p = Image(root.transform, "Panel", PanelColor, 0.2f, 0.06f, 0.8f, 0.94f);
            Set(w, "titleText", Text(p, "Title", "클리어!", 64, 0.05f, 0.87f, 0.95f, 0.98f));
            TMP_Text deaths = Text(p, "Deaths", "0", 150, 0.3f, 0.68f, 0.7f, 0.87f);
            deaths.fontStyle = FontStyles.Bold;
            Set(w, "deathsText", deaths);
            TMP_Text badge = Text(p, "ZeroDeath", "0데스!", 40, 0.7f, 0.74f, 0.97f, 0.84f);
            badge.color = new Color(1f, 0.85f, 0.3f);
            Set(w, "zeroDeathBadge", badge.gameObject);
            Set(w, "detailText", Text(p, "Detail", "", 32, 0.05f, 0.61f, 0.95f, 0.68f));
            Set(w, "recordText", Text(p, "Record", "", 26, 0.05f, 0.47f, 0.95f, 0.61f));

            RectTransform review = Rect(p, "Review", 0.03f, 0.17f, 0.97f, 0.46f);
            Set(w, "reviewRoot", review.gameObject);
            Set(w, "likeButton", Button(review, "Like", "좋아요", 24, 0.01f, 0.72f, 0.19f, 0.97f, ButtonColor));
            var diffs = new Button[4];
            for (int i = 0; i < 4; i++) diffs[i] = Button(review, "Difficulty" + i, "", 22, 0.21f + i * 0.13f, 0.72f, 0.33f + i * 0.13f, 0.97f, ButtonColor);
            SetArray(w, "difficultyButtons", diffs);
            Set(w, "sendReviewButton", Button(review, "Send", "후기 남기기", 24, 0.75f, 0.72f, 0.99f, 0.97f, AccentColor));
            var phrases = new Button[8];
            for (int i = 0; i < 8; i++)
            {
                int col = i % 4, row = i / 4;
                float y1 = row == 0 ? 0.66f : 0.34f;
                phrases[i] = Button(review, "Phrase" + i, "", 20, 0.01f + col * 0.2475f, y1 - 0.29f, 0.24f + col * 0.2475f, y1, ButtonColor);
            }
            SetArray(w, "phraseButtons", phrases);

            TMP_Text multi = Text(p, "Multiplayer", "", 24, 0.03f, 0.17f, 0.97f, 0.46f, TextAlignmentOptions.TopLeft);
            multi.gameObject.SetActive(false);
            Set(w, "multiplayerText", multi);

            Set(w, "retryButton", Button(p, "Retry", "다시 하기", 32, 0.04f, 0.03f, 0.32f, 0.14f, ButtonColor));
            Set(w, "nextButton", Button(p, "Next", "다음 스테이지", 32, 0.36f, 0.03f, 0.64f, 0.14f, AccentColor));
            Set(w, "exitButton", Button(p, "Exit", "나가기", 32, 0.68f, 0.03f, 0.96f, 0.14f, ButtonColor));
        }

        // ── 3. Base 씬 ─────────────────────────────────────────────

        [MenuItem(MENU + "3. Base 씬에 연결 (디렉터·보드·카메라·씬 컨트롤러)", false, 200)]
        public static void IntegrateIntoBaseScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(BASE_SCENE_PATH, OpenSceneMode.Single);

            int removed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            if (cam == null)
            {
                EditorUtility.DisplayDialog("Million Death Square", "Base 씬에서 카메라를 찾지 못했습니다.", "확인");
                return;
            }
            SetupCamera(cam);
            MDSGameDirector director = EnsureGameObjects(cam);

            int linked = 0;
            foreach (var c in Object.FindObjectsByType<Seunghak.SceneManager.InGameSceneController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { SetField(c, "director", director); linked++; }
            foreach (var c in Object.FindObjectsByType<Seunghak.SceneManager.LobbySceneController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { SetField(c, "director", director); linked++; }

            // PC 가로 화면 기준 UI (베이스는 세로 720×1280)
            foreach (var bc in Object.FindObjectsByType<Seunghak.UIManager.BaseCanvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var scaler = bc.GetComponent<CanvasScaler>();
                if (scaler == null) continue;
                scaler.referenceResolution = Reference;
                EditorUtility.SetDirty(scaler);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MDSSetup] Base 씬 연결 완료 — Missing Script " + removed + "개 제거, 씬 컨트롤러 " + linked + "개 연결");
        }

        private static void SetupCamera(Camera cam)
        {
            Undo.RecordObject(cam, "MDS camera");
            Undo.RecordObject(cam.transform, "MDS camera");
            cam.orthographic = true;
            cam.orthographicSize = 10f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            EditorUtility.SetDirty(cam);
        }

        /// <summary>"MillionDeathSquare" 오브젝트 (디렉터) + 자식 "Board" (보드 뷰)를 만들거나 갱신</summary>
        private static MDSGameDirector EnsureGameObjects(Camera cam)
        {
            MDSGameDirector director = Object.FindFirstObjectByType<MDSGameDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                var go = new GameObject("MillionDeathSquare");
                Undo.RegisterCreatedObjectUndo(go, "MDS");
                director = go.AddComponent<MDSGameDirector>();
            }
            MDSBoardView board = Object.FindFirstObjectByType<MDSBoardView>(FindObjectsInactive.Include);
            if (board == null)
            {
                var b = new GameObject("Board");
                b.transform.SetParent(director.transform, false);
                board = b.AddComponent<MDSBoardView>();
            }
            SetField(board, "palettes", AssetDatabase.LoadAssetAtPath<MDSPaletteAsset>(PALETTE_ASSET));
            SetField(board, "sprites", AssetDatabase.LoadAssetAtPath<MDSSpriteLibrary>(SPRITES_ASSET));
            SetField(board, "boardCamera", cam);
            SetField(director, "board", board);
            SetField(director, "campaignAsset", AssetDatabase.LoadAssetAtPath<MDSCampaignAsset>(CAMPAIGN_ASSET));
            if (AssetDatabase.LoadAssetAtPath<MDSSpriteLibrary>(SPRITES_ASSET) == null)
                Debug.LogWarning("[MDSSetup] 스프라이트 에셋이 없어 런타임 생성본을 씁니다. '1. 에셋 생성'을 먼저 실행하세요.");
            return director;
        }

        // ── 4. 테스트 씬 ───────────────────────────────────────────

        [MenuItem(MENU + "4. 테스트 씬 생성 (UI 없이 바로 플레이)", false, 201)]
        public static void CreateTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Camera cam = Camera.main;
            SetupCamera(cam);
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) Object.DestroyImmediate(l.gameObject);
            MDSGameDirector director = EnsureGameObjects(cam);
            SetField(director, "useUiFramework", false);
            new GameObject("TestBootstrap").AddComponent<MDSTestBootstrap>();
            EditorSceneManager.SaveScene(scene, TEST_SCENE_PATH);
            Debug.Log("[MDSSetup] 테스트 씬 생성 → " + TEST_SCENE_PATH + " (플레이: 방향키, R, N/B 스테이지 이동)");
        }

        // ── 5. 입력 축 ─────────────────────────────────────────────

        [MenuItem(MENU + "5. 게임패드 방향 패드 축 추가 (Input Manager)", false, 202)]
        public static void AddGamepadAxes()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset");
            if (assets == null || assets.Length == 0) { Debug.LogError("[MDSSetup] InputManager.asset을 찾지 못했습니다"); return; }
            var so = new SerializedObject(assets[0]);
            SerializedProperty axes = so.FindProperty("m_Axes");
            // Windows XInput 기준: 방향 패드 가로 = 6번째 축(인덱스 5), 세로 = 7번째 축(인덱스 6, 위가 +)
            AddAxis(axes, "MDS_DPadX", 5, false);
            AddAxis(axes, "MDS_DPadY", 6, false);
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[MDSSetup] Input Manager 축 MDS_DPadX / MDS_DPadY 확인 완료");
        }

        private static void AddAxis(SerializedProperty axes, string name, int axisIndex, bool invert)
        {
            for (int i = 0; i < axes.arraySize; i++)
                if (axes.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue == name) return;
            axes.arraySize++;
            SerializedProperty a = axes.GetArrayElementAtIndex(axes.arraySize - 1);
            a.FindPropertyRelative("m_Name").stringValue = name;
            a.FindPropertyRelative("descriptiveName").stringValue = "Million Death Square D-Pad";
            a.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
            a.FindPropertyRelative("negativeButton").stringValue = "";
            a.FindPropertyRelative("positiveButton").stringValue = "";
            a.FindPropertyRelative("altNegativeButton").stringValue = "";
            a.FindPropertyRelative("altPositiveButton").stringValue = "";
            a.FindPropertyRelative("gravity").floatValue = 0f;
            a.FindPropertyRelative("dead").floatValue = 0.2f;
            a.FindPropertyRelative("sensitivity").floatValue = 1f;
            a.FindPropertyRelative("snap").boolValue = false;
            a.FindPropertyRelative("invert").boolValue = invert;
            a.FindPropertyRelative("type").intValue = 2; // Joystick Axis
            a.FindPropertyRelative("axis").intValue = axisIndex;
            a.FindPropertyRelative("joyNum").intValue = 0;
        }

        // ── Steam ──────────────────────────────────────────────────

        [MenuItem(MENU + "Steam 켜기 (STEAMWORKS_NET 정의)", false, 300)]
        public static void EnableSteam()
        {
            Type api = Type.GetType("Steamworks.SteamAPI, com.rlabrecque.steamworks.net");
            if (api == null)
            {
                EditorUtility.DisplayDialog("Million Death Square",
                    "Steamworks.NET이 설치되지 않았습니다.\nPackage Manager > Add package from git URL:\nhttps://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2024.8.0\n(README 참고)", "확인");
                return;
            }
            SetDefine(NamedBuildTarget.Standalone, STEAM_DEFINE, true);
            string appIdFile = Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt");
            if (!File.Exists(appIdFile)) File.WriteAllText(appIdFile, Seunghak.Platform.MDSSteamAppId.Value.ToString());
            Debug.Log("[MDSSetup] STEAMWORKS_NET 정의 추가 (Standalone). steam_appid.txt = " + File.ReadAllText(appIdFile).Trim() + " — 실제 앱 ID로 바꾸세요");
        }

        [MenuItem(MENU + "Steam 끄기 (STEAMWORKS_NET 제거)", false, 301)]
        public static void DisableSteam()
        {
            SetDefine(NamedBuildTarget.Standalone, STEAM_DEFINE, false);
            Debug.Log("[MDSSetup] STEAMWORKS_NET 정의 제거 — 로컬(오프라인) 서비스로 동작");
        }

        private static void SetDefine(NamedBuildTarget target, string define, bool on)
        {
            string current = PlayerSettings.GetScriptingDefineSymbols(target);
            var list = new List<string>(current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            bool has = list.Contains(define);
            if (on && !has) list.Add(define);
            if (!on && has) list.Remove(define);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list.ToArray()));
        }

        // ── UI 도우미 ──────────────────────────────────────────────

        private static RectTransform Rect(Transform parent, string name, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Stretch(rt, x0, y0, x1, y1);
            return rt;
        }

        private static RectTransform Image(Transform parent, string name, Color color, float x0, float y0, float x1, float y1)
        {
            RectTransform rt = Rect(parent, name, x0, y0, x1, y1);
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.color = color;
            return rt;
        }

        private static RawImage Raw(Transform parent, string name, float x0, float y0, float x1, float y1)
        {
            RectTransform rt = Rect(parent, name, x0, y0, x1, y1);
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.color = Color.white;
            raw.raycastTarget = false;
            return raw;
        }

        private static TMP_Text Text(Transform parent, string name, string text, float size, float x0, float y0, float x1, float y1,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            RectTransform rt = Rect(parent, name, x0, y0, x1, y1);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null) tmp.font = _font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = Mathf.Min(14f, size);
            tmp.fontSizeMax = size;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button Button(Transform parent, string name, string label, float size, float x0, float y0, float x1, float y1, Color color)
        {
            RectTransform rt = Image(parent, name, color, x0, y0, x1, y1);
            var btn = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            btn.colors = cb;
            TMP_Text t = Text(rt, "Label", label, size, 0.03f, 0.05f, 0.97f, 0.95f);
            t.textWrappingMode = TextWrappingModes.Normal;
            return btn;
        }

        private static TMP_InputField InputField(Transform parent, string name, string placeholder, float size, float x0, float y0, float x1, float y1)
        {
            RectTransform rt = Image(parent, name, new Color(1f, 1f, 1f, 0.95f), x0, y0, x1, y1);
            var input = rt.gameObject.AddComponent<TMP_InputField>();
            RectTransform area = Rect(rt, "Text Area", 0, 0, 1, 1);
            area.offsetMin = new Vector2(12f, 4f);
            area.offsetMax = new Vector2(-12f, -4f);
            area.gameObject.AddComponent<RectMask2D>();
            TMP_Text ph = Text(area, "Placeholder", placeholder, size, 0, 0, 1, 1, TextAlignmentOptions.Left);
            ph.color = new Color(0.4f, 0.4f, 0.45f, 0.8f);
            ph.fontStyle = FontStyles.Italic;
            TMP_Text tx = Text(area, "Text", "", size, 0, 0, 1, 1, TextAlignmentOptions.Left);
            tx.color = new Color(0.1f, 0.1f, 0.12f);
            tx.enableAutoSizing = false;
            tx.textWrappingMode = TextWrappingModes.NoWrap;
            input.textViewport = area;
            input.textComponent = tx;
            input.placeholder = ph;
            if (_font != null) input.fontAsset = _font;
            input.pointSize = size;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }

        private static void Stretch(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ── 직렬화 도우미 ──────────────────────────────────────────

        private static void Set(Object target, string field, Object value) { SetField(target, field, value); }

        private static void SetField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError("[MDSSetup] " + target.GetType().Name + "." + field + " 필드를 찾을 수 없습니다.");
                return;
            }
            if (value == null || value is Object) p.objectReferenceValue = (Object)value;
            else if (value is bool) p.boolValue = (bool)value;
            else if (value is int) p.intValue = (int)value;
            else if (value is float) p.floatValue = (float)value;
            else if (value is string) p.stringValue = (string)value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null || !p.isArray)
            {
                Debug.LogError("[MDSSetup] " + target.GetType().Name + "." + field + " 배열 필드를 찾을 수 없습니다.");
                return;
            }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
