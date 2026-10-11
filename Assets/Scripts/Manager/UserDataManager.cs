using Seunghak.LoginSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Seunghak.Manager;
using Seunghak.UIManager;

namespace Seunghak.Common
{
    public enum PlayerPrefKey
    {
        SaveTest,
        UserData,
        UserToken,


        /// <summary>
        /// Unity Editor일때 저장하는타입 용
        /// </summary>
        UserInfoData = 3000,
        UserOptionData,
        UserItemDatas,
        UserStoryDatas,
        UserLobbyDatas,

        /// <summary>최고점수 (로컬 캐시)</summary>
        BestScore = 4000,
        /// <summary>보유 Gold (로컬 캐시)</summary>
        Gold = 4001,
        /// <summary>총 플레이 횟수</summary>
        PlayCount = 4002,
    }
    public class UserDataManager : UnitySingleton<UserDataManager>
    {
        private UserDataInfo userDataInfo = new UserDataInfo();
        private LoginInterface userLogin;
        private BaseDataBase userDataBase;
        private string userIDToken = "";
        private bool _goldLoaded;
        public string UserIDToken
        {
            get
            {
                return userIDToken;
            }
        }
        private string userNickName = "";
        public string UserNickName
        {
            get
            {
                return userNickName;
            }
        }

        // LoadUserData()가 정의만 되어있고 어디서도 호출되지 않아서, 골드/아이템 등을 저장은
        // 해도 앱을 재실행하면 항상 새 UserDataInfo()로 시작하는 버그가 있었다. 여기서 최초
        // .Instance 접근 시 1회 로드되게 한다. SetDataBaseInfo()를 먼저 호출해 userDataBase가
        // 아직 SetLoginInfo() 전이라 null인 상태로 LoadDB()가 호출되는 것도 막는다.
        //
        // LoadGold()도 여기서 함께 호출한다 — 예전엔 인게임 GameManager 초기화에서만
        // 호출됐는데, 그건 인게임 씬 진입 시점이라 로비(GameStartWindow)의 GoldDisplayUI가
        // 그보다 먼저 GetGold()를 읽으면(앱 실행 후 게임을 한 번도 안 들어간 상태) LoadGold()가
        // 아직 실행 전이라 오래된/빈 골드 값이 표시되는 버그가 있었다.
        protected override void InitSingleton()
        {
            SetDataBaseInfo();
            LoadUserData();
            LoadGold();
        }

        /// <summary>
        /// PlayerPrefs에서 유저 데이터 / 골드를 다시 읽습니다.
        /// 플랫폼 저장소 스냅샷을 PlayerPrefs에 복원한 직후 호출됩니다
        /// (InitSingleton이 복원 전의 값으로 이미 한 번 로드해 둔 상태를 덮어씀).
        /// </summary>
        public void ReloadFromLocalCache()
        {
            long prevGold = GetGold();
            LoadUserData();
            _goldLoaded = false;
            LoadGold();
            if (prevGold != GetGold())
                OnGoldChanged?.Invoke(prevGold, GetGold());
        }

        public void SetLoginInfo(E_LOGIN_TYPE loginType)
        {
            switch (loginType)
            {
                case E_LOGIN_TYPE.GUEST_LOGIN:
                    Debug.Log("Init GuestLogin");
                    userLogin = new GuestLogin();
                    break;
                case E_LOGIN_TYPE.APPS_IN_TOSS_LOGIN:
                    userLogin = new LoginSystem.AppsInTossLogin();
                    break;
                case E_LOGIN_TYPE.FACEBOOK_INSTANT_LOGIN:
                    userLogin = new LoginSystem.FacebookInstantLogin();
                    break;
            }
            userDataInfo.userInfoData.userLoginType = loginType;
            SetDataBaseInfo();
            userLogin.InitLogin();
        }
        public void SetDataBaseInfo()
        {
#if UNITY_EDITOR
            userDataBase = new LocalDataBase();
#else
            userDataBase = new LocalDataBase();
#endif
        }
        public void LoginPlatform(Action<string,bool> successResultAction)
        {
            userLogin.PlatformLogin(successResultAction);
        }

        /// <summary>
        /// 현재 로그인 플랫폼의 LoginInterface 인스턴스 반환
        /// </summary>
        public LoginInterface GetLoginPlatform()
        {
            return userLogin;
        }

