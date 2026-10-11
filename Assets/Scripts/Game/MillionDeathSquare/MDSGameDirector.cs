using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Seunghak.Manager;
using Seunghak.Platform;
using Seunghak.UIManager;
using UnityEngine;

namespace Seunghak.Game.MillionDeathSquare
{
    public enum PlayKind
    {
        Campaign,
        /// <summary>맵 코드/공유 코드로 받은 유저 맵</summary>
        UserMap,
        /// <summary>에디터 시험 플레이 (클리어하면 업로드가 열린다)</summary>
        EditorTest,
        Multiplayer,
    }

    /// <summary>무엇을 플레이할지</summary>
    public sealed class PlayRequest
    {
        public PlayKind Kind;
        public ResolvedMap Map;
        /// <summary>에디터 시험 플레이 클리어 시 입력 기록을 받는 곳</summary>
        public Action<InputRecording> OnEditorClear;
        /// <summary>멀티 시작 카운트다운 (틱)</summary>
        public int CountdownTicks;
        public MatchMode Mode;

        public static PlayRequest Campaign(ResolvedMap m) { return new PlayRequest { Kind = PlayKind.Campaign, Map = m }; }
        public static PlayRequest User(ResolvedMap m) { return new PlayRequest { Kind = PlayKind.UserMap, Map = m }; }
    }

    /// <summary>판 결과 (결과 화면에 표시)</summary>
    public sealed class PlayResult
    {
        public PlayRequest Request;
        public bool Cleared;
        public int Deaths;
        public int Ticks;
        public bool ZeroDeath;
        public ClearOutcome CampaignOutcome;
        public StageRecord Best;
        public InputRecording Recording;
        /// <summary>멀티 결과 (순위, 각자 데스, 가장 많이 죽은 장소)</summary>
        public List<PlayerResult> Multiplayer;
        /// <summary>순위표 제출 상태 문구</summary>
        public string RecordStatus = "";
    }

    /// <summary>
    /// 판 흐름 + 고정 스텝 드라이버 (60틱/초, 표시 보간). 단일 씬의 보드(MDSBoardView)를 움직인다.
    ///   - 메뉴 → RequestPlay(요청) → InGameSceneController → BeginPendingPlay → StartPlay
    ///   - 에디터 시험 플레이는 씬 전환 없이 StartPlay + 플레이 창 Push (끝나면 Pop으로 에디터 복귀)
    ///   - 입력은 프레임마다 한 번 읽어 그 프레임의 모든 스텝에 쓰고, 스텝마다 InputRecording에 기록 (리플레이 검증용)
    /// UnitySingleton이므로 Awake/OnDestroy를 정의하지 않는다 (초기화는 InitSingleton).
    /// </summary>
    public sealed class MDSGameDirector : UnitySingleton<MDSGameDirector>, IMDSSessionControl
    {
        private const float STEP = 1f / Fixed.TICKS_PER_SECOND;
        private const int MAX_STEPS_PER_FRAME = 8;
        private const int POSITION_SEND_INTERVAL = 3; // 20Hz

        [SerializeField] private MDSBoardView board;
        [SerializeField] private MDSCampaignAsset campaignAsset;
        [Tooltip("클리어 후 결과 화면까지 (만세 연출)")]
        [SerializeField] private float resultDelay = 0.9f;
        [Tooltip("UIManager 화면(일시정지/결과 팝업)을 쓸지. 테스트 씬(MDSTestBootstrap)은 끈다")]
        [SerializeField] private bool useUiFramework = true;

        private PlayRequest _pending;
        private PlayRequest _current;
        private Simulation _sim;
        private InputRecording _recording;
        private float _acc;
        private bool _playing;
        private bool _paused;
        private bool _restartQueued;
        private bool _resultShown;
        private int _countdown;
        /// <summary>멀티: 카운트다운 뒤 흐른 판 시간 (내가 먼저 골인해도 계속 흐름 — 방장의 시간 제한 판정용)</summary>
        private int _matchTick;
        private int _prevPlayerX, _prevPlayerY;
        private readonly int[] _prevObsX = new int[Rules.MAX_OBSTACLES];
        private readonly int[] _prevObsY = new int[Rules.MAX_OBSTACLES];
        private MDSMultiplayer _multi;
        private UI_TYPE _returnTarget = UI_TYPE.None;
        private PlayResult _lastResult;
        private int _resumeFrame = -1;

