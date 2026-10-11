namespace Seunghak.Game.MillionDeathSquare
{
    /// <summary>
    /// 캠페인 스테이지의 0데스 풀이 입력 대본 (InputRecording.FromScript 형식).
    /// MapSolver로 만들었고, 테스트가 ReplayVerifier로 재생해 "깰 수 있는 맵"임을 증명한다.
    /// 맵을 고치면 대본도 다시 만들어야 한다: tools/solve-campaign.sh
    /// 개발 중 데모 플레이(유령 시연)에도 쓸 수 있다.
    /// </summary>
    public static class CampaignSolutions
    {
        private static readonly string[] World1 =
        {
            /* 1-1 */ "L15 R45 W6 R90 D15 R15 U15 R41",
            /* 1-2 */ "U30 R90 D60 R60 U30 R68",
            /* 1-3 */ "U75 R60 D30 L135 U90 R53",
            /* 1-4 */ "U45 R120 D90 R15 U45 R113",
            /* 1-5 */ "U30 R150 D30 R30 U15 R75 W15 U15 D30 R53",
            /* 1-6 */ "R45 D30 R15 D15 R45 D15 R90 D30 R53",
            /* 1-7 */ "R75 W6 U15 L15 U15 D15 R75 W3 U15 D15 R75 W6 U15 D15 R38",
            /* 1-8 */ "R30 D30 R30 W15 R15 U60 L30 R75 D30 R60 U15 W9 R60 U15 D15 R30 D15 R8",
            /* 1-9 */ "D60 R90 U120 L180 D75 L15 D45 R90 D8",
            /* 1-10 */ "U30 R45 D75 R30 U45 R120 U15 R75 D15 R15 D15 R15 U30 R90 D30 R8",
        };

        // 임시 맵(월드 2~5) 풀이 — 임시 맵도 깰 수 있음을 보장. 맵이 확정되면 다시 생성
        private static readonly string[][] Placeholders =
        {
            null, // 월드 1은 World1
            new[]
            {
                /* 2-1 */ "D30 R45 U60 R90 U15 R15 W6 D75 U30 R38",
                /* 2-2 */ "D30 R45 U60 R90 U15 R15 W6 D75 U30 R38",
                /* 2-3 */ "D30 R45 U60 R90 U15 R15 W6 D75 U30 R38",
                /* 2-4 */ "D30 R45 U60 R90 U15 R15 W6 D75 U30 R38",
                /* 2-5 */ "D30 R30 W6 R15 U45 W3 U15 R105 D60 U30 R38",
                /* 2-6 */ "D30 R30 W6 R15 U45 W3 U15 R105 D60 U30 R38",
                /* 2-7 */ "U30 R45 D60 U15 R15 D15 R90 U45 W3 U15 D15 R30 D15 R8",
                /* 2-8 */ "U30 R45 D60 U15 R15 D15 R90 U45 W3 U15 D15 R30 D15 R8",
                /* 2-9 */ "D30 R45 U75 R105 U15 D75 U30 W3 R38",
                /* 2-10 */ "D30 R45 U75 R105 U15 D75 U30 W3 R38",
            },
            new[]
            {
                /* 3-1 */ "R30 U30 R30 D30 R15 D15 R15 D30 R15 W3 R30 U60 D30 R53",
                /* 3-2 */ "R30 U30 R30 D30 R15 D15 R15 D30 R15 W3 R30 U60 D30 R53",
                /* 3-3 */ "R30 U30 R30 D30 R15 D15 R15 D30 R15 W3 R30 U60 D30 R53",
                /* 3-4 */ "R30 U30 R30 D30 R15 D15 R15 D30 R15 W3 R30 U60 D30 R53",
                /* 3-5 */ "R30 U30 R30 D30 R30 D30 R30 U15 R15 U45 D30 R53",
                /* 3-6 */ "R30 U30 D15 R30 D15 R30 D30 R30 U15 R15 U45 D30 R53",
                /* 3-7 */ "R30 U30 D15 R30 D15 R30 D30 R30 U15 R15 U45 D30 R53",
                /* 3-8 */ "R30 U30 D15 R30 D15 R30 D30 R30 U15 R15 U45 D30 R53",
                /* 3-9 */ "R30 W12 U30 R30 D30 R30 D30 R30 U30 R15 U30 D30 R53",
                /* 3-10 */ "R30 W12 U30 R30 D30 R30 D30 R30 U30 R15 U30 D30 R53",
            },
            new[]
            {
                /* 4-1 */ "D15 R60 U15 W3 R15 U60 D15 R15",
                /* 4-2 */ "D15 R60 U15 W3 R15 U60 D15 R15",
                /* 4-3 */ "D15 R60 U15 W3 R15 U60 D15 R15",
                /* 4-4 */ "D15 R60 U15 W3 R15 U60 D15 R15",
                /* 4-5 */ "D15 R60 W9 U15 W6 D15 R75 U1",
                /* 4-6 */ "D15 R60 W9 U15 W6 D15 R75 U1",
                /* 4-7 */ "D15 R60 W9 U15 W6 D15 R75 U1",
                /* 4-8 */ "D15 R60 W9 U15 W6 D15 R75 U1",
                /* 4-9 */ "D15 R60 U15 W3 R15 U60 D15 R15",
                /* 4-10 */ "D15 R60 U15 W3 R15 U60 D15 R15",
            },
            new[]
            {
                /* 5-1 */ "U30 R60 D30 R135 U30 R30 D30 R23",
                /* 5-2 */ "U30 R60 D30 R135 U30 R30 D30 R23",
                /* 5-3 */ "U30 R60 D30 R135 U30 R30 D30 R23",
                /* 5-4 */ "U30 R60 D30 R135 U30 R30 D30 R23",
                /* 5-5 */ "U30 R60 D30 R135 U30 R30 D30 R23",
                /* 5-6 */ "U30 R60 D30 R135 U30 R30 D30 R23",
                /* 5-7 */ "U30 R60 D30 R135 U30 R30 D30 R23",
                /* 5-8 */ "U30 R60 D30 R135 U30 R30 D30 R23",
                /* 5-9 */ "U30 R60 D30 R135 U30 R30 D30 R23",
                /* 5-10 */ "U30 R60 D30 R135 U30 R30 D30 R23",
            },
        };

        /// <summary>풀이 대본 (없으면 null)</summary>
        public static string Get(int world, int stage)
        {
            if (!Campaign.IsValidStage(world, stage)) return null;
            if (world == 1) return World1[stage - 1];
            string[] w = Placeholders[world - 1];
            return w != null ? w[stage - 1] : null;
        }
    }
}
