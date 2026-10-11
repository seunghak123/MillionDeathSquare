using Seunghak.Common;
using Seunghak.Manager;
using Seunghak.Game.Suika3D;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 설정 팝업 (인게임/로비 공용).
    /// SettingsWindow의 팝업 버전 — 어느 화면에서도 오버레이로 표시 가능.
    ///
    /// 오디오 설정:
    ///   - 전체 (masterVolume) → AudioListener.volume
    ///   - 음악 (soundVolume)  → BGM 채널
    ///   - 캐릭터 (fbxVolume)  → 캐릭터 사운드 채널
    ///   - 효과음 (sfxVolume)  → SFX 채널
    ///
    /// [에디터 세팅]
    ///   - 각 Slider, TextMeshProUGUI, Button을 인스펙터에서 연결
    /// </summary>
    public class SettingPopup : BaseUIPopup
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

        [Header("햅틱 (선택)")]
        [SerializeField] private Toggle hapticToggle;

        [Header("지각 변동 모드 (선택) — 기획서 9.4/9.5")]
        [Tooltip("기울기 감도: 0 낮음 / 1 보통 / 2 높음 (wholeNumbers 슬라이더)")]
        [SerializeField] private Slider tiltSensitivitySlider;
        [SerializeField] private TextMeshProUGUI tiltSensitivityText;
        [Tooltip("흔들림 강도 0~100% (0이면 시각적 흔들림 완전히 끔)")]
        [SerializeField] private Slider shakeIntensitySlider;
        [SerializeField] private TextMeshProUGUI shakeIntensityText;
        [Tooltip("기준 자세 재설정 (현재 폰 각도를 수평으로)")]
        [SerializeField] private Button recalibrateButton;

        [Header("버튼")]
        [SerializeField] private Button inquiryButton;
        [SerializeField] private Button closeButton;

        private bool _wasGamePaused;

        // ── 생명주기 ────────────────────────────────────────────────────────

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.SettingPopup;
        }

        public override void EnterWindow()
        {
            RegistEvent();
            base.EnterWindow();
            LoadValues();

            var gm = SuikaGameManager.Instance;
            if (gm != null &&
                (gm.GameState == E_SUIKA_GAME_STATE.PLAYING ||
                 gm.GameState == E_SUIKA_GAME_STATE.READY))
            {
                _wasGamePaused = false;
                gm.Pause();
            }
            else if (gm != null && gm.GameState == E_SUIKA_GAME_STATE.PAUSED)
            {
                _wasGamePaused = false; // 외부에서 이미 Pause됨 → 닫을 때 Resume 필요
            }
            else
            {
                _wasGamePaused = true;
            }
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

            // 팝업이 열릴 때 게임이 진행 중이었으면 재개
            if (!_wasGamePaused && SuikaGameManager.Instance != null)
                SuikaGameManager.Instance.Resume();

            base.ExitWindow();
        }

        // inquiryButton/closeButton은 프리팹 Inspector에서 OnClick이 이미 연결되어 있음.
        // 슬라이더는 실제 Slider 컴포넌트가 아직 없어(이미지로만 그려진 바) 연결 대상이 없다 —
        // Slider가 생기면 여기 AddListener로 등록.
        public override void RegistEvent()
        {
            masterSlider?.onValueChanged.AddListener(OnMasterChanged);
            bgmSlider?.onValueChanged.AddListener(OnBgmChanged);
            characterSlider?.onValueChanged.AddListener(OnCharacterChanged);
            sfxSlider?.onValueChanged.AddListener(OnSfxChanged);
            if (hapticToggle != null)
            {
                hapticToggle.SetIsOnWithoutNotify(SuikaHaptics.Enabled);
                hapticToggle.onValueChanged.AddListener(OnHapticChanged);
            }
            if (tiltSensitivitySlider != null)
            {
                tiltSensitivitySlider.wholeNumbers = true;
                tiltSensitivitySlider.minValue = 0;
                tiltSensitivitySlider.maxValue = 2;
                tiltSensitivitySlider.SetValueWithoutNotify(SuikaSettings.TiltSensitivity);
                tiltSensitivitySlider.onValueChanged.AddListener(OnTiltSensitivityChanged);
                OnTiltSensitivityLabel(SuikaSettings.TiltSensitivity);
            }
            if (shakeIntensitySlider != null)
            {
                shakeIntensitySlider.minValue = 0f;
                shakeIntensitySlider.maxValue = 1f;
                shakeIntensitySlider.SetValueWithoutNotify(SuikaSettings.ShakeIntensity);
                shakeIntensitySlider.onValueChanged.AddListener(OnShakeIntensityChanged);
                OnShakeIntensityLabel(SuikaSettings.ShakeIntensity);
            }
            recalibrateButton?.onClick.AddListener(OnClickRecalibrate);
        }

        public override void DeleteRegistedEvent()
        {
            masterSlider?.onValueChanged.RemoveListener(OnMasterChanged);
            bgmSlider?.onValueChanged.RemoveListener(OnBgmChanged);
            characterSlider?.onValueChanged.RemoveListener(OnCharacterChanged);
            sfxSlider?.onValueChanged.RemoveListener(OnSfxChanged);
            hapticToggle?.onValueChanged.RemoveListener(OnHapticChanged);
            tiltSensitivitySlider?.onValueChanged.RemoveListener(OnTiltSensitivityChanged);
            shakeIntensitySlider?.onValueChanged.RemoveListener(OnShakeIntensityChanged);
            recalibrateButton?.onClick.RemoveListener(OnClickRecalibrate);
        }

        private void OnTiltSensitivityChanged(float v)
        {
            SuikaSettings.TiltSensitivity = Mathf.RoundToInt(v);
            OnTiltSensitivityLabel(SuikaSettings.TiltSensitivity);
        }

        private void OnTiltSensitivityLabel(int v)
        {
            if (tiltSensitivityText != null) tiltSensitivityText.text = v == 0 ? "낮음" : v == 2 ? "높음" : "보통";
        }

        private void OnShakeIntensityChanged(float v)
        {
            SuikaSettings.ShakeIntensity = v;
            OnShakeIntensityLabel(v);
        }

        private void OnShakeIntensityLabel(float v)
        {
            if (shakeIntensityText != null) shakeIntensityText.text = $"{Mathf.RoundToInt(v * 100f)}%";
        }

        private void OnClickRecalibrate()
        {
            var gm = SuikaGameManager.Instance;
            gm?.Plate?.TiltInput?.Calibrate();
            SuikaHaptics.Play(SuikaHaptics.DROP);
        }

        private void OnHapticChanged(bool on)
        {
            SuikaHaptics.Enabled = on;
            if (on) SuikaHaptics.Play(SuikaHaptics.DROP); // 켰을 때 바로 느껴지게
        }

        // ── 값 로드/저장 ─────────────────────────────────────────────────────

        private void LoadValues()
        {
            var opt = UserDataManager.Instance.GetUserOptionData();
            SetSlider(masterSlider,    masterValueText,    opt.masterVolume);
            SetSlider(bgmSlider,       bgmValueText,       opt.soundVolume);
            SetSlider(characterSlider, characterValueText, opt.fbxVolume);
            SetSlider(sfxSlider,       sfxValueText,       opt.sfxVolume);
        }

        private void SaveValues()
        {
            var opt = UserDataManager.Instance.GetUserOptionData();
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
            // 마스터(AudioListener) 반영까지 SoundManager가 전담 — 게임 시작 시
            // 저장값을 적용하는 경로와 동일한 코드를 타게 된다.
            SaveValues();
            if (SoundManager.Instance != null)
                SoundManager.Instance.ApplyUserVolumeSettings();
        }

        // ── 버튼 핸들러 ─────────────────────────────────────────────────────

        public void OnClickInquiry()
        {
            Debug.Log("[SettingPopup] 문의하기 클릭");
            Application.OpenURL("mailto:support@yourdomain.com");
        }

        public void OnClickClose()
        {
            ExitWindow();
        }

        public void OnClickShop()
        {
            UIManager.Instance.PushUI(UI_TYPE.CharacterShopWindow);
        }

        public void OnClickHome()
        {
            ExitWindow();
            Seunghak.SceneManager.SceneManager.Instance.ChangeScene(E_SCENE_TYPE.LOBBY);
        }
    }
}
