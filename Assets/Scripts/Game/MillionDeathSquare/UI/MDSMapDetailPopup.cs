using System.Collections.Generic;
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
    /// 맵 상세·후기 (기획서 6.3·6.4·11장): 클리어율, 최소 데스·최단 시간 보유자, 체감 난이도 분포, 좋아요, 한 줄 후기(정해진 문구),
    /// 만든 사람의 클리어 기록. 기록은 이 판(PC/모바일) 순위표만 보여 준다.
    /// </summary>
    public sealed class MDSMapDetailPopup : BaseUIPopup
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RawImage preview;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private Image[] difficultyBars = new Image[4];
        [SerializeField] private TMP_Text phrasesText;
        [SerializeField] private TMP_Text recordsText;
        [SerializeField] private Button playButton;
        [SerializeField] private Button likeButton;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Button closeButton;

        private ResolvedMap _map;
        private Texture2D _tex;

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.MDSMapDetailPopup;
            MDSUi.Bind(closeButton, ExitWindow);
            MDSUi.Bind(playButton, () =>
            {
                if (_map == null) return;
                ExitWindow();
                MDSGameDirector.Instance.RequestPlay(PlayRequest.User(_map));
            });
            MDSUi.Bind(likeButton, () => LikeAsync().Forget());
            MDSUi.Bind(copyCodeButton, () => { if (_map != null) MDSUi.CopyToClipboard(_map.MapRef); MDSUi.SetLabel(copyCodeButton, "복사됨"); });
        }

        public void Show(ResolvedMap map)
        {
            _map = map;
            MDSUi.SetText(titleText, (string.IsNullOrEmpty(map.Title) ? "이름 없는 맵" : map.Title) + "   " + map.MapRef);
            MDSUi.SetLabel(copyCodeButton, "코드 복사");
            MDSUi.SetLabel(likeButton, "좋아요");
            if (preview != null)
            {
                if (_tex != null) Destroy(_tex);
                _tex = MDSMapPreview.Render(map.Map, MDSPaletteColors.FromCore(Palettes.Get(map.Map.PaletteId)));
                preview.texture = _tex;
            }
            MDSUi.SetText(statsText, "불러오는 중…");
            MDSUi.SetText(phrasesText, "");
            MDSUi.SetText(recordsText, "");
            LoadAsync().Forget();
        }

        private async UniTaskVoid LoadAsync()
        {
            ResolvedMap m = _map;
            ReviewSummary s = m.WorkshopId != 0 ? await MDSServices.Workshop.GetSummaryAsync(m.WorkshopId) : new ReviewSummary();
            List<RecordEntry> deaths = await MDSServices.Leaderboards.GetTopAsync(m.RecordKey, RecordKind.Deaths, 3);
            List<RecordEntry> times = await MDSServices.Leaderboards.GetTopAsync(m.RecordKey, RecordKind.Time, 1);
            if (this == null || _map != m) return;

            var sb = new StringBuilder();
            sb.Append("클리어율 ").Append(s.ClearRateText).Append("  (").Append(s.Clears).Append('/').Append(s.Attempts).Append(")   좋아요 ").Append(s.Likes);
            if (m.Item != null && m.Item.CreatorDeaths >= 0)
                sb.Append("\n만든 사람 ").Append(m.Item.CreatorName).Append(" 클리어 — 데스 ").Append(m.Item.CreatorDeaths).Append(" · ").Append(MDSUi.FormatTicks(m.Item.CreatorTicks));
            sb.Append("\n체감 난이도: 쉬움 ").Append(s.DifficultyCounts[0]).Append(" · 보통 ").Append(s.DifficultyCounts[1])
              .Append(" · 어려움 ").Append(s.DifficultyCounts[2]).Append(" · 지옥 ").Append(s.DifficultyCounts[3]);
            MDSUi.SetText(statsText, sb.ToString());

            int max = 1;
            for (int i = 0; i < 4; i++) if (s.DifficultyCounts[i] > max) max = s.DifficultyCounts[i];
            for (int i = 0; i < difficultyBars.Length; i++)
            {
                if (difficultyBars[i] == null) continue;
                difficultyBars[i].fillAmount = (float)s.DifficultyCounts[i] / max;
            }

            var ph = new StringBuilder();
            foreach (int id in s.TopPhrases(3)) ph.Append('"').Append(ReviewPhrases.Get(id)).Append("\" ").Append(s.PhraseCounts[id]).Append("명\n");
            MDSUi.SetText(phrasesText, ph.Length > 0 ? ph.ToString() : "아직 후기가 없어요");

            var rec = new StringBuilder();
            rec.Append(MDSServices.RecordPlatform == RecordPlatform.PC ? "PC 기록" : "모바일 기록").Append('\n');
            if (deaths.Count == 0) rec.Append("아직 깬 사람이 없어요");
            for (int i = 0; i < deaths.Count; i++)
                rec.Append(i + 1).Append(". ").Append(deaths[i].PlayerName).Append("  데스 ").Append(deaths[i].Score).Append('\n');
            if (times.Count > 0) rec.Append("최단 시간: ").Append(times[0].PlayerName).Append("  ").Append(MDSUi.FormatTicks(times[0].Score));
            MDSUi.SetText(recordsText, rec.ToString());
        }

        private async UniTaskVoid LikeAsync()
        {
            if (_map == null || _map.WorkshopId == 0) return;
            bool ok = await MDSServices.Workshop.LikeAsync(_map.WorkshopId);
            if (this != null) MDSUi.SetLabel(likeButton, ok ? "좋아요 누름" : "실패");
        }
    }
}
