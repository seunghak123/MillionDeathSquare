using System.Collections.Generic;
using System.IO;
using Seunghak.Game.Suika3D;
using Seunghak.UIManager;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Seunghak.EditorTools
{
    /// <summary>
    /// Fossil Merge(가제, 기획: 민수님) 셋업 메뉴 — Tools/Fossil Merge
    /// (에셋/씬/오브젝트 이름에는 Suika를 쓰지 않는다. 클래스 이름은 출시 전 정리 작업에서 변경 예정)
    ///   1. 화석 프리팹 + 테이블 생성 — 11단계 노듈(둥근 돌덩이) 임시 그래픽: 단계별 색/크기
    ///   2. 테스트 씬 생성 — 발굴대 / 벽 / 카메라 리그 / 매니저 / 드롭 / 스킬 / 지진 / 기울기 / 테스트 HUD / 부트스트랩
    ///   3·4. 빌드 씬 전환 — Toss 테스트 빌드용 FossilMergeTest만 / Base 복원
    ///   5. Base 씬에 연결 — 삭제된 스크립트 정리 + 게임 오브젝트 생성/업그레이드 + 씬 컨트롤러 연결
    ///   6. 레벨업 팝업 프리팹 생성 — LevelUpPopup (Addressables UI 그룹 등록까지)
    ///   7. 열린 씬 업그레이드 — 기존 FossilMergeGame에 새 컴포넌트/벽/화석 테이블 적용
    /// </summary>
    public static class Suika3DSetupTool
    {
        private const string ROOT = "Assets/BaseSources/FossilMerge";
        private const string FOSSIL_DIR = ROOT + "/Fossils";
        private const string FOSSIL_MAT_DIR = FOSSIL_DIR + "/Materials";
        private const string TABLE_PATH = ROOT + "/FossilTable.asset";
        private const string PHYS_MAT_PATH = ROOT + "/Fossil.physicMaterial";
        private const string PLATE_MAT_PATH = ROOT + "/FossilPlate.mat";
        private const string WALL_MAT_PATH = ROOT + "/FossilPlateWall.mat";
        private const string MARKER_MAT_PATH = ROOT + "/FossilLandingMarker.mat";
        private const string PLATE_PHYS_MAT_PATH = ROOT + "/FossilPlate.physicMaterial";
        private const string SCENE_PATH = "Assets/Scenes/FossilMergeTest.unity";
        private const string BASE_SCENE_PATH = "Assets/Scenes/Base.unity";
        private const string UI_PREFAB_DIR = "Assets/BaseSources/Prefabs/UI";
        private const string FONT_PATH = "Assets/BaseSources/UI/Font/ONE Mobile POP OTF SDF.asset";

        private const float PLATE_RADIUS = 3.5f;
        private const int WALL_SEGMENTS = 24;
        private const float WALL_HEIGHT = 1.1f;
        private const float WALL_THICKNESS = 0.2f;

        // 기획서 4장: 1~11단계 (1~5단계만 떨어뜨리는 화석으로 등장)
        private static readonly string[] FOSSIL_NAMES =
        {
            "모래알", "조개 화석", "삼엽충", "암모나이트", "물고기 화석", "호박(곤충)",
            "공룡 알", "공룡 이빨", "공룡 발톱", "공룡 두개골", "공룡 골격",
        };

        // 작은 폰 화면에서도 한눈에 구분되도록 단계별로 뚜렷하게 다른 색
        private static readonly Color[] FOSSIL_COLORS =
        {
            new Color(0.93f, 0.85f, 0.62f), // 모래알 — 모래색
            new Color(0.98f, 0.70f, 0.62f), // 조개 — 연분홍
            new Color(0.55f, 0.62f, 0.75f), // 삼엽충 — 청회색
            new Color(0.95f, 0.58f, 0.25f), // 암모나이트 — 주황
            new Color(0.35f, 0.70f, 0.85f), // 물고기 — 하늘
            new Color(0.98f, 0.78f, 0.15f), // 호박 — 호박색
            new Color(0.90f, 0.92f, 0.80f), // 공룡 알 — 아이보리
            new Color(0.55f, 0.80f, 0.45f), // 이빨 — 연두
            new Color(0.62f, 0.45f, 0.80f), // 발톱 — 보라
            new Color(0.85f, 0.30f, 0.30f), // 두개골 — 빨강
            new Color(0.95f, 0.95f, 0.98f), // 골격 — 흰색
        };

        private const float BASE_RADIUS = 0.28f;
        private const float RADIUS_GROWTH = 1.18f;
        private const int SPAWN_TIERS = 5;

        // ── 1. 화석 프리팹 + 테이블 ─────────────────────────────────────

        [MenuItem("Tools/Fossil Merge/1. 화석 프리팹 + 테이블 생성", false, 100)]
        public static void CreateFossilPrefabsAndTable()
        {
            EnsureFolder(FOSSIL_MAT_DIR);
            PhysicMaterial phys = LoadOrCreatePhysicMaterial(PHYS_MAT_PATH, 0.5f, 0.6f, 0.05f);

            var table = AssetDatabase.LoadAssetAtPath<SuikaFruitTable>(TABLE_PATH);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<SuikaFruitTable>();
                AssetDatabase.CreateAsset(table, TABLE_PATH);
            }

            var tiers = new List<SuikaFruitTable.Tier>();
            for (int i = 0; i < FOSSIL_NAMES.Length; i++)
            {
                float r = BASE_RADIUS * Mathf.Pow(RADIUS_GROWTH, i);
                Color c = FOSSIL_COLORS[i];
                Material body = LoadOrCreateMaterial($"{FOSSIL_MAT_DIR}/Fossil_{i:00}.mat", c, false, overwriteColor: true);
                Color markColor = new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 1f);
                Material mark = LoadOrCreateMaterial($"{FOSSIL_MAT_DIR}/Fossil_{i:00}_Mark.mat", markColor, false, overwriteColor: true);
                GameObject prefab = BuildFossilPrefab($"{FOSSIL_DIR}/Fossil_{i + 1:00}.prefab", body, mark, phys);

                tiers.Add(new SuikaFruitTable.Tier
                {
                    displayName = FOSSIL_NAMES[i],
                    prefab = prefab,
                    radius = r,
                    mass = 4f * r * r,
                    mergeScore = SuikaRules.MergeScore(i),
                    spawnWeight = i < SPAWN_TIERS ? 1f : 0f,
                });
            }

            table.tiers = tiers.ToArray();
            table.maxTierClearBonus = 100;
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();

            Selection.activeObject = table;
            Debug.Log($"[FossilSetup] 화석 {tiers.Count}단계 생성 완료 → {TABLE_PATH}");
        }

        // 노듈(둥근 돌덩이) + 화석 자국 하나 (굴러가는 게 보이도록)
        private static GameObject BuildFossilPrefab(string savePath, Material body, Material mark, PhysicMaterial phys)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(savePath));
            try
            {
                var col = root.AddComponent<SphereCollider>();
                col.radius = 0.5f;
                col.sharedMaterial = phys;
                var rb = root.AddComponent<Rigidbody>();
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                root.AddComponent<SuikaFruit>();

                GameObject nodule = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                nodule.name = "Nodule";
                Object.DestroyImmediate(nodule.GetComponent<Collider>());
                nodule.transform.SetParent(root.transform, false);
                nodule.GetComponent<MeshRenderer>().sharedMaterial = body;

                GameObject imprint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                imprint.name = "Imprint";
                Object.DestroyImmediate(imprint.GetComponent<Collider>());
                imprint.transform.SetParent(root.transform, false);
                imprint.transform.localPosition = new Vector3(0f, 0.43f, 0f);
                imprint.transform.localScale = new Vector3(0.5f, 0.16f, 0.5f);
                imprint.GetComponent<MeshRenderer>().sharedMaterial = mark;

                return PrefabUtility.SaveAsPrefabAsset(root, savePath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static SuikaFruitTable EnsureTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<SuikaFruitTable>(TABLE_PATH);
            if (table != null && table.Count == FOSSIL_NAMES.Length) return table;
            if (!EditorUtility.DisplayDialog("Fossil Merge", "화석 11단계 테이블이 없습니다. 지금 생성할까요?", "생성", "취소")) return null;
            CreateFossilPrefabsAndTable();
            table = AssetDatabase.LoadAssetAtPath<SuikaFruitTable>(TABLE_PATH);
            return table != null && table.Count > 0 ? table : null;
        }

        // ── 2. 테스트 씬 ───────────────────────────────────────────────

        [MenuItem("Tools/Fossil Merge/2. 테스트 씬 생성", false, 101)]
        public static void CreateTestScene()
        {
            var table = EnsureTable();
            if (table == null) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Camera cam = Camera.main;
            SetupCamera(cam);
            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
                sun.shadows = LightShadows.Soft;
            }

            var manager = BuildGame(cam, table, Vector3.zero);
            SetField(manager, "autoInitialize", false); // SuikaTestBootstrap이 플랫폼 준비 후 시작
            SetField(manager, "allowLockedModes", true); // 테스트 씬은 지각 변동 모드 바로 확인 가능

            var hud = manager.gameObject.AddComponent<SuikaTestHUD>();
            SetField(hud, "manager", manager);
            var boot = manager.gameObject.AddComponent<SuikaTestBootstrap>();
            SetField(boot, "manager", manager);

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            Debug.Log($"[FossilSetup] 테스트 씬 생성 완료 → {SCENE_PATH} (Play로 바로 테스트, 에디터 기울기 = I/J/K/L)");
        }

        // ── 5. Base 씬 연결 ────────────────────────────────────────────

        [MenuItem("Tools/Fossil Merge/5. Base 씬에 연결 (정리 + 업그레이드)", false, 300)]
        public static void IntegrateIntoBaseScene()
        {
            var table = EnsureTable();
            if (table == null) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(BASE_SCENE_PATH, OpenSceneMode.Single);

            // 1) 삭제된 스크립트(Missing Script) 컴포넌트 제거
            int removed = 0;
            var emptied = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    int n = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                    if (n <= 0) continue;
                    removed += n;
                    if (t.GetComponents<Component>().Length <= 1 && t.childCount == 0)
                        emptied.Add(GetPath(t));
                }
            }

            // 2) 게임 오브젝트 생성 또는 업그레이드
            var manager = Object.FindFirstObjectByType<SuikaGameManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
                if (cam == null)
                {
                    EditorUtility.DisplayDialog("Fossil Merge", "Base 씬에서 카메라를 찾지 못했습니다.", "확인");
                    return;
                }
                SetupCamera(cam);
                manager = BuildGame(cam, table, Vector3.zero);
            }
            else
            {
                UpgradeGame(manager, table);
            }
            // Base에서는 InGameSceneController가 인게임 진입 시 RestartGame()으로 시작한다
            SetField(manager, "autoInitialize", false);
            SetField(manager, "allowLockedModes", false);

            // 3) 씬 컨트롤러 연결
            int linked = 0;
            foreach (var c in Object.FindObjectsByType<Seunghak.SceneManager.InGameSceneController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { SetField(c, "gameManager", manager); linked++; }
            foreach (var c in Object.FindObjectsByType<Seunghak.SceneManager.LobbySceneController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { SetField(c, "gameManager", manager); linked++; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[FossilSetup] Base 씬 연결 완료 — Missing Script {removed}개 제거, 씬 컨트롤러 {linked}개 연결");
            if (emptied.Count > 0)
            {
                sb.AppendLine("아래 오브젝트는 스크립트가 빠져 빈 오브젝트가 됐습니다. 확인 후 직접 삭제하세요:");
                foreach (string e in emptied) sb.AppendLine("  - " + e);
            }
            Debug.Log(sb.ToString());
        }

        // ── 7. 열린 씬 업그레이드 ──────────────────────────────────────

        [MenuItem("Tools/Fossil Merge/7. 열린 씬 업그레이드 (새 컴포넌트·벽·화석 테이블)", false, 302)]
        public static void UpgradeOpenScene()
        {
            var table = EnsureTable();
            if (table == null) return;
            var manager = Object.FindFirstObjectByType<SuikaGameManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                EditorUtility.DisplayDialog("Fossil Merge", "열린 씬에 SuikaGameManager가 없습니다. 2번(테스트 씬) 또는 5번(Base)을 쓰세요.", "확인");
                return;
            }
            UpgradeGame(manager, table);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Debug.Log("[FossilSetup] 열린 씬 업그레이드 완료 — 저장하세요");
        }

        /// <summary>기존 FossilMergeGame에 빠진 컴포넌트/벽/참조를 채운다 (여러 번 실행해도 같은 결과)</summary>
        private static void UpgradeGame(SuikaGameManager manager, SuikaFruitTable table)
        {
            GameObject game = manager.gameObject;
            SetField(manager, "fruitTable", table);

            var skills = game.GetComponent<SuikaSkillSystem>();
            if (skills == null) skills = game.AddComponent<SuikaSkillSystem>();
            var quakes = game.GetComponent<SuikaQuakeDirector>();
            if (quakes == null) quakes = game.AddComponent<SuikaQuakeDirector>();
            var tilt = game.GetComponent<SuikaTiltInput>();
            if (tilt == null) tilt = game.AddComponent<SuikaTiltInput>();
            SetField(skills, "manager", manager);
            SetField(manager, "skillSystem", skills);
            SetField(manager, "quakeDirector", quakes);

            var plate = Object.FindFirstObjectByType<SuikaPlate>(FindObjectsInactive.Include);
            if (plate != null)
            {
                var walls = Object.FindFirstObjectByType<SuikaPlateWalls>(FindObjectsInactive.Include);
                if (walls == null) walls = BuildWalls(game.transform, plate.transform.position);
                Camera cam = manager.CameraRig != null ? manager.CameraRig.Camera : Camera.main;
                SetField(plate, "walls", walls);
                SetField(plate, "tiltInput", tilt);
                SetField(plate, "quakeDirector", quakes);
                if (cam != null) SetField(plate, "viewCamera", cam);
            }
        }

        // ── 공통: 게임 오브젝트 구성 ───────────────────────────────────

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }

        private static void SetupCamera(Camera cam)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.86f, 0.78f, 0.62f); // 발굴 현장 모래빛
            cam.fieldOfView = 50f;
        }

        /// <summary>발굴대 + 받침대 + 벽 + 게임 루트(카메라 리그/매니저/드롭/스킬/지진/기울기/가이드/그림자)</summary>
        private static SuikaGameManager BuildGame(Camera cam, SuikaFruitTable table, Vector3 platePos)
        {
            var game = new GameObject("FossilMergeGame");

            // 발굴대
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plate.name = "Plate";
            plate.transform.SetParent(game.transform, false);
            plate.transform.position = platePos;
            plate.transform.localScale = new Vector3(PLATE_RADIUS * 2f, 0.15f, PLATE_RADIUS * 2f);
            Object.DestroyImmediate(plate.GetComponent<Collider>());
            var plateCol = plate.AddComponent<MeshCollider>();
            plateCol.convex = true;
            plateCol.sharedMaterial = LoadOrCreatePhysicMaterial(PLATE_PHYS_MAT_PATH, 0.7f, 0.8f, 0f);
            plate.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateMaterial(PLATE_MAT_PATH, new Color(0.72f, 0.58f, 0.42f), false);
            var plateRb = plate.AddComponent<Rigidbody>();
            plateRb.isKinematic = true;
            var plateComp = plate.AddComponent<SuikaPlate>();
            SetField(plateComp, "radius", PLATE_RADIUS);

            // 받침대 (시각용, 충돌 없음)
            GameObject stand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stand.name = "PlateStand";
            stand.transform.SetParent(game.transform, false);
            stand.transform.position = platePos + new Vector3(0f, -1.1f, 0f);
            stand.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
            Object.DestroyImmediate(stand.GetComponent<Collider>());

            var fruitRoot = new GameObject("Fossils").transform;
            fruitRoot.SetParent(game.transform, false);

            var rig = game.AddComponent<SuikaCameraRig>();
            SetField(rig, "targetCamera", cam);
            SetField(rig, "pivot", plate.transform);

            var manager = game.AddComponent<SuikaGameManager>();
            SetField(manager, "fruitTable", table);
            SetField(manager, "plate", plateComp);
            SetField(manager, "cameraRig", rig);
            SetField(manager, "fruitRoot", fruitRoot);

            // 낙하 위치 가이드: 수직선
            var guideGo = new GameObject("DropGuide");
            guideGo.transform.SetParent(game.transform, false);
            var line = guideGo.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.04f;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Line.mat");
            line.startColor = line.endColor = new Color(1f, 1f, 1f, 0.8f);
            line.enabled = false;

            // 낙하 위치 가이드: 그림자 원판 (충돌 없음)
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "LandingMarker";
            marker.transform.SetParent(game.transform, false);
            marker.transform.localScale = new Vector3(1f, 0.005f, 1f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            var markerRenderer = marker.GetComponent<MeshRenderer>();
            markerRenderer.sharedMaterial = LoadOrCreateMaterial(MARKER_MAT_PATH, new Color(0f, 0f, 0f, 0.35f), true);
            markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
            markerRenderer.receiveShadows = false;
            marker.SetActive(false);

            var dropper = game.AddComponent<SuikaDropper>();
            SetField(dropper, "manager", manager);
            SetField(dropper, "guideLine", line);
            SetField(dropper, "landingMarker", marker.transform);

            UpgradeGame(manager, table); // 스킬/지진/기울기/벽 + 발굴대 참조
            return manager;
        }

        /// <summary>발굴대 테두리 벽 (지진 스킬 동안 솟아오름): 박스 세그먼트 링</summary>
        private static SuikaPlateWalls BuildWalls(Transform parent, Vector3 platePos)
        {
            var go = new GameObject("PlateWalls");
            go.transform.SetParent(parent, false);
            go.transform.position = platePos;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var walls = go.AddComponent<SuikaPlateWalls>();

            var ring = new GameObject("Ring").transform;
            ring.SetParent(go.transform, false);

            Material mat = LoadOrCreateMaterial(WALL_MAT_PATH, new Color(0.55f, 0.42f, 0.30f), false);
            float r = PLATE_RADIUS + WALL_THICKNESS * 0.5f;
            float segLen = 2f * Mathf.PI * r / WALL_SEGMENTS * 1.08f;
            for (int i = 0; i < WALL_SEGMENTS; i++)
            {
                float a = i * Mathf.PI * 2f / WALL_SEGMENTS;
                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube); // BoxCollider 포함
                seg.name = $"Wall_{i:00}";
                seg.transform.SetParent(ring, false);
                seg.transform.localPosition = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                seg.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
                seg.transform.localScale = new Vector3(segLen, WALL_HEIGHT, WALL_THICKNESS);
                var mr = seg.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }

            SetField(walls, "ring", ring);
            return walls;
        }

        // ── 6. 레벨업 팝업 프리팹 ──────────────────────────────────────

        [MenuItem("Tools/Fossil Merge/6. 레벨업 팝업 프리팹 생성", false, 301)]
        public static void CreateLevelUpPopupPrefab()
        {
            EnsureFolder(UI_PREFAB_DIR);
            string path = $"{UI_PREFAB_DIR}/LevelUpPopup.prefab";
            if (File.Exists(path) &&
                !EditorUtility.DisplayDialog("Fossil Merge", "LevelUpPopup 프리팹이 이미 있습니다. 덮어쓸까요?", "덮어쓰기", "취소"))
                return;

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            var root = new GameObject("LevelUpPopup");
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
                root.AddComponent<GraphicRaycaster>();
                var popup = root.AddComponent<LevelUpPopup>();

                var dim = UiImage(root.transform, "Dim", new Color(0f, 0f, 0f, 0.6f), Vector2.zero, Vector2.one);
                var panel = UiImage(root.transform, "Panel", new Color(0.18f, 0.14f, 0.11f, 0.97f), new Vector2(0.06f, 0.2f), new Vector2(0.94f, 0.82f));
                UiText(panel, "Title", "레벨업!\n스킬 1개를 고르세요", 64, font, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.98f));

                var optButtons = new Button[3];
                var optNames = new TextMeshProUGUI[3];
                var optDescs = new TextMeshProUGUI[3];
                for (int i = 0; i < 3; i++)
                {
                    float top = 0.78f - i * 0.2f;
                    var b = UiButton(panel, $"Option{i}", new Color(0.36f, 0.27f, 0.18f), new Vector2(0.06f, top - 0.18f), new Vector2(0.94f, top));
                    optButtons[i] = b;
                    optNames[i] = UiText(b.transform, "Name", "스킬", 52, font, new Vector2(0.04f, 0.5f), new Vector2(0.96f, 0.95f));
                    optDescs[i] = UiText(b.transform, "Desc", "설명", 34, font, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.5f));
                }

                var reroll = UiButton(panel, "RerollButton", new Color(0.2f, 0.55f, 0.85f), new Vector2(0.15f, 0.04f), new Vector2(0.85f, 0.15f));
                UiText(reroll.transform, "Label", "광고 보고 다시 뽑기", 44, font, Vector2.zero, Vector2.one);

                // 보유 칸이 가득 찼을 때: 버릴 스킬 선택
                var replace = UiImage(panel, "ReplaceRoot", new Color(0.1f, 0.08f, 0.06f, 0.98f), Vector2.zero, Vector2.one);
                var replaceTitle = UiText(replace, "Title", "스킬 칸이 가득 찼어요", 40, font, new Vector2(0.05f, 0.8f), new Vector2(0.95f, 0.97f));
                var slotButtons = new Button[4];
                var slotLabels = new TextMeshProUGUI[4];
                for (int i = 0; i < 4; i++)
                {
                    float top = 0.78f - i * 0.15f;
                    var b = UiButton(replace, $"Slot{i}", new Color(0.45f, 0.25f, 0.2f), new Vector2(0.1f, top - 0.13f), new Vector2(0.9f, top));
                    slotButtons[i] = b;
                    slotLabels[i] = UiText(b.transform, "Label", "스킬", 44, font, Vector2.zero, Vector2.one);
                }
                var discard = UiButton(replace, "DiscardButton", new Color(0.35f, 0.35f, 0.35f), new Vector2(0.2f, 0.04f), new Vector2(0.8f, 0.14f));
                UiText(discard.transform, "Label", "새 스킬 포기", 40, font, Vector2.zero, Vector2.one);
                replace.gameObject.SetActive(false);

                SetArray(popup, "optionButtons", optButtons);
                SetArray(popup, "optionNames", optNames);
                SetArray(popup, "optionDescs", optDescs);
                SetField(popup, "rerollButton", reroll);
                SetField(popup, "replaceRoot", replace.gameObject);
                SetField(popup, "replaceTitle", replaceTitle);
                SetArray(popup, "replaceSlotButtons", slotButtons);
                SetArray(popup, "replaceSlotLabels", slotLabels);
                SetField(popup, "discardButton", discard);
                dim.SetAsFirstSibling();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            // 빌드에서도 로드되도록 Addressables UI 그룹 등록 (주소 = 파일명)
            Seunghak.Editor.AddressablesBuildSetup.SetupGroups();
            Debug.Log($"[FossilSetup] LevelUpPopup 프리팹 생성 → {path} (Addressables UI 그룹 등록 완료)");
        }

        private static Transform UiImage(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color;
            Stretch(go.GetComponent<RectTransform>(), min, max);
            return go.transform;
        }

        private static Button UiButton(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var t = UiImage(parent, name, color, min, max);
            return t.gameObject.AddComponent<Button>();
        }

        private static TextMeshProUGUI UiText(Transform parent, string name, string text, float size, TMP_FontAsset font, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            Stretch(go.GetComponent<RectTransform>(), min, max);
            return tmp;
        }

        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ── 3. 빌드 씬 전환 ────────────────────────────────────────────

        [MenuItem("Tools/Fossil Merge/3. 빌드 씬을 FossilMergeTest만으로 설정 (Toss 테스트 빌드용)", false, 200)]
        public static void UseTestSceneForBuild()
        {
            if (!File.Exists(SCENE_PATH))
            {
                EditorUtility.DisplayDialog("Fossil Merge", "테스트 씬이 없습니다. 먼저 '2. 테스트 씬 생성'을 실행하세요.", "확인");
                return;
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SCENE_PATH, true) };
            Debug.Log("[FossilSetup] 빌드 씬 = FossilMergeTest. 이후 Tools/WebGL Portals/Toss 미니게임 빌드를 실행하세요.");
        }

        [MenuItem("Tools/Fossil Merge/4. 빌드 씬을 Base로 복원", false, 201)]
        public static void RestoreBaseSceneForBuild()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BASE_SCENE_PATH, true) };
            Debug.Log("[FossilSetup] 빌드 씬 = Base.unity 로 복원");
        }

        // ── 유틸 ───────────────────────────────────────────────────────

        private static void SetField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError($"[FossilSetup] {target.GetType().Name}.{field} 필드를 찾을 수 없습니다.");
                return;
            }
            switch (value)
            {
                case Object o: p.objectReferenceValue = o; break;
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
                case bool b: p.boolValue = b; break;
                case string s: p.stringValue = s; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null || !p.isArray)
            {
                Debug.LogError($"[FossilSetup] {target.GetType().Name}.{field} 배열 필드를 찾을 수 없습니다.");
                return;
            }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static PhysicMaterial LoadOrCreatePhysicMaterial(string path, float dynamicFriction, float staticFriction, float bounce)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var mat = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
            if (mat == null)
            {
                mat = new PhysicMaterial(Path.GetFileNameWithoutExtension(path));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.dynamicFriction = dynamicFriction;
            mat.staticFriction = staticFriction;
            mat.bounciness = bounce;
            mat.frictionCombine = PhysicMaterialCombine.Average;
            mat.bounceCombine = PhysicMaterialCombine.Minimum;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreateMaterial(string path, Color color, bool transparent, bool overwriteColor = false)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                if (overwriteColor) { mat.color = color; EditorUtility.SetDirty(mat); }
                return mat;
            }

            mat = new Material(Shader.Find("Standard")) { color = color };
            mat.SetFloat("_Glossiness", 0.15f);
            if (transparent)
            {
                // Standard 셰이더 Fade 모드
                mat.SetFloat("_Mode", 2f);
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
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
