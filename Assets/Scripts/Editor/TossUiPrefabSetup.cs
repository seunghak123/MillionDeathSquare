using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.EditorTools
{
    /// <summary>
    /// 토스 플랫폼용 UI 요소(dev 라벨, 랭킹/초대/공유 버튼, 골드 구매 슬롯)를 프리팹에 배치하고 필드를 연결한다.
    /// 여러 번 실행해도 이름으로 기존 오브젝트를 찾아 갱신한다.
    /// </summary>
    public static class TossUiPrefabSetup
    {
        private const string Dir = "Assets/BaseSources/Prefabs/UI/";

        [MenuItem("Tools/Toss/Dump UI Prefabs")]
        public static void Dump()
        {
            foreach (var n in new[] { "GameStartWindow", "GameOverPopup", "CharacterShopWindow" })
            {
                var root = PrefabUtility.LoadPrefabContents(Dir + n + ".prefab");
                var sb = new StringBuilder("[DUMP] " + n + "\n");
                Walk(root.transform, 0, sb);
                System.IO.File.WriteAllText("Temp/dump_" + n + ".txt", sb.ToString());
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/Toss/Setup UI Prefabs")]
        public static void Setup()
        {
            SetupGameStart();
            SetupGameOver();
            SetupShop();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TossUiPrefabSetup] done");
        }

        private static void SetupGameStart()
        {
            string path = Dir + "GameStartWindow.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var rootRt = (RectTransform)root.transform;
            var template = rootRt.Find("Character card/select Character/PurchaseButton");

            var lb = GetOrClone(rootRt, "LeaderboardButton", template);
            PlaceTopLeft(lb, new Vector2(16, -16), new Vector2(130, 52), "랭킹");
            var inv = GetOrClone(rootRt, "InviteButton", template);
            PlaceTopLeft(inv, new Vector2(16, -76), new Vector2(130, 52), "친구 초대");

            var label = rootRt.Find("DevBuildLabel") as RectTransform;
            if (label == null)
            {
                var go = new GameObject("DevBuildLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                label = (RectTransform)go.transform;
                label.SetParent(rootRt, false);
            }
            var src = rootRt.Find("High Score Text").GetComponent<TextMeshProUGUI>();
            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.font = src.font;
            tmp.fontSharedMaterial = src.fontSharedMaterial;
            tmp.text = "dev";
            tmp.fontSize = 26;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(1f, 0.85f, 0.2f, 1f);
            tmp.alignment = TextAlignmentOptions.BottomRight;
            tmp.raycastTarget = false;
            label.anchorMin = label.anchorMax = new Vector2(1, 0);
            label.pivot = new Vector2(1, 0);
            label.anchoredPosition = new Vector2(-16, 12);
            label.sizeDelta = new Vector2(120, 40);
            label.SetAsLastSibling();
            label.gameObject.SetActive(false);

            var so = new SerializedObject(FindComp(root, "GameStartWindow"));
            so.FindProperty("devBuildLabel").objectReferenceValue = label.gameObject;
            so.FindProperty("leaderboardButton").objectReferenceValue = lb.GetComponent<Button>();
            so.FindProperty("inviteButton").objectReferenceValue = inv.GetComponent<Button>();
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void SetupGameOver()
        {
            string path = Dir + "GameOverPopup.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var rootRt = (RectTransform)root.transform;
            var template = rootRt.Find("ButtonRetry");

            var lb = GetOrClone(rootRt, "ButtonLeaderboard", template);
            PlaceCentered(lb, new Vector2(-150, -412), new Vector2(240, 76), "랭킹");
            var sh = GetOrClone(rootRt, "ButtonShare", template);
            PlaceCentered(sh, new Vector2(150, -412), new Vector2(240, 76), "자랑하기");

            var so = new SerializedObject(FindComp(root, "GameOverPopup"));
            so.FindProperty("leaderboardButton").objectReferenceValue = lb.GetComponentInChildren<Button>(true);
            so.FindProperty("shareButton").objectReferenceValue = sh.GetComponentInChildren<Button>(true);
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void SetupShop()
        {
            string path = Dir + "CharacterShopWindow.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var rootRt = (RectTransform)root.transform;
            var panel = rootRt.Find("Panel Group/Panel Group (1)");
            var btnTemplate = panel.Find("Card_box2/Button Object/button image");
            var txtTemplate = panel.Find("Card_box2/Button Object/button nameLabel");

            var section = panel.Find("IapSection") as RectTransform;
            if (section == null)
            {
                var go = new GameObject("IapSection", typeof(RectTransform));
                section = (RectTransform)go.transform;
                section.SetParent(panel, false);
            }
            section.anchorMin = section.anchorMax = new Vector2(0.5f, 0.5f);
            section.pivot = new Vector2(0.5f, 0.5f);
            section.sizeDelta = new Vector2(510, 150);
            section.SetAsLastSibling();

            var slots = new Button[3];
            var names = new TextMeshProUGUI[3];
            var prices = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                var slot = GetOrClone(section, "IapSlot" + (i + 1), btnTemplate);
                slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0.5f);
                slot.pivot = new Vector2(0.5f, 0.5f);
                slot.anchoredPosition = new Vector2((i - 1) * 170, 0);
                slot.sizeDelta = new Vector2(160, 130);
                slots[i] = slot.GetComponent<Button>();

                names[i] = GetOrCloneText(slot, "NameLabel", txtTemplate, new Vector2(0, 24), new Vector2(150, 40), 24, "골드");
                prices[i] = GetOrCloneText(slot, "PriceLabel", txtTemplate, new Vector2(0, -24), new Vector2(150, 40), 28, "0원");
            }
            section.gameObject.SetActive(false);

            var so = new SerializedObject(FindComp(root, "CharacterShopWindow"));
            so.FindProperty("iapSection").objectReferenceValue = section.gameObject;
            var arr = so.FindProperty("iapSlots");
            arr.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("button").objectReferenceValue = slots[i];
                e.FindPropertyRelative("nameLabel").objectReferenceValue = names[i];
                e.FindPropertyRelative("priceLabel").objectReferenceValue = prices[i];
            }
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static Component FindComp(GameObject root, string typeName)
        {
            return root.GetComponents<MonoBehaviour>().First(c => c != null && c.GetType().Name == typeName);
        }

        private static RectTransform GetOrClone(Transform parent, string name, Transform template)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) { ClearOnClick(existing); return existing; }
            var go = Object.Instantiate(template.gameObject, parent, false);
            go.name = name;
            go.SetActive(true);
            var rt = (RectTransform)go.transform;
            ClearOnClick(rt);
            return rt;
        }

        // 복제본에 남은 영구 OnClick 리스너 제거 (리스너는 코드가 등록)
        private static void ClearOnClick(Transform t)
        {
            foreach (var b in t.GetComponentsInChildren<Button>(true))
            {
                var so = new SerializedObject(b);
                so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").arraySize = 0;
                so.ApplyModifiedProperties();
            }
        }

        private static TextMeshProUGUI GetOrCloneText(RectTransform parent, string name, Transform template, Vector2 pos, Vector2 size, float fontSize, string text)
        {
            var rt = parent.Find(name) as RectTransform;
            if (rt == null)
            {
                var go = Object.Instantiate(template.gameObject, parent, false);
                go.name = name;
                rt = (RectTransform)go.transform;
            }
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var tmp = rt.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.enableAutoSizing = false;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void PlaceTopLeft(RectTransform rt, Vector2 pos, Vector2 size, string label)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var tmp = rt.GetComponentInChildren<TextMeshProUGUI>(true);
            tmp.text = label;
            tmp.enableAutoSizing = false;
            tmp.fontSize = 24;
            tmp.raycastTarget = false;
            var trt = (RectTransform)tmp.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
        }

        // ButtonRetry 구조: 빈 컨테이너 > (button image, button nameLabel)
        private static void PlaceCentered(RectTransform rt, Vector2 pos, Vector2 size, string label)
        {
            rt.anchoredPosition = pos;
            ((RectTransform)rt.Find("button image")).sizeDelta = size;
            var tmp = rt.GetComponentInChildren<TextMeshProUGUI>(true);
            tmp.text = label;
            tmp.raycastTarget = false;
        }

        private static void Walk(Transform t, int d, StringBuilder sb)
        {
            var rt = t as RectTransform;
            string comps = "";
            foreach (var c in t.GetComponents<Component>()) if (c != null && !(c is Transform)) comps += c.GetType().Name + ",";
            sb.Append(new string(' ', d * 2)).Append(t.name).Append(t.gameObject.activeSelf ? "" : "(off)");
            if (rt != null) sb.Append($" aMin{rt.anchorMin} aMax{rt.anchorMax} pos{rt.anchoredPosition} size{rt.sizeDelta} piv{rt.pivot}");
            sb.Append(" [").Append(comps).Append("]\n");
            foreach (Transform c in t) Walk(c, d + 1, sb);
        }
    }
}
