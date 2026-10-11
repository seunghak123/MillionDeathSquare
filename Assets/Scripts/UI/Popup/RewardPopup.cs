using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Seunghak.Common;
using Seunghak.Manager;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 보상 지급 팝업.
    /// 일일 보상, 광고 보상, 미션 보상 등 다양한 보상을 표시.
    ///
    /// [에디터 세팅]
    ///   - rewardIconImage : 보상 아이콘 이미지
    ///   - rewardAmountText : 보상 수량 텍스트
    ///   - claimButton : 보상 수령 버튼
    ///   - watchAdButton : 광고 보고 2배 수령 버튼 (없으면 비활성)
    ///   - closeButton : 닫기 버튼
    /// </summary>
    public class RewardPopup : BaseUIPopup
    {
        [Header("보상 표시")]
        [SerializeField] private Image rewardIconImage;
        [SerializeField] private TextMeshProUGUI rewardAmountText;
        [SerializeField] private TextMeshProUGUI rewardTitleText;

        [Header("버튼")]
        [SerializeField] private Button claimButton;
        [SerializeField] private Button watchAdButton;
        [SerializeField] private Button closeButton;

        private int _pendingGold;
        private System.Action _onClaimed;

        // ── 생명주기 ────────────────────────────────────────────────────────

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.RewardPopup;
        }

        public override void EnterWindow()
        {
            RegistEvent();
            base.EnterWindow();
        }

        public override void ExitWindow()
        {
            DeleteRegistedEvent();
            base.ExitWindow();
        }

        public override void RegistEvent()
        {
            claimButton?.onClick.AddListener(OnClickClaim);
            watchAdButton?.onClick.AddListener(OnClickWatchAd);
            closeButton?.onClick.AddListener(OnClickClose);
        }

        public override void DeleteRegistedEvent()
        {
            claimButton?.onClick.RemoveListener(OnClickClaim);
            watchAdButton?.onClick.RemoveListener(OnClickWatchAd);
            closeButton?.onClick.RemoveListener(OnClickClose);
        }

        // ── 데이터 세팅 ─────────────────────────────────────────────────────

        /// <summary>
        /// 보상 팝업을 표시할 데이터를 설정.
        /// UIManager.Instance.PushUI(UI_TYPE.RewardPopup) 전에 호출하거나
        /// EnterWindow 오버라이드 전에 호출.
        /// </summary>
        public void SetReward(int goldAmount, string title = "보상 획득!", System.Action onClaimed = null)
        {
            _pendingGold = goldAmount;
            _onClaimed = onClaimed;

            if (rewardTitleText != null)
                rewardTitleText.text = title;

            if (rewardAmountText != null)
                rewardAmountText.text = $"Gold +{goldAmount}";

            RefreshAdButton();
        }

        private void RefreshAdButton()
        {
            if (watchAdButton == null) return;
            bool adAvailable = AdManager.Instance != null && AdManager.Instance.IsAdSupported();
            watchAdButton.gameObject.SetActive(adAvailable);
        }

        // ── 버튼 핸들러 ─────────────────────────────────────────────────────

        private void OnClickClaim()
        {
            GrantReward(_pendingGold);
            ExitWindow();
        }

        private void OnClickWatchAd()
        {
            WatchAdForDoubleRewardAsync().Forget();
        }

        private void OnClickClose()
        {
            ExitWindow();
        }

        private void GrantReward(int amount)
        {
            if (amount > 0 && UserDataManager.Instance != null)
            {
                UserDataManager.Instance.AddGold(amount);
                Debug.Log($"[RewardPopup] Gold +{amount} 지급 완료");
            }
            _onClaimed?.Invoke();
        }

        private async Cysharp.Threading.Tasks.UniTaskVoid WatchAdForDoubleRewardAsync()
        {
            if (watchAdButton != null) watchAdButton.interactable = false;

            bool rewarded = await AdManager.Instance.ShowRewardedAdAsync();

            if (rewarded)
            {
                GrantReward(_pendingGold * 2);
                ExitWindow();
            }
            else
            {
                if (watchAdButton != null) watchAdButton.interactable = true;
            }
        }
    }
}
