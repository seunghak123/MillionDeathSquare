using System.Collections.Generic;
using UnityEngine;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 보드 표시 (2D SpriteRenderer + 직교 카메라). 맵 한 화면 전체가 보이게 카메라를 맞춘다 (기획서 5.3).
    /// 렌더러는 처음 한 번 만들고 다시 쓴다 (맵이 바뀌어도 새로 할당하지 않음 — 칸 최대 32×18).
    /// 좌표: 맵 단위(1칸 = Fixed.CELL) → 월드 1칸 = 1유닛, 맵 가운데가 원점, y는 위가 +.
    /// </summary>
    public sealed class MDSBoardView : MonoBehaviour
    {
        public const int SORT_TILE = 0;
        public const int SORT_TILE_DECO = 2;
        public const int SORT_ITEM = 10;
        public const int SORT_PATH = 20;
        public const int SORT_OBSTACLE = 30;
        public const int SORT_PLAYER = 40;
        public const int SORT_EFFECT = 60;

        [SerializeField] private MDSPaletteAsset palettes;
        [SerializeField] private MDSSpriteLibrary sprites;
        [SerializeField] private Camera boardCamera;
        [Tooltip("위쪽 HUD(데스 카운터)가 가리는 높이 (칸)")]
        [SerializeField] private float topMarginCells = 1.6f;
        [SerializeField] private float sideMarginCells = 0.6f;
        [Tooltip("에디터 화면에서 왼쪽 도구 패널이 가리는 너비 (칸)")]
        [SerializeField] private float editorLeftMarginCells = 5f;

        private MDSSpriteLibrary _lib;
        private MapData _map;
        private MDSPaletteColors _pal;
        private Transform _tileRoot, _itemRoot, _obstacleRoot, _actorRoot, _fxRoot;
        private readonly List<SpriteRenderer> _tiles = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _tileDeco = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _coins = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _keys = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _doors = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _goals = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _goalGlows = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _checkpoints = new List<SpriteRenderer>();
        private readonly List<MDSObstacleView> _obstacles = new List<MDSObstacleView>();
        private readonly List<MDSPlayerView> _ghosts = new List<MDSPlayerView>();
        private readonly List<LineRenderer> _paths = new List<LineRenderer>();
        private SpriteRenderer _startMarker;
        private MDSPlayerView _player;
        private MDSEffects _effects;
        private Material _lineMaterial;
        private int[] _prevObsX = new int[0], _prevObsY = new int[0];
        private bool _editorLayout;

        public MDSPlayerView Player { get { return _player; } }
        public MDSEffects Effects { get { return _effects; } }
        public Camera BoardCamera { get { return boardCamera != null ? boardCamera : Camera.main; } }
        public MapData Map { get { return _map; } }
        public MDSPaletteColors Palette { get { return _pal; } }
        public MDSSpriteLibrary Sprites { get { EnsureInit(); return _lib; } }

        private void EnsureInit()
        {
            if (_lib != null) return;
            _lib = MDSSpriteLibrary.ResolveOrCreate(sprites);
            _tileRoot = NewRoot("Tiles");
            _itemRoot = NewRoot("Items");
            _obstacleRoot = NewRoot("Obstacles");
            _actorRoot = NewRoot("Actors");
            _fxRoot = NewRoot("Effects");
            _player = MDSPlayerView.Create(_actorRoot, _lib, Color.red, Color.black, Rules.PLAYER_HALF * 2f / Fixed.CELL, SORT_PLAYER + 5, false);
            _effects = new GameObject("FX").AddComponent<MDSEffects>();
            _effects.transform.SetParent(_fxRoot, false);
            _effects.Init(_lib, BoardCamera, SORT_EFFECT);
            _startMarker = NewSprite(_itemRoot, _lib.ring, SORT_ITEM);
            _lineMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        private Transform NewRoot(string n)
        {
            var t = new GameObject(n).transform;
            t.SetParent(transform, false);
            return t;
        }

        private static SpriteRenderer NewSprite(Transform parent, Sprite sprite, int order)
        {
            var sr = new GameObject("S").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        private static SpriteRenderer Pooled(List<SpriteRenderer> pool, int index, Transform parent, Sprite sprite, int order)
        {
            while (pool.Count <= index) pool.Add(NewSprite(parent, sprite, order));
            SpriteRenderer sr = pool[index];
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.gameObject.SetActive(true);
            return sr;
        }

        private static void HideFrom(List<SpriteRenderer> pool, int count)
        {
            for (int i = count; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }

        // ── 좌표 ──

        /// <summary>맵 단위 → 월드 좌표</summary>
        public Vector3 UnitToWorld(float ux, float uy)
        {
            if (_map == null) return Vector3.zero;
            return new Vector3(ux / Fixed.CELL - _map.Width * 0.5f, _map.Height * 0.5f - uy / Fixed.CELL, 0f);
        }

        public Vector3 CellToWorld(int cx, int cy) { return UnitToWorld(Fixed.CellCenter(cx), Fixed.CellCenter(cy)); }

        /// <summary>월드 좌표 → 칸 (맵 밖이면 false)</summary>
        public bool WorldToCell(Vector3 world, out int cx, out int cy)
        {
            cx = cy = -1;
            if (_map == null) return false;
            float fx = world.x + _map.Width * 0.5f;
            float fy = _map.Height * 0.5f - world.y;
            cx = Mathf.FloorToInt(fx);
            cy = Mathf.FloorToInt(fy);
            return cx >= 0 && cy >= 0 && cx < _map.Width && cy < _map.Height;
        }

        /// <summary>월드 좌표 → 가장 가까운 반 칸 격자점 (장애물 경로 점 찍기)</summary>
        public PathPoint WorldToHalfPoint(Vector3 world)
        {
            float fx = (world.x + _map.Width * 0.5f) * 2f;
            float fy = (_map.Height * 0.5f - world.y) * 2f;
            return new PathPoint(Mathf.Clamp(Mathf.RoundToInt(fx), 0, _map.Width * 2), Mathf.Clamp(Mathf.RoundToInt(fy), 0, _map.Height * 2));
        }

        public Vector3 HalfPointToWorld(PathPoint p) { return UnitToWorld(p.UnitX, p.UnitY); }

        // ── 맵 표시 ──

        /// <summary>맵을 보드에 그린다 (편집 후 다시 부르면 갱신). editorLayout이면 왼쪽 도구 패널 자리를 비운다.</summary>
        public void Build(MapData map, bool editorLayout)
        {
            EnsureInit();
            _map = map;
            _editorLayout = editorLayout;
            _pal = palettes != null ? palettes.Get(map.PaletteId) : MDSPaletteColors.FromCore(Palettes.Get(map.PaletteId));

            int n = map.Width * map.Height;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    int i = y * map.Width + x;
                    SpriteRenderer sr = Pooled(_tiles, i, _tileRoot, _lib.square, SORT_TILE);
                    sr.transform.localPosition = CellToWorld(x, y);
                    sr.transform.localScale = Vector3.one;
                    sr.transform.localRotation = Quaternion.identity;
                    SpriteRenderer deco = Pooled(_tileDeco, i, _tileRoot, _lib.arrow, SORT_TILE_DECO);
                    deco.transform.localPosition = CellToWorld(x, y);
                    StyleTile(sr, deco, map.GetTile(x, y), x, y);
                }
            }
            HideFrom(_tiles, n);
            HideFrom(_tileDeco, n);

            for (int i = 0; i < map.Coins.Count; i++)
            {
                SpriteRenderer c = Pooled(_coins, i, _itemRoot, _lib.circle, SORT_ITEM + 2);
                c.transform.localPosition = CellToWorld(map.Coins[i].X, map.Coins[i].Y);
                c.transform.localScale = Vector3.one * (Rules.COIN_RADIUS * 2f / Fixed.CELL);
                c.color = _pal.coin;
            }
            HideFrom(_coins, map.Coins.Count);

            for (int i = 0; i < map.Keys.Count; i++)
            {
                SpriteRenderer k = Pooled(_keys, i, _itemRoot, _lib.key, SORT_ITEM + 2);
                k.transform.localPosition = CellToWorld(map.Keys[i].Cell.X, map.Keys[i].Cell.Y);
                k.transform.localScale = Vector3.one * 0.7f;
                k.color = _pal.Key(map.Keys[i].Color);
            }
            HideFrom(_keys, map.Keys.Count);

            for (int i = 0; i < map.Doors.Count; i++)
            {
                SpriteRenderer d = Pooled(_doors, i, _itemRoot, _lib.lockIcon, SORT_ITEM + 1);
                d.transform.localPosition = CellToWorld(map.Doors[i].Cell.X, map.Doors[i].Cell.Y);
                d.transform.localScale = Vector3.one * 0.95f;
                d.color = _pal.Key(map.Doors[i].Color);
            }
            HideFrom(_doors, map.Doors.Count);

            for (int i = 0; i < map.Goals.Count; i++)
            {
                SpriteRenderer g = Pooled(_goals, i, _itemRoot, _lib.roundedSquare, SORT_ITEM);
                g.transform.localPosition = CellToWorld(map.Goals[i].X, map.Goals[i].Y);
                g.transform.localScale = Vector3.one * 0.96f;
                g.color = _pal.goal;
                SpriteRenderer glow = Pooled(_goalGlows, i, _itemRoot, _lib.glow, SORT_ITEM - 1);
                glow.transform.localPosition = g.transform.localPosition;
                glow.transform.localScale = Vector3.one * 1.8f;
                Color gc = _pal.goal; gc.a = 0.6f;
                glow.color = gc;
            }
            HideFrom(_goals, map.Goals.Count);
            HideFrom(_goalGlows, map.Goals.Count);

            for (int i = 0; i < map.Checkpoints.Count; i++)
            {
                SpriteRenderer f = Pooled(_checkpoints, i, _itemRoot, _lib.flag, SORT_ITEM + 1);
                f.transform.localPosition = CellToWorld(map.Checkpoints[i].X, map.Checkpoints[i].Y);
                f.transform.localScale = Vector3.one * 0.8f;
                f.color = _pal.checkpoint;
            }
            HideFrom(_checkpoints, map.Checkpoints.Count);

            _startMarker.gameObject.SetActive(true);
            _startMarker.transform.localPosition = CellToWorld(map.Start.X, map.Start.Y);
            _startMarker.transform.localScale = Vector3.one * 0.85f;
            Color sc = _pal.player; sc.a = 0.35f;
            _startMarker.color = sc;

            float diameter = Rules.OBSTACLE_RADIUS * 2f / Fixed.CELL;
            while (_obstacles.Count < map.Obstacles.Count) _obstacles.Add(MDSObstacleView.Create(_obstacleRoot, _lib, diameter, SORT_OBSTACLE));
            for (int i = 0; i < _obstacles.Count; i++)
            {
                bool on = i < map.Obstacles.Count;
                _obstacles[i].gameObject.SetActive(on);
                if (on) _obstacles[i].SetColors(_pal.obstacle, _pal.eye);
            }
            if (_prevObsX.Length < map.Obstacles.Count)
            {
                _prevObsX = new int[Rules.MAX_OBSTACLES];
                _prevObsY = new int[Rules.MAX_OBSTACLES];
            }

            _player.gameObject.SetActive(true);
            _player.SetVisible(true);
            _player.SetColors(_pal.player, _pal.eye);
            _player.ResetPose();
            _player.transform.localPosition = CellToWorld(map.Start.X, map.Start.Y);
            for (int i = 0; i < _ghosts.Count; i++) _ghosts[i].gameObject.SetActive(false);
            ClearPaths();
            FitCamera();
        }

        private void StyleTile(SpriteRenderer sr, SpriteRenderer deco, TileType t, int x, int y)
        {
            deco.gameObject.SetActive(false);
            deco.transform.localRotation = Quaternion.identity;
            deco.transform.localScale = Vector3.one;
            switch (t)
            {
                case TileType.Empty:
                    sr.color = _pal.background;
                    break;
                case TileType.Wall:
                    sr.color = _pal.wall;
                    break;
                case TileType.Ice:
                    sr.color = _pal.ice;
                    deco.sprite = _lib.iceShine;
                    deco.color = new Color(1f, 1f, 1f, 0.7f);
                    deco.gameObject.SetActive(true);
                    break;
                case TileType.ConveyorUp:
                case TileType.ConveyorDown:
                case TileType.ConveyorLeft:
                case TileType.ConveyorRight:
                    sr.color = _pal.conveyor;
                    deco.sprite = _lib.arrow;
                    Color ac = _pal.wall; ac.a = 0.8f;
                    deco.color = ac;
                    deco.transform.localScale = Vector3.one * 0.7f;
                    deco.transform.localRotation = Quaternion.Euler(0f, 0f, ConveyorAngle(t));
                    deco.gameObject.SetActive(true);
                    break;
                case TileType.Vanishing:
                    sr.color = _pal.vanishing;
                    break;
                default:
                    sr.color = ((x + y) & 1) == 0 ? _pal.floor : _pal.floorAlt;
                    break;
            }
        }

        private static float ConveyorAngle(TileType t)
        {
            switch (t)
            {
                case TileType.ConveyorUp: return 90f;
                case TileType.ConveyorDown: return -90f;
                case TileType.ConveyorLeft: return 180f;
                default: return 0f;
            }
        }

        /// <summary>맵 전체가 한 화면에 보이도록 직교 카메라 크기/위치 조정</summary>
        public void FitCamera()
        {
            Camera cam = BoardCamera;
            if (cam == null || _map == null) return;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = _pal.background;
            float aspect = cam.aspect > 0.01f ? cam.aspect : 16f / 9f;
            float left = _editorLayout ? editorLeftMarginCells : sideMarginCells;
            float needW = _map.Width + left + sideMarginCells;
            float needH = _map.Height + topMarginCells + sideMarginCells;
            float size = Mathf.Max(needH * 0.5f, needW * 0.5f / aspect);
            cam.orthographicSize = size;
            // HUD/도구 패널 쪽으로 여백을 두기 위해 카메라를 비켜 놓는다
            float offsetX = -(left - sideMarginCells) * 0.5f;
            float offsetY = (topMarginCells - sideMarginCells) * 0.5f;
            cam.transform.position = new Vector3(transform.position.x + offsetX, transform.position.y + offsetY, -10f);
            cam.transform.rotation = Quaternion.identity;
            _effects.SetCamera(cam);
        }

        // ── 매 프레임 ──

        /// <summary>플레이 중 표시 갱신 (alpha = 다음 틱까지 보간 비율 0..1)</summary>
        public void Render(Simulation sim, float alpha, int[] prevX, int[] prevY, float dt)
        {
            if (_map == null) return;
            int tick = sim.Tick;

            for (int i = 0; i < _map.Coins.Count && i < _coins.Count; i++)
            {
                bool show = !sim.IsCoinCollected(i);
                SpriteRenderer c = _coins[i];
                if (c.enabled != show) c.enabled = show;
                if (show)
                {
                    float s = (Rules.COIN_RADIUS * 2f / Fixed.CELL) * (1f + 0.08f * Mathf.Sin((tick + alpha) * 0.12f + i));
                    c.transform.localScale = new Vector3(s, s, 1f);
                }
            }
            for (int i = 0; i < _map.Keys.Count && i < _keys.Count; i++)
            {
                bool show = !sim.IsKeyCollected(i);
                if (_keys[i].enabled != show) _keys[i].enabled = show;
            }
            for (int i = 0; i < _map.Doors.Count && i < _doors.Count; i++)
            {
                bool open = sim.IsDoorOpen(_map.Doors[i].Color);
                Color dc = _pal.Key(_map.Doors[i].Color);
                dc.a = open ? 0.2f : 1f;
                _doors[i].color = dc;
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin((tick + alpha) * 0.08f);
            for (int i = 0; i < _map.Goals.Count && i < _goalGlows.Count; i++)
            {
                Color gc = _pal.goal;
                gc.a = sim.AllCoinsCollected ? 0.45f + 0.45f * pulse : 0.15f;
                _goalGlows[i].color = gc;
            }
            for (int i = 0; i < _map.Checkpoints.Count && i < _checkpoints.Count; i++)
            {
                Color fc = _pal.checkpoint;
                if (sim.ActiveCheckpoint == i) fc = Color.Lerp(_pal.checkpoint, _pal.goal, 0.6f);
                _checkpoints[i].color = fc;
            }
            RenderVanishing(tick);

            for (int i = 0; i < _map.Obstacles.Count && i < _obstacles.Count; i++)
            {
                float ox = Mathf.Lerp(prevX[i], sim.ObstacleX(i), alpha);
                float oy = Mathf.Lerp(prevY[i], sim.ObstacleY(i), alpha);
                var dir = new Vector2(sim.ObstacleX(i) - prevX[i], -(sim.ObstacleY(i) - prevY[i]));
                _obstacles[i].Tick(UnitToWorld(ox, oy), dir);
            }
        }

        /// <summary>장애물만 틱 위치로 (에디터 미리보기 / 대기 화면)</summary>
        public void RenderObstaclesAtTick(int tick)
        {
            if (_map == null) return;
            for (int i = 0; i < _map.Obstacles.Count && i < _obstacles.Count; i++)
            {
                var m = new ObstacleMotion(_map.Obstacles[i]);
                int x0, y0, x1, y1;
                m.Evaluate(tick > 0 ? tick - 1 : 0, out x0, out y0);
                m.Evaluate(tick, out x1, out y1);
                _obstacles[i].Tick(UnitToWorld(x1, y1), new Vector2(x1 - x0, -(y1 - y0)));
            }
            RenderVanishing(tick);
        }

        private void RenderVanishing(int tick)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                for (int x = 0; x < _map.Width; x++)
                {
                    if (_map.GetTile(x, y) != TileType.Vanishing) continue;
                    SpriteRenderer sr = _tiles[y * _map.Width + x];
                    Simulation.VanishState st = Simulation.VanishPhaseState(tick, _map.GetParam(x, y));
                    Color c = _pal.vanishing;
                    if (st == Simulation.VanishState.Gone) c = _pal.background;
                    else if (st == Simulation.VanishState.Warning && (tick / 4) % 2 == 0) c = Color.Lerp(_pal.vanishing, _pal.background, 0.6f);
                    sr.color = c;
                }
            }
        }

        // ── 멀티: 다른 플레이어 (반투명, 서로 통과) ──

        public MDSPlayerView GetGhost(int index)
        {
            EnsureInit();
            while (_ghosts.Count <= index)
                _ghosts.Add(MDSPlayerView.Create(_actorRoot, _lib, Color.white, Color.black, Rules.PLAYER_HALF * 2f / Fixed.CELL, SORT_PLAYER, true));
            _ghosts[index].gameObject.SetActive(true);
            return _ghosts[index];
        }

        public void HideGhosts()
        {
            for (int i = 0; i < _ghosts.Count; i++) _ghosts[i].gameObject.SetActive(false);
        }

        // ── 에디터: 장애물 경로 선 ──

        public void ClearPaths()
        {
            for (int i = 0; i < _paths.Count; i++) _paths[i].gameObject.SetActive(false);
        }

        /// <summary>장애물 경로 미리보기 (선택한 장애물은 진하게)</summary>
        public void DrawPaths(int selected)
        {
            if (_map == null) return;
            EnsureInit();
            for (int i = 0; i < _map.Obstacles.Count; i++)
            {
                while (_paths.Count <= i)
                {
                    var lr = new GameObject("Path").AddComponent<LineRenderer>();
                    lr.transform.SetParent(_itemRoot, false);
                    lr.material = _lineMaterial;
                    lr.useWorldSpace = false;
                    lr.sortingOrder = SORT_PATH;
                    lr.numCapVertices = 2;
                    _paths.Add(lr);
                }
                LineRenderer line = _paths[i];
                ObstacleDef o = _map.Obstacles[i];
                line.gameObject.SetActive(true);
                bool sel = i == selected;
                Color c = _pal.obstacle;
                c.a = sel ? 0.9f : 0.35f;
                line.startColor = c;
                line.endColor = c;
                line.widthMultiplier = sel ? 0.1f : 0.06f;
                if (o.Mode == PathMode.Circle && o.Points.Length > 0)
                {
                    const int seg = 48;
                    line.positionCount = seg + 1;
                    line.loop = false;
                    Vector3 center = HalfPointToWorld(o.Points[0]);
                    float r = o.RadiusHalf * 0.5f;
                    for (int k = 0; k <= seg; k++)
                    {
                        float a = k * Mathf.PI * 2f / seg;
                        line.SetPosition(k, center + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
                    }
                }
                else
                {
                    bool closed = o.Mode == PathMode.Loop && o.Points.Length > 1;
                    line.positionCount = o.Points.Length + (closed ? 1 : 0);
                    for (int k = 0; k < o.Points.Length; k++) line.SetPosition(k, HalfPointToWorld(o.Points[k]));
                    if (closed) line.SetPosition(o.Points.Length, HalfPointToWorld(o.Points[0]));
                }
            }
            for (int i = _map.Obstacles.Count; i < _paths.Count; i++) _paths[i].gameObject.SetActive(false);
        }

        /// <summary>플레이어 숨기기/보이기 (에디터에서는 시작점만 보이게)</summary>
        public void SetPlayerVisible(bool v) { EnsureInit(); _player.gameObject.SetActive(v); }

        public void Clear()
        {
            _map = null;
            HideFrom(_tiles, 0);
            HideFrom(_tileDeco, 0);
            HideFrom(_coins, 0);
            HideFrom(_keys, 0);
            HideFrom(_doors, 0);
            HideFrom(_goals, 0);
            HideFrom(_goalGlows, 0);
            HideFrom(_checkpoints, 0);
            for (int i = 0; i < _obstacles.Count; i++) _obstacles[i].gameObject.SetActive(false);
            HideGhosts();
            ClearPaths();
            if (_startMarker != null) _startMarker.gameObject.SetActive(false);
            if (_player != null) _player.gameObject.SetActive(false);
        }
    }
}