        /// <summary>
        /// 결제/광고 기능이 지원되는 플랫폼인지 확인
        /// </summary>
        public bool IsPlatformFeatureSupported()
        {
            return userLogin != null && userLogin.IsAdSupported();
        }
        public int GetUserItemCount(int id)
        {
            return 0;
        }
        public void SetUserNickName(string userPlatformNickName)
        {
            userNickName = userPlatformNickName;
        }
        public void SetUserToken(string userToken)
        {
            userIDToken = userToken;

            userDataBase.InitDB();
        }
        public bool AutoLogin()
        {
            userIDToken = CommonUtil.GetPlayerPref<string>(PlayerPrefKey.UserToken);
            if(string.IsNullOrEmpty(userIDToken))
            {
                return false;
            }
            //userIDToken 으로 서버쪽에 로그인 메소드 보내고

            return true;
        }
        #region 점수
        /// <summary>로컬에 저장된 최고점수를 반환합니다.</summary>
        public int GetBestScore()
        {
            return CommonUtil.GetPlayerPref<int>(PlayerPrefKey.BestScore);
        }

        /// <summary>최고점수를 갱신합니다. 현재 저장값보다 클 때만 저장합니다.</summary>
        public void UpdateBestScore(int score)
        {
            int saved = GetBestScore();
            if (score > saved)
                CommonUtil.SavePlayerPref(PlayerPrefKey.BestScore, score);
        }
        #endregion

        #region 플레이 횟수
        /// <summary>총 플레이 횟수를 반환합니다.</summary>
        public int GetPlayCount()
        {
            return CommonUtil.GetPlayerPref<int>(PlayerPrefKey.PlayCount);
        }

        /// <summary>플레이 횟수를 1 증가시킵니다. 게임 시작 시 호출.</summary>
        public void IncrementPlayCount()
        {
            int count = GetPlayCount() + 1;
            CommonUtil.SavePlayerPref(PlayerPrefKey.PlayCount, count);
        }
        #endregion

        #region Gold (인게임 재화)

        /// <summary>Gold가 변경될 때 발생. (이전값, 새값)</summary>
        public event System.Action<long, long> OnGoldChanged;

        /// <summary>현재 보유 Gold 반환.</summary>
        public long GetGold()
        {
            return userDataInfo.userItemDatas.GetItemCount(E_ITEM_TYPE.GOLD);
        }

        /// <summary>Gold 추가. amount는 양수여야 합니다.</summary>
        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            long prev = GetGold();
            userDataInfo.userItemDatas.AddItem((int)E_ITEM_TYPE.GOLD, amount);
            CommonUtil.SavePlayerPref(PlayerPrefKey.Gold, (int)GetGold());
            OnGoldChanged?.Invoke(prev, GetGold());
        }

        /// <summary>Gold 차감. 잔액 부족 시 false 반환.</summary>
        public bool SpendGold(int amount)
        {
            if (amount <= 0) return false;
            if (GetGold() < amount) return false;
            long prev = GetGold();
            userDataInfo.userItemDatas.AddItem((int)E_ITEM_TYPE.GOLD, -amount);
            CommonUtil.SavePlayerPref(PlayerPrefKey.Gold, (int)GetGold());
            OnGoldChanged?.Invoke(prev, GetGold());
            return true;
        }

        /// <summary>PlayerPrefs에서 Gold 로드 (게임 시작 시 1회 호출).</summary>
        public void LoadGold()
        {
            if (_goldLoaded) return;
            _goldLoaded = true;
            // Gold 키는 AddGold/SpendGold가 골드가 바뀔 때마다 확실하게 갱신하는 전용 키라,
            // UserItemDatas 전체 스냅샷(SaveUserData가 매번 호출되진 않아 오래된 골드 값을
            // 담고 있을 수 있음)보다 신뢰도가 높다. AddItem으로 더하면 LoadUserData()가 이미
            // 복원해둔 골드 위에 또 얹어져 중복 적립되므로, 덮어써서 이 값을 최종 진실로 삼는다.
            //
            // 단, 키가 아직 없는 상태(이 전용 키가 생기기 전에 저장된 데이터, 또는 골드를 한 번도
            // 얻지 않은 신규 유저)에서 GetPlayerPref는 0을 돌려준다. 그 0으로 덮어쓰면 LoadUserData가
            // 복원해둔 보유 골드가 날아가 "게임 시작 시 0골드"로 보이므로, 키가 있을 때만 덮어쓰고
            // 없으면 복원된 값을 그대로 쓰면서 그 값으로 키를 채워둔다.
            string goldKey = PlayerPrefKey.Gold.ToString();
            if (PlayerPrefs.HasKey(goldKey))
            {
                int saved = CommonUtil.GetPlayerPref<int>(PlayerPrefKey.Gold);
                userDataInfo.userItemDatas.itemDic[(int)E_ITEM_TYPE.GOLD] = saved;
            }
            else
            {
                CommonUtil.SavePlayerPref(PlayerPrefKey.Gold, (int)GetGold());
            }
        }
        #endregion

