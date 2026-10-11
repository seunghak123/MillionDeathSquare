using UnityEngine;

namespace Seunghak.Data
{
    /// <summary>
    /// 상점/인벤토리 아이템 1종 정의 (게임 장르와 무관한 공용 데이터).
    /// 구 CrossyRoadItemData의 스크립트 GUID를 그대로 이어받아, 기존 ItemData_*.asset과
    /// 상점 카드(CharacterShopWindow.ItemCardUI) 참조가 끊기지 않게 했다.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemData_New", menuName = "Seunghak/Shop Item Data")]
    public class ShopItemData : ScriptableObject
    {
        [Header("인벤토리 ID")]
        [Tooltip("보유 개수 저장에 쓰이는 고유 ID. E_ITEM_TYPE(GOLD=0/CRYSTALS=1/VALID_CRYSTALS=2)과 겹치지 않게 10 이상으로 지정.")]
        public int itemId = 10;

        [Header("표시")]
        public string displayName;
        public Sprite icon;

        [Header("상점 구매 설정")]
        [Tooltip("상점에서 구매할 때 소모되는 재화량")]
        public int price = 50;
        public E_ITEM_TYPE priceType = E_ITEM_TYPE.GOLD;

        [Tooltip("false면 디버그 채우기(Shift+F10) 등 인벤토리 대상에서 제외")]
        public bool isInventoryItem = true;
    }
}
