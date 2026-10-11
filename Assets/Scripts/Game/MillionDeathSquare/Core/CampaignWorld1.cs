namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 월드 1 "파스텔" — 새 장치: 왕복하는 장애물 (기획서 4.1). 평균 데스 목표 5회 이하.
    /// 모든 스테이지는 CampaignSolutions의 입력 대본으로 0데스 클리어가 가능함을 테스트가 증명한다.
    /// 좌표는 칸 단위 (x 오른쪽, y 아래쪽). 장애물 경로 점은 칸 중심.
    /// </summary>
    internal static class CampaignWorld1
    {
        private const int PALETTE = 0; // 파스텔

        private static PathPoint C(int x, int y) { return PathPoint.CellCenter(x, y); }

        /// <summary>세로 왕복 (x열, y0 → y1)</summary>
        private static ObstacleDef V(int x, int y0, int y1, int speed, int delay = 0)
        {
            return ObstacleDef.PingPong(speed, delay, C(x, y0), C(x, y1));
        }

        /// <summary>가로 왕복 (y행, x0 → x1)</summary>
        private static ObstacleDef H(int y, int x0, int x1, int speed, int delay = 0)
        {
            return ObstacleDef.PingPong(speed, delay, C(x0, y), C(x1, y));
        }

        public static MapData Build(int stage)
        {
            MapData m;
            switch (stage)
            {
                case 1:
                    m = MapAscii.Parse("첫 걸음", PALETTE,
                        "################",
                        "#..............#",
                        "#..............#",
                        "#@......$.....E#",
                        "#..............#",
                        "#..............#",
                        "################");
                    m.Obstacles.Add(V(5, 1, 5, 1));
                    m.Obstacles.Add(V(10, 5, 1, 1));
                    return m;

                case 2:
                    m = MapAscii.Parse("엇갈림", PALETTE,
                        "##################",
                        "#......$.........#",
                        "#................#",
                        "#@..............E#",
                        "#................#",
                        "#..........$.....#",
                        "##################");
                    m.Obstacles.Add(V(4, 1, 5, 2));
                    m.Obstacles.Add(V(6, 5, 1, 2));
                    m.Obstacles.Add(V(8, 1, 5, 2));
                    m.Obstacles.Add(V(10, 5, 1, 2));
                    m.Obstacles.Add(V(12, 1, 5, 2));
                    return m;

                case 3:
                    m = MapAscii.Parse("올라가기", PALETTE,
                        "############",
                        "#....E.....#",
                        "#..........#",
                        "#$.........#",
                        "#..........#",
                        "#.........$#",
                        "#..........#",
                        "#$.........#",
                        "#..........#",
                        "#..........#",
                        "#.....@....#",
                        "############");
                    m.Obstacles.Add(H(2, 1, 10, 2));
                    m.Obstacles.Add(H(4, 10, 1, 2));
                    m.Obstacles.Add(H(6, 1, 10, 2));
                    m.Obstacles.Add(H(8, 10, 1, 2));
                    return m;

                case 4:
                    m = MapAscii.Parse("기차", PALETTE,
                        "####################",
                        "#........$.........#",
                        "#..................#",
                        "#..................#",
                        "#@................E#",
                        "#..................#",
                        "#..................#",
                        "#.........$........#",
                        "####################");
                    // 같은 경로에 출발 시점만 다르게 → 기차 (기획서 5.2)
                    for (int i = 0; i < 4; i++) m.Obstacles.Add(V(6, 1, 7, 2, i * 5));
                    for (int i = 0; i < 4; i++) m.Obstacles.Add(V(13, 7, 1, 2, i * 5));
                    return m;

                case 5:
                    m = MapAscii.Parse("쉼표", PALETTE,
                        "########################",
                        "#...........#..........#",
                        "#.....$.....#.....$....#",
                        "#...........#..........#",
                        "#@..........P.........E#",
                        "#...........#..........#",
                        "#...........#..........#",
                        "#...........#..........#",
                        "########################");
                    m.Obstacles.Add(V(3, 1, 7, 2));
                    m.Obstacles.Add(V(5, 7, 1, 2));
                    m.Obstacles.Add(V(7, 1, 7, 2));
                    m.Obstacles.Add(V(9, 7, 1, 2));
                    m.Obstacles.Add(H(2, 13, 22, 2));
                    m.Obstacles.Add(H(4, 22, 13, 2));
                    m.Obstacles.Add(H(6, 13, 22, 2));
                    return m;

                case 6:
                    m = MapAscii.Parse("차선", PALETTE,
                        "####################",
                        "#@.................#",
                        "#..................#",
                        "#....$.............#",
                        "#..................#",
                        "#.............$....#",
                        "#..................#",
                        "#.................E#",
                        "####################");
                    m.Obstacles.Add(H(2, 1, 18, 1));
                    m.Obstacles.Add(H(3, 18, 1, 2));
                    m.Obstacles.Add(H(4, 1, 18, 3));
                    m.Obstacles.Add(H(5, 18, 1, 2));
                    m.Obstacles.Add(H(6, 1, 18, 1));
                    return m;

                case 7:
                    m = MapAscii.Parse("숨을 곳", PALETTE,
                        "####################",
                        "#####.####.####.####",
                        "#@.......$........E#",
                        "#####.####.####.####",
                        "####################");
                    m.Obstacles.Add(H(2, 18, 2, 3));
                    return m;

                case 8:
                    m = MapAscii.Parse("바둑판", PALETTE,
                        "######################",
                        "#.........#..........#",
                        "#...$.....#......$...#",
                        "#.........#..........#",
                        "#@........P.........E#",
                        "#.........#..........#",
                        "#.....$...#..........#",
                        "#.........#..........#",
                        "######################");
                    m.Obstacles.Add(V(2, 1, 7, 2));
                    m.Obstacles.Add(V(4, 7, 1, 2));
                    m.Obstacles.Add(V(6, 1, 7, 2));
                    m.Obstacles.Add(V(8, 7, 1, 2));
                    m.Obstacles.Add(V(12, 1, 7, 3));
                    m.Obstacles.Add(V(14, 7, 1, 3));
                    m.Obstacles.Add(V(16, 1, 7, 3));
                    m.Obstacles.Add(V(18, 7, 1, 3));
                    return m;

                case 9:
                    m = MapAscii.Parse("네 귀퉁이", PALETTE,
                        "###############",
                        "#$...........$#",
                        "#.............#",
                        "#.............#",
                        "#.............#",
                        "#......@......#",
                        "#.............#",
                        "#.............#",
                        "#.............#",
                        "#$...........$#",
                        "#######E#######");
                    m.Obstacles.Add(H(3, 1, 13, 2));
                    m.Obstacles.Add(H(7, 13, 1, 2));
                    m.Obstacles.Add(V(4, 9, 1, 1));
                    m.Obstacles.Add(V(10, 1, 9, 1));
                    return m;

                default:
                    m = MapAscii.Parse("파스텔 끝판", PALETTE,
                        "##############################",
                        "#.......#...........#........#",
                        "#...$...#...........#.....$..#",
                        "#.......#...........#........#",
                        "#@......P.....$.....P.......E#",
                        "#.......#...........#........#",
                        "#.......#...........#........#",
                        "#.....$.#...........#........#",
                        "##############################");
                    m.Obstacles.Add(V(3, 1, 7, 2));
                    m.Obstacles.Add(V(5, 7, 1, 2));
                    for (int i = 0; i < 3; i++) m.Obstacles.Add(V(11, 1, 7, 2, i * 5));
                    for (int i = 0; i < 3; i++) m.Obstacles.Add(V(16, 7, 1, 2, i * 5));
                    m.Obstacles.Add(H(2, 9, 19, 1));
                    m.Obstacles.Add(H(6, 19, 9, 1));
                    m.Obstacles.Add(V(22, 1, 7, 3));
                    m.Obstacles.Add(V(24, 7, 1, 3));
                    m.Obstacles.Add(V(26, 1, 7, 3));
                    return m;
            }
        }
    }
}
