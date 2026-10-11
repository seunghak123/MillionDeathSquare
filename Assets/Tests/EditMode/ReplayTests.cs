using System;
using NUnit.Framework;
using Seunghak.Game.MillionDeathSquare;

namespace Seunghak.Game.MillionDeathSquare.Tests
{
    public class ReplayTests
    {
        private static InputRecording Solution(int world, int stage)
        {
            MapData map = Campaign.Get(world, stage).Map;
            return InputRecording.FromScript(map.ContentHash(), CampaignSolutions.Get(world, stage));
        }

        [Test]
        public void Recording_SerializeRoundTrip_IsCompact()
        {
            InputRecording rec = Solution(1, 10);
            byte[] bytes = rec.Serialize();
            Assert.Less(bytes.Length, 80, "RLE 덕분에 수백 틱 기록도 작음");
            InputRecording back = InputRecording.Deserialize(bytes);
            Assert.AreEqual(rec.Length, back.Length);
            Assert.AreEqual(rec.MapHash, back.MapHash);
            for (int i = 0; i < rec.Length; i++) Assert.AreEqual(rec[i], back[i]);
            InputRecording b64 = InputRecording.FromBase64(rec.ToBase64());
            Assert.AreEqual(rec.Length, b64.Length);
        }

        [Test]
        public void Recording_RejectsCorruption()
        {
            byte[] bytes = Solution(1, 1).Serialize();
            bytes[bytes.Length / 2] ^= 0x55;
            Assert.Throws<FormatException>(() => InputRecording.Deserialize(bytes));
            Assert.Throws<FormatException>(() => InputRecording.FromScript(0, "R10 Q5"));
        }

        [Test]
        public void Verifier_AcceptsHonestRun_AndMatchesClaims()
        {
            MapData map = Campaign.Get(1, 4).Map;
            InputRecording rec = Solution(1, 4);
            ReplayResult r = ReplayVerifier.Run(map, rec);
            Assert.IsTrue(r.IsValid, r.ToString());
            Assert.AreEqual(rec.Length, r.ClearTick);
            Assert.IsTrue(ReplayVerifier.Verify(map, rec, r.Deaths, r.ClearTick).IsValid);
            Assert.AreEqual(ReplayVerdict.DeathsMismatch, ReplayVerifier.Verify(map, rec, r.Deaths + 1, r.ClearTick).Verdict);
            Assert.AreEqual(ReplayVerdict.TimeMismatch, ReplayVerifier.Verify(map, rec, r.Deaths, r.ClearTick - 30).Verdict, "시간을 줄여 주장하면 거부");
        }

        [Test]
        public void Verifier_RejectsWrongMap_TrailingInput_AndUnfinishedRuns()
        {
            MapData map = Campaign.Get(1, 4).Map;
            InputRecording rec = Solution(1, 4);

            Assert.AreEqual(ReplayVerdict.MapMismatch, ReplayVerifier.Run(Campaign.Get(1, 5).Map, rec).Verdict);

            InputRecording trailing = InputRecording.FromScript(map.ContentHash(), CampaignSolutions.Get(1, 4) + " W30");
            Assert.AreEqual(ReplayVerdict.TrailingInput, ReplayVerifier.Run(map, trailing).Verdict);

            InputRecording cut = InputRecording.FromScript(map.ContentHash(), "R30 W30");
            Assert.AreEqual(ReplayVerdict.NotCleared, ReplayVerifier.Run(map, cut).Verdict);

            InputRecording oldRules = Solution(1, 4);
            oldRules.RulesVersion = Rules.RULES_VERSION + 1;
            Assert.AreEqual(ReplayVerdict.RulesMismatch, ReplayVerifier.Run(map, oldRules).Verdict);
        }

        [Test]
        public void Verifier_CountsDeathsDuringRun_AndRejectsHiddenDeaths()
        {
            MapData map = TestUtil.Map(
                "#######",
                "#.....#",
                "#@...E#",
                "#######");
            map.Obstacles.Add(ObstacleDef.PingPong(1, 0, TestUtil.P(1, 1))); // 시작점 바로 위
            uint hash = map.ContentHash();
            // 위로 가서 한 번 죽고, 재시작 후 오른쪽으로 골인 (입력이 남음)
            ReplayResult loose = ReplayVerifier.Run(map, InputRecording.FromScript(hash, "U10 W20 R100"));
            Assert.AreEqual(ReplayVerdict.TrailingInput, loose.Verdict);
            Assert.AreEqual(1, loose.Deaths);
            int clear = loose.ClearTick;
            InputRecording exact = InputRecording.FromScript(hash, "U10 W20 R" + (clear - 30));
            ReplayResult r = ReplayVerifier.Verify(map, exact, 1, clear);
            Assert.IsTrue(r.IsValid, r.ToString());
            Assert.AreEqual(ReplayVerdict.DeathsMismatch, ReplayVerifier.Verify(map, exact, 0, clear).Verdict, "죽은 걸 숨긴 0데스 주장은 거부");
        }

        [Test]
        public void AllWorld1Solutions_ClearWithZeroDeaths()
        {
            for (int s = 1; s <= Rules.STAGES_PER_WORLD; s++)
            {
                MapData map = Campaign.Get(1, s).Map;
                string script = CampaignSolutions.Get(1, s);
                Assert.IsNotNull(script, "1-" + s + " 풀이 없음");
                ReplayResult r = ReplayVerifier.Run(map, InputRecording.FromScript(map.ContentHash(), script));
                Assert.IsTrue(r.IsValid, "1-" + s + ": " + r);
                Assert.AreEqual(0, r.Deaths, "1-" + s + " 0데스 풀이");
            }
        }
    }
}
