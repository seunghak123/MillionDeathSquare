using UnityEngine;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 테스트 씬(MillionDeathSquareTest.unity) 전용 — UI 프레임워크 없이 보드만으로 캠페인을 바로 플레이.
    /// N/B = 다음/이전 스테이지, F2 = 모든 월드 열기(개발용), 클리어하면 잠시 뒤 다음 스테이지. 화면 표시는 OnGUI(개발용).
    /// </summary>
    public sealed class MDSTestBootstrap : MonoBehaviour
    {
        [SerializeField] private int startWorld = 1;
        [SerializeField] private int startStage = 1;

        private int _world, _stage;
        private float _nextTimer = -1f;
        private GUIStyle _big;

        private void Start()
        {
            _world = startWorld;
            _stage = startStage;
            MDSGameDirector d = MDSGameDirector.Instance;
            d.UseUiFramework = false;
            d.Finished += r => _nextTimer = 1.5f;
            Play();
        }

        private void Play()
        {
            MDSGameDirector d = MDSGameDirector.Instance;
            d.StartPlay(PlayRequest.Campaign(MDSMapResolver.ResolveCampaign(_world, _stage, d.CampaignAsset)));
        }

        private void Step(int delta)
        {
            int index = (_world - 1) * Rules.STAGES_PER_WORLD + (_stage - 1) + delta;
            int total = Rules.WORLD_COUNT * Rules.STAGES_PER_WORLD;
            index = ((index % total) + total) % total;
            _world = index / Rules.STAGES_PER_WORLD + 1;
            _stage = index % Rules.STAGES_PER_WORLD + 1;
            _nextTimer = -1f;
            Play();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.N)) Step(1);
            if (Input.GetKeyDown(KeyCode.B)) Step(-1);
            if (Input.GetKeyDown(KeyCode.F2)) { MDSSaveStore.Campaign.UnlockAll = true; MDSSaveStore.SaveCampaign(); }
            if (_nextTimer > 0f)
            {
                _nextTimer -= Time.unscaledDeltaTime;
                if (_nextTimer <= 0f) Step(1);
            }
        }

        private void OnGUI()
        {
            Simulation sim = MDSGameDirector.Instance != null ? MDSGameDirector.Instance.Sim : null;
            if (sim == null) return;
            if (_big == null)
            {
                _big = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
                _big.normal.textColor = Color.white;
            }
            GUI.Label(new Rect(0, 8, Screen.width, 60), sim.Deaths.ToString(), _big);
            GUI.Label(new Rect(12, 12, 600, 24), _world + "-" + _stage + "  " + sim.Map.Name + "   동전 " + sim.CoinsCollected + "/" + sim.CoinCount
                + "   " + MDSUi.FormatTicks(sim.Tick) + (sim.IsCleared ? "   클리어!" : ""));
            GUI.Label(new Rect(12, Screen.height - 30, 900, 24), "방향키·WASD 이동 · R 처음부터 · N/B 다음/이전 스테이지 · F2 모든 월드 열기");
        }
    }
}
