using NUnit.Framework;
using Seunghak.Game.MillionDeathSquare;

namespace Seunghak.Game.MillionDeathSquare.Tests
{
    public class ReviewRecordTests
    {
        [Test]
        public void Review_PresetPhrasesOnly_RoundTrip()
        {
            var r = new Review { Liked = true, Difficulty = PerceivedDifficulty.Hell, PhraseId = 1, Cleared = true };
            Assert.IsTrue(r.IsValid);
            Review back;
            Assert.IsTrue(Review.TryDecode(r.Encode(), out back));
            Assert.IsTrue(back.Liked);
            Assert.AreEqual(PerceivedDifficulty.Hell, back.Difficulty);
            Assert.AreEqual("악마가 만든 맵", ReviewPhrases.Get(back.PhraseId));
            Assert.AreEqual("지옥", Review.DifficultyLabel(back.Difficulty));

            var bad = new Review { PhraseId = ReviewPhrases.Count };
            Assert.IsFalse(bad.IsValid, "정해진 문구 밖 번호는 거부 (자유 입력 없음)");
            Assert.IsFalse(Review.TryDecode("R1;l1;d9;p0;c1", out back));
            Assert.IsFalse(Review.TryDecode("free text review", out back));
        }

        [Test]
        public void ReviewSummary_CountsLikesDifficultyAndClearRate()
        {
            var s = new ReviewSummary { Attempts = 1000, Clears = 8 };
            s.Add(new Review { Liked = true, Difficulty = PerceivedDifficulty.Hell, PhraseId = 1 });
            s.Add(new Review { Liked = false, Difficulty = PerceivedDifficulty.Hell, PhraseId = 1 });
            s.Add(new Review { Liked = true, Difficulty = PerceivedDifficulty.Hard, PhraseId = 2 });
            Assert.AreEqual(2, s.Likes);
            Assert.AreEqual(PerceivedDifficulty.Hell, s.MostVotedDifficulty);
            Assert.AreEqual("0.8%", s.ClearRateText);
            Assert.AreEqual(1, s.TopPhrases(1)[0]);
            Assert.AreEqual("기록 없음", new ReviewSummary().ClearRateText);
        }

        [Test]
        public void RecordTable_KeepsBestPerPlayer_LowerIsBetter()
        {
            var t = new RecordTable(3);
            Assert.IsTrue(t.Submit("a", "에이", 10, 0));
            Assert.IsTrue(t.Submit("b", "비", 5, 0));
            Assert.IsFalse(t.Submit("a", "에이", 12, 0), "더 나쁜 기록은 무시");
            Assert.IsTrue(t.Submit("a", "에이", 3, 0));
            Assert.AreEqual("a", t.Best.PlayerId);
            Assert.AreEqual(2, t.Count, "한 사람당 하나");
            t.Submit("c", "씨", 7, 0);
            Assert.IsFalse(t.Submit("d", "디", 99, 0), "정원 밖");
            Assert.AreEqual(3, t.RankOf("c"));
            RecordTable back = RecordTable.Parse(t.Serialize(), 3);
            Assert.AreEqual(t.Serialize(), back.Serialize());
        }

        [Test]
        public void RecordBoards_AreSeparatedByPlatform()
        {
            string key = RecordBoards.WorkshopKey(3141592653UL);
            string pc = RecordBoards.BoardName(key, RecordPlatform.PC, RecordKind.Deaths);
            string mo = RecordBoards.BoardName(key, RecordPlatform.Mobile, RecordKind.Deaths);
            Assert.AreNotEqual(pc, mo);
            Assert.IsTrue(pc.StartsWith("mds_"));
            Assert.Less(pc.Length, 128, "Steam 순위표 이름 길이 제한");
            Assert.AreEqual("mds_c_1-3_pc_time", RecordBoards.BoardName(RecordBoards.CampaignKey(1, 3), RecordPlatform.PC, RecordKind.Time));
            Assert.AreEqual("mds_h_0000beef_mo_deaths", RecordBoards.BoardName(RecordBoards.HashKey(0xBEEF), RecordPlatform.Mobile, RecordKind.Deaths));
        }

        [Test]
        public void Palettes_EightDistinct()
        {
            Assert.AreEqual(Rules.PALETTE_COUNT, Palettes.Count);
            for (int i = 0; i < Palettes.Count; i++)
            {
                PaletteDef p = Palettes.Get(i);
                Assert.IsNotNull(p.Name);
                Assert.AreNotEqual(p.Floor, p.Wall, p.Name);
                Assert.AreNotEqual(p.Player, p.Obstacle, p.Name);
            }
        }
    }
}