        // ── 상태 (HUD가 매 프레임 읽음) ──

        public Simulation Sim { get { return _sim; } }
        public PlayRequest Current { get { return _current; } }
        public bool IsPlaying { get { return _playing; } }
        public bool IsPaused { get { return _paused; } }
        public int CurrentDeaths { get { return _sim != null ? _sim.Deaths : 0; } }
        public int CountdownTicks { get { return _countdown; } }
        public MDSBoardView Board { get { if (board == null) board = FindFirstObjectByType<MDSBoardView>(FindObjectsInactive.Include); return board; } }
        public MDSCampaignAsset CampaignAsset { get { return campaignAsset; } }
        public MDSMultiplayer Multiplayer { get { if (_multi == null) CreateMultiplayer(); return _multi; } }
        public PlayResult LastResult { get { return _lastResult; } }
        public bool UseUiFramework { get { return useUiFramework; } set { useUiFramework = value; } }

        /// <summary>데스 수가 바뀔 때 (HUD 펀치 연출)</summary>
        public event Action<int> DeathsChanged;
        public event Action<PlayResult> Finished;

        protected override void InitSingleton()
        {
            MDSSession.Control = this;
        }

        private void CreateMultiplayer()
        {
            _multi = new MDSMultiplayer();
            _multi.MatchStarting += OnMultiplayerStart;
            _multi.TeamCoin += i => { if (_sim != null) _sim.ApplyTeamCoin(i); };
            _multi.ResultsReady += OnMultiplayerResults;
            _multi.RemoteDied += rp =>
            {
                if (!_playing || Board == null) return;
                Board.Effects.DeathBurst(Board.UnitToWorld(rp.X, rp.Y), new Color(1f, 1f, 1f, 0.6f));
            };
        }

        // ── 진입 ──

        /// <summary>메뉴에서: 무엇을 플레이할지 정하고 인게임으로</summary>
        public void RequestPlay(PlayRequest request)
        {
            _pending = request;
            if (SoundManager.Instance != null) SoundManager.Instance.StopBGM();
            Seunghak.SceneManager.SceneManager.Instance.ChangeScene(E_SCENE_TYPE.INGAME);
        }

        /// <summary>InGameSceneController가 HUD를 띄운 뒤 호출</summary>
        public void BeginPendingPlay()
        {
            PlayRequest r = _pending;
            _pending = null;
            if (r == null) r = PlayRequest.Campaign(MDSMapResolver.ResolveCampaign(1, 1, campaignAsset));
            StartPlay(r);
        }

        /// <summary>바로 시작 (에디터 시험 플레이, 다시 하기, 다음 스테이지)</summary>
        public void StartPlay(PlayRequest r)
        {
            if (r == null || r.Map == null || !r.Map.Ok) { Debug.LogError("[MDSGameDirector] 맵 없이 시작할 수 없습니다"); return; }
            _current = r;
            _sim = new Simulation(r.Map.Map);
            _recording = new InputRecording(r.Map.Map.ContentHash(), 4096);
            _acc = 0f;
            _paused = false;
            _restartQueued = false;
            _resultShown = false;
            _countdown = r.Kind == PlayKind.Multiplayer ? r.CountdownTicks : 0;
            _matchTick = 0;
            _playing = true;
            PlatformSaveSync.GameplayActive = true;

            MDSBoardView b = Board;
            if (b != null)
            {
                b.Build(r.Map.Map, false);
                b.SetPlayerVisible(true);
                SnapshotPrev();
                b.Render(_sim, 0f, _prevObsX, _prevObsY, 0f);
            }
            PlayBgm(r);
            MDSAnalytics.StageStart(r.Map.RecordKey);
            if (r.Kind == PlayKind.UserMap && r.Map.WorkshopId != 0) MDSServices.Workshop.RecordAttemptAsync(r.Map.WorkshopId).Forget();
            if (DeathsChanged != null) DeathsChanged(0);
        }

        private static void PlayBgm(PlayRequest r)
        {
            if (SoundManager.Instance == null) return;
            string key;
            switch (r.Kind)
            {
                case PlayKind.EditorTest: key = SoundKeys.MDS_BGM_EDITOR; break;
                case PlayKind.Multiplayer: key = SoundKeys.MDS_BGM_MULTI; break;
                default: key = (r.Map.World & 1) == 0 ? SoundKeys.BGM_GAME2 : SoundKeys.BGM_GAME; break;
            }
            SoundManager.Instance.PlayBGM(key);
        }