        #region 개발자용 초기화 (치트)
#if UNITY_EDITOR
        /// <summary>디버그용으로 미리 채워둘 골드 양.</summary>
        private const int DEBUG_FILL_GOLD = 500;
        /// <summary>디버그용으로 미리 채워둘 아이템 종류별 개수.</summary>
        private const int DEBUG_FILL_ITEM_COUNT = 50;

        /// <summary>개발자 전용 디버그 단축키.
        /// Shift + F9  → 전체 데이터 초기화
        /// Shift + F10 → 골드/아이템 디버그 수량 채우기
        /// 릴리즈 빌드에는 포함되지 않도록 UNITY_EDITOR로 감싼다.</summary>
        private void Update()
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift && Input.GetKeyDown(KeyCode.F9))
                ResetAllDataCheat();
            if (shift && Input.GetKeyDown(KeyCode.F10))
                FillDebugResourcesCheat();
        }

        private void ResetAllDataCheat()
        {
            ResetAllData();
            CharacterManager.Instance?.ResetOwnedCharacters();
            RefreshLobbyDisplay();

            Debug.Log("[UserDataManager] 치트 단축키(Shift+F9) 실행: 전체 데이터 초기화");
        }

        private void FillDebugResourcesCheat()
        {
            FillDebugResources();
            RefreshLobbyDisplay();

            Debug.Log("[UserDataManager] 치트 단축키(Shift+F10) 실행: 골드/아이템 디버그 수량 채우기");
        }

        // 현재 로비 화면이 열려있으면 골드/아이템/캐릭터 표시를 즉시 갱신.
        // RestoreWindow()는 GameStartWindow에 이미 public으로 존재하고 내부에서 Refresh()를 호출한다.
        private void RefreshLobbyDisplay()
        {
            var lobby = UnityEngine.Object.FindFirstObjectByType<GameStartWindow>();
            lobby?.RestoreWindow();
        }

        /// <summary>개발자용: 골드를 DEBUG_FILL_GOLD, 보유 아이템을 종류별 DEBUG_FILL_ITEM_COUNT까지 채웁니다.
        /// 이미 목표 수량 이상을 들고 있으면 그대로 둡니다 — 테스트 중 모아둔 재화를 깎지 않기 위함입니다.</summary>
        public void FillDebugResources()
        {
            long prevGold = GetGold();
            //if (prevGold < DEBUG_FILL_GOLD)
            {
                // AddGold로 더하면 목표치를 넘겨버리므로, 목표 수량으로 직접 맞춘다.
                // 골드는 LoadGold()가 Gold 전용 키를 최종 진실로 삼으므로 그 키도 함께 갱신해야 한다.
                userDataInfo.userItemDatas.itemDic[(int)E_ITEM_TYPE.GOLD] += DEBUG_FILL_GOLD;
                CommonUtil.SavePlayerPref(PlayerPrefKey.Gold, DEBUG_FILL_GOLD);
                OnGoldChanged?.Invoke(prevGold, DEBUG_FILL_GOLD);
            }

            foreach (var itemData in LoadInventoryItemDatas())
            {
                if (userDataInfo.userItemDatas.GetItemCount(itemData.itemId) >= DEBUG_FILL_ITEM_COUNT) continue;
                userDataInfo.userItemDatas.itemDic[itemData.itemId] += DEBUG_FILL_ITEM_COUNT;
            }

            // AddItem/itemDic 조작은 메모리만 바꾸므로, 여기서 저장해야 재실행 후에도 남는다.
            SaveUserData();
            PlayerPrefs.Save();

            Debug.Log($"[UserDataManager] 개발자 치트: 골드 {DEBUG_FILL_GOLD} / 아이템 종류별 {DEBUG_FILL_ITEM_COUNT}개로 채움");
        }

        // 보유 개수로 쌓이는 아이템 에셋 목록. 아이템이 추가돼도 여기서 자동으로 잡히도록
        // id를 하드코딩하지 않고 에셋을 훑는다(에디터 전용이라 AssetDatabase 사용 가능).
        // isInventoryItem=false(즉시 재화로 바뀌는 픽업 등)는 제외한다.
        private static List<Seunghak.Data.ShopItemData> LoadInventoryItemDatas()
        {
            var result = new List<Seunghak.Data.ShopItemData>();
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:ShopItemData"))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var data = UnityEditor.AssetDatabase.LoadAssetAtPath<Seunghak.Data.ShopItemData>(path);
                if (data == null || !data.isInventoryItem) continue;
                result.Add(data);
            }
            return result;
        }
