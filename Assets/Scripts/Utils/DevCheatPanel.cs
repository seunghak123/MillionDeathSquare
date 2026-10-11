#if UNITY_EDITOR || TOSS_DEV || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using Seunghak.Manager;
using Seunghak.UIManager;

namespace Seunghak.Common
{
    /// <summary>
    /// dev 서버(TOSS_DEV) / 에디터 / Development Build 전용 치트 패널.
    /// 화면 좌상단 "CHEAT" 버튼으로 열고, 명령어 입력 또는 빠른 버튼으로 실행한다.
    /// prod 빌드에는 심볼이 없어 코드 자체가 컴파일되지 않는다.
    ///
    /// UI 오브젝트를 런타임 생성하지 않도록 IMGUI(OnGUI)로만 그린다.
    /// </summary>
    public class DevCheatPanel : MonoBehaviour
    {
        // 인벤토리 보유 아이템 id (Assets/BaseSources/Data/ItemData_*.asset 기준)
        private static readonly int[] INVENTORY_ITEM_IDS = { 10, 11, 12, 13 };

        private const float REF_HEIGHT = 720f;
        private const int MAX_LOG = 8;

        private bool _open;
        private string _input = string.Empty;
        private readonly List<string> _log = new List<string>();
        private Vector2 _scroll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[DevCheatPanel]");
            DontDestroyOnLoad(go);
            go.AddComponent<DevCheatPanel>();
        }

        private void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.height / REF_HEIGHT);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;

            if (GUI.Button(new Rect(4, 4, 64, 28), _open ? "닫기" : "CHEAT"))
                _open = !_open;
            if (!_open) return;

            float panelW = Mathf.Min(380f, w - 8f);
            GUILayout.BeginArea(new Rect(4, 36, panelW, 420), GUI.skin.box);

            GUILayout.BeginHorizontal();
            // TextField가 Enter 이벤트를 소비하기 전에 먼저 확인한다.
            bool submit = Event.current.type == EventType.KeyDown
                          && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                          && GUI.GetNameOfFocusedControl() == "CheatInput";
            GUI.SetNextControlName("CheatInput");
            _input = GUILayout.TextField(_input, GUILayout.Height(28));
            if (GUILayout.Button("실행", GUILayout.Width(56), GUILayout.Height(28)) || submit)
            {
                Run(_input);
                _input = string.Empty;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("골드 +1000")) Run("gold 1000");
            if (GUILayout.Button("골드 +10000")) Run("gold 10000");
            if (GUILayout.Button("아이템 +10")) Run("items 10");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("캐릭터 전부")) Run("chars");
            if (GUILayout.Button("최고점 0")) Run("best 0");
            if (GUILayout.Button("전체 초기화")) Run("reset");
            GUILayout.EndHorizontal();
            if (GUILayout.Button("도움말")) Run("help");

            _scroll = GUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _log.Count; i++)
                GUILayout.Label(_log[i]);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void Run(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            string[] args = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            string result;
            try
            {
                result = Execute(args[0].ToLowerInvariant(), args);
            }
            catch (Exception e)
            {
                result = $"오류: {e.Message}";
            }
            Log($"> {line}\n{result}");
        }

        private string Execute(string cmd, string[] args)
        {
            var user = UserDataManager.Instance;
            switch (cmd)
            {
                case "gold":
                {
                    int amount = ArgInt(args, 1, 1000);
                    if (amount > 0) user.AddGold(amount);
                    else user.SpendGold(Mathf.Min(-amount, (int)user.GetGold()));
                    RefreshLobby();
                    return $"골드: {user.GetGold()}";
                }
                case "item":
                {
                    int id = ArgInt(args, 1, -1);
                    if (id < 0) return "사용법: item <id> [개수]";
                    int count = ArgInt(args, 2, 10);
                    user.GetUserItemData().AddItem(id, count);
                    user.SaveUserData();
                    RefreshLobby();
                    return $"아이템 {id}: {user.GetUserItemData().GetItemCount(id)}개";
                }
                case "items":
                {
                    int count = ArgInt(args, 1, 10);
                    var items = user.GetUserItemData();
                    foreach (int id in INVENTORY_ITEM_IDS)
                        items.AddItem(id, count);
                    user.SaveUserData();
                    RefreshLobby();
                    return $"보유 아이템 전부 +{count}";
                }
                case "chars":
                {
                    foreach (E_CHARACTER_ID id in Enum.GetValues(typeof(E_CHARACTER_ID)))
                        if (id != E_CHARACTER_ID.NONE && !CharacterManager.Instance.IsOwned(id))
                            CharacterManager.Instance.AddOwnedCharacter(id);
                    RefreshLobby();
                    return "모든 캐릭터 해금";
                }
                case "best":
                {
                    int score = ArgInt(args, 1, 0);
                    CommonUtil.SavePlayerPref(PlayerPrefKey.BestScore, score);
                    RefreshLobby();
                    return $"최고점: {score}";
                }
                case "reset":
                    user.ResetAllData();
                    CharacterManager.Instance?.ResetOwnedCharacters();
                    RefreshLobby();
                    return "전체 데이터 초기화";
                case "help":
                    return "gold <n> : 골드 +n (음수=차감)\n"
                         + "item <id> [n] : 아이템 id에 n개 (10~13)\n"
                         + "items [n] : 보유 아이템 전부 +n\n"
                         + "chars : 모든 캐릭터 해금\n"
                         + "best <n> : 최고점 설정\n"
                         + "reset : 전체 데이터 초기화";
                default:
                    return "알 수 없는 명령. help 입력";
            }
        }

        private static int ArgInt(string[] args, int index, int fallback)
        {
            return args.Length > index && int.TryParse(args[index], out int v) ? v : fallback;
        }

        // 로비가 열려 있으면 골드/아이템/캐릭터 표시를 즉시 갱신.
        private static void RefreshLobby()
        {
            var lobby = FindFirstObjectByType<GameStartWindow>();
            if (lobby != null && lobby.isActiveAndEnabled) lobby.RestoreWindow();
        }

        private void Log(string msg)
        {
            _log.Insert(0, msg);
            if (_log.Count > MAX_LOG) _log.RemoveAt(_log.Count - 1);
            _scroll = Vector2.zero;
            Debug.Log($"[DevCheatPanel] {msg}");
        }
    }
}
#endif
