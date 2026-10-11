namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 월드 2~5 임시 맵 (TODO: 레벨 기획 후 교체 — Docs/TODO.md "스테이지 11~50").
    /// 각 월드의 새 장치를 보여 주는 템플릿 하나에 스테이지 번호로 속도/개수만 바꾼다. 모두 MapValidator를 통과한다.
    /// </summary>
    internal static class CampaignPlaceholders
    {
        private static PathPoint C(int x, int y) { return PathPoint.CellCenter(x, y); }

        public static MapData Build(int world, int stage)
        {
            int speed = 1 + (stage - 1) / 4;   // 1,1,1,1,2,2,2,2,3,3
            string name = world + "-" + stage + " (임시)";
            MapData m;
            switch (world)
            {
                case 2: // 노을 — 원을 그리며 도는 장애물
                    m = MapAscii.Parse(name, 1,
                        "################",
                        "#..............#",
                        "#...$......$...#",
                        "#..............#",
                        "#@............E#",
                        "#..............#",
                        "#...$......$...#",
                        "#..............#",
                        "################");
                    m.Obstacles.Add(ObstacleDef.Circle(C(5, 4), 4, 0, true, speed, 0));
                    m.Obstacles.Add(ObstacleDef.Circle(C(10, 4), 4, 8, false, speed, 0));
                    if (stage >= 4) m.Obstacles.Add(ObstacleDef.Circle(C(5, 4), 4, 8, true, speed, 0));
                    if (stage >= 7) m.Obstacles.Add(ObstacleDef.Circle(C(10, 4), 4, 0, false, speed, 0));
                    return m;

                case 3: // 숲 — 열쇠와 문
                    m = MapAscii.Parse(name, 2,
                        "################",
                        "#.....#........#",
                        "#..r..#...$....#",
                        "#.....#........#",
                        "#@....R.......E#",
                        "#.....#........#",
                        "#.....#..$.....#",
                        "#.....#........#",
                        "################");
                    m.Obstacles.Add(ObstacleDef.PingPong(speed, 0, C(9, 1), C(9, 7)));
                    if (stage >= 3) m.Obstacles.Add(ObstacleDef.PingPong(speed, 0, C(12, 7), C(12, 1)));
                    if (stage >= 6) m.Obstacles.Add(ObstacleDef.PingPong(speed, 0, C(1, 2), C(5, 2)));
                    return m;

                case 4: // 겨울 — 얼음 바닥, 컨베이어
                    m = MapAscii.Parse(name, 3,
                        "################",
                        "#..............#",
                        "#.~~~~~~~~~~~..#",
                        "#.~~~~~$~~~~~..#",
                        "#@~~~~~~~~~~~.E#",
                        "#.>>>>>>>>>>>..#",
                        "#..............#",
                        "################");
                    if (stage >= 2) m.Obstacles.Add(ObstacleDef.PingPong(speed, 0, C(13, 1), C(13, 6)));
                    if (stage >= 5) m.Obstacles.Add(ObstacleDef.PingPong(speed, 0, C(2, 6), C(12, 6)));
                    return m;

                default: // 네온 — 사라졌다 나타나는 바닥 + 전부 섞기
                    m = MapAscii.Parse(name, 4,
                        "####################",
                        "#.....#      #.....#",
                        "#..$..#      #..$..#",
                        "#.....#      #.....#",
                        "#@....33221100....E#",
                        "#.....#      #.....#",
                        "#.....#      #.....#",
                        "####################");
                    m.Obstacles.Add(ObstacleDef.PingPong(speed, 0, C(3, 1), C(3, 6)));
                    if (stage >= 4) m.Obstacles.Add(ObstacleDef.Circle(C(16, 4), 3, 0, true, speed, 0));
                    return m;
            }
        }
    }
}
