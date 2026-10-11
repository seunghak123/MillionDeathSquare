using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.UIManager
{
    /// <summary>
    /// 랭킹 시스템 윈도우 (기획서 슬라이드6).
    ///
    /// 표시 정보:
    ///   - 주간 / 월간 탭 전환
    ///   - 랭킹 목록 (등수 · 닉네임 · 점수) 스크롤 리스트
    /// </summary>
    public class RankingWindow : BaseUIWindow
    {
        [Header("탭 버튼")]
        [SerializeField] private Button btnWeekly;
        [SerializeField] private Button btnMonthly;

        [Header("탭 강조 (활성 탭 색상 변경용)")]
        [SerializeField] private Image imgWeeklyHighlight;
        [SerializeField] private Image imgMonthlyHighlight;

        [Header("랭킹 리스트")]
        [SerializeField] private Transform rankListParent;
        [SerializeField] private RankingItemUI rankItemPrefab;

        [Header("닫기")]
        [SerializeField] private Button closeButton;

        private bool _isWeekly = true;

        // ── 생명주기 ────────────────────────────────────────────────────────

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.RankingWindow;
        }

        public override void EnterWindow()
        {
            RegistEvent();
            base.EnterWindow();
            ShowTab(weekly: true);
        }

        public override void RestoreWindow()
        {
            base.RestoreWindow();
            ShowTab(_isWeekly);
        }

        public override void ExitWindow()
        {
            DeleteRegistedEvent();
            UIManager.Instance.PopUI();
        }

        public override void RegistEvent()
        {
            btnWeekly?.onClick.AddListener(OnClickWeekly);
            btnMonthly?.onClick.AddListener(OnClickMonthly);
            closeButton?.onClick.AddListener(OnClickClose);
        }

        public override void DeleteRegistedEvent()
        {
            btnWeekly?.onClick.RemoveListener(OnClickWeekly);
            btnMonthly?.onClick.RemoveListener(OnClickMonthly);
            closeButton?.onClick.RemoveListener(OnClickClose);
        }

        // ── 탭 전환 ─────────────────────────────────────────────────────────

        private void ShowTab(bool weekly)
        {
            _isWeekly = weekly;

            if (imgWeeklyHighlight != null)  imgWeeklyHighlight.enabled  = weekly;
            if (imgMonthlyHighlight != null) imgMonthlyHighlight.enabled = !weekly;

            RefreshList();
        }

        // ── 리스트 갱신 ─────────────────────────────────────────────────────

        private void RefreshList()
        {
            if (rankListParent == null || rankItemPrefab == null) return;

            // 기존 항목 제거
            foreach (Transform child in rankListParent)
                Destroy(child.gameObject);

            List<RankEntry> entries = GetRankData(_isWeekly);
            for (int i = 0; i < entries.Count; i++)
            {
                var item = Instantiate(rankItemPrefab, rankListParent);
                item.Setup(i + 1, entries[i].nickname, entries[i].score);
            }
        }

        /// <summary>
        /// 랭킹 데이터를 반환합니다.
        /// 현재는 로컬 플레이어 데이터만 포함하며, 서버/Facebook 연동 시 교체합니다.
        /// </summary>
        private List<RankEntry> GetRankData(bool weekly)
        {
            var list = new List<RankEntry>();
            int best = Seunghak.Common.UserDataManager.Instance.GetBestScore();
            string nick = Seunghak.Common.UserDataManager.Instance.UserNickName;
            if (string.IsNullOrEmpty(nick)) nick = "Player";
            if (best > 0)
                list.Add(new RankEntry { nickname = nick, score = best });
            return list;
        }

        // ── 버튼 핸들러 ─────────────────────────────────────────────────────

        public void OnClickWeekly()  => ShowTab(weekly: true);
        public void OnClickMonthly() => ShowTab(weekly: false);
        public void OnClickClose()   => ExitWindow();

        // ── 내부 데이터 구조 ─────────────────────────────────────────────────

        private struct RankEntry
        {
            public string nickname;
            public int score;
        }
    }

    // ── 랭킹 아이템 UI (프리팹 컴포넌트) ────────────────────────────────────

    /// <summary>랭킹 목록 한 줄 항목. 등수 · 닉네임 · 점수 표시.</summary>
    public class RankingItemUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private TextMeshProUGUI nicknameText;
        [SerializeField] private TextMeshProUGUI scoreText;

        public void Setup(int rank, string nickname, int score)
        {
            if (rankText != null)    rankText.text    = rank.ToString();
            if (nicknameText != null) nicknameText.text = nickname;
            if (scoreText != null)   scoreText.text   = score.ToString();
        }
    }
}