#endif

        /// <summary>개발자용: 골드/아이템/최고점/플레이횟수를 초기 상태로 리셋합니다.
        /// 보유 캐릭터/선택 캐릭터는 CharacterManager가 별도로 관리하므로 여기서 건드리지 않습니다.</summary>
        public void ResetAllData()
        {
            long prevGold = GetGold();
            userDataInfo.userItemDatas.itemDic.Clear();
            CommonUtil.SavePlayerPref(PlayerPrefKey.Gold, 0);
            if (prevGold != 0)
                OnGoldChanged?.Invoke(prevGold, 0);

            PlayerPrefs.DeleteKey(PlayerPrefKey.BestScore.ToString());
            PlayerPrefs.DeleteKey(PlayerPrefKey.PlayCount.ToString());

            // 키 삭제도 SaveUserData()의 SavePlayerPref를 거치며 플랫폼 저장소에 반영된다
            SaveUserData();
            PlayerPrefs.Save();

            Debug.Log("[UserDataManager] 개발자 치트: 전체 데이터 초기화 완료 (골드/아이템/최고점/플레이횟수)");
        }
        #endregion
        public UserLobbyInfoData GetUserLobbyInfoData()
        {
            return userDataInfo.userLobbyInfoDatas;
        }
        public UserItemDatas GetUserItemData()
        {
            return userDataInfo.userItemDatas;
        }
        public UserOptionData GetUserOptionData()
        {
            return userDataInfo.userOption;
        }
        public ScenarioStageResultInfo GetScenarioStageResultData(int stageID)
        {
            if (userDataInfo.userStoryDatas.scenarioStageResults.TryGetValue(stageID, out ScenarioStageResultInfo data))
                return data;

            return null;
        }
        public bool IsScenarioStageResult(int stageID)
        {
            if (stageID == 0)
                return true;

            return userDataInfo.userStoryDatas.scenarioStageResults.ContainsKey(stageID);
        }
#region DB_SAVE
        /// <summary>
        /// 해당 내용들은 로컬 DB를 사용할 경우에만 해당한다
        /// 하나는 PlayerPref를 이용 차후엔 확장해서 직접적으로 호출하는게 아닌 
        /// DB매니저를 통해서 어떤 DB를 통하든 특정 인터페이스를 상속받은 DB의 행동을 실행하도록한다.
        /// </summary>
        public void SaveUserData()
        {
            userDataBase.SaveDB(userDataInfo);
        }
        public void LoadUserData()
        {
            userDataInfo = userDataBase.LoadDB();

            // 저장된 키가 아예 없는 최초 실행에서는 LoadDB()가 각 필드에 null을 담아 돌려준다.
            // 이 상태로 두면 게임 시작 시 옵션을 읽는 SoundManager나 설정창의 opt.masterVolume
            // 접근이 널 참조로 죽으므로, 필드 기본값이 채워진 빈 인스턴스로 채워둔다.
            if (userDataInfo == null) userDataInfo = new UserDataInfo();
            if (userDataInfo.userInfoData == null) userDataInfo.userInfoData = new UserInfoData();
            if (userDataInfo.userOption == null) userDataInfo.userOption = new UserOptionData();
            if (userDataInfo.userItemDatas == null) userDataInfo.userItemDatas = new UserItemDatas();
            if (userDataInfo.userStoryDatas == null) userDataInfo.userStoryDatas = new UserStoryDatas();
            if (userDataInfo.userLobbyInfoDatas == null) userDataInfo.userLobbyInfoDatas = new UserLobbyInfoData();
        }
