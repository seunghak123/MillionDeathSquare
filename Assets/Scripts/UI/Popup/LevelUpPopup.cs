using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Seunghak.Game.Suika3D;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 레벨업 스킬 선택 팝업 (기획서 8.1, 11장 화면 3).
    ///  - 랜덤 스킬 3개 중 1개 선택 (선택 중에는 게임/타이머 정지 — SuikaGameManager가 처리)
    ///  - 광고 보고 다시 뽑기 (레벨업당 1회)
    ///  - 보유 칸(최대 4)이 가득 차면 버릴 스킬을 고르거나 새 스킬을 포기
    /// 프리팹: Tools/Fossil Merge/6. 레벨업 팝업 프리팹 생성
    /// 버튼은 프리팹 OnClick이 아니라 여기서 코드로 연결한다 (Remove 후 Add로 중복 방지).
    /// </summary>
    public class LevelUpPopup : BaseUIPopup
    {
        [Header("선택지 (3개)")]
        [SerializeField] private Button[] optionButtons = new Button[3];
        [SerializeField] private TextMeshProUGUI[] optionNames = new TextMeshProUGUI[3];
        [SerializeField] private TextMeshProUGUI[] optionDescs = new TextMeshProUGUI[3];

        [Header("다시 뽑기 (광고)")]
        [SerializeField] private Button rerollButton;

        [Header("보유 칸이 가득 찼을 때")]
        [SerializeField] private GameObject replaceRoot;
        [SerializeField] private TextMeshProUGUI replaceTitle;
        [SerializeField] private Button[] replaceSlotButtons = new Button[4];
        [SerializeField] private TextMeshProUGUI[] replaceSlotLabels = new TextMeshProUGUI[4];
        [SerializeField] private Button discardButton;

        private SuikaSkillSystem _skills;
        private bool _busy;
        private bool _closing;

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.LevelUpPopup;
        }

        public override void EnterWindow()
        {
            base.EnterWindow();
            SuikaAnalytics.Screen("level_up");
            _closing = false;
            _skills = SuikaGameManager.Instance != null ? SuikaGameManager.Instance.Skills : null;
            RegistEvent();
            Refresh();
        }

        public override void ExitWindow()
        {
            DeleteRegistedEvent();
            base.ExitWindow();
        }

        public override void RegistEvent()
        {
            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (optionButtons[i] == null) continue;
                int idx = i;
                optionButtons[i].onClick.RemoveAllListeners();
                optionButtons[i].onClick.AddListener(() => _skills?.ChooseOffer(idx));
            }
            for (int i = 0; i < replaceSlotButtons.Length; i++)
            {
                if (replaceSlotButtons[i] == null) continue;
                int idx = i;
                replaceSlotButtons[i].onClick.RemoveAllListeners();
                replaceSlotButtons[i].onClick.AddListener(() => _skills?.ReplaceHeld(idx));
            }
            if (rerollButton != null)
            {
                rerollButton.onClick.RemoveAllListeners();
                rerollButton.onClick.AddListener(OnClickReroll);
            }
            if (discardButton != null)
            {
                discardButton.onClick.RemoveAllListeners();
                discardButton.onClick.AddListener(() => _skills?.DiscardOverflow());
            }

            if (_skills != null)
            {
                _skills.OnOfferChanged += Refresh;
                _skills.OnNeedReplace += OnNeedReplace;
                _skills.OnOfferClosed += OnOfferClosed;
            }
        }

        public override void DeleteRegistedEvent()
        {
            if (_skills == null) return;
            _skills.OnOfferChanged -= Refresh;
            _skills.OnNeedReplace -= OnNeedReplace;
            _skills.OnOfferClosed -= OnOfferClosed;
        }

        private void Refresh()
        {
            if (_skills == null) return;
            for (int i = 0; i < optionButtons.Length; i++)
            {
                E_FOSSIL_SKILL s = _skills.GetOffer(i);
                if (i < optionNames.Length && optionNames[i] != null) optionNames[i].text = SuikaSkillInfo.Name(s);
                if (i < optionDescs.Length && optionDescs[i] != null) optionDescs[i].text = SuikaSkillInfo.Description(s);
                if (optionButtons[i] != null) optionButtons[i].interactable = _skills.Overflow == E_FOSSIL_SKILL.NONE;
            }

            if (rerollButton != null)
                rerollButton.gameObject.SetActive(_skills.CanReroll && SuikaPlatformBridge.IsAdSupported);

            bool replacing = _skills.Overflow != E_FOSSIL_SKILL.NONE;
            if (replaceRoot != null) replaceRoot.SetActive(replacing);
            if (replacing)
            {
                if (replaceTitle != null)
                    replaceTitle.text = $"스킬 칸이 가득 찼어요. 버릴 스킬을 고르세요 (새 스킬: {SuikaSkillInfo.Name(_skills.Overflow)})";
                for (int i = 0; i < replaceSlotButtons.Length; i++)
                {
                    bool has = i < _skills.HeldCount;
                    if (replaceSlotButtons[i] != null) replaceSlotButtons[i].gameObject.SetActive(has);
                    if (has && i < replaceSlotLabels.Length && replaceSlotLabels[i] != null)
                        replaceSlotLabels[i].text = SuikaSkillInfo.Name(_skills.GetHeld(i));
                }
            }
        }

        private void OnNeedReplace(E_FOSSIL_SKILL incoming) => Refresh();

        private void OnOfferClosed()
        {
            if (_closing) return;
            _closing = true;
            ExitWindow();
        }

        private void OnClickReroll()
        {
            if (_busy) return;
            RerollAsync().Forget();
        }

        private async UniTaskVoid RerollAsync()
        {
            _busy = true;
            if (rerollButton != null) rerollButton.interactable = false;
            try
            {
                await SuikaPlatformBridge.RerollSkillsWithAdAsync();
            }
            finally
            {
                _busy = false;
                if (this != null)
                {
                    if (rerollButton != null) rerollButton.interactable = true;
                    Refresh();
                }
            }
        }
    }
}
