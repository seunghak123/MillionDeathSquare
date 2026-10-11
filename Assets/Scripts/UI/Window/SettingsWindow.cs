using Seunghak.Common;
using Seunghak.Manager;
using Seunghak.Game.MillionDeathSquare;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 설정 윈도우 (기획서 슬라이드10).
    ///
    /// 오디오 설정:
    ///   - 전체 (masterVolume) → AudioListener.volume
    ///   - 음악 (soundVolume)  → BGM 채널
    ///   - 캐릭터 (fbxVolume)  → 캐릭터 사운드 채널
    ///   - 효과음 (sfxVolume)  → SFX 채널
    /// 기타:
    ///   - 문의하기 버튼
    /// </summary>
    public class SettingsWindow : BaseUIWindow
    {
        [Header("오디오 슬라이더")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider characterSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("오디오 값 텍스트 (선택)")]
        [SerializeField] private TextMeshProUGUI masterValueText;
        [SerializeField] private TextMeshProUGUI bgmValueText;
        [SerializeField] private TextMeshProUGUI characterValueText;
        [SerializeField] private TextMeshProUGUI sfxValueText;

        [Header("버튼")]
        [SerializeField] private Button inquiryButton;
        [SerializeField] private Button closeButton;

        // ── 생명주기 ────────────────────────────────────────────────────────

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.SettingsWindow;
        }

        public override void EnterWindow()
        {
            RegistEvent();
            base.EnterWindow();
            LoadValues();

            // 인게임 중이면 일시정지
            MDSSession.Pause();
        }

        public override void RestoreWindow()
        {
            base.RestoreWindow();
            LoadValues();
        }

        public override void ExitWindow()
        {
            SaveValues();
            DeleteRegistedEvent();

            // 인게임 일시정지 해제
            MDSSession.Resume();

            UIManager.Instance.PopUI();
        }

        public override void RegistEvent()
        {
            masterSlider?.onValueChanged.AddListener(OnMasterChanged);
            bgmSlider?.onValueChanged.AddListener(OnBgmChanged);
            characterSlider?.onValueChanged.AddListener(OnCharacterChanged);
            sfxSlider?.onValueChanged.AddListener(OnSfxChanged);
            inquiryButton?.onClick.AddListener(OnClickInquiry);
            closeButton?.onClick.AddListener(OnClickClose);
        }

        public override void DeleteRegistedEvent()
        {
            masterSlider?.onValueChanged.RemoveListener(OnMasterChanged);
            bgmSlider?.onValueChanged.RemoveListener(OnBgmChanged);
            characterSlider?.onValueChanged.RemoveListener(OnCharacterChanged);
            sfxSlider?.onValueChanged.RemoveListener(OnSfxChanged);
            inquiryButton?.onClick.RemoveListener(OnClickInquiry);
            closeButton?.onClick.RemoveListener(OnClickClose);
        }

        // ── 값 로드/저장 ─────────────────────────────────────────────────────

        private void LoadValues()
        {
            UserOptionData opt = UserDataManager.Instance.GetUserOptionData();

            SetSlider(masterSlider,    masterValueText,    opt.masterVolume);
            SetSlider(bgmSlider,       bgmValueText,       opt.soundVolume);
            SetSlider(characterSlider, characterValueText, opt.fbxVolume);
            SetSlider(sfxSlider,       sfxValueText,       opt.sfxVolume);
        }

        private void SaveValues()
        {
            UserOptionData opt = UserDataManager.Instance.GetUserOptionData();
            if (masterSlider    != null) opt.masterVolume = Mathf.RoundToInt(masterSlider.value);
            if (bgmSlider       != null) opt.soundVolume  = Mathf.RoundToInt(bgmSlider.value);
            if (characterSlider != null) opt.fbxVolume    = Mathf.RoundToInt(characterSlider.value);
            if (sfxSlider       != null) opt.sfxVolume    = Mathf.RoundToInt(sfxSlider.value);
            UserDataManager.Instance.SaveUserData();
        }

        private void SetSlider(Slider slider, TextMeshProUGUI label, int value)
        {
            if (slider != null)
            {
                slider.minValue = 0;
                slider.maxValue = 100;
                slider.SetValueWithoutNotify(value);
            }
            if (label != null) label.text = value.ToString();
        }

        // ── 슬라이더 콜백 ────────────────────────────────────────────────────

        private void OnMasterChanged(float val)
        {
            if (masterValueText != null) masterValueText.text = Mathf.RoundToInt(val).ToString();
            ApplyVolumeToSoundManager();
        }

        private void OnBgmChanged(float val)
        {
            if (bgmValueText != null) bgmValueText.text = Mathf.RoundToInt(val).ToString();
            ApplyVolumeToSoundManager();
        }

        private void OnCharacterChanged(float val)
        {
            if (characterValueText != null) characterValueText.text = Mathf.RoundToInt(val).ToString();
            ApplyVolumeToSoundManager();
        }

        private void OnSfxChanged(float val)
        {
            if (sfxValueText != null) sfxValueText.text = Mathf.RoundToInt(val).ToString();
            ApplyVolumeToSoundManager();
        }

        private void ApplyVolumeToSoundManager()
        {
            // 옵션 데이터에 즉시 반영 후 SoundManager에 알림.
            // 마스터(AudioListener) 반영까지 SoundManager가 전담하므로, 게임 시작 시
            // 저장값을 적용하는 경로와 완전히 동일한 코드를 타게 된다.
            SaveValues();
            if (SoundManager.Instance != null)
                SoundManager.Instance.ApplyUserVolumeSettings();
        }

        // ── 버튼 핸들러 ─────────────────────────────────────────────────────

        public void OnClickInquiry()
        {
            // 문의하기: 외부 URL 또는 팝업 연결 예정
            Debug.Log("[SettingsWindow] 문의하기 클릭");
            Application.OpenURL("mailto:support@yourdomain.com");
        }

        public void OnClickClose()
        {
            ExitWindow();
        }
    }
}
