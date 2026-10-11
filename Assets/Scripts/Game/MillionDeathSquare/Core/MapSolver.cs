using System.Collections.Generic;
using System.Text;

namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 개발용 풀이 탐색기 — 맵을 "한 번도 죽지 않고" 깰 수 있는지 찾아 입력 대본을 만든다.
    /// 캠페인 맵 제작 검증(에디터 품질 확인)과 테스트의 풀이 대본 생성에 쓴다. 게임 런타임에서는 쓰지 않는다.
    ///
    /// 행동: 기다리기(WaitStep 틱) 또는 4방향 중 하나로 MoveStep 틱 이동 (기본 15틱 = 정확히 1칸).
    /// 가중 A*: 우선순위 = 경과 틱 + Weight × (남은 동전·골인까지 칸 거리 × 15틱).
    /// 상태 키: 위치·속도·먹은 동전/열쇠·체크포인트·(장애물 주기로 나눈) 틱.
    /// 찾은 풀이는 가장 빠르다는 보장은 없지만 ReplayVerifier로 그대로 검증된다.
    /// </summary>
    public sealed class MapSolver
    {
        public int WaitStep = 3;
        public int MoveStep = 15;
        public int MaxTicks = 60 * 60;
        public int MaxStates = 1500000;
        /// <summary>휴리스틱 가중치 (0이면 시간순 너비 우선)</summary>
        public int Weight = 2;

        private struct Node
        {
            public int Parent;
            public byte Action;   // 0 = 기다림, 1..4 = 상하좌우
            public int Tick;
        }

        private static readonly InputBits[] Actions = { InputBits.None, InputBits.Up, InputBits.Down, InputBits.Left, InputBits.Right };
        private static readonly char[] ActionChars = { 'W', 'U', 'D', 'L', 'R' };

        private MapData _map;
        private long _period;
        private readonly List<long> _heapKey = new List<long>();
        private readonly List<int> _heapId = new List<int>();

        public int ExploredStates { get; private set; }

        /// <summary>풀이 대본(InputRecording.FromScript 형식)을 찾으면 반환, 없으면 null</summary>
        public string Solve(MapData map)
        {
            _map = map;
            _period = ComputePeriod(map);
            _heapKey.Clear();
            _heapId.Clear();

            var nodes = new List<Node>();
            var sims = new List<Simulation>();
            var visited = new HashSet<string>();

            var root = new Simulation(map);
            nodes.Add(new Node { Parent = -1, Action = 0, Tick = 0 });
            sims.Add(root);
            visited.Add(Key(root));
            HeapPush(Priority(root), 0);

            var scratch = new Simulation(map);
            while (_heapId.Count > 0)
            {
                int id = HeapPop();
                Simulation from = sims[id];
                int t = nodes[id].Tick;
                for (int a = 0; a < Actions.Length; a++)
                {
                    int dur = a == 0 ? WaitStep : MoveStep;
                    scratch.CopyStateFrom(from);
                    bool dead = false;
                    int startX = scratch.PlayerX, startY = scratch.PlayerY;
                    for (int k = 0; k < dur; k++)
                    {
                        scratch.Step(Actions[a]);
                        if (scratch.State == SimState.Dead) { dead = true; break; }
                        if (scratch.IsCleared) break;
                    }
                    if (dead) continue;
                    if (a != 0 && scratch.PlayerX == startX && scratch.PlayerY == startY && !scratch.OnIce && !scratch.IsCleared)
                        continue; // 벽에 막혀 제자리 = 기다리기와 같음

                    if (scratch.IsCleared)
                    {
                        nodes.Add(new Node { Parent = id, Action = (byte)a, Tick = scratch.ClearTick });
                        ExploredStates = nodes.Count;
                        return BuildScript(nodes, nodes.Count - 1);
                    }
                    int nt = t + dur;
                    if (nt > MaxTicks) continue;
                    if (!visited.Add(Key(scratch))) continue;
                    var copy = new Simulation(map);
                    copy.CopyStateFrom(scratch);
                    nodes.Add(new Node { Parent = id, Action = (byte)a, Tick = nt });
                    sims.Add(copy);
                    HeapPush(Priority(copy), nodes.Count - 1);
                    if (nodes.Count > MaxStates) { ExploredStates = nodes.Count; return null; }
                }
                sims[id] = null; // 펼친 상태는 메모리 해제
            }
            ExploredStates = nodes.Count;
            return null;
        }

        /// <summary>장애물·사라지는 바닥의 공통 주기 (너무 크면 0 = 주기 축약 안 함)</summary>
        private static long ComputePeriod(MapData map)
        {
            long p = 1;
            bool any = false;
            for (int i = 0; i < map.Obstacles.Count; i++)
            {
                ObstacleDef o = map.Obstacles[i];
                var m = new ObstacleMotion(o);
                long per = m.PeriodTicks;
                // 정확히 나누어떨어지는 주기만 축약에 쓴다 (원/대각선은 반올림 오차 → 축약 안 함)
                if (o.Mode == PathMode.Circle || !IsAxisAligned(o) || o.DelayTenths > 0) return 0;
                p = Lcm(p, per);
                any = true;
                if (p > 1000000) return 0;
            }
            if (map.CountTiles(TileType.Vanishing) > 0) { p = Lcm(p, Rules.VANISH_PERIOD); any = true; }
            return any ? p : 1;
        }

        private static bool IsAxisAligned(ObstacleDef o)
        {
            for (int i = 1; i < o.Points.Length; i++)
                if (o.Points[i].HX != o.Points[i - 1].HX && o.Points[i].HY != o.Points[i - 1].HY) return false;
            if (o.Mode == PathMode.Loop && o.Points.Length > 1)
            {
                PathPoint a = o.Points[o.Points.Length - 1], b = o.Points[0];
                if (a.HX != b.HX && a.HY != b.HY) return false;
            }
            return true;
        }

        private static long Gcd(long a, long b) { while (b != 0) { long t = a % b; a = b; b = t; } return a; }
        private static long Lcm(long a, long b) { return a / Gcd(a, b) * b; }

        private long Priority(Simulation s)
        {
            int px = Fixed.CellOf(s.PlayerX), py = Fixed.CellOf(s.PlayerY);
            int h = 0;
            int remaining = 0;
            int nearest = int.MaxValue;
            for (int i = 0; i < s.CoinCount; i++)
            {
                if (s.IsCoinCollected(i)) continue;
                remaining++;
                CellPos c = _map.Coins[i];
                int d = Fixed.Abs(c.X - px) + Fixed.Abs(c.Y - py);
                if (d < nearest) nearest = d;
            }
            if (remaining > 0) h = nearest + (remaining - 1) * 4;
            else
            {
                nearest = int.MaxValue;
                for (int i = 0; i < _map.Goals.Count; i++)
                {
                    int d = Fixed.Abs(_map.Goals[i].X - px) + Fixed.Abs(_map.Goals[i].Y - py);
                    if (d < nearest) nearest = d;
                }
                h = nearest;
            }
            return (long)s.Tick + (long)Weight * h * MoveStep;
        }

        private string Key(Simulation s)
        {
            long tickKey = _period > 0 ? s.Tick % _period : s.Tick;
            var sb = new StringBuilder(48);
            sb.Append(s.PlayerX).Append(',').Append(s.PlayerY).Append(',').Append(s.VelocityX).Append(',').Append(s.VelocityY)
              .Append(',').Append(s.ActiveCheckpoint).Append(',').Append(tickKey).Append(',');
            for (int i = 0; i < s.CoinCount; i++) sb.Append(s.IsCoinCollected(i) ? '1' : '0');
            sb.Append('|');
            for (int i = 0; i < s.Map.Keys.Count; i++) sb.Append(s.IsKeyCollected(i) ? '1' : '0');
            return sb.ToString();
        }

        // ── 이진 힙 (우선순위 낮은 것 먼저, 같으면 먼저 넣은 것) ──

        private void HeapPush(long key, int id)
        {
            long k = key * 4194304L + id; // 같은 우선순위는 삽입 순서
            _heapKey.Add(k);
            _heapId.Add(id);
            int i = _heapKey.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (_heapKey[p] <= _heapKey[i]) break;
                Swap(i, p);
                i = p;
            }
        }

        private int HeapPop()
        {
            int top = _heapId[0];
            int last = _heapKey.Count - 1;
            _heapKey[0] = _heapKey[last];
            _heapId[0] = _heapId[last];
            _heapKey.RemoveAt(last);
            _heapId.RemoveAt(last);
            int i = 0;
            int n = _heapKey.Count;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < n && _heapKey[l] < _heapKey[m]) m = l;
                if (r < n && _heapKey[r] < _heapKey[m]) m = r;
                if (m == i) break;
                Swap(i, m);
                i = m;
            }
            return top;
        }

        private void Swap(int a, int b)
        {
            long k = _heapKey[a]; _heapKey[a] = _heapKey[b]; _heapKey[b] = k;
            int d = _heapId[a]; _heapId[a] = _heapId[b]; _heapId[b] = d;
        }

        private static string BuildScript(List<Node> nodes, int last)
        {
            var actions = new List<int>();
            var durations = new List<int>();
            int id = last;
            while (nodes[id].Parent >= 0)
            {
                Node n = nodes[id];
                Node p = nodes[n.Parent];
                actions.Add(n.Action);
                durations.Add(n.Tick - p.Tick);
                id = n.Parent;
            }
            actions.Reverse();
            durations.Reverse();

            var sb = new StringBuilder();
            int i = 0;
            while (i < actions.Count)
            {
                int a = actions[i];
                int total = 0;
                while (i < actions.Count && actions[i] == a) { total += durations[i]; i++; }
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(ActionChars[a]).Append(total);
            }
            return sb.ToString();
        }
    }
}