        /// <summary>판 정리 (보드 비움)</summary>
        public void StopPlay()
        {
            _playing = false;
            _paused = false;
            _sim = null;
            PlatformSaveSync.GameplayActive = false;
            if (Board != null) Board.Clear();
        }

        /// <summary>메뉴로 돌아갈 때 이어서 열 창 (LobbySceneController가 꺼내 씀)</summary>
        public bool ConsumeReturnTarget(out UI_TYPE target)
        {
            target = _returnTarget;
            _returnTarget = UI_TYPE.None;
            return target != UI_TYPE.None;
        }

        public void SetReturnTarget(UI_TYPE target) { _returnTarget = target; }

        // ── IMDSSessionControl ──

        public void Pause()
        {
            if (!_playing || _current == null || _current.Kind == PlayKind.Multiplayer) return; // 멀티는 멈추지 않는다
            _paused = true;
        }

        public void Resume()
        {
            _paused = false;
            _acc = 0f;
            _resumeFrame = Time.frameCount; // 같은 프레임의 Esc로 다시 일시정지되지 않게
        }

        /// <summary>처음부터 (R 키와 같음 — 데스·시간은 유지, 기록도 이어짐)</summary>
        public void Restart()
        {
            _paused = false;
            _restartQueued = true;
        }

        public void ExitToMenu()
        {
            PlayRequest r = _current;
            if (_playing && _sim != null && !_sim.IsCleared && r != null)
            {
                if (r.Kind == PlayKind.Campaign)
                {
                    MDSSaveStore.Campaign.RecordDeaths(r.Map.World, r.Map.Stage, _sim.Deaths);
                    MDSSaveStore.SaveCampaign();
                }
                MDSAnalytics.StageQuit(r.Map.RecordKey, _sim.Deaths, _sim.Tick);
            }
            StopPlay();
            if (SoundManager.Instance != null) SoundManager.Instance.StopBGM();

            if (r != null && r.Kind == PlayKind.EditorTest)
            {
                // 에디터 창으로 (플레이 창 Pop → 에디터 RestoreWindow가 보드를 다시 그림)
                UIManager.UIManager.Instance.PopUI();
                return;
            }
            if (r != null)
            {
                switch (r.Kind)
                {
                    case PlayKind.Campaign: _returnTarget = UI_TYPE.MDSCampaignWindow; break;
                    case PlayKind.UserMap: _returnTarget = UI_TYPE.MDSMapFindWindow; break;
                    case PlayKind.Multiplayer: _returnTarget = UI_TYPE.MDSRoomWindow; break;
                }
            }
            Seunghak.SceneManager.SceneManager.Instance.ChangeScene(E_SCENE_TYPE.LOBBY);
        }

        /// <summary>결과 화면의 "다시 하기" — 새 판 (데스·시간 0부터)</summary>
        public void Retry()
        {
            if (_current == null) return;
            StartPlay(_current);
        }

        /// <summary>결과 화면의 "다음 스테이지"</summary>
        public bool PlayNextStage()
        {
            if (_current == null || _current.Kind != PlayKind.Campaign) return false;
            int w = _current.Map.World, s = _current.Map.Stage + 1;
            if (s > Rules.STAGES_PER_WORLD) { w++; s = 1; }
            if (!Campaign.IsValidStage(w, s) || !MDSSaveStore.Campaign.IsStageUnlocked(w, s)) return false;
            StartPlay(PlayRequest.Campaign(MDSMapResolver.ResolveCampaign(w, s, campaignAsset)));
            return true;
        }

        // ── 매 프레임 ──

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_multi != null) _multi.Update(dt);
            if (!_playing || _sim == null) return;

            if (useUiFramework && !_paused && Time.frameCount != _resumeFrame && MDSInput.PausePressed() && !_sim.IsCleared)
            {
                Pause();
                UIManager.UIManager.Instance.PushUI(UI_TYPE.InGamePause);
            }
            if (_paused) return;
            if (MDSInput.RestartPressed()) _restartQueued = true;