#endregion DB_SAVE 
    }
    [Serializable]
    public class UserItemDatas
    {
        // JsonUtility는 순수 Dictionary를 직렬화하지 못한다 — SerializableDictionary(내부적으로
        // key/value 리스트로 풀어서 저장)를 써야 골드/아이템 보유량이 실제로 저장/복원된다.
        public SerializableDictionary<int, long> itemDic = new SerializableDictionary<int, long>();
        public bool AddItem(int id, long itemCount)
        {
            if ((int)E_ITEM_TYPE.CRYSTALS == id || (int)E_ITEM_TYPE.VALID_CRYSTALS == id)
            {
                long useritemCount = itemDic[(int)E_ITEM_TYPE.CRYSTALS] + itemDic[(int)E_ITEM_TYPE.VALID_CRYSTALS];

                if (itemCount > 0)
                {
                    itemDic[id] += itemCount;
                    return true;
                }
                else
                {
                    long remainCount = 0;
                    if (useritemCount + itemCount > 0)
                    {
                        itemDic[(int)E_ITEM_TYPE.VALID_CRYSTALS] -= itemCount;
                        if (itemDic[(int)E_ITEM_TYPE.VALID_CRYSTALS] < 0)
                        {
                            remainCount = itemDic[(int)E_ITEM_TYPE.VALID_CRYSTALS];
                            itemDic[(int)E_ITEM_TYPE.VALID_CRYSTALS] = 0;
                        }
                        itemDic[(int)E_ITEM_TYPE.CRYSTALS] -= remainCount;

                        return true;
                    }
                    return false;
                }

            }
            else
            {
                if (!itemDic.ContainsKey(id))
                {
                    itemDic[id] = 0;
                }
                if (itemDic[id] + itemCount >= 0)
                {
                    itemDic[id] += itemCount;
                    return true;
                }
                return false;
            }
        }
        public long GetItemCount(E_ITEM_TYPE itemType)
        {
            int id = (int)itemType;
            return GetItemCount(id);
        }
        public long GetItemCount(int id)
        {
            if (!itemDic.ContainsKey(id))
            {
                itemDic[id] = 0;
            }

            if ((int)E_ITEM_TYPE.CRYSTALS == id || (int)E_ITEM_TYPE.VALID_CRYSTALS == id)
            {
                if (!itemDic.ContainsKey((int)E_ITEM_TYPE.CRYSTALS))
                {
                    itemDic[(int)E_ITEM_TYPE.CRYSTALS] = 0;
                }

                if (!itemDic.ContainsKey((int)E_ITEM_TYPE.VALID_CRYSTALS))
                {
                    itemDic[(int)E_ITEM_TYPE.VALID_CRYSTALS] = 0;
                }

                return itemDic[(int)E_ITEM_TYPE.CRYSTALS] + itemDic[(int)E_ITEM_TYPE.VALID_CRYSTALS];
            }

            return itemDic[id];
        }
    }
    /// <summary>
    /// 유저 플랫폼, 로그인, 
    /// </summary>
    [Serializable]
    public class UserInfoData
    {
        public string userKey = string.Empty;
        public string userCurrentLoginTime = string.Empty;
        public bool isLogined = false;
        public E_LOGIN_TYPE userLoginType = E_LOGIN_TYPE.GUEST_LOGIN;
        public int platformType = 0;
        public string userNickName = string.Empty;
        public string userUserEmail = string.Empty;
    }
    [Serializable]
    public class UserLobbyInfoData
    {
        public int userCharacterCurrentId = 1;
    }
    /// <summary>
    /// 유저 옵션 데이터
    /// </summary>
    [Serializable]
    public class UserOptionData
    {
        //로컬 저장예정
        [Range(0, 100)] public int masterVolume = 100;
        /// <summary>BGM 볼륨</summary>
        [Range(0, 100)] public int soundVolume = 100;
        /// <summary>캐릭터 사운드 볼륨</summary>
        [Range(0, 100)] public int fbxVolume = 100;
        /// <summary>효과음 볼륨</summary>
        [Range(0, 100)] public int sfxVolume = 100;
        public E_LANGUAGE_TYPE userLangType = E_LANGUAGE_TYPE.KOREAN;
        public int MasterVolume { get { return masterVolume; } set { masterVolume = value; } }
        public int SoundVolume { get { return soundVolume; } set { soundVolume = value; } }
        public int FBXVolume { get { return fbxVolume; } set { fbxVolume = value; } }
        public bool IsMute { get; set; } = false;
        public bool IsAdult { get; set; } = false;
        public E_LANGUAGE_TYPE UserLanguageType { get { return userLangType; }set { userLangType = value; } }
    }
    /// <summary>
    /// 유저 스토리 데이터
    /// </summary>
    [Serializable]
    public class ScenarioStageResultInfo
    {
        public int storyId;
        public int maxScore;
    }
    [Serializable]
    public class UserStoryDatas
    {
        public SerializableDictionary<int, ScenarioStageResultInfo> scenarioStageResults = new SerializableDictionary<int, ScenarioStageResultInfo>();
    }
    /// <summary>
    /// 유저 데이터 베이스 저장용 구조
    /// 필요한 경우에는 UserData 클래스 종류를 늘려줄 것
    /// </summary>
    public class UserDataInfo
    {
        public UserInfoData userInfoData = new UserInfoData();
        public UserOptionData userOption = new UserOptionData();
        public UserItemDatas userItemDatas = new UserItemDatas();
        public UserStoryDatas userStoryDatas = new UserStoryDatas();
        public UserLobbyInfoData userLobbyInfoDatas = new UserLobbyInfoData();
    }
}