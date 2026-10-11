using System.Text;
using Seunghak.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 플레이 HUD (기획서 11장 화면 1): 위쪽 가운데 데스 수를 가장 크게, 왼쪽 월드-스테이지, 오른쪽 동전 n/m.
    /// 죽을 때마다 데스 숫자가 크게 튄다 (기획서 3.2). 멀티는 카운트다운과 참가자 데스 목록.
    /// </summary>
    public sealed class MDSPlayWindow : BaseUIWindow
    {
        [SerializeField] private TMP_Text deathText;
        [SerializeField] private TMP_Text deathLabel;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text coinText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private TMP_Text rosterText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private Button pauseButton;
        [Tooltip("죽을 때 데스 숫자가 커지는 배율")]
        [SerializeField] private float punchScale = 1.6f;

        private float _punch;
        private int _shownDeaths = -1;
        private int _shownCoins = -1;
        private int _shownSeconds = -1;
        private readonly StringBuilder _sb = new StringBuilder(128);

        public override void StartWindow()
        {
            ui_Type = UI_TYPE.MDSPlayWindow;
            MDSUi.Bind(pauseButton, OnClickPause);
            MDSUi.SetText(deathLabel, "데스");
            MDSUi.SetText(hintText, "방향키·WASD · 게임패드   R 처음부터   Esc 일시정지");
        }

        public override void EnterWindow()
        {
            base.EnterWindow();
            MDSGameDirector d = MDSGameDirector.Instance;
            d.DeathsChanged -= OnDeaths;
            d.DeathsChanged += OnDeaths;
            _shownDeaths = _shownCoins = _shownSeconds = -1;
        }

        public override void ExitWindow()
        {
            if (MDSGameDirector.Instance != null) MDSGameDirector.Instance.DeathsChanged -= OnDeaths;
            base.ExitWindow();
        }

        private void OnDeaths(int deaths)
        {
            if (deaths > 0) _punch = 1f;
        }

        private void OnClickPause()
        {
            MDSSession.Pause();
            UIManager.UIManager.Instance.PushUI(UI_TYPE.InGamePause);
        }

        private void Update()
        {
            MDSGameDirector d = MDSGameDirector.Instance;
            Simulation sim = d != null ? d.Sim : null;
            if (sim == null) return;

            if (sim.Deaths != _shownDeaths)
            {
                _shownDeaths = sim.Deaths;
                MDSUi.SetText(deathText, _shownDeaths.ToString());
            }
            if (deathText != null)
            {
                _punch = Mathf.MoveTowards(_punch, 0f, Time.unscaledDeltaTime * 4f);
                float s = 1f + (punchScale - 1f) * _punch * _punch;
                deathText.transform.localScale = new Vector3(s, s, 1f);
            }

            int coins = sim.CoinsCollected;
            if (coins != _shownCoins)
            {
                _shownCoins = coins;
                MDSUi.SetText(coinText, sim.CoinCount > 0 ? "동전 " + coins + "/" + sim.CoinCount : "동전 없음");
            }

            int seconds = sim.Tick / Fixed.TICKS_PER_SECOND;
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                MDSUi.SetText(timeText, (seconds / 60) + ":" + (seconds % 60).ToString("00"));
                PlayRequest r = d.Current;
                if (r != null) MDSUi.SetText(stageText, StageLabel(r));
                if (r != null && r.Kind == PlayKind.Multiplayer) RefreshRoster(d, sim);
                else MDSUi.SetActive(rosterText, false);
            }

            int cd = d.CountdownTicks;
            MDSUi.SetActive(countdownText, cd > 0);
            if (cd > 0) MDSUi.SetText(countdownText, ((cd + Fixed.TICKS_PER_SECOND - 1) / Fixed.TICKS_PER_SECOND).ToString());
        }

        private static string StageLabel(PlayRequest r)
        {
            switch (r.Kind)
            {
                case PlayKind.Campaign: return "월드 " + r.Map.World + "-" + r.Map.Stage + "  " + r.Map.Map.Name;
                case PlayKind.EditorTest: return "시험 플레이 — 깨면 업로드할 수 있어요";
                case PlayKind.Multiplayer: return ModeLabel(r.Mode) + "  " + r.Map.Title;
                default: return r.Map.Title;
            }
        }

        public static string ModeLabel(MatchMode m)
        {
            switch (m)
            {
                case MatchMode.Coop: return "협동";
                case MatchMode.Survival: return "생존";
                default: return "레이스";
            }
        }

        private void RefreshRoster(MDSGameDirector d, Simulation sim)
        {
            MDSMultiplayer mp = d.Multiplayer;
            MDSUi.SetActive(rosterText, true);
            _sb.Length = 0;
            _sb.Append("나  데스 ").Append(sim.Deaths).Append(sim.IsCleared ? "  골인!" : "");
            foreach (RemotePlayer rp in mp.Remotes.Values)
                _sb.Append('\n').Append(rp.Name).Append("  데스 ").Append(rp.Deaths).Append(rp.Finished ? "  골인!" : "");
            if (d.Current.Mode == MatchMode.Survival)
            {
                int left = Mathf.Max(0, Rules.SURVIVAL_TICKS - sim.Tick) / Fixed.TICKS_PER_SECOND;
                _sb.Append("\n남은 시간 ").Append(left / 60).Append(':').Append((left % 60).ToString("00"));
            }
            MDSUi.SetText(rosterText, _sb.ToString());
        }
    }
}
