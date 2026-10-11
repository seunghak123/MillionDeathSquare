using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 상점 골드 구매(인앱결제) 슬롯 1개. 에디터에서 미리 만들어 둔 고정 개수 슬롯을
    /// CharacterShopWindow.iapSlots 배열에 연결해 사용한다. (런타임 생성 금지)
    /// </summary>
    [Serializable]
    public class IapProductSlot
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI priceLabel;

        private string _sku;
        private Action<string> _onClick;

        public Button Button => button;

        /// <summary>슬롯에 상품을 채우고 보이게 한다. 필드가 비어 있으면 해당 요소만 건너뜀.</summary>
        public void Setup(string sku, string displayName, string displayPrice, Action<string> onClick)
        {
            _sku = sku;
            _onClick = onClick;

            if (nameLabel != null) nameLabel.text = displayName;
            if (priceLabel != null) priceLabel.text = displayPrice;

            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
                button.onClick.AddListener(HandleClick);
                button.gameObject.SetActive(true);
                button.interactable = true;
            }
        }

        public void Hide()
        {
            ClearListeners();
            if (button != null) button.gameObject.SetActive(false);
        }

        public void SetInteractable(bool value)
        {
            if (button != null) button.interactable = value;
        }

        public void ClearListeners()
        {
            if (button != null) button.onClick.RemoveListener(HandleClick);
            _onClick = null;
        }

        private void HandleClick() => _onClick?.Invoke(_sku);
    }
}
