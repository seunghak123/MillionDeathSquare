using System;
using System.Collections.Generic;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>에디터 도구 (숫자 키 1~0 + 목록 순서, 기획서 5.1 / 8.1)</summary>
    public enum EditorTool
    {
        Wall,
        Floor,
        Empty,
        Ice,
        ConveyorUp,
        ConveyorDown,
        ConveyorLeft,
        ConveyorRight,
        Vanishing,
        Start,
        Goal,
        Coin,
        Checkpoint,
        KeyRed,
        KeyGreen,
        KeyBlue,
        DoorRed,
        DoorGreen,
        DoorBlue,
        Obstacle,
        Eraser,
    }

    public enum EditResult
    {
        Changed,
        NoChange,
        OutOfBounds,
        Locked,
        LimitReached,
        NotAllowedHere,
        NoSelection,
    }

    /// <summary>
    /// 맵 에디터 모델 (기획서 5장, 엔진 독립).
    ///   - 칸 배치/지우기, 장애물 배치 + 경로 그리기(최대 8점, 왕복/반복/원, 속도, 출발 지연)
    ///   - 되돌리기/다시하기 (스냅샷, 드래그 한 번 = 한 단계)
    ///   - 업로드 게이트: 지금 맵을 직접 깬 입력 기록이 검증돼야 업로드 가능. 맵 내용이 바뀌면 즉시 초기화 (5.4)
    ///   - 사용 가능한 장치는 캠페인 해금(EditorDevice)으로 제한
    /// </summary>
    public sealed class MapEditorModel
    {
        public const int UNDO_LIMIT = 200;

        private MapData _map;
        private readonly List<byte[]> _undo = new List<byte[]>();
        private readonly List<byte[]> _redo = new List<byte[]>();
        private byte[] _strokeBefore;
        private bool _strokeChanged;
        private ClearProof _proof;

        public EditorDevice Devices;
        public EditorTool CurrentTool = EditorTool.Wall;
        /// <summary>선택된 장애물 (-1 = 없음)</summary>
        public int SelectedObstacle = -1;
        /// <summary>새 사라지는 바닥의 위상 0~3</summary>
        public int VanishPhase;

        /// <summary>맵 내용이 바뀔 때마다 (화면 갱신용)</summary>
        public event Action Changed;

        public MapEditorModel(MapData map, EditorDevice devices)
        {
            _map = map ?? MapData.CreateBlank(24, 14);
            Devices = devices;
        }

        public MapData Map { get { return _map; } }
        public bool CanUndo { get { return _undo.Count > 0; } }
        public bool CanRedo { get { return _redo.Count > 0; } }
        public int UndoCount { get { return _undo.Count; } }

        // ── 도구 ──

        public static EditorDevice RequiredDevice(EditorTool tool)
        {
            switch (tool)
            {
                case EditorTool.Ice:
                case EditorTool.ConveyorUp:
                case EditorTool.ConveyorDown:
                case EditorTool.ConveyorLeft:
                case EditorTool.ConveyorRight:
                    return EditorDevice.IceConveyor;
                case EditorTool.Vanishing:
                    return EditorDevice.Vanishing;
                case EditorTool.KeyRed:
                case EditorTool.KeyGreen:
                case EditorTool.KeyBlue:
                case EditorTool.DoorRed:
                case EditorTool.DoorGreen:
                case EditorTool.DoorBlue:
                    return EditorDevice.KeysDoors;
                case EditorTool.Obstacle:
                    return EditorDevice.PathObstacle;
                default:
                    return EditorDevice.None;
            }
        }

        public bool IsToolUnlocked(EditorTool tool)
        {
            EditorDevice need = RequiredDevice(tool);
            return need == EditorDevice.None || (Devices & need) == need;
        }

        public bool IsModeUnlocked(PathMode mode)
        {
            if (mode == PathMode.Circle) return (Devices & EditorDevice.CircleObstacle) != 0;
            return (Devices & EditorDevice.PathObstacle) != 0;
        }

        // ── 드래그 묶음 ──

        /// <summary>마우스를 누를 때. 놓을 때(EndStroke)까지의 변경이 되돌리기 한 단계가 된다.</summary>
        public void BeginStroke()
        {
            if (_strokeBefore != null) EndStroke();
            _strokeBefore = MapSerializer.Serialize(_map);
            _strokeChanged = false;
        }

        public void EndStroke()
        {
            if (_strokeBefore == null) return;
            if (_strokeChanged) PushUndo(_strokeBefore);
            _strokeBefore = null;
            _strokeChanged = false;
        }

        private void PushUndo(byte[] snapshot)
        {
            _undo.Add(snapshot);
            if (_undo.Count > UNDO_LIMIT) _undo.RemoveAt(0);
            _redo.Clear();
        }

        /// <summary>변경 하나를 기록 (드래그 중이면 묶음에 포함)</summary>
        private void Edit(Action change)
        {
            if (_strokeBefore != null)
            {
                change();
                _strokeChanged = true;
            }
            else
            {
                byte[] before = MapSerializer.Serialize(_map);
                change();
                PushUndo(before);
            }
            OnContentChanged();
        }

        private void OnContentChanged()
        {
            _proof = null; // 업로드 게이트 초기화 (기획서 5.4)
            if (Changed != null) Changed();
        }

        public bool Undo()
        {
            EndStroke();
            if (_undo.Count == 0) return false;
            _redo.Add(MapSerializer.Serialize(_map));
            byte[] snap = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            _map = MapSerializer.Deserialize(snap);
            ClampSelection();
            OnContentChanged();
            return true;
        }

        public bool Redo()
        {
            EndStroke();
            if (_redo.Count == 0) return false;
            _undo.Add(MapSerializer.Serialize(_map));
            byte[] snap = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            _map = MapSerializer.Deserialize(snap);
            ClampSelection();
            OnContentChanged();
            return true;
        }

        private void ClampSelection()
        {
            if (SelectedObstacle >= _map.Obstacles.Count) SelectedObstacle = _map.Obstacles.Count - 1;
        }

        // ── 칸 편집 ──

        /// <summary>현재 도구로 (x,y)에 적용</summary>
        public EditResult Apply(int x, int y) { return Apply(CurrentTool, x, y); }

        public EditResult Apply(EditorTool tool, int x, int y)
        {
            if (!_map.InBounds(x, y)) return EditResult.OutOfBounds;
            if (!IsToolUnlocked(tool)) return EditResult.Locked;
            var cell = new CellPos(x, y);
            switch (tool)
            {
                case EditorTool.Wall: return SetTerrain(x, y, TileType.Wall);
                case EditorTool.Floor: return SetTerrain(x, y, TileType.Floor);
                case EditorTool.Empty: return SetTerrain(x, y, TileType.Empty);
                case EditorTool.Ice: return SetTerrain(x, y, TileType.Ice);
                case EditorTool.ConveyorUp: return SetTerrain(x, y, TileType.ConveyorUp);
                case EditorTool.ConveyorDown: return SetTerrain(x, y, TileType.ConveyorDown);
                case EditorTool.ConveyorLeft: return SetTerrain(x, y, TileType.ConveyorLeft);
                case EditorTool.ConveyorRight: return SetTerrain(x, y, TileType.ConveyorRight);
                case EditorTool.Vanishing: return SetTerrain(x, y, TileType.Vanishing);
                case EditorTool.Start:
                    if (_map.Start == cell) return EditResult.NoChange;
                    if (!CanHoldItem(x, y)) return EditResult.NotAllowedHere;
                    Edit(() => { RemoveItemsAt(cell); _map.Start = cell; });
                    return EditResult.Changed;
                case EditorTool.Goal: return PlaceItem(cell, _map.Goals, Rules.MAX_GOALS);
                case EditorTool.Coin: return PlaceItem(cell, _map.Coins, Rules.MAX_COINS);
                case EditorTool.Checkpoint: return PlaceItem(cell, _map.Checkpoints, Rules.MAX_CHECKPOINTS);
                case EditorTool.KeyRed: return PlaceKey(cell, KeyColor.Red);
                case EditorTool.KeyGreen: return PlaceKey(cell, KeyColor.Green);
                case EditorTool.KeyBlue: return PlaceKey(cell, KeyColor.Blue);
                case EditorTool.DoorRed: return PlaceDoor(cell, KeyColor.Red);
                case EditorTool.DoorGreen: return PlaceDoor(cell, KeyColor.Green);
                case EditorTool.DoorBlue: return PlaceDoor(cell, KeyColor.Blue);
                case EditorTool.Obstacle: return PlaceObstacle(x, y);
                case EditorTool.Eraser: return Erase(x, y);
            }
            return EditResult.NoChange;
        }

        private EditResult SetTerrain(int x, int y, TileType t)
        {
            int phase = t == TileType.Vanishing ? (VanishPhase & 3) : 0;
            if (_map.GetTile(x, y) == t && _map.GetParam(x, y) == phase) return EditResult.NoChange;
            if (_map.Start.X == x && _map.Start.Y == y && (!MapData.IsWalkable(t) || t == TileType.Vanishing)) return EditResult.NotAllowedHere;
            Edit(() =>
            {
                _map.SetTile(x, y, t);
                if (t == TileType.Vanishing) _map.SetParam(x, y, phase);
                // 벽/빈칸/사라지는 바닥이 되면 그 칸의 아이템은 함께 지운다 (시작점 제외)
                if (!MapData.IsWalkable(t) || t == TileType.Vanishing) RemoveItemsAt(new CellPos(x, y));
            });
            return EditResult.Changed;
        }

        private bool CanHoldItem(int x, int y)
        {
            TileType t = _map.GetTile(x, y);
            return MapData.IsWalkable(t) && t != TileType.Vanishing;
        }

        private bool HasItemAt(CellPos c)
        {
            if (_map.Start == c) return true;
            if (_map.Goals.Contains(c) || _map.Coins.Contains(c) || _map.Checkpoints.Contains(c)) return true;
            for (int i = 0; i < _map.Keys.Count; i++) if (_map.Keys[i].Cell == c) return true;
            for (int i = 0; i < _map.Doors.Count; i++) if (_map.Doors[i].Cell == c) return true;
            return false;
        }

        /// <summary>칸의 아이템 제거 (시작점은 옮길 수만 있고 지울 수 없음)</summary>
        private bool RemoveItemsAt(CellPos c)
        {
            bool any = false;
            any |= _map.Goals.Remove(c);
            any |= _map.Coins.Remove(c);
            any |= _map.Checkpoints.Remove(c);
            for (int i = _map.Keys.Count - 1; i >= 0; i--) if (_map.Keys[i].Cell == c) { _map.Keys.RemoveAt(i); any = true; }
            for (int i = _map.Doors.Count - 1; i >= 0; i--) if (_map.Doors[i].Cell == c) { _map.Doors.RemoveAt(i); any = true; }
            return any;
        }

        private EditResult PlaceItem(CellPos c, List<CellPos> list, int limit)
        {
            if (list.Contains(c)) return EditResult.NoChange;
            if (!CanHoldItem(c.X, c.Y) || _map.Start == c) return EditResult.NotAllowedHere;
            if (list.Count >= limit) return EditResult.LimitReached;
            Edit(() => { RemoveItemsAt(c); list.Add(c); });
            return EditResult.Changed;
        }

        private EditResult PlaceKey(CellPos c, KeyColor color)
        {
            for (int i = 0; i < _map.Keys.Count; i++) if (_map.Keys[i].Cell == c && _map.Keys[i].Color == color) return EditResult.NoChange;
            if (!CanHoldItem(c.X, c.Y) || _map.Start == c) return EditResult.NotAllowedHere;
            if (_map.Keys.Count >= Rules.MAX_KEYS) return EditResult.LimitReached;
            Edit(() => { RemoveItemsAt(c); _map.Keys.Add(new KeyItem(c, color)); });
            return EditResult.Changed;
        }

        private EditResult PlaceDoor(CellPos c, KeyColor color)
        {
            for (int i = 0; i < _map.Doors.Count; i++) if (_map.Doors[i].Cell == c && _map.Doors[i].Color == color) return EditResult.NoChange;
            if (!CanHoldItem(c.X, c.Y) || _map.Start == c) return EditResult.NotAllowedHere;
            if (_map.Doors.Count >= Rules.MAX_DOORS) return EditResult.LimitReached;
            Edit(() => { RemoveItemsAt(c); _map.Doors.Add(new DoorItem(c, color)); });
            return EditResult.Changed;
        }

        /// <summary>지우개: 아이템이 있으면 아이템, 장애물 경로 시작점이면 장애물, 아니면 바닥으로</summary>
        public EditResult Erase(int x, int y)
        {
            if (!_map.InBounds(x, y)) return EditResult.OutOfBounds;
            var c = new CellPos(x, y);
            if (HasItemAt(c) && _map.Start != c)
            {
                Edit(() => RemoveItemsAt(c));
                return EditResult.Changed;
            }
            int obs = FindObstacleAt(x, y);
            if (obs >= 0) return DeleteObstacle(obs);
            if (_map.Start == c) return EditResult.NotAllowedHere;
            if (_map.GetTile(x, y) == TileType.Floor) return EditResult.NoChange;
            return SetTerrain(x, y, TileType.Floor);
        }

        // ── 장애물 ──

        /// <summary>칸 (x,y)에 첫 지점이 있는 장애물 번호 (-1 = 없음)</summary>
        public int FindObstacleAt(int x, int y)
        {
            PathPoint p = PathPoint.CellCenter(x, y);
            for (int i = _map.Obstacles.Count - 1; i >= 0; i--)
            {
                PathPoint[] pts = _map.Obstacles[i].Points;
                if (pts.Length > 0 && pts[0].Equals(p)) return i;
            }
            return -1;
        }

        /// <summary>장애물 배치 (칸 중심에 멈춰 있는 왕복 장애물). 이미 있으면 선택만.</summary>
        public EditResult PlaceObstacle(int x, int y)
        {
            if (!_map.InBounds(x, y)) return EditResult.OutOfBounds;
            if (!IsToolUnlocked(EditorTool.Obstacle)) return EditResult.Locked;
            int existing = FindObstacleAt(x, y);
            if (existing >= 0) { SelectedObstacle = existing; return EditResult.NoChange; }
            if (_map.Obstacles.Count >= Rules.MAX_OBSTACLES) return EditResult.LimitReached;
            Edit(() =>
            {
                _map.Obstacles.Add(ObstacleDef.PingPong(2, 0, PathPoint.CellCenter(x, y)));
                SelectedObstacle = _map.Obstacles.Count - 1;
            });
            return EditResult.Changed;
        }

        private ObstacleDef Selected
        {
            get { return SelectedObstacle >= 0 && SelectedObstacle < _map.Obstacles.Count ? _map.Obstacles[SelectedObstacle] : null; }
        }

        /// <summary>선택한 장애물 경로에 지점 추가 (반 칸 단위, 최대 8개). 원 회전이면 중심 이동.</summary>
        public EditResult AddPathPoint(PathPoint p)
        {
            ObstacleDef o = Selected;
            if (o == null) return EditResult.NoSelection;
            if (p.HX < 0 || p.HY < 0 || p.HX > _map.Width * 2 || p.HY > _map.Height * 2) return EditResult.OutOfBounds;
            if (o.Mode == PathMode.Circle)
            {
                if (o.Points.Length == 1 && o.Points[0].Equals(p)) return EditResult.NoChange;
                Edit(() => o.Points = new[] { p });
                return EditResult.Changed;
            }
            if (o.Points.Length >= Rules.MAX_PATH_POINTS) return EditResult.LimitReached;
            if (o.Points.Length > 0 && o.Points[o.Points.Length - 1].Equals(p)) return EditResult.NoChange;
            Edit(() =>
            {
                var pts = new PathPoint[o.Points.Length + 1];
                Array.Copy(o.Points, pts, o.Points.Length);
                pts[pts.Length - 1] = p;
                o.Points = pts;
            });
            return EditResult.Changed;
        }

        public EditResult RemoveLastPathPoint()
        {
            ObstacleDef o = Selected;
            if (o == null) return EditResult.NoSelection;
            if (o.Points.Length <= 1) return EditResult.NoChange;
            Edit(() =>
            {
                var pts = new PathPoint[o.Points.Length - 1];
                Array.Copy(o.Points, pts, pts.Length);
                o.Points = pts;
            });
            return EditResult.Changed;
        }

        public EditResult SetPathMode(PathMode mode)
        {
            ObstacleDef o = Selected;
            if (o == null) return EditResult.NoSelection;
            if (!IsModeUnlocked(mode)) return EditResult.Locked;
            if (o.Mode == mode) return EditResult.NoChange;
            Edit(() =>
            {
                if (mode == PathMode.Circle && o.Points.Length > 1) o.Points = new[] { o.Points[0] };
                o.Mode = mode;
            });
            return EditResult.Changed;
        }

        public EditResult SetSpeed(int tier)
        {
            ObstacleDef o = Selected;
            if (o == null) return EditResult.NoSelection;
            tier = Fixed.Clamp(tier, 1, 3);
            if (o.SpeedTier == tier) return EditResult.NoChange;
            Edit(() => o.SpeedTier = tier);
            return EditResult.Changed;
        }

        public EditResult SetDelay(int tenths)
        {
            ObstacleDef o = Selected;
            if (o == null) return EditResult.NoSelection;
            tenths = Fixed.Clamp(tenths, 0, 255);
            if (o.DelayTenths == tenths) return EditResult.NoChange;
            Edit(() => o.DelayTenths = tenths);
            return EditResult.Changed;
        }

        public EditResult SetCircle(int radiusHalf, int startAngle, bool clockwise)
        {
            ObstacleDef o = Selected;
            if (o == null) return EditResult.NoSelection;
            if (o.Mode != PathMode.Circle) return EditResult.NotAllowedHere;
            radiusHalf = Fixed.Clamp(radiusHalf, 1, Rules.MAX_CIRCLE_RADIUS_HALF);
            startAngle = startAngle & 15;
            if (o.RadiusHalf == radiusHalf && o.StartAngle == startAngle && o.Clockwise == clockwise) return EditResult.NoChange;
            Edit(() => { o.RadiusHalf = radiusHalf; o.StartAngle = startAngle; o.Clockwise = clockwise; });
            return EditResult.Changed;
        }

        /// <summary>선택한 장애물을 같은 경로로 복제 (기차 만들기용, 출발 지연 +addDelay)</summary>
        public EditResult DuplicateSelected(int addDelayTenths)
        {
            ObstacleDef o = Selected;
            if (o == null) return EditResult.NoSelection;
            if (_map.Obstacles.Count >= Rules.MAX_OBSTACLES) return EditResult.LimitReached;
            Edit(() =>
            {
                ObstacleDef copy = o.Clone();
                copy.DelayTenths = Fixed.Clamp(o.DelayTenths + addDelayTenths, 0, 255);
                _map.Obstacles.Add(copy);
                SelectedObstacle = _map.Obstacles.Count - 1;
            });
            return EditResult.Changed;
        }

        public EditResult DeleteObstacle(int index)
        {
            if (index < 0 || index >= _map.Obstacles.Count) return EditResult.NoSelection;
            Edit(() =>
            {
                _map.Obstacles.RemoveAt(index);
                if (SelectedObstacle >= _map.Obstacles.Count) SelectedObstacle = _map.Obstacles.Count - 1;
            });
            return EditResult.Changed;
        }

        // ── 맵 속성 ──

        public EditResult SetPalette(int paletteId)
        {
            paletteId = Fixed.Clamp(paletteId, 0, Rules.PALETTE_COUNT - 1);
            if (_map.PaletteId == paletteId) return EditResult.NoChange;
            Edit(() => _map.PaletteId = paletteId);
            return EditResult.Changed;
        }

        /// <summary>
        /// 이름 변경 — 금지어/길이 검사. 다른 편집과 마찬가지로 업로드 게이트를 초기화한다 (어떤 편집이든 다시 깨야 업로드).
        /// 이름은 맵 내용 해시(리플레이 검증)에는 포함되지 않는다.
        /// </summary>
        public bool SetName(string name, out string error)
        {
            error = null;
            name = (name ?? "").Trim();
            if (name.Length > Rules.MAX_NAME_LENGTH) { error = "맵 이름은 " + Rules.MAX_NAME_LENGTH + "자 이하여야 합니다"; return false; }
            string reason;
            if (name.Length > 0 && !MapNameFilter.IsAllowed(name, out reason)) { error = "맵 이름에 쓸 수 없는 말이 있습니다"; return false; }
            if (_map.Name == name) return true;
            Edit(() => _map.Name = name);
            return true;
        }

        /// <summary>새 크기로 (안쪽 내용 유지, 밖으로 나간 것은 제거)</summary>
        public EditResult Resize(int width, int height)
        {
            width = Fixed.Clamp(width, Rules.MIN_SIZE, Rules.MAX_WIDTH);
            height = Fixed.Clamp(height, Rules.MIN_SIZE, Rules.MAX_HEIGHT);
            if (width == _map.Width && height == _map.Height) return EditResult.NoChange;
            Edit(() =>
            {
                MapData old = _map;
                var m = new MapData(width, height, TileType.Floor);
                m.Name = old.Name;
                m.PaletteId = old.PaletteId;
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        if (old.InBounds(x, y)) { m.SetTile(x, y, old.GetTile(x, y)); m.SetParam(x, y, old.GetParam(x, y)); }
                m.Start = new CellPos(Fixed.Clamp(old.Start.X, 0, width - 1), Fixed.Clamp(old.Start.Y, 0, height - 1));
                m.SetTile(m.Start.X, m.Start.Y, TileType.Floor);
                foreach (CellPos c in old.Goals) if (m.InBounds(c) && c != m.Start) m.Goals.Add(c);
                foreach (CellPos c in old.Coins) if (m.InBounds(c) && c != m.Start) m.Coins.Add(c);
                foreach (CellPos c in old.Checkpoints) if (m.InBounds(c) && c != m.Start) m.Checkpoints.Add(c);
                foreach (KeyItem k in old.Keys) if (m.InBounds(k.Cell) && k.Cell != m.Start) m.Keys.Add(k);
                foreach (DoorItem d in old.Doors) if (m.InBounds(d.Cell) && d.Cell != m.Start) m.Doors.Add(d);
                foreach (ObstacleDef o in old.Obstacles)
                {
                    bool inside = true;
                    foreach (PathPoint p in o.Points) if (p.HX > width * 2 || p.HY > height * 2) inside = false;
                    if (inside) m.Obstacles.Add(o.Clone());
                }
                _map = m;
                ClampSelection();
            });
            return EditResult.Changed;
        }

        public ValidationResult Validate() { return MapValidator.Validate(_map); }

        // ── 시험 플레이 / 업로드 게이트 (기획서 5.4) ──

        /// <summary>직접 클리어 증명: 입력 기록 + 그때의 맵 해시 + 결과</summary>
        public sealed class ClearProof
        {
            public uint MapHash;
            public InputRecording Recording;
            public int Deaths;
            public int ClearTicks;
        }

        /// <summary>시험 플레이용 맵 복사본 (편집과 분리)</summary>
        public MapData CreateTestPlayMap() { return _map.Clone(); }

        /// <summary>
        /// 시험 플레이 결과 제출. 지금 맵에서 재생 검증이 통과하면 업로드가 열린다.
        /// 반환: 검증 결과 (Valid가 아니면 업로드는 계속 잠김)
        /// </summary>
        public ReplayResult SubmitTestPlay(InputRecording recording)
        {
            ReplayResult r = ReplayVerifier.Run(_map, recording);
            if (r.IsValid)
            {
                _proof = new ClearProof { MapHash = _map.ContentHash(), Recording = recording, Deaths = r.Deaths, ClearTicks = r.ClearTick };
            }
            return r;
        }

        /// <summary>업로드 가능 = 지금 맵을 직접 깬 증명이 있고 규칙 검사를 통과</summary>
        public bool CanUpload
        {
            get { return _proof != null && _proof.MapHash == _map.ContentHash() && Validate().IsValid; }
        }

        /// <summary>업로드에 함께 공개되는 만든 사람의 클리어 기록 (없으면 null)</summary>
        public ClearProof CreatorClear { get { return CanUpload ? _proof : null; } }

        /// <summary>다른 맵을 열기 (되돌리기 기록·업로드 증명 초기화)</summary>
        public void Load(MapData map)
        {
            _map = map ?? MapData.CreateBlank(24, 14);
            _undo.Clear();
            _redo.Clear();
            _strokeBefore = null;
            SelectedObstacle = -1;
            _proof = null;
            if (Changed != null) Changed();
        }
    }
}