            InputBits input = MDSInput.ReadMove();
            _acc += Mathf.Min(dt, 0.25f);
            int steps = 0;
            while (_acc >= STEP && steps < MAX_STEPS_PER_FRAME)
            {
                _acc -= STEP;
                steps++;
                if (_countdown > 0) { _countdown--; continue; }
                _matchTick++;
                if (_sim.IsCleared)
                {
                    if (_current.Kind == PlayKind.Multiplayer) continue; // 판 시간은 계속 흐른다
                    break;
                }
                InputBits stepInput = input;
                if (_restartQueued) { stepInput |= InputBits.Restart; _restartQueued = false; }
                StepOnce(stepInput);
            }
            if (steps == MAX_STEPS_PER_FRAME) _acc = 0f; // 너무 밀리면 따라잡기 포기 (멈춤 방지)

            // 방장: 시간 제한/유예 판정 (내가 이미 골인했어도 진행)
            if (_current.Kind == PlayKind.Multiplayer && _multi != null && _multi.Session != null && _multi.Session.IsHost && _countdown <= 0)
                _multi.Session.HostAdvance(_matchTick);

            Render(_acc / STEP, dt);
        }

        private void SnapshotPrev()
        {
            _prevPlayerX = _sim.PlayerX;
            _prevPlayerY = _sim.PlayerY;
            int n = _sim.ObstacleCount;
            for (int i = 0; i < n; i++) { _prevObsX[i] = _sim.ObstacleX(i); _prevObsY[i] = _sim.ObstacleY(i); }
        }

        private void StepOnce(InputBits input)
        {
            SnapshotPrev();
            _recording.Add(input);
            _sim.Step(input);
            HandleEvents();

            if (_current.Kind == PlayKind.Multiplayer && _multi != null && _multi.Session != null)
            {
                RoomSession s = _multi.Session;
                if (_sim.Tick % POSITION_SEND_INTERVAL == 0 || _sim.IsCleared)
                {
                    int flags = (_sim.State == SimState.Dead ? 1 : 0) | (_sim.IsCleared ? 2 : 0);
                    s.SendPosition(_sim.Tick, _sim.PlayerX, _sim.PlayerY, flags);
                }
            }

            if (_sim.IsCleared) OnCleared();
        }

        private void HandleEvents()
        {
            MDSBoardView b = Board;
            if (b == null) return;
            MDSPaletteColors pal = b.Palette;
            for (int i = 0; i < _sim.EventCount; i++)
            {
                SimEvent e = _sim.GetEvent(i);
                Vector3 pos = b.UnitToWorld(e.X, e.Y);
                switch (e.Type)
                {
                    case SimEventType.Death:
                        b.Effects.DeathBurst(pos, pal.player);
                        b.Player.SetVisible(false);
                        PlaySfx(DeathSound());
                        if (DeathsChanged != null) DeathsChanged(_sim.Deaths);
                        if (MDSSettings.RumbleEnabled && PlatformFactory.Current != null) PlatformFactory.Current.Vibrate(1);
                        if (_current.Kind == PlayKind.Multiplayer && _multi != null && _multi.Session != null)
                            _multi.Session.ReportDeath(_sim.Tick, e.X, e.Y, _sim.Deaths);
                        break;
                    case SimEventType.Respawn:
                    case SimEventType.Restart:
                        b.Player.SetVisible(true);
                        b.Player.ResetPose();
                        _prevPlayerX = _sim.PlayerX; // 순간 이동 — 보간하지 않음
                        _prevPlayerY = _sim.PlayerY;
                        if (e.Type == SimEventType.Restart && _current.Kind == PlayKind.Multiplayer && _multi != null && _multi.Session != null)
                            _multi.Session.ReportRestart(_sim.Tick, _sim.PlayerX, _sim.PlayerY);
                        break;
                    case SimEventType.Coin:
                        b.Effects.CoinPop(pos, pal.coin);
                        b.Player.Happy(0.35f);
                        PlaySfx(SoundKeys.MDS_COIN);
                        if (_current.Kind == PlayKind.Multiplayer && _multi != null && _multi.Session != null)
                            _multi.Session.ReportCoin(_sim.Tick, e.Index);
                        break;
                    case SimEventType.Key:
                        b.Effects.CoinPop(pos, pal.Key(_current.Map.Map.Keys[e.Index].Color));
                        PlaySfx(SoundKeys.MDS_KEY);
                        break;
                    case SimEventType.DoorOpen:
                        PlaySfx(SoundKeys.MDS_DOOR_OPEN);
                        break;
                    case SimEventType.Checkpoint:
                        b.Effects.CheckpointSparkle(pos, pal.checkpoint);
                        b.Player.Happy(0.5f);
                        PlaySfx(SoundKeys.MDS_CHECKPOINT);
                        break;
                    case SimEventType.Bump:
                        b.Player.Bump(e.Index);
                        break;
                    case SimEventType.Clear:
                        b.Effects.GoalConfetti(pos, pal);
                        b.Player.Cheer(resultDelay + 1f);
                        PlaySfx(SoundKeys.MDS_GOAL);
                        break;
                }
            }
        }

        private static string DeathSound()
        {
            switch (UnityEngine.Random.Range(0, 3))
            {
                case 0: return SoundKeys.MDS_DEATH_1;
                case 1: return SoundKeys.MDS_DEATH_2;
                default: return SoundKeys.MDS_DEATH_3;
            }
        }

        private static void PlaySfx(string key)
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(key);
        }

        private void Render(float alpha, float dt)
        {
            MDSBoardView b = Board;
            if (b == null) return;
            b.Render(_sim, alpha, _prevObsX, _prevObsY, dt);

            float px = Mathf.Lerp(_prevPlayerX, _sim.PlayerX, alpha);
            float py = Mathf.Lerp(_prevPlayerY, _sim.PlayerY, alpha);
            var vel = new Vector2(_sim.PlayerX - _prevPlayerX, _sim.PlayerY - _prevPlayerY);
            b.Player.Tick(b.UnitToWorld(px, py), vel, NearestObstacleCells(), _sim.OnIce, dt);

            if (_current.Kind == PlayKind.Multiplayer && _multi != null) RenderGhosts(b);
        }

        /// <summary>가장 가까운 장애물까지 거리 (칸, 네모 가장자리 기준 대략)</summary>
        private float NearestObstacleCells()
        {
            long best = long.MaxValue;
            for (int i = 0; i < _sim.ObstacleCount; i++)
            {
                long dx = _sim.ObstacleX(i) - _sim.PlayerX;
                long dy = _sim.ObstacleY(i) - _sim.PlayerY;
                long d = dx * dx + dy * dy;
                if (d < best) best = d;
            }
            if (best == long.MaxValue) return 99f;
            return Mathf.Sqrt(best) / Fixed.CELL;
        }

        private void RenderGhosts(MDSBoardView b)
        {
            if (!MDSSettings.ShowGhosts) { b.HideGhosts(); return; }
            int i = 0;
            foreach (RemotePlayer rp in _multi.Remotes.Values)
            {
                MDSPlayerView g = b.GetGhost(i++);
                // 위치 메시지 간격(3틱) 동안 이전 → 최신으로 보간
                float t = Mathf.Clamp01((Time.unscaledTime - rp.ReceivedAt) / (POSITION_SEND_INTERVAL * STEP));
                float gx = Mathf.Lerp(rp.PrevX, rp.X, t);
                float gy = Mathf.Lerp(rp.PrevY, rp.Y, t);
                g.SetColors(SlotColor(rp.Slot, b.Palette), b.Palette.eye);
                g.SetAlpha(0.45f);
                g.SetVisible(!rp.Dead);
                g.Tick(b.UnitToWorld(gx, gy), new Vector2(rp.X - rp.PrevX, rp.Y - rp.PrevY), 99f, false, Time.unscaledDeltaTime);
            }
        }

        public static Color SlotColor(int slot, MDSPaletteColors pal)
        {
            switch (slot)
            {
                case 0: return pal.player;
                case 1: return pal.keyBlue;
                case 2: return pal.keyGreen;
                default: return pal.coin;
            }
        }

        // ── 클리어 ──

        private void OnCleared()
        {
            if (_resultShown) return;
            _resultShown = true;
            PlayRequest r = _current;
            var result = new PlayResult
            {
                Request = r,
                Cleared = true,
                Deaths = _sim.Deaths,
                Ticks = _sim.ClearTick,
                ZeroDeath = _sim.ZeroDeath,
                Recording = _recording,
            };
            MDSAnalytics.StageClear(r.Map.RecordKey, result.Deaths, result.Ticks);

            switch (r.Kind)
            {
                case PlayKind.Campaign:
                    result.CampaignOutcome = MDSSaveStore.Campaign.RecordClear(r.Map.World, r.Map.Stage, result.Deaths, result.Ticks);
                    result.Best = MDSSaveStore.Campaign.Get(r.Map.World, r.Map.Stage);
                    MDSSaveStore.SaveCampaign();
                    SubmitRecord(result).Forget();
                    break;
                case PlayKind.UserMap:
                    MDSSaveStore.RecordPlayed(r.Map.MapRef, r.Map.Title, true, result.Deaths);
                    SubmitRecord(result).Forget();
                    break;
                case PlayKind.EditorTest:
                    if (r.OnEditorClear != null) r.OnEditorClear(_recording);
                    break;
                case PlayKind.Multiplayer:
                    if (_multi != null && _multi.Session != null) _multi.Session.ReportFinish(result.Ticks, result.Deaths);
                    break;
            }
            PlatformSaveSync.GameplayActive = false;
            PlatformSaveSync.FlushNow();
            _lastResult = result;
            if (r.Kind != PlayKind.Multiplayer && useUiFramework) ShowResultLater(result).Forget();
            if (Finished != null) Finished(result);
        }

        private async UniTaskVoid SubmitRecord(PlayResult result)
        {
            result.RecordStatus = "기록 확인 중…";
            bool ok = await MDSServices.Leaderboards.SubmitAsync(result.Request.Map.RecordKey, result.Request.Map.Map, result.Recording, result.Deaths, result.Ticks);
            result.RecordStatus = ok ? (MDSServices.UsingSteam ? "Steam 순위표에 기록됨" : "기록 저장됨 (오프라인)") : "기록을 올리지 못했어요";
        }

        private async UniTaskVoid ShowResultLater(PlayResult result)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(resultDelay), DelayType.UnscaledDeltaTime);
            if (this == null || _lastResult != result) return;
            var popup = UIManager.UIManager.Instance.PushUI(UI_TYPE.MDSResultPopup) as MDSResultPopup;
            if (popup != null) popup.Show(result);
        }

        // ── 멀티 ──

        private void OnMultiplayerStart(ResolvedMap map, NetMessage start)
        {
            var req = new PlayRequest { Kind = PlayKind.Multiplayer, Map = map, CountdownTicks = start.Tick, Mode = start.Mode };
            _returnTarget = UI_TYPE.None;
            // 방 화면에서 플레이 화면으로 (씬 전환 없이 — 카운트다운이 이미 흐르고 있음)
            UIManager.UIManager.Instance.PushUI(UI_TYPE.MDSPlayWindow);
            StartPlay(req);
        }

        private void OnMultiplayerResults(List<PlayerResult> results)
        {
            var result = _lastResult != null && _lastResult.Request == _current && _current != null && _current.Kind == PlayKind.Multiplayer
                ? _lastResult
                : new PlayResult { Request = _current, Cleared = false, Deaths = _sim != null ? _sim.Deaths : 0, Ticks = _sim != null ? _sim.Tick : 0 };
            result.Multiplayer = results;
            _lastResult = result;
            _playing = false; // 판 종료 — 더 진행하지 않음 (보드는 마지막 모습 그대로)
            var popup = UIManager.UIManager.Instance.PushUI(UI_TYPE.MDSResultPopup) as MDSResultPopup;
            if (popup != null) popup.Show(result);
        }

        // ── 에디터 ──

        /// <summary>에디터 화면: 보드에 편집 중인 맵을 그린다 (시뮬레이션 없음)</summary>
        public void ShowEditorMap(MapData map, int selectedObstacle, int previewTick)
        {
            _playing = false;
            _sim = null;
            MDSBoardView b = Board;
            if (b == null) return;
            b.Build(map, true);
            b.SetPlayerVisible(false);
            b.DrawPaths(selectedObstacle);
            b.RenderObstaclesAtTick(previewTick);
        }

        /// <summary>에디터 → 시험 플레이 (클리어하면 onClear로 입력 기록 전달)</summary>
        public void StartEditorTest(MapData map, Action<InputRecording> onClear)
        {
            var resolved = new ResolvedMap { Map = map.Clone(), Title = string.IsNullOrEmpty(map.Name) ? "시험 플레이" : map.Name, MapRef = "editor", RecordKey = "editor" };
            UIManager.UIManager.Instance.PushUI(UI_TYPE.MDSPlayWindow);
            StartPlay(new PlayRequest { Kind = PlayKind.EditorTest, Map = resolved, OnEditorClear = onClear });
        }
    }
}
