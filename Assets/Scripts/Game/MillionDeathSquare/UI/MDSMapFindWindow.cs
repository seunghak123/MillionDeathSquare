using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Seunghak.Platform;
using Seunghak.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 맵 찾기 (기획서 11장, 1단계): 맵 코드 입력칸 + 공유 코드 붙여넣기 + 내가 플레이한 맵 목록 + 내가 올린 맵.
    /// 공개 맵 목록은 없다 (운영 인력 확보 후 2·3단계, 기획서 6.1).
    /// </summary>
    public sealed class MDSMapFindWindow : BaseUIWindow
    {
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private Button findButton;
        [SerializeField] private Button pasteButton;
        [SerializeField] private TMP_Text statusText;
        [Header("찾은 맵")]
        [SerializeField] private GameObject resultRoot;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private RawImage resultPreview;
        [SerializeField] private Button playButton;
        [SerializeField] private Button detailButton;
        [Header("목록 (플레이한 맵 / 내가 올린 맵)")]
        [SerializeField] private Button[] listButtons = new Button[8];
        [SerializeField] private Button playedTab;
        [SerializeField] private Button mineTab;
        [SerializeField] private Button backButton;

        private ResolvedMap _found;
        private Texture2D _previewTex;
        private bool _busy;
        private bool _showMine;
        private readonly List<string> _listRefs = new List<string>();

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.MDSMapFindWindow;
            MDSUi.Bind(findButton, () => FindAsync(codeInput != null ? codeInput.text : "").Forget());
            MDSUi.Bind(pasteButton, () =>
            {
                string clip = MDSUi.ReadClipboard().Trim();
                if (codeInput != null) codeInput.text = clip;
                FindAsync(clip).Forget();
            });
            MDSUi.Bind(playButton, Play);
            MDSUi.Bind(detailButton, OpenDetail);
            MDSUi.Bind(backButton, ExitWindow);
            MDSUi.Bind(playedTab, () => { _showMine = false; RefreshListAsync().Forget(); });
            MDSUi.Bind(mineTab, () => { _showMine = true; RefreshListAsync().Forget(); });
            for (int i = 0; i < listButtons.Length; i++)
            {
                int index = i;
                MDSUi.Bind(listButtons[i], () =>
                {
                    if (index < _listRefs.Count) FindAsync(_listRefs[index]).Forget();
                });
            }
            if (codeInput != null) codeInput.onSubmit.AddListener(s => FindAsync(s).Forget());
        }

        public override void EnterWindow()
        {
            base.EnterWindow();
            MDSUi.SetActive(resultRoot, _found != null);
            MDSUi.SetText(statusText, "친구에게 받은 맵 코드(예: 2XJ-FKTD)나 공유 코드(MDS1-…)를 넣으세요");
            RefreshListAsync().Forget();
            MDSAnalytics.Screen("map_find");
        }

        public override void RestoreWindow()
        {
            base.RestoreWindow();
            RefreshListAsync().Forget();
        }

        private async UniTaskVoid FindAsync(string code)
        {
            if (_busy) return;
            _busy = true;
            MDSUi.SetText(statusText, "찾는 중…");
            ResolvedMap m = await MDSMapResolver.ResolveAsync(code, MDSGameDirector.Instance.CampaignAsset);
            _busy = false;
            if (this == null) return;
            if (!m.Ok)
            {
                _found = null;
                MDSUi.SetActive(resultRoot, false);
                MDSUi.SetText(statusText, m.Error);
                return;
            }
            _found = m;
            MDSUi.SetActive(resultRoot, true);
            string creator = m.Item != null && m.Item.CreatorDeaths >= 0
                ? "\n만든 사람 " + m.Item.CreatorName + " — 데스 " + m.Item.CreatorDeaths + " · " + MDSUi.FormatTicks(m.Item.CreatorTicks)
                : "";
            MDSUi.SetText(resultText, (string.IsNullOrEmpty(m.Title) ? "이름 없는 맵" : m.Title) + "\n" + m.Map.Width + "×" + m.Map.Height
                + " · 동전 " + m.Map.Coins.Count + " · 장애물 " + m.Map.Obstacles.Count + creator);
            MDSUi.SetActive(detailButton, m.WorkshopId != 0);
            MDSUi.SetText(statusText, "");
            if (resultPreview != null)
            {
                if (_previewTex != null) Destroy(_previewTex);
                _previewTex = MDSMapPreview.Render(m.Map, MDSPaletteColors.FromCore(Palettes.Get(m.Map.PaletteId)));
                resultPreview.texture = _previewTex;
            }
        }

        private async UniTaskVoid RefreshListAsync()
        {
            _listRefs.Clear();
            var labels = new List<string>();
            if (_showMine)
            {
                List<WorkshopItem> mine = await MDSServices.Workshop.GetMyItemsAsync();
                if (this == null) return;
                foreach (WorkshopItem it in mine) { _listRefs.Add(it.MapCode); labels.Add(it.Title + "  " + it.MapCode + "  좋아요 " + it.Likes); }
            }
            else
            {
                foreach (MDSSaveStore.PlayedMap p in MDSSaveStore.PlayedMaps)
                {
                    _listRefs.Add(p.MapRef);
                    string code = p.MapRef.StartsWith(MapSerializer.SHARE_PREFIX) ? "(공유 코드)" : p.MapRef;
                    labels.Add((string.IsNullOrEmpty(p.Name) ? "이름 없는 맵" : p.Name) + "  " + code + (p.Cleared ? "  클리어 데스 " + p.BestDeaths : ""));
                }
            }
            for (int i = 0; i < listButtons.Length; i++)
            {
                bool on = i < labels.Count;
                MDSUi.SetActive(listButtons[i], on);
                if (on) MDSUi.SetLabel(listButtons[i], labels[i]);
            }
            MDSUi.Tint(playedTab, _showMine ? MDSUi.Normal : MDSUi.Selected);
            MDSUi.Tint(mineTab, _showMine ? MDSUi.Selected : MDSUi.Normal);
        }

        private void Play()
        {
            if (_found == null) return;
            MDSSaveStore.RecordPlayed(_found.MapRef, _found.Title, false, 0);
            MDSGameDirector.Instance.RequestPlay(_found.IsCampaign ? PlayRequest.Campaign(_found) : PlayRequest.User(_found));
        }

        private void OpenDetail()
        {
            if (_found == null) return;
            var popup = UIManager.UIManager.Instance.PushUI(UI_TYPE.MDSMapDetailPopup) as MDSMapDetailPopup;
            if (popup != null) popup.Show(_found);
        }
    }
}
