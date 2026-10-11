using Seunghak.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 캠페인 선택 (월드 5 × 스테이지 10). 월드 탭 + 스테이지 버튼 10개(최소 데스·0데스 표시).
    /// 월드 해금: 앞 월드 7개 클리어. 월드를 깨면 그 장치가 에디터에 해금된다는 안내도 함께.
    /// </summary>
    public sealed class MDSCampaignWindow : BaseUIWindow
    {
        private static readonly string[] DeviceNames = { "왕복하는 장애물", "원을 그리며 도는 장애물", "열쇠와 문", "얼음 바닥 · 컨베이어", "사라졌다 나타나는 바닥" };

        [SerializeField] private Button[] worldButtons = new Button[Rules.WORLD_COUNT];
        [SerializeField] private Button[] stageButtons = new Button[Rules.STAGES_PER_WORLD];
        [SerializeField] private TMP_Text[] stageLabels = new TMP_Text[Rules.STAGES_PER_WORLD];
        [SerializeField] private TMP_Text worldTitleText;
        [SerializeField] private TMP_Text worldInfoText;
        [SerializeField] private Button backButton;

        private int _world = 1;

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.MDSCampaignWindow;
            for (int i = 0; i < worldButtons.Length; i++)
            {
                int w = i + 1;
                MDSUi.Bind(worldButtons[i], () => SelectWorld(w));
            }
            for (int i = 0; i < stageButtons.Length; i++)
            {
                int s = i + 1;
                MDSUi.Bind(stageButtons[i], () => PlayStage(s));
            }
            MDSUi.Bind(backButton, ExitWindow);
            // 마지막으로 열린 월드부터
            for (int w = Rules.WORLD_COUNT; w >= 1; w--) if (MDSSaveStore.Campaign.IsWorldUnlocked(w)) { _world = w; break; }
            Refresh();
        }

        public override void EnterWindow() { base.EnterWindow(); Refresh(); }
        public override void RestoreWindow() { base.RestoreWindow(); Refresh(); }

        private void SelectWorld(int w)
        {
            if (!MDSSaveStore.Campaign.IsWorldUnlocked(w)) return;
            _world = w;
            Refresh();
        }

        private void Refresh()
        {
            CampaignProgress p = MDSSaveStore.Campaign;
            MDSDirectorHelpers.ShowCampaignPreview(_world);
            for (int i = 0; i < worldButtons.Length; i++)
            {
                int w = i + 1;
                bool unlocked = p.IsWorldUnlocked(w);
                MDSUi.SetInteractable(worldButtons[i], unlocked);
                MDSUi.SetLabel(worldButtons[i], unlocked ? "월드 " + w : "잠김 " + w);
                MDSUi.Tint(worldButtons[i], w == _world ? MDSUi.Selected : (unlocked ? MDSUi.Normal : MDSUi.Locked));
            }
            PaletteDef pal = Palettes.Get(Campaign.PaletteOfWorld(_world));
            MDSUi.SetText(worldTitleText, "월드 " + _world + " · " + pal.Name);
            int cleared = p.ClearedCount(_world);
            string next = _world < Rules.WORLD_COUNT
                ? (p.IsWorldCleared(_world) ? "다음 월드 열림!" : "다음 월드까지 " + (Rules.STAGES_TO_UNLOCK_NEXT_WORLD - cleared) + "개 더 클리어")
                : "마지막 월드";
            string device = "새 장치: " + DeviceNames[_world - 1] + (p.IsWorldCleared(_world) ? " (에디터 해금됨)" : " — 월드를 깨면 에디터에서 쓸 수 있어요");
            MDSUi.SetText(worldInfoText, "클리어 " + cleared + "/" + Rules.STAGES_PER_WORLD + " · " + next + "\n" + device);

            for (int i = 0; i < stageButtons.Length; i++)
            {
                int s = i + 1;
                CampaignStage st = Campaign.Get(_world, s);
                StageRecord r = p.Get(_world, s);
                string label = _world + "-" + s + "\n" + st.Name;
                if (r != null && r.Cleared)
                    label += "\n데스 " + r.BestDeaths + (r.ZeroDeath ? " (0데스!)" : "") + " · " + MDSUi.FormatTicks(r.BestTicks);
                else if (r != null && r.TotalDeaths > 0)
                    label += "\n(죽은 횟수 " + r.TotalDeaths + ")";
                if (i < stageLabels.Length) MDSUi.SetText(stageLabels[i], label);
                MDSUi.Tint(stageButtons[i], r != null && r.Cleared ? (r.ZeroDeath ? new Color(1f, 0.8f, 0.3f) : new Color(0.4f, 0.75f, 0.45f)) : MDSUi.Normal);
            }
        }

        private void PlayStage(int s)
        {
            if (!MDSSaveStore.Campaign.IsStageUnlocked(_world, s)) return;
            MDSGameDirector d = MDSGameDirector.Instance;
            d.RequestPlay(PlayRequest.Campaign(MDSMapResolver.ResolveCampaign(_world, s, d.CampaignAsset)));
        }
    }

    /// <summary>메뉴 화면 뒤 배경으로 보드를 보여 주는 도우미</summary>
    public static class MDSDirectorHelpers
    {
        /// <summary>캠페인 선택 중: 월드 첫 스테이지를 배경으로 (장애물은 움직이지 않음)</summary>
        public static void ShowCampaignPreview(int world)
        {
            MDSGameDirector d = MDSGameDirector.Instance;
            if (d == null || d.IsPlaying || d.Board == null) return;
            CampaignStage s = d.CampaignAsset != null ? d.CampaignAsset.GetStage(world, 1) : Campaign.Get(world, 1);
            d.Board.Build(s.Map, false);
            d.Board.SetPlayerVisible(false);
            d.Board.RenderObstaclesAtTick(0);
        }
    }
}
