using System;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>한 틱 입력 비트</summary>
    [Flags]
    public enum InputBits : byte
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 4,
        Right = 8,
        /// <summary>시작점으로 되돌아가기 (진행 초기화, 데스·시간 유지). 멀티방의 R 키</summary>
        Restart = 16,
    }

    public enum SimState : byte
    {
        Playing = 0,
        /// <summary>죽음 연출 중 (0.3초 뒤 체크포인트에서 재시작)</summary>
        Dead = 1,
        Cleared = 2,
    }

    public enum SimEventType : byte
    {
        Death,
        Respawn,
        Coin,
        Key,
        DoorOpen,
        Checkpoint,
        Clear,
        Restart,
        /// <summary>이동 중 벽에 막히기 시작함 (찌그러짐 연출용). Index: 0 = 가로, 1 = 세로</summary>
        Bump,
        /// <summary>얼음 위에서 미끄러지기 시작</summary>
        SlideStart,
    }

    public struct SimEvent
    {
        public SimEventType Type;
        /// <summary>동전/열쇠/체크포인트 번호, 문 색 등</summary>
        public int Index;
        public int X;
        public int Y;
    }

    /// <summary>
    /// 한 플레이어의 결정적 시뮬레이션 (60틱/초, 정수 연산). 기획서 4·8.1·13장 규칙.
    ///   - 이동: 방향키 축마다 초당 4칸 일정. 키를 떼면 즉시 정지 (얼음 위는 미끄러짐 유지)
    ///   - 컨베이어: 위에 있으면 초당 2칸으로 밀림 (입력 이동에 더해짐)
    ///   - 사라지는 바닥: 3초 주기(보임 2초, 사라짐 1초), 사라졌을 때 중심이 위에 있으면 죽음
    ///   - 벽/빈칸/닫힌 문은 막힘. 장애물(원)과 네모가 겹치면 죽음
    ///   - 동전 모두 + 골인 칸 도착 = 클리어. 체크포인트에 닿으면 그때까지 먹은 동전·열쇠가 저장됨
    ///   - 죽으면 0.3초 뒤 마지막 체크포인트(없으면 시작점)에서 재시작, 저장 안 된 동전·열쇠는 되돌림
    /// Step()은 할당 없이 동작한다 (이벤트는 고정 버퍼).
    /// </summary>
    public sealed class Simulation
    {
        private const byte ITEM_AVAILABLE = 0;
        private const byte ITEM_TAKEN = 1;   // 먹었지만 체크포인트에 저장 전
        private const byte ITEM_SAVED = 2;   // 체크포인트에 저장됨 (죽어도 유지)
        private const int MAX_EVENTS = 64;

        private readonly MapData _map;
        private readonly ObstacleMotion[] _motions;
        private readonly int[] _obsX;
        private readonly int[] _obsY;
        private readonly sbyte[] _doorColorAt;   // 칸별 문 색 (-1 = 문 없음)
        private readonly byte[] _coinState;
        private readonly byte[] _keyState;
        private readonly bool[] _colorOpen = new bool[Rules.KEY_COLORS];
        private readonly SimEvent[] _events = new SimEvent[MAX_EVENTS];
        private int _eventCount;

        private int _tick;
        private SimState _state;
        private int _x, _y;
        private int _velX, _velY;
        private int _deaths;
        private int _clearTick;
        private int _deadTimer;
        private int _checkpoint = -1;
        private int _respawnX, _respawnY;
        private bool _blockedX, _blockedY;
        private bool _onIce;

        public Simulation(MapData map)
        {
            if (map == null) throw new ArgumentNullException("map");
            _map = map;
            int n = map.Obstacles.Count;
            _motions = new ObstacleMotion[n];
            _obsX = new int[n];
            _obsY = new int[n];
            for (int i = 0; i < n; i++) _motions[i] = new ObstacleMotion(map.Obstacles[i]);
            _doorColorAt = new sbyte[map.Width * map.Height];
            for (int i = 0; i < _doorColorAt.Length; i++) _doorColorAt[i] = -1;
            for (int i = 0; i < map.Doors.Count; i++)
            {
                CellPos c = map.Doors[i].Cell;
                if (map.InBounds(c)) _doorColorAt[c.Y * map.Width + c.X] = (sbyte)map.Doors[i].Color;
            }
            _coinState = new byte[map.Coins.Count];
            _keyState = new byte[map.Keys.Count];
            Reset();
        }

        // ── 상태 조회 ──

        public MapData Map { get { return _map; } }
        /// <summary>지금까지 진행한 틱 수 (시작 = 0)</summary>
        public int Tick { get { return _tick; } }
        public SimState State { get { return _state; } }
        public bool IsCleared { get { return _state == SimState.Cleared; } }
        public int PlayerX { get { return _x; } }
        public int PlayerY { get { return _y; } }
        public int VelocityX { get { return _velX; } }
        public int VelocityY { get { return _velY; } }
        public bool OnIce { get { return _onIce; } }
        public int Deaths { get { return _deaths; } }
        /// <summary>클리어한 틱 (클리어 전 -1). 클리어 시간 = ClearTick / 60초</summary>
        public int ClearTick { get { return _state == SimState.Cleared ? _clearTick : -1; } }
        public bool ZeroDeath { get { return _deaths == 0; } }
        public int ActiveCheckpoint { get { return _checkpoint; } }
        public int RespawnTicksLeft { get { return _state == SimState.Dead ? _deadTimer : 0; } }
        public int ObstacleCount { get { return _motions.Length; } }
        public int ObstacleX(int i) { return _obsX[i]; }
        public int ObstacleY(int i) { return _obsY[i]; }
        public ObstacleMotion GetMotion(int i) { return _motions[i]; }

        public int CoinCount { get { return _coinState.Length; } }
        public bool IsCoinCollected(int i) { return _coinState[i] != ITEM_AVAILABLE; }
        public int CoinsCollected
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _coinState.Length; i++) if (_coinState[i] != ITEM_AVAILABLE) n++;
                return n;
            }
        }
        public bool AllCoinsCollected
        {
            get
            {
                for (int i = 0; i < _coinState.Length; i++) if (_coinState[i] == ITEM_AVAILABLE) return false;
                return true;
            }
        }
        public bool IsKeyCollected(int i) { return _keyState[i] != ITEM_AVAILABLE; }
        public bool IsDoorOpen(KeyColor color) { return _colorOpen[(int)color]; }

        public int EventCount { get { return _eventCount; } }
        public SimEvent GetEvent(int i) { return _events[i]; }

        // ── 진행 ──

        /// <summary>처음 상태로 (데스·시간까지 초기화)</summary>
        public void Reset()
        {
            _tick = 0;
            _deaths = 0;
            _clearTick = -1;
            _eventCount = 0;
            ResetProgressToStart();
            UpdateObstacles();
        }

        private void ResetProgressToStart()
        {
            for (int i = 0; i < _coinState.Length; i++) _coinState[i] = ITEM_AVAILABLE;
            for (int i = 0; i < _keyState.Length; i++) _keyState[i] = ITEM_AVAILABLE;
            RecomputeDoors();
            _checkpoint = -1;
            _respawnX = _map.Start.CenterX;
            _respawnY = _map.Start.CenterY;
            PlaceAtRespawn();
            _state = SimState.Playing;
        }

        private void PlaceAtRespawn()
        {
            _x = _respawnX;
            _y = _respawnY;
            _velX = 0;
            _velY = 0;
            _blockedX = false;
            _blockedY = false;
            _onIce = false;
            _deadTimer = 0;
        }

        /// <summary>한 틱 진행. 클리어 후에는 아무것도 하지 않는다.</summary>
        public void Step(InputBits input)
        {
            _eventCount = 0;
            if (_state == SimState.Cleared) return;

            _tick++;
            UpdateObstacles();

            if (_state == SimState.Dead)
            {
                _deadTimer--;
                if (_deadTimer <= 0) Respawn();
                return;
            }

            if ((input & InputBits.Restart) != 0)
            {
                ResetProgressToStart();
                AddEvent(SimEventType.Restart, 0, _x, _y);
                return;
            }

            Move(input);
            CollectItems();
            CheckCheckpoints();

            if (IsOnVanishedFloor() || TouchesObstacle())
            {
                Die();
                return;
            }

            if (AllCoinsCollected && IsOnGoal())
            {
                _state = SimState.Cleared;
                _clearTick = _tick;
                AddEvent(SimEventType.Clear, 0, _x, _y);
            }
        }

        /// <summary>협동 모드: 팀원이 먹은 동전을 내 판에도 반영 (저장된 상태로)</summary>
        public void ApplyTeamCoin(int index)
        {
            if (index < 0 || index >= _coinState.Length) return;
            _coinState[index] = ITEM_SAVED;
        }

        private void UpdateObstacles()
        {
            for (int i = 0; i < _motions.Length; i++) _motions[i].Evaluate(_tick, out _obsX[i], out _obsY[i]);
        }

        private void Move(InputBits input)
        {
            int inX = 0, inY = 0;
            if ((input & InputBits.Left) != 0) inX -= 1;
            if ((input & InputBits.Right) != 0) inX += 1;
            if ((input & InputBits.Up) != 0) inY -= 1;
            if ((input & InputBits.Down) != 0) inY += 1;

            TileType under = _map.GetTile(Fixed.CellOf(_x), Fixed.CellOf(_y));
            bool ice = under == TileType.Ice;
            if (ice)
            {
                // 얼음: 멈춰 있을 때만 새 방향으로 출발, 그 뒤엔 벽에 막힐 때까지 미끄러짐
                if (_velX == 0 && _velY == 0)
                {
                    _velX = inX * Rules.PLAYER_SPEED;
                    _velY = inY * Rules.PLAYER_SPEED;
                    if (_velX != 0 || _velY != 0) AddEvent(SimEventType.SlideStart, 0, _x, _y);
                }
            }
            else
            {
                _velX = inX * Rules.PLAYER_SPEED;
                _velY = inY * Rules.PLAYER_SPEED;
            }
            _onIce = ice;

            int pushX = 0, pushY = 0;
            switch (under)
            {
                case TileType.ConveyorUp: pushY = -Rules.CONVEYOR_SPEED; break;
                case TileType.ConveyorDown: pushY = Rules.CONVEYOR_SPEED; break;
                case TileType.ConveyorLeft: pushX = -Rules.CONVEYOR_SPEED; break;
                case TileType.ConveyorRight: pushX = Rules.CONVEYOR_SPEED; break;
            }

            int dx = _velX + pushX;
            int dy = _velY + pushY;

            bool hitX = MoveX(dx);
            if (hitX && ice) _velX = 0;
            if (hitX && dx != 0 && !_blockedX) AddEvent(SimEventType.Bump, 0, _x, _y);
            _blockedX = hitX;

            bool hitY = MoveY(dy);
            if (hitY && ice) _velY = 0;
            if (hitY && dy != 0 && !_blockedY) AddEvent(SimEventType.Bump, 1, _x, _y);
            _blockedY = hitY;
        }

        /// <summary>가로 이동 후 막혔으면 true</summary>
        private bool MoveX(int dx)
        {
            if (dx == 0) return false;
            int h = Rules.PLAYER_HALF;
            int nx = _x + dx;
            int r0 = Fixed.CellOf(_y - h);
            int r1 = Fixed.CellOf(_y + h - 1);
            if (dx > 0)
            {
                int col = Fixed.CellOf(nx + h - 1);
                for (int r = r0; r <= r1; r++)
                {
                    if (IsSolid(col, r)) { _x = col * Fixed.CELL - h; return true; }
                }
            }
            else
            {
                int col = Fixed.CellOf(nx - h);
                for (int r = r0; r <= r1; r++)
                {
                    if (IsSolid(col, r)) { _x = (col + 1) * Fixed.CELL + h; return true; }
                }
            }
            _x = nx;
            return false;
        }

        private bool MoveY(int dy)
        {
            if (dy == 0) return false;
            int h = Rules.PLAYER_HALF;
            int ny = _y + dy;
            int c0 = Fixed.CellOf(_x - h);
            int c1 = Fixed.CellOf(_x + h - 1);
            if (dy > 0)
            {
                int row = Fixed.CellOf(ny + h - 1);
                for (int c = c0; c <= c1; c++)
                {
                    if (IsSolid(c, row)) { _y = row * Fixed.CELL - h; return true; }
                }
            }
            else
            {
                int row = Fixed.CellOf(ny - h);
                for (int c = c0; c <= c1; c++)
                {
                    if (IsSolid(c, row)) { _y = (row + 1) * Fixed.CELL + h; return true; }
                }
            }
            _y = ny;
            return false;
        }

        /// <summary>막힌 칸인지 (맵 밖, 빈칸, 벽, 닫힌 문)</summary>
        public bool IsSolid(int cx, int cy)
        {
            if (!_map.InBounds(cx, cy)) return true;
            TileType t = _map.Tiles[cy * _map.Width + cx];
            if (t == TileType.Empty || t == TileType.Wall) return true;
            int door = _doorColorAt[cy * _map.Width + cx];
            return door >= 0 && !_colorOpen[door];
        }

        private void CollectItems()
        {
            for (int i = 0; i < _coinState.Length; i++)
            {
                if (_coinState[i] != ITEM_AVAILABLE) continue;
                CellPos c = _map.Coins[i];
                if (BoxTouchesCircle(c.CenterX, c.CenterY, Rules.COIN_RADIUS))
                {
                    _coinState[i] = ITEM_TAKEN;
                    AddEvent(SimEventType.Coin, i, c.CenterX, c.CenterY);
                }
            }
            for (int i = 0; i < _keyState.Length; i++)
            {
                if (_keyState[i] != ITEM_AVAILABLE) continue;
                CellPos c = _map.Keys[i].Cell;
                if (BoxTouchesCircle(c.CenterX, c.CenterY, Rules.KEY_RADIUS))
                {
                    _keyState[i] = ITEM_TAKEN;
                    AddEvent(SimEventType.Key, i, c.CenterX, c.CenterY);
                    int color = (int)_map.Keys[i].Color;
                    if (!_colorOpen[color])
                    {
                        _colorOpen[color] = true;
                        AddEvent(SimEventType.DoorOpen, color, c.CenterX, c.CenterY);
                    }
                }
            }
        }

        private void RecomputeDoors()
        {
            for (int c = 0; c < _colorOpen.Length; c++) _colorOpen[c] = false;
            for (int i = 0; i < _keyState.Length; i++)
                if (_keyState[i] != ITEM_AVAILABLE) _colorOpen[(int)_map.Keys[i].Color] = true;
        }

        /// <summary>
        /// 체크포인트 칸 위에 있는 동안 먹은 동전·열쇠를 계속 저장한다 (다시 돌아와도 저장됨).
        /// 새 체크포인트로 바뀔 때만 이벤트를 낸다.
        /// </summary>
        private void CheckCheckpoints()
        {
            int cx = Fixed.CellOf(_x);
            int cy = Fixed.CellOf(_y);
            for (int i = 0; i < _map.Checkpoints.Count; i++)
            {
                CellPos c = _map.Checkpoints[i];
                if (c.X != cx || c.Y != cy) continue;
                for (int k = 0; k < _coinState.Length; k++) if (_coinState[k] == ITEM_TAKEN) _coinState[k] = ITEM_SAVED;
                for (int k = 0; k < _keyState.Length; k++) if (_keyState[k] == ITEM_TAKEN) _keyState[k] = ITEM_SAVED;
                if (_checkpoint == i) return;
                _checkpoint = i;
                _respawnX = c.CenterX;
                _respawnY = c.CenterY;
                AddEvent(SimEventType.Checkpoint, i, c.CenterX, c.CenterY);
                return;
            }
        }

        private bool IsOnGoal()
        {
            int cx = Fixed.CellOf(_x);
            int cy = Fixed.CellOf(_y);
            for (int i = 0; i < _map.Goals.Count; i++)
                if (_map.Goals[i].X == cx && _map.Goals[i].Y == cy) return true;
            return false;
        }

        private bool IsOnVanishedFloor()
        {
            int cx = Fixed.CellOf(_x);
            int cy = Fixed.CellOf(_y);
            if (_map.GetTile(cx, cy) != TileType.Vanishing) return false;
            return VanishPhaseState(_tick, _map.GetParam(cx, cy)) == VanishState.Gone;
        }

        private bool TouchesObstacle()
        {
            for (int i = 0; i < _motions.Length; i++)
                if (BoxTouchesCircle(_obsX[i], _obsY[i], Rules.OBSTACLE_RADIUS)) return true;
            return false;
        }

        /// <summary>네모(현재 위치)와 원이 겹치는지 (경계에 딱 닿는 건 안 겹침)</summary>
        private bool BoxTouchesCircle(int cx, int cy, int radius)
        {
            return BoxCircleOverlap(_x, _y, Rules.PLAYER_HALF, cx, cy, radius);
        }

        public static bool BoxCircleOverlap(int bx, int by, int half, int cx, int cy, int radius)
        {
            int nx = cx < bx - half ? bx - half : (cx > bx + half ? bx + half : cx);
            int ny = cy < by - half ? by - half : (cy > by + half ? by + half : cy);
            long dx = cx - nx;
            long dy = cy - ny;
            return dx * dx + dy * dy < (long)radius * radius;
        }

        private void Die()
        {
            _deaths++;
            _state = SimState.Dead;
            _deadTimer = Rules.RESPAWN_TICKS;
            AddEvent(SimEventType.Death, _deaths, _x, _y);
        }

        private void Respawn()
        {
            for (int i = 0; i < _coinState.Length; i++) if (_coinState[i] == ITEM_TAKEN) _coinState[i] = ITEM_AVAILABLE;
            for (int i = 0; i < _keyState.Length; i++) if (_keyState[i] == ITEM_TAKEN) _keyState[i] = ITEM_AVAILABLE;
            RecomputeDoors();
            PlaceAtRespawn();
            _state = SimState.Playing;
            AddEvent(SimEventType.Respawn, _checkpoint, _x, _y);
        }

        private void AddEvent(SimEventType type, int index, int x, int y)
        {
            if (_eventCount >= MAX_EVENTS) return;
            _events[_eventCount].Type = type;
            _events[_eventCount].Index = index;
            _events[_eventCount].X = x;
            _events[_eventCount].Y = y;
            _eventCount++;
        }

        // ── 사라지는 바닥 ──

        public enum VanishState : byte { Solid, Warning, Gone }

        /// <summary>틱 t, 위상 p(0~3)인 사라지는 바닥의 상태 (순수 함수)</summary>
        public static VanishState VanishPhaseState(int tick, int phase)
        {
            int t = (int)Fixed.PosMod(tick + (phase & 3) * Rules.VANISH_PHASE_STEP, Rules.VANISH_PERIOD);
            if (t >= Rules.VANISH_SOLID_TICKS) return VanishState.Gone;
            if (t >= Rules.VANISH_SOLID_TICKS - Rules.VANISH_WARN_TICKS) return VanishState.Warning;
            return VanishState.Solid;
        }

        // ── 복제 (탐색 도구·테스트용) ──

        /// <summary>같은 맵을 공유하는 다른 시뮬레이션의 상태를 그대로 복사 (할당 없음)</summary>
        public void CopyStateFrom(Simulation other)
        {
            if (other._map != _map) throw new InvalidOperationException("같은 맵의 시뮬레이션끼리만 복사할 수 있습니다");
            _tick = other._tick;
            _state = other._state;
            _x = other._x; _y = other._y;
            _velX = other._velX; _velY = other._velY;
            _deaths = other._deaths;
            _clearTick = other._clearTick;
            _deadTimer = other._deadTimer;
            _checkpoint = other._checkpoint;
            _respawnX = other._respawnX; _respawnY = other._respawnY;
            _blockedX = other._blockedX; _blockedY = other._blockedY;
            _onIce = other._onIce;
            Buffer.BlockCopy(other._coinState, 0, _coinState, 0, _coinState.Length);
            Buffer.BlockCopy(other._keyState, 0, _keyState, 0, _keyState.Length);
            Array.Copy(other._colorOpen, _colorOpen, _colorOpen.Length);
            Array.Copy(other._obsX, _obsX, _obsX.Length);
            Array.Copy(other._obsY, _obsY, _obsY.Length);
            _eventCount = 0;
        }
    }
}
