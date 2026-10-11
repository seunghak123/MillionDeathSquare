using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Seunghak.Data;
using Seunghak.Manager;
using Seunghak.Platform;
using Seunghak.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 상점 UI (기획서 슬라이드7~9).
    ///
    /// 구성:
    ///   1. 재화 표시 (상단 고정)
    ///   2. 캐릭터 구매/선택 (캐릭터 카드 그리드)
    ///   3. 아이템 구매 (3종 아이템, BUY + AD 버튼)
    ///   4. 일일 선물 (1~50C, 하루 1회)
    ///   5. 광고 보상 (10~100C, 광고 시청 후)
    /// </summary>
    public class CharacterShopWindow : BaseUIWindow
    {
        [Header("골드 표시")]
        [SerializeField] private GoldDisplayUI goldDisplay;

        [Header("닫기")]
        [SerializeField] private Button closeButton;

        [Header("설정")]
        [SerializeField] private Button settingsButton;

        [Header("캐릭터 카드 (CHICK, CAT, DOG, SQUIRREL, RABBIT 순서 — 슬롯 수가 5개 미만이면 뒤쪽 캐릭터는 표시되지 않음)")]
        [SerializeField] private List<CharacterCardUI> characterCards = new List<CharacterCardUI>();

        [Header("아이템 구매 (3종)")]
        [SerializeField] private List<ItemCardUI> itemCards = new List<ItemCardUI>();

        [Header("일일 선물")]
        [SerializeField] private Button dailyGiftButton;
        [SerializeField] private TextMeshProUGUI dailyGiftLabel;
        [SerializeField] private GameObject dailyGiftAvailableIcon;

        [Header("광고 보상")]
        [SerializeField] private Button adRewardButton;
        [SerializeField] private TextMeshProUGUI adRewardLabel;
        [SerializeField] private GameObject adRewardAvailableIcon;

        [Header("골드 구매 (인앱결제) — 미연결이면 건너뜀. 슬롯 수만큼 상품 표시")]
        [SerializeField] private GameObject iapSection;
        [SerializeField] private IapProductSlot[] iapSlots = new IapProductSlot[0];

        // 구매 진행 중 재진입 방지 (지급은 TossIAP가 직접 처리하므로 여기서 골드를 지급하지 않는다)
        private bool _iapBusy;
        // 상품 목록 로딩이 늦게 끝나도 최신 요청만 반영하기 위한 세대 번호
        private int _iapRefreshGen;

        // 일일 선물/광고 보상 키
        private const string DAILY_GIFT_KEY = "DailyGiftDate";
        private const string AD_REWARD_KEY = "AdRewardTime";
        private const double AD_REWARD_COOLDOWN_HOURS = 1.0;

        // 광고 보상 대기 카운트다운 갱신 주기 (초 단위 표시라 매 프레임 갱신할 필요 없음)
        private const float AD_REWARD_TICK_INTERVAL = 1f;
        private float _adRewardTickTimer;

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.CharacterShopWindow;
            CharacterManager.Instance.PreloadAllCharacterData();
        }

        public override void EnterWindow()
        {
            RegistEvent();
            base.EnterWindow();
            RefreshAll();
            RefreshIapSectionAsync().Forget();
        }

        public override void RestoreWindow()
        {
            base.RestoreWindow();
            RefreshAll();
        }

        public override void ExitWindow()
        {
            DeleteRegistedEvent();
            UIManager.Instance.PopUI();
        }

        // closeButton/settingsButton/dailyGiftButton/adRewardButton은 프리팹 Inspector에서
        // OnClick이 이미 연결되어 있음 (여기서 AddListener까지 하면 클릭당 2번 호출됨).
        // 캐릭터/아이템 카드는 슬롯 재사용 방식이라 데이터별로 런타임에 계속 다시 묶어줘야 하므로
        // 카드 내부 리스너는 그대로 코드에서 관리한다.
        public override void RegistEvent()
        {
        }

        public override void DeleteRegistedEvent()
        {
            foreach (var card in characterCards)
                card.ClearListeners();
            foreach (var item in itemCards)
                item.ClearListeners();
            if (iapSlots != null)
            {
                foreach (var slot in iapSlots)
                    slot?.ClearListeners();
            }
            _iapRefreshGen++; // 진행 중인 목록 로딩 결과 무효화
        }

        // ── 골드 구매 (인앱결제) ─────────────────────────────────────────

        private async UniTaskVoid RefreshIapSectionAsync()
        {
            if (iapSlots == null || iapSlots.Length == 0)
            {
                if (iapSection != null) iapSection.SetActive(false);
                return;
            }

            int gen = ++_iapRefreshGen;
            var token = this.destroyCancellationToken;
            var registered = TossConfig.Instance.iapProducts;

            // 상품 목록: (sku, 이름, 가격 표시)
            var entries = new List<(string sku, string name, string price)>();

#if UNITY_EDITOR
            // 에디터에서는 IAP가 불가능하지만 UI 확인을 위해 등록 상품만으로 슬롯을 채운다.
            // (TossIAP.PurchaseAsync가 에디터에서 목 지급 처리)
            foreach (var p in registered)
                entries.Add((p.sku, p.sku, $"{p.goldAmount} G"));
#else
            var iap = PlatformFactory.Current.IAP;
            if (iap == null || !iap.IsAvailable())
            {
                if (iapSection != null) iapSection.SetActive(false);
                return;
            }

            var products = await iap.GetProductListAsync();
            // 로딩 중 창이 닫혔거나 더 최신 요청이 있으면 결과 버림
            if (token.IsCancellationRequested || gen != _iapRefreshGen) return;

            if (products != null)
            {
                foreach (var product in products)
                {
                    if (product != null && IsRegisteredSku(registered, product.sku))
                        entries.Add((product.sku, product.displayName, product.displayAmount));
                }
            }
#endif

            if (token.IsCancellationRequested || gen != _iapRefreshGen) return;

            for (int i = 0; i < iapSlots.Length; i++)
            {
                var slot = iapSlots[i];
                if (slot == null) continue;

                if (i < entries.Count)
                    slot.Setup(entries[i].sku, entries[i].name, entries[i].price, OnClickIapProduct);
                else
                    slot.Hide();
            }

            if (iapSection != null) iapSection.SetActive(entries.Count > 0);
        }

        private static bool IsRegisteredSku(TossConfig.IapProduct[] registered, string sku)
        {
            if (registered == null) return false;
            for (int i = 0; i < registered.Length; i++)
            {
                if (registered[i].sku == sku) return true;
            }
            return false;
        }

        private void OnClickIapProduct(string sku)
        {
            if (_iapBusy) return;
            PurchaseIapAsync(sku).Forget();
        }

        private async UniTaskVoid PurchaseIapAsync(string sku)
        {
            _iapBusy = true;
            SetIapSlotsInteractable(false);
            try
            {
                var result = await PlatformFactory.Current.IAP.PurchaseAsync(sku);
                // 골드 지급은 TossIAP가 직접 처리하고 GoldDisplayUI가 OnGoldChanged로 갱신한다.
                if (result != null && result.success)
                    Debug.Log($"[CharacterShop] 골드 구매 완료: sku={sku}, orderId={result.orderId}");
                else
                    Debug.Log($"[CharacterShop] 골드 구매 실패/취소: sku={sku}, error={(result != null ? result.error : "null")}");
            }
            finally
            {
                _iapBusy = false;
                // 대기 중 창이 파괴됐을 수 있으므로 Unity null 체크 후 복구
                if (this != null) SetIapSlotsInteractable(true);
            }
        }

        private void SetIapSlotsInteractable(bool value)
        {
            if (iapSlots == null) return;
            foreach (var slot in iapSlots)
                slot?.SetInteractable(value);
        }

        // ── 갱신 ─────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            RefreshGoldLabel();
            RefreshCards();
            RefreshItems();
            RefreshDailyGift();
            RefreshAdReward();
        }

        private void RefreshGoldLabel() { /* GoldDisplayUI가 자동 갱신 */ }

        private void RefreshCards()
        {
            E_CHARACTER_ID[] ids = { E_CHARACTER_ID.CHICK, E_CHARACTER_ID.CAT, E_CHARACTER_ID.DOG, E_CHARACTER_ID.SQUIRREL, E_CHARACTER_ID.RABBIT, E_CHARACTER_ID.DRAGON };

            for (int i = 0; i < characterCards.Count && i < ids.Length; i++)
            {
                E_CHARACTER_ID id = ids[i];
                CharacterData data = CharacterManager.Instance.GetCharacterData(id);
                if (data == null) continue;

                bool owned = CharacterManager.Instance.IsOwned(id);
                bool selected = CharacterManager.Instance.SelectedCharacterId == id;
                characterCards[i].Setup(data, owned, selected, OnClickBuy, OnClickSelect);
            }
        }

        private void RefreshItems()
        {
            for (int i = 0; i < itemCards.Count; i++)
            {
                itemCards[i].Refresh(OnClickBuyItem, OnClickAdItem);
            }
        }

        private void RefreshDailyGift()
        {
            bool available = IsDailyGiftAvailable();
            if (dailyGiftButton != null)
                dailyGiftButton.interactable = available;
            if (dailyGiftAvailableIcon != null)
                dailyGiftAvailableIcon.SetActive(available);
            if (dailyGiftLabel != null)
                dailyGiftLabel.text = available ? "1 ~ 50 C" : "수령 완료";
        }

        private void RefreshAdReward()
        {
            bool available = IsAdRewardAvailable();
            if (adRewardButton != null)
                adRewardButton.interactable = available;
            if (adRewardAvailableIcon != null)
                adRewardAvailableIcon.SetActive(available);
            if (adRewardLabel != null)
                adRewardLabel.text = available ? "10 ~ 100 C" : FormatAdRewardRemaining();
        }

        // 화면이 열려있는 동안 1초마다 광고 보상 남은 시간 텍스트를 갱신하고,
        // 쿨다운이 끝나면 자동으로 버튼을 다시 활성화한다.
        private void Update()
        {
            _adRewardTickTimer += Time.unscaledDeltaTime;
            if (_adRewardTickTimer < AD_REWARD_TICK_INTERVAL) return;
            _adRewardTickTimer = 0f;

            if (adRewardButton != null && !adRewardButton.interactable && IsAdRewardAvailable())
            {
                // 대기 시간이 다 지남 — "10 ~ 100 C"로 자동 복귀
                RefreshAdReward();
            }
            else if (adRewardLabel != null && adRewardButton != null && !adRewardButton.interactable)
            {
                adRewardLabel.text = FormatAdRewardRemaining();
            }
        }

        /// <summary>광고 보상 재시청까지 남은 시간을 "MM:SS 후 가능" 형식으로 반환. 이미 가능하면 "대기 중"(방어용).</summary>
        private string FormatAdRewardRemaining()
        {
            string lastTime = PlayerPrefs.GetString(AD_REWARD_KEY, "");
            if (string.IsNullOrEmpty(lastTime) || !DateTime.TryParse(lastTime, out DateTime last))
                return "대기 중";

            TimeSpan remaining = last.AddHours(AD_REWARD_COOLDOWN_HOURS) - DateTime.Now;
            if (remaining.TotalSeconds <= 0) return "대기 중";

            return $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2} 후 가능";
        }

        // ── 일일 선물 / 광고 보상 체크 ──────────────────────────────────

        private bool IsDailyGiftAvailable()
        {
            string lastDate = PlayerPrefs.GetString(DAILY_GIFT_KEY, "");
            string today = DateTime.Now.ToString("yyyyMMdd");
            return lastDate != today;
        }

        // 시간제한을 풀어서 항상 가능하게 함 (요청에 따라 쿨다운 비활성화 — 다시 걸고 싶으면
        // 아래 한 줄만 원래대로 되돌리면 됨: 나머지 카운트다운 표시 로직은 그대로 살아있음).
        private bool IsAdRewardAvailable() => true;

        // ── 캐릭터 버튼 핸들러 ──────────────────────────────────────────

        public void OnClickClose()
        {
            ExitWindow();
        }

        public void OnClickSettings()
        {
            UIManager.Instance.PushUI(UI_TYPE.SettingPopup);
        }

        private void OnClickBuy(E_CHARACTER_ID id)
        {
            CharacterData data = CharacterManager.Instance.GetCharacterData(id);
            if (data == null) return;

            if (data.priceType == E_ITEM_TYPE.GOLD && data.price > 0)
            {
                if (!UserDataManager.Instance.SpendGold(data.price))
                {
                    Debug.Log($"[CharacterShop] Gold 부족. 필요: {data.price}, 보유: {UserDataManager.Instance.GetGold()}");
                    return;
                }
            }

            CharacterManager.Instance.AddOwnedCharacter(id);
            Debug.Log($"[CharacterShop] 구매 완료: {id}");
            RefreshAll();
        }

        private void OnClickSelect(E_CHARACTER_ID id)
        {
            SelectAsync(id).Forget();
        }

        private async UniTaskVoid SelectAsync(E_CHARACTER_ID id)
        {
            await CharacterManager.Instance.SelectCharacterAsync(id, this.destroyCancellationToken);
            RefreshAll();
        }

        // ── 아이템 버튼 핸들러 ──────────────────────────────────────────

        public void OnClickBuyItem(int itemIndex, int price)
        {
            if (!UserDataManager.Instance.SpendGold(price))
            {
                Debug.Log($"[CharacterShop] Gold 부족. 필요: {price}");
                return;
            }

            // 아이템 추가 (UserDataManager에 아이템 관리 기능 필요)
            UserDataManager.Instance.GetUserItemData().AddItem(itemIndex, 1);
            // AddItem은 메모리만 갱신하고 저장은 안 하므로, 여기서 즉시 저장해야 앱을 완전히
            // 껐다 켜도 구매한 아이템이 남는다.
            UserDataManager.Instance.SaveUserData();
            Debug.Log($"[CharacterShop] 아이템 {itemIndex} 구매 완료");
            RefreshAll();
        }

        // 광고 시청은 응답이 올 때까지 시간이 걸려서(async), 그 사이 연타하면 광고가 중복으로
        // 뜨거나 보상이 여러 번 지급될 수 있다 — 진행 중에는 재요청을 막는다.
        private bool _adItemBusy;

        public void OnClickAdItem(int itemIndex)
        {
            if (_adItemBusy) return;
            WatchAdForItem(itemIndex).Forget();
        }

        private async UniTaskVoid WatchAdForItem(int itemIndex)
        {
            if (AdManager.Instance == null) return;

            _adItemBusy = true;
            try
            {
                bool success = await AdManager.Instance.ShowRewardedAdAsync();
                if (success)
                {
                    UserDataManager.Instance.GetUserItemData().AddItem(itemIndex, 1);
                    UserDataManager.Instance.SaveUserData();
                    Debug.Log($"[CharacterShop] 광고 보상으로 아이템 {itemIndex} 획득");
                    RefreshAll();
                }
            }
            finally
            {
                _adItemBusy = false;
            }
        }

        // ── 일일 선물 핸들러 ────────────────────────────────────────────

        public void OnClickDailyGift()
        {
            if (!IsDailyGiftAvailable()) return;

            int reward = UnityEngine.Random.Range(1, 51); // 1~50 C
            UserDataManager.Instance.AddGold(reward);
            PlayerPrefs.SetString(DAILY_GIFT_KEY, DateTime.Now.ToString("yyyyMMdd"));
            Seunghak.Platform.PlatformSaveSync.MarkDirty();
            PlayerPrefs.Save();

            Debug.Log($"[CharacterShop] 일일 선물: {reward} C 획득!");
            RefreshAll();
        }

        // ── 광고 보상 핸들러 ────────────────────────────────────────────

        // 버튼 클릭 → 팝업이 실제로 화면을 가릴 때까지 몇 프레임 걸릴 수 있어(연타 시 두 번 밀려
        // AdGachaBox가 중복으로 뜨거나 보상이 두 번 지급될 수 있음), 진행 중에는 재요청을 막는다.
        // 팝업의 onClosed(보상 지급 여부와 무관하게 항상 호출됨)에서 풀어준다 — ESC로 취소해도
        // 플래그가 영원히 안 풀리는 걸 방지.
        private bool _adRewardBusy;

        public void OnClickAdReward()
        {
            if (!IsAdRewardAvailable() || _adRewardBusy) return;
            _adRewardBusy = true;
            WatchAdForReward().Forget();
        }

        // 반드시 광고를 끝까지 시청(성공)한 경우에만 상자 연출 → 보상 지급으로 넘어간다.
        // 예전엔 상자 팝업이 광고 없이 바로 떠서 상자만 열면 보상이 지급되는 버그가 있었다.
        private async UniTaskVoid WatchAdForReward()
        {
            if (AdManager.Instance == null)
            {
                Debug.LogWarning("[CharacterShop] AdManager가 없어 광고 보상을 진행할 수 없습니다.");
                _adRewardBusy = false;
                return;
            }

            bool success = await AdManager.Instance.ShowRewardedAdAsync();
            if (!success)
            {
                Debug.Log("[CharacterShop] 광고 미시청/실패 — 보상 없음");
                _adRewardBusy = false;
                return;
            }

            var popup = UIManager.Instance.PushUI(UI_TYPE.AdGachaBox) as AdGachaBox;
            if (popup == null)
            {
                // 팝업을 못 찾으면(빌드 설정 문제 등) 상자 연출 없이 바로 보상.
                Debug.LogWarning("[CharacterShop] AdGachaBox를 찾을 수 없어 상자 연출 없이 바로 보상합니다.");
                GiveAdReward();
                _adRewardBusy = false;
                return;
            }

            // 광고는 이미 봤으므로, 상자를 안 열고 ESC로 닫아도 보상은 한 번 지급한다.
            bool rewarded = false;
            void GrantOnce()
            {
                if (rewarded) return;
                rewarded = true;
                GiveAdReward();
            }
            popup.Open(GrantOnce, onClosed: () =>
            {
                GrantOnce();
                _adRewardBusy = false;
            });
        }

        private void GiveAdReward()
        {
            int reward = UnityEngine.Random.Range(10, 101); // 10~100 C
            UserDataManager.Instance.AddGold(reward);
            PlayerPrefs.SetString(AD_REWARD_KEY, DateTime.Now.ToString("o"));
            Seunghak.Platform.PlatformSaveSync.MarkDirty();
            PlayerPrefs.Save();

            Debug.Log($"[CharacterShop] 광고 보상: {reward} C 획득!");
            RefreshAll();
        }
    }

    // ── 캐릭터 카드 UI 헬퍼 ─────────────────────────────────────────────

    [Serializable]
    public class CharacterCardUI
    {
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI priceLabel;
        [SerializeField] private TextMeshProUGUI statusLabel;  // "보유중" / "선택됨"
        [SerializeField] private Button buyButton;
        [SerializeField] private Button selectButton;

        private E_CHARACTER_ID _characterId;
        private Action<E_CHARACTER_ID> _onBuy;
        private Action<E_CHARACTER_ID> _onSelect;

        public void Setup(CharacterData data, bool owned, bool selected,
                          Action<E_CHARACTER_ID> onBuy, Action<E_CHARACTER_ID> onSelect)
        {
            _characterId = data.characterId;
            _onBuy    = onBuy;
            _onSelect = onSelect;

            ClearListeners();

            if (nameLabel != null)
                nameLabel.text = data.characterName;

            if (priceLabel != null)
            {
                if (data.price <= 0)
                    priceLabel.text = "무료";
                else
                    priceLabel.text = $"{data.price} C";
            }

            if (statusLabel != null)
            {
                if (selected)       statusLabel.text = "선택됨";
                else if (owned)     statusLabel.text = "보유중";
                else                statusLabel.text = "";
            }

            if (buyButton != null)
            {
                bool canBuy = !owned;
                buyButton.gameObject.SetActive(canBuy);
                if (canBuy)
                    buyButton.onClick.AddListener(HandleBuy);
            }

            if (selectButton != null)
            {
                selectButton.gameObject.SetActive(owned);
                if (owned)
                {
                    selectButton.interactable = !selected;
                    selectButton.onClick.AddListener(HandleSelect);
                }
            }
        }

        public void ClearListeners()
        {
            buyButton?.onClick.RemoveAllListeners();
            selectButton?.onClick.RemoveAllListeners();
        }

        private void HandleBuy()    => _onBuy?.Invoke(_characterId);
        private void HandleSelect() => _onSelect?.Invoke(_characterId);
    }

    // ── 아이템 카드 UI 헬퍼 ─────────────────────────────────────────────

    [Serializable]
    public class ItemCardUI
    {
        [Tooltip("가격/itemId 등 카드 데이터의 단일 출처. ShopItemData(ItemData_*.asset)를 직접 참조하므로 " +
                 "프리팹에 가격을 따로 박아두지 않는다 — InGameHUDWindow의 아이템 슬롯과 동일한 에셋을 " +
                 "가리켜야 상점에서 산 아이템이 인게임 슬롯 수량과 일치한다.")]
        [SerializeField] private ShopItemData itemData;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI countLabel;    // "x12"
        [SerializeField] private TextMeshProUGUI priceLabel;    // "50 C"
        [SerializeField] private Button buyButton;
        [SerializeField] private Button adButton;

        private Action<int, int> _onBuy;
        private Action<int> _onAd;

        public void Refresh(Action<int, int> onBuy, Action<int> onAd)
        {
            if (itemData == null) return;

            _onBuy = onBuy;
            _onAd = onAd;

            ClearListeners();

            if (nameLabel != null)
                nameLabel.text = string.IsNullOrEmpty(itemData.displayName) ? itemData.name : itemData.displayName;

            long ownedCount = UserDataManager.Instance.GetUserItemData().GetItemCount(itemData.itemId);

            if (countLabel != null)
                countLabel.text = $"x{ownedCount}";

            if (priceLabel != null)
                priceLabel.text = $"{itemData.price} C";

            if (buyButton != null)
                buyButton.onClick.AddListener(HandleBuy);
            if (adButton != null)
                adButton.onClick.AddListener(HandleAd);
        }

        public void ClearListeners()
        {
            buyButton?.onClick.RemoveAllListeners();
            adButton?.onClick.RemoveAllListeners();
        }

        private void HandleBuy() => _onBuy?.Invoke(itemData.itemId, itemData.price);
        private void HandleAd()  => _onAd?.Invoke(itemData.itemId);
    }
}
