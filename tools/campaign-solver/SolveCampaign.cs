// 캠페인 스테이지 풀이 대본 생성기 (MapSolver). 맵을 고친 뒤 CampaignSolutions.cs를 갱신할 때 사용.
//   tools/solve-campaign.sh            → 50개 전부
//   tools/solve-campaign.sh 1          → 월드 1만
//   tools/solve-campaign.sh 1 3        → 1-3만
using System;
using Seunghak.Game.MillionDeathSquare;

public static class SolveCampaign
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        int w0 = 1, w1 = Rules.WORLD_COUNT, s0 = 1, s1 = Rules.STAGES_PER_WORLD;
        if (args.Length > 0) { w0 = w1 = int.Parse(args[0]); }
        if (args.Length > 1) { s0 = s1 = int.Parse(args[1]); }
        int failed = 0;
        for (int w = w0; w <= w1; w++)
        {
            for (int s = s0; s <= s1; s++)
            {
                CampaignStage st = Campaign.Get(w, s);
                ValidationResult v = MapValidator.Validate(st.Map);
                if (!v.IsValid) { Console.WriteLine("/* " + st.Id + " */ // INVALID: " + v); failed++; continue; }
                var solver = new MapSolver();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                string script = solver.Solve(st.Map);
                if (script == null) { Console.WriteLine("/* " + st.Id + " */ // UNSOLVED (" + solver.ExploredStates + " states)"); failed++; continue; }
                ReplayResult r = ReplayVerifier.Run(st.Map, InputRecording.FromScript(st.Map.ContentHash(), script));
                Console.WriteLine("/* " + st.Id + " */ \"" + script + "\",   // " + r + ", " + watch.ElapsedMilliseconds + " ms");
                if (!r.IsValid) failed++;
            }
        }
        return failed == 0 ? 0 : 1;
    }
}
