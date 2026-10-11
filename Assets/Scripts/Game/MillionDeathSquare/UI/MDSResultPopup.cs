using System.Text;
using Cysharp.Threading.Tasks;
using Seunghak.Platform;
using Seunghak.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 결과 (기획서 11장): 데스 수, 시간, 0데스, 새 기록/해금 안내, 후기 남기기(좋아요·체감 난이도·정해진 문구),
    /// 멀티는 순위와 각자 "가장 많이 죽은 장소". 버튼: 다시 하기 / 다음 스테이지 / 나가기.
    /// </summary>
    public sealed class MDSResultPopup : BaseUIPopup
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text deathsText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private TMP_Text recordText;
        [SerializeField] private GameObject zeroDeathBadge;
        [Header("후기 (유저 맵)")]
        [SerializeField] private GameObject reviewRoot;
        [SerializeField] private Button likeButton;
        [SerializeField] private Button[] difficultyButtons = new Button[4];
        [SerializeField] private Button[] phraseButtons = new Button[8];
        [SerializeField] private Button sendReviewButton;
        [Header("멀티")]
        [SerializeField] private TMP_Text multiplayerText;
        [Header("버튼")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button exitButton;

        private PlayResult _result;
        private readonly Review _review = new Review();
        private bool _reviewSent;
        private string _recordBase = "";
        private string _shownStatus;

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.MDSResultPopup;
            MDSUi.Bind(retryButton, OnRetry);
            MDSUi.Bind(nextButton, OnNext);
            MDSUi.Bind(exitButton, OnExit);
            MDSUi.Bind(likeButton, () => { _review.Liked = !_review.Liked; RefreshReview(); });
            for (int i = 0; i < difficultyButtons.Length; i++)
            {
                int d = i;
                MDSUi.Bind(difficultyButtons[i], () => { _review.Difficulty = (PerceivedDifficulty)d; RefreshReview(); });
                MDSUi.SetLabel(difficultyButtons[i], Review.DifficultyLabel((PerceivedDifficulty)i));
            }
            for (int i = 0; i < phraseButtons.Length; i++)
            {
                int p = i;
                MDSUi.Bind(phraseButtons[i], () => { _review.PhraseId = _review.PhraseId == p ? Review.NO_PHRASE : p; RefreshReview(); });
                MDSUi.SetLabel(phraseButtons[i], ReviewPhrases.Get(i));
                MDSUi.SetActive(phraseButtons[i], i < ReviewPhrases.Count);
            }
            MDSUi.Bind(sendReviewButton, () => SendReviewAsync().Forget());
        }

        public void Show(PlayResult r)
        {
            _result = r;
            _reviewSent = false;
            _review.Liked = false;
            _review.Difficulty = PerceivedDifficulty.Normal;
            _review.PhraseId = Review.NO_PHRASE;
            _review.Cleared = r.Cleared;

            PlayKind kind = r.Request != null ? r.Request.Kind : PlayKind.UserMap;
            MDSUi.SetText(titleText, r.Cleared ? "클리어!" : "판 끝");
            MDSUi.SetText(deathsText, r.Deaths.ToString());
            MDSUi.SetText(detailText, "죽은 횟수 " + r.Deaths + "  ·  " + MDSUi.FormatTicks(r.Ticks));
            MDSUi.SetActive(zeroDeathBadge, r.Cleared && r.ZeroDeath);

            var sb = new StringBuilder();
            if (kind == PlayKind.Campaign && r.Cleared)
            {
                ClearOutcome o = r.CampaignOutcome;
                if (o.FirstClear) sb.Append("첫 클리어! ");
                if (o.NewBestDeaths && !o.FirstClear) sb.Append("최소 데스 갱신! ");
                if (o.NewBestTime && !o.FirstClear) sb.Append("최단 시간 갱신! ");
                if (o.FirstZeroDeath) sb.Append("0데스 클리어! ");
                if (o.UnlockedWorld > 0) sb.Append("\n월드 ").Append(o.UnlockedWorld).Append(" 열림!");
                if (o.UnlockedDevice != EditorDevice.None) sb.Append("\n에디터에 새 장치 해금!");
                if (r.Best != null) sb.Append("\n내 최고: 데스 ").Append(r.Best.BestDeaths).Append(" · ").Append(MDSUi.FormatTicks(r.Best.BestTicks));
            }
            else if (kind == PlayKind.EditorTest && r.Cleared)
            {
                sb.Append("직접 깼어요 — 이제 업로드할 수 있어요");
            }
            _recordBase = sb.ToString();
            _shownStatus = null;
            MDSUi.SetText(recordText, _recordBase);

            MDSUi.SetActive(reviewRoot, kind == PlayKind.UserMap);
            RefreshReview();

            bool multi = r.Multiplayer != null;
            MDSUi.SetActive(multiplayerText, multi);
            if (multi) MDSUi.SetText(multiplayerText, MultiplayerSummary(r));

            MDSUi.SetActive(retryButton, kind != PlayKind.Multiplayer);
            MDSUi.SetActive(nextButton, kind == PlayKind.Campaign && r.Cleared);
            MDSUi.SetLabel(exitButton, kind == PlayKind.EditorTest ? "에디터로" : (multi ? "방으로" : "나가기"));
            MDSUi.SetLabel(retryButton, "다시 하기");
            MDSUi.SetLabel(nextButton, "다음 스테이지");
        }

        private static string MultiplayerSummary(PlayResult r)
        {
            MDSMultiplayer mp = MDSGameDirector.Instance.Multiplayer;
            var sb = new StringBuilder();
            string mode = r.Request != null ? MDSPlayWindow.ModeLabel(r.Request.Mode) : "";
            sb.Append(mode).Append(" 결과\n");
            foreach (PlayerResult p in r.Multiplayer)
            {
                sb.Append(p.Rank).Append("위  ").Append(mp.NameOf(p.PlayerId)).Append("  데스 ").Append(p.Deaths);
                if (p.FinishTick >= 0) sb.Append("  ").Append(MDSUi.FormatTicks(p.FinishTick));
                else if (p.Finished) sb.Append("  팀 클리어");
                if (p.MostDeathsCount > 0) sb.Append("  · 가장 많이 죽은 곳 (").Append(p.MostDeathsCellX).Append(',').Append(p.MostDeathsCellY).Append(") ").Append(p.MostDeathsCount).Append("번");
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private void RefreshReview()
        {
            MDSUi.SetLabel(likeButton, _review.Liked ? "좋아요 누름" : "좋아요");
            MDSUi.Tint(likeButton, _review.Liked ? MDSUi.Selected : MDSUi.Normal);
            for (int i = 0; i < difficultyButtons.Length; i++) MDSUi.Tint(difficultyButtons[i], (int)_review.Difficulty == i ? MDSUi.Selected : MDSUi.Normal);
            for (int i = 0; i < phraseButtons.Length; i++) MDSUi.Tint(phraseButtons[i], _review.PhraseId == i ? MDSUi.Selected : MDSUi.Normal);
            MDSUi.SetInteractable(sendReviewButton, !_reviewSent);
            MDSUi.SetLabel(sendReviewButton, _reviewSent ? "후기 보냄" : "후기 남기기");
        }

        private async UniTaskVoid SendReviewAsync()
        {
            if (_reviewSent || _result == null || _result.Request == null) return;
            _reviewSent = true;
            RefreshReview();
            ulong id = _result.Request.Map.WorkshopId;
            if (id != 0) await MDSServices.Workshop.SubmitReviewAsync(id, _review);
            if (this != null)
            {
                _recordBase = "후기 고마워요!";
                _shownStatus = null;
                MDSUi.SetText(recordText, _recordBase);
            }
        }

        private void Update()
        {
            // 순위표 제출 결과가 늦게 오면 갱신
            if (_result != null && _result.Request != null && _result.Request.Kind != PlayKind.EditorTest && _result.Multiplayer == null
                && !string.IsNullOrEmpty(_result.RecordStatus) && _result.RecordStatus != _shownStatus)
            {
                _shownStatus = _result.RecordStatus;
                MDSUi.SetText(recordText, _recordBase.Length > 0 ? _recordBase + "\n" + _shownStatus : _shownStatus);
            }
            if (MDSInput.ConfirmPressed())
            {
                if (nextButton != null && nextButton.gameObject.activeSelf) OnNext();
                else if (retryButton != null && retryButton.gameObject.activeSelf) OnRetry();
            }
        }

        private void OnRetry()
        {
            ExitWindow();
            MDSGameDirector.Instance.Retry();
        }

        private void OnNext()
        {
            ExitWindow();
            if (!MDSGameDirector.Instance.PlayNextStage()) MDSGameDirector.Instance.ExitToMenu();
        }

        private void OnExit()
        {
            ExitWindow();
            MDSGameDirector.Instance.ExitToMenu();
        }
    }
}
