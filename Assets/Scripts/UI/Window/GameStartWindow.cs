using System;
using Cysharp.Threading.Tasks;
using Seunghak.Common;
using Seunghak.Data;
using Seunghak.Game.Suika3D;
using Seunghak.Manager;
using Seunghak.Platform;
using Seunghak.SceneManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 로비 메인 화면 (Fossil Merge).
    ///
    /// 레이아웃 (기획서 슬라이드4 기준):
    ///   - 상단  : 게임 타이틀 / 보유 Gold
    ///   - 중앙  : 모드 선택 [일반][지각 변동] (지각 변동은 일반 5,000점 달성 시 해금, 자물쇠 표시)
    ///   - 중앙  : 장착 스킬 2칸 + [코인 확정 구매(150)] [광고 랜덤(하루 10회)] (기획서 8장 — 상점 없이 로비에서 구매)
    ///   - 중하단: Play 버튼
    ///   - 하단  : 내정보(4) / 랭킹(5) / 로비(6·현재) / 상점(7) / 설정(8) 네비게이션
    ///
    /// 버튼 이벤트는 인스펙터 OnClick()에서 직접 연결합니다.
    /// </summary>
    public class GameStartWindow : BaseUIWindow
    {
        [System.Serializable]
        private class ItemCountSlotUI
        {
            public TextMeshProUGUI countText;
            [Tooltip("이 슬롯이 표시할 아이템 에셋. itemId 기준으로 보유 개수를 조회한다.")]
            public ShopItemData itemData;
        }

        [SerializeField] private GoldDisplayUI goldDisplay;

        [Header("최고 점수")]
        [SerializeField] private TextMeshProUGUI bestScoreText;

        [Header("모드 (선택) — 일반: 발굴대 고정 / 지각 변동: 폰 기울기 + 지진 (해금형)")]
        [Tooltip("현재 선택된 모드 표시")]
        [SerializeField] private TextMeshProUGUI difficultyText;
        [Tooltip("선택된 쪽을 강조할 오브젝트 (예: 버튼 하이라이트). 비워도 됨")]
        [SerializeField] private GameObject normalSelectedMark;
        [SerializeField] private GameObject hardSelectedMark;
        [Tooltip("지각 변동 잠금 표시 (자물쇠 + \"5,000점 달성 시 해금\")")]
        [SerializeField] private GameObject tectonicLockRoot;
        [SerializeField] private TextMeshProUGUI tectonicLockText;
        [Tooltip("지각 변동 첫 해금 안내 (멀미 안내 포함) — 해금 후 로비에서 1회 표시")]
        [SerializeField] private GameObject tectonicUnlockNotice;

        [Header("장착 스킬 2칸 (판 시작 시 들고 들어감, 1회용)")]
        [SerializeField] private TextMeshProUGUI[] loadoutSlotLabels = new TextMeshProUGUI[2];
        [Tooltip("스킬 확정 구매 선택 패널 (OnClickBuySkill(0~4) 버튼들). 비우면 순서대로 구매")]
        [SerializeField] private GameObject buySkillPanel;
        [SerializeField] private TextMeshProUGUI buyResultText;
        [SerializeField] private Button adSkillButton;
        [SerializeField] private TextMeshProUGUI adSkillCountText;

        [Header("보유 아이템 개수 (3종)")]
        [SerializeField] private ItemCountSlotUI[] itemCountSlots = new ItemCountSlotUI[3];

        [Header("토스 출시 — dev 표시 / 리더보드 / 친구 초대 (미연결이면 해당 기능만 건너뜀)")]
        [Tooltip("토스 dev 빌드에서만 켜지는 'dev' 텍스트 오브젝트 (로비 우측 하단)")]
        [SerializeField] private GameObject devBuildLabel;
        [SerializeField] private Button leaderboardButton;
        [SerializeField] private Button inviteButton;

        private bool _leaderboardBusy;
        private bool _inviteBusy;

        private const string TITLE_STRING = "Fossil Merge";

        // 아이템 개수(인게임 사용/획득)는 별도 변경 이벤트가 없어, 로비가 열려있는 동안
        // 주기적으로(1초 간격) 갱신해 실시간처럼 보이게 한다. 매 프레임 갱신할 필요는 없다.
        private const float ITEM_COUNT_POLL_INTERVAL = 1f;
        private float _itemCountPollTimer;

        // ── 생명주기 ────────────────────────────────────────────────────────

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.GameStartWindow;
            Refresh();
            SoundManager.Instance?.PlayBGM(SoundKeys.BGM_LOBBY);
        }

        public override void RestoreWindow()
        {
            base.RestoreWindow();
            Refresh();
            SoundManager.Instance?.PlayBGM(SoundKeys.BGM_LOBBY);
        }

        public override void ExitWindow()
        {
            UIManager.Instance.PopUI();
        }

        // ── 데이터 갱신 ─────────────────────────────────────────────────────

        private void Refresh()
        {
            RefreshBestScore();
            RefreshItemCounts();
            RefreshDifficulty();
            RefreshLoadout();
            RefreshPlatformButtons();
        }

        private void RefreshDifficulty()
        {
            var gm = SuikaGameManager.Instance;
            if (gm == null) return;
            bool tectonic = gm.Difficulty == E_SUIKA_DIFFICULTY.TECTONIC;
            if (difficultyText != null) difficultyText.text = tectonic ? "지각 변동" : "일반";
            if (normalSelectedMark != null) normalSelectedMark.SetActive(!tectonic);
            if (hardSelectedMark != null) hardSelectedMark.SetActive(tectonic);

            bool available = gm.IsModeAvailable(E_SUIKA_DIFFICULTY.TECTONIC);
            if (tectonicLockRoot != null) tectonicLockRoot.SetActive(!available);
            if (tectonicLockText != null)
            {
                tectonicLockText.text = !SuikaTiltInput.IsSupported
                    ? "기울기 센서를 지원하지 않는 기기예요"
                    : $"{gm.Rules.tectonicUnlockScore:N0}점 달성 시 해금";
            }

            // 첫 해금 안내 (기획서 11.1 모드 해금 팝업 — 멀미 안내 포함)
            if (tectonicUnlockNotice != null)
            {
                bool show = SuikaProgress.ShouldShowUnlockNotice;
                tectonicUnlockNotice.SetActive(show);
                if (show) SuikaProgress.MarkUnlockNoticeShown();
            }
        }

        private void RefreshLoadout()
        {
            for (int i = 0; i < loadoutSlotLabels.Length; i++)
            {
                if (loadoutSlotLabels[i] == null) continue;
                E_FOSSIL_SKILL s = SuikaProgress.GetLoadout(i);
                loadoutSlotLabels[i].text = s == E_FOSSIL_SKILL.NONE ? "빈 칸" : SuikaSkillInfo.Name(s);
            }
            int left = SuikaProgress.AdSkillsLeftToday;
            if (adSkillCountText != null) adSkillCountText.text = $"오늘 {left}회 남음";
            if (adSkillButton != null)
                adSkillButton.interactable = left > 0 && SuikaProgress.HasEmptyLoadoutSlot && !_adSkillBusy;
        }

        // 토스 출시용 요소 갱신. 필드가 씬에 연결되지 않았으면(null) 해당 기능만 건너뛴다.
        // 새 버튼은 인스펙터 OnClick 대신 코드로 연결하되, Remove 후 Add 해서 중복 등록을 막는다.
        private void RefreshPlatformButtons()
        {
            if (devBuildLabel != null)
                devBuildLabel.SetActive(TossConfig.IsDevBuild);

            var platform = PlatformFactory.Current;

            if (leaderboardButton != null)
            {
                bool supported = platform != null && platform.SupportsLeaderboardUI;
                leaderboardButton.gameObject.SetActive(supported);
                leaderboardButton.onClick.RemoveListener(OnClickLeaderboard);
                if (supported)
                    leaderboardButton.onClick.AddListener(OnClickLeaderboard);
            }

            if (inviteButton != null)
            {
                bool supported = platform != null && platform.SupportsInvite;
                inviteButton.gameObject.SetActive(supported);
                inviteButton.onClick.RemoveListener(OnClickInvite);
                if (supported)
                    inviteButton.onClick.AddListener(OnClickInvite);
            }
        }

        private void OnDestroy()
        {
            if (leaderboardButton != null) leaderboardButton.onClick.RemoveListener(OnClickLeaderboard);
            if (inviteButton != null) inviteButton.onClick.RemoveListener(OnClickInvite);
        }

        private void RefreshBestScore()
        {
            if (bestScoreText == null) return;
            var gm = SuikaGameManager.Instance;
            var mode = gm != null ? gm.Difficulty : E_SUIKA_DIFFICULTY.NORMAL;
            bestScoreText.text = SuikaProgress.GetBest(mode).ToString();
        }

        // 보유 아이템 개수 표시 갱신
        private void RefreshItemCounts()
        {
            if (itemCountSlots == null || UserDataManager.Instance == null) return;

            foreach (var slot in itemCountSlots)
            {
                if (slot == null || slot.itemData == null || slot.countText == null) continue;
                long count = UserDataManager.Instance.GetUserItemData().GetItemCount(slot.itemData.itemId);
                slot.countText.text = $"x{count}";
            }
        }

        // ── 버튼 핸들러 (인스펙터 OnClick 연결용 — public) ─────────────────

        // 모드 선택만 (Play 버튼은 따로 누르는 구성)
        public void OnClickSelectNormal() => SelectDifficulty(E_SUIKA_DIFFICULTY.NORMAL);
        public void OnClickSelectTectonic() => SelectDifficulty(E_SUIKA_DIFFICULTY.TECTONIC);
        public void OnClickToggleDifficulty()
        {
            var gm = SuikaGameManager.Instance;
            if (gm == null) return;
            SelectDifficulty(gm.Difficulty == E_SUIKA_DIFFICULTY.TECTONIC ? E_SUIKA_DIFFICULTY.NORMAL : E_SUIKA_DIFFICULTY.TECTONIC);
        }

        // 모드 선택 + 바로 시작 (모드별 Play 버튼을 따로 두는 구성)
        public void OnClickPlayNormal() { if (SelectDifficulty(E_SUIKA_DIFFICULTY.NORMAL)) OnClickPlay(); }
        public void OnClickPlayTectonic() { if (SelectDifficulty(E_SUIKA_DIFFICULTY.TECTONIC)) OnClickPlay(); }

        public void OnClickCloseUnlockNotice()
        {
            if (tectonicUnlockNotice != null) tectonicUnlockNotice.SetActive(false);
        }

        private bool SelectDifficulty(E_SUIKA_DIFFICULTY difficulty)
        {
            var gm = SuikaGameManager.Instance;
            bool ok = gm != null && gm.SetDifficulty(difficulty);
            RefreshDifficulty();
            RefreshBestScore();
            return ok;
        }

        // ── 스킬 장착 (로비) ────────────────────────────────────────────────

        private bool _adSkillBusy;

        /// <summary>코인 확정 구매 버튼 — 구매 패널을 연다 (패널이 없으면 버튼 인자 없이 순서대로)</summary>
        public void OnClickOpenBuySkill()
        {
            if (buySkillPanel != null) buySkillPanel.SetActive(true);
        }

        public void OnClickCloseBuySkill()
        {
            if (buySkillPanel != null) buySkillPanel.SetActive(false);
        }

        /// <summary>구매 패널의 스킬 버튼 (OnClick 인자: 0 망치, 1 브러시, 2 지진, 3 자석, 4 진화)</summary>
        public void OnClickBuySkill(int skill)
        {
            string msg;
            if (!SuikaProgress.HasEmptyLoadoutSlot) msg = "장착 칸이 가득 찼어요";
            else if (SuikaProgress.TryBuySkill((E_FOSSIL_SKILL)skill)) msg = $"{SuikaSkillInfo.Name((E_FOSSIL_SKILL)skill)} 장착!";
            else msg = $"코인이 부족해요 ({SuikaProgress.SKILL_PRICE} 코인)";
            if (buyResultText != null) buyResultText.text = msg;
            if (buySkillPanel != null && SuikaProgress.GetLoadout(SuikaProgress.LOADOUT_SLOTS - 1) != E_FOSSIL_SKILL.NONE)
                buySkillPanel.SetActive(false);
            RefreshLoadout();
        }

        /// <summary>광고 보고 랜덤 스킬 1개 장착 (하루 10회)</summary>
        public void OnClickAdSkill()
        {
            if (_adSkillBusy) return;
            AdSkillAsync().Forget();
        }

        private async UniTaskVoid AdSkillAsync()
        {
            _adSkillBusy = true;
            RefreshLoadout();
            try
            {
                E_FOSSIL_SKILL got = await SuikaPlatformBridge.GetRandomSkillWithAdAsync();
                if (this != null && buyResultText != null && got != E_FOSSIL_SKILL.NONE)
                    buyResultText.text = $"광고 보상: {SuikaSkillInfo.Name(got)}!";
            }
            finally
            {
                _adSkillBusy = false;
                if (this != null) RefreshLoadout();
            }
        }

        /// <summary>장착 칸 비우기 (OnClick 인자: 칸 번호) — 잘못 장착했을 때. 환불은 없음</summary>
        public void OnClickClearLoadout(int slot)
        {
            SuikaProgress.ClearLoadoutSlot(slot);
            RefreshLoadout();
        }

        /// <summary>마지막으로 선택한 모드로 시작</summary>
        public void OnClickPlay()
        {
            // 로비 BGM 크로스페이드를 명시적으로 끊어 인게임 BGM과의 경합을 방지한다.
            // InGamePause.OnClickHome()이 StopBGM()으로 인게임→로비 전환 시 동일한 문제를
            // 해결한 것과 대칭되는 처리다.
            SoundManager.Instance?.StopBGM();
            SceneManager.SceneManager.Instance.ChangeScene(E_SCENE_TYPE.INGAME);
        }

        public void OnClickMyInfo()
        {
            UIManager.Instance.PushUI(UI_TYPE.UserInfoPopup);
        }

        public void OnClickRanking()
        {
            UIManager.Instance.PushUI(UI_TYPE.RankingWindow);
        }

        public void OnClickShop()
        {
            UIManager.Instance.PushUI(UI_TYPE.CharacterShopWindow);
        }

        public void OnClickSettings()
        {
            UIManager.Instance.PushUI(UI_TYPE.SettingPopup);
        }

        private void OnClickLeaderboard()
        {
            if (_leaderboardBusy) return;
            OpenLeaderboardAsync().Forget();
        }

        private async UniTaskVoid OpenLeaderboardAsync()
        {
            _leaderboardBusy = true;
            try
            {
                await PlatformFactory.Current.OpenLeaderboardAsync();
            }
            finally
            {
                _leaderboardBusy = false;
            }
        }

        private void OnClickInvite()
        {
            if (_inviteBusy) return;
            InviteAsync().Forget();
        }

        private async UniTaskVoid InviteAsync()
        {
            _inviteBusy = true;
            if (inviteButton != null) inviteButton.interactable = false;
            try
            {
                int reward = await PlatformFactory.Current.InviteFriendsAsync();
                // 리워드는 토스포인트 등 플랫폼 측 지급이므로 게임 내 골드는 지급하지 않고 로그만 남긴다.
                Debug.Log($"[GameStartWindow] 친구 초대 완료 — 플랫폼 리워드 합계: {reward}");
            }
            finally
            {
                _inviteBusy = false;
                // 대기 중 창이 파괴됐을 수 있으므로 Unity null 체크 후 복구
                if (this != null && inviteButton != null) inviteButton.interactable = true;
            }
        }

        // 아이템 개수는 별도 변경 이벤트가 없어 여기서 주기적으로 갱신한다.
        private void Update()
        {
            _itemCountPollTimer += Time.unscaledDeltaTime;
            if (_itemCountPollTimer < ITEM_COUNT_POLL_INTERVAL) return;
            _itemCountPollTimer = 0f;
            RefreshItemCounts();
        }
    }
}
