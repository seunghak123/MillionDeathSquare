using System;
using NUnit.Framework;
using Seunghak.Game.MillionDeathSquare;

namespace Seunghak.Game.MillionDeathSquare.Tests
{
    public class MapFormatTests
    {
        private static MapData RichMap()
        {
            MapData m = MapAscii.Parse("모든 장치 테스트", 4,
                "##############",
                "#@.$~~>>^^vv<#",
                "#..r.R..0123.#",
                "#..g.G..P..E.#",
                "#..b.B.......#",
                "##############");
            m.Obstacles.Add(ObstacleDef.PingPong(1, 3, TestUtil.P(2, 4), TestUtil.P(12, 4), new PathPoint(10, 3)));
            m.Obstacles.Add(ObstacleDef.Loop(3, 0, TestUtil.P(6, 1), TestUtil.P(6, 4)));
            m.Obstacles.Add(ObstacleDef.Circle(TestUtil.P(10, 3), 3, 5, false, 2, 255));
            return m;
        }

        [Test]
        public void Serialize_RoundTrip_PreservesEverything()
        {
            MapData a = RichMap();
            Assert.IsTrue(MapValidator.Validate(a).IsValid, MapValidator.Validate(a).ToString());
            byte[] bytes = MapSerializer.Serialize(a);
            MapData b = MapSerializer.Deserialize(bytes);
            Assert.AreEqual(a.Name, b.Name);
            Assert.AreEqual(a.Width, b.Width);
            Assert.AreEqual(a.Height, b.Height);
            Assert.AreEqual(a.PaletteId, b.PaletteId);
            Assert.AreEqual(MapAscii.ToAscii(a), MapAscii.ToAscii(b));
            Assert.AreEqual(a.Obstacles.Count, b.Obstacles.Count);
            for (int i = 0; i < a.Obstacles.Count; i++)
            {
                Assert.AreEqual(a.Obstacles[i].Mode, b.Obstacles[i].Mode);
                Assert.AreEqual(a.Obstacles[i].SpeedTier, b.Obstacles[i].SpeedTier);
                Assert.AreEqual(a.Obstacles[i].DelayTenths, b.Obstacles[i].DelayTenths);
                Assert.AreEqual(a.Obstacles[i].RadiusHalf, b.Obstacles[i].RadiusHalf);
                Assert.AreEqual(a.Obstacles[i].StartAngle, b.Obstacles[i].StartAngle);
                Assert.AreEqual(a.Obstacles[i].Clockwise, b.Obstacles[i].Clockwise);
                Assert.AreEqual(a.Obstacles[i].Points, b.Obstacles[i].Points);
            }
            Assert.AreEqual(a.ContentHash(), b.ContentHash());
            Assert.AreEqual(bytes, MapSerializer.Serialize(b), "다시 직렬화해도 같은 바이트");
        }

        [Test]
        public void ShareCode_RoundTrip_AndIsCompact()
        {
            MapData a = Campaign.Get(1, 10).Map;
            string code = MapSerializer.ToShareCode(a);
            Assert.IsTrue(code.StartsWith(MapSerializer.SHARE_PREFIX));
            Assert.Less(code.Length, 400, "30×9 맵 공유 코드는 짧아야 함 (RLE)");
            MapData b;
            string error;
            Assert.IsTrue(MapSerializer.TryParseShareCode("  " + code + "\n", out b, out error), error);
            Assert.AreEqual(a.ContentHash(), b.ContentHash());
            Assert.AreEqual(code, MapSerializer.ToShareCode(b));
        }

        [Test]
        public void ShareCode_RejectsCorruption()
        {
            string code = MapSerializer.ToShareCode(Campaign.Get(1, 3).Map);
            MapData m;
            string error;
            char[] chars = code.ToCharArray();
            int i = code.Length / 2;
            chars[i] = chars[i] == 'A' ? 'B' : 'A';
            Assert.IsFalse(MapSerializer.TryParseShareCode(new string(chars), out m, out error));
            Assert.IsNotNull(error);
            Assert.IsFalse(MapSerializer.TryParseShareCode("MDS1-!!!", out m, out error));
            Assert.IsFalse(MapSerializer.TryParseShareCode("hello", out m, out error));
            Assert.IsFalse(MapSerializer.TryParseShareCode(code.Substring(0, code.Length - 6), out m, out error));
        }

        [Test]
        public void ContentHash_IgnoresName_ButNotContent()
        {
            MapData a = Campaign.Get(1, 1).Map.Clone();
            uint h = a.ContentHash();
            a.Name = "다른 이름";
            Assert.AreEqual(h, a.ContentHash());
            a.Coins.Add(new CellPos(3, 1));
            Assert.AreNotEqual(h, a.ContentHash());
        }

        [TestCase(0UL)]
        [TestCase(1UL)]
        [TestCase(31UL)]
        [TestCase(3141592653UL)]
        [TestCase(1234567890123UL)]
        [TestCase(18446744073709551615UL)]
        public void MapCode_RoundTrip(ulong id)
        {
            string code = MapCode.Encode(id);
            ulong back;
            Assert.IsTrue(MapCode.TryDecode(code, out back), code);
            Assert.AreEqual(id, back);
            Assert.IsTrue(MapCode.TryDecode(code.ToLowerInvariant().Replace("-", " "), out back), "소문자·공백 허용");
            Assert.AreEqual(id, back);
        }

        [Test]
        public void MapCode_IsShort_AndDetectsTypos()
        {
            string code = MapCode.Encode(3141592653UL);
            Assert.LessOrEqual(code.Replace("-", "").Length, 8, code);
            ulong v;
            int detected = 0, total = 0;
            string raw = code.Replace("-", "");
            const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            for (int i = 0; i < raw.Length; i++)
            {
                foreach (char c in alphabet)
                {
                    if (c == raw[i]) continue;
                    total++;
                    char[] t = raw.ToCharArray();
                    t[i] = c;
                    if (!MapCode.TryDecode(new string(t), out v) || v != 3141592653UL) detected++;
                }
            }
            Assert.Greater(detected * 100 / total, 90, "한 글자 오타 대부분 검출");
            // 인접 글자 바뀜
            char[] sw = raw.ToCharArray();
            char tmp = sw[1]; sw[1] = sw[2]; sw[2] = tmp;
            if (sw[1] != sw[2]) Assert.IsFalse(MapCode.TryDecode(new string(sw), out v) && v == 3141592653UL);
            Assert.IsFalse(MapCode.TryDecode("", out v));
            Assert.IsFalse(MapCode.TryDecode("U!", out v));
        }

        [Test]
        public void Validation_AcceptsAllCampaignStages()
        {
            foreach (CampaignStage s in Campaign.All())
            {
                ValidationResult r = MapValidator.Validate(s.Map);
                Assert.IsTrue(r.IsValid, s.Id + ": " + r);
            }
        }

        [Test]
        public void Validation_EnforcesLimits()
        {
            MapData m = MapData.CreateBlank(Rules.MAX_WIDTH, Rules.MAX_HEIGHT);
            Assert.IsTrue(MapValidator.Validate(m).IsValid, MapValidator.Validate(m).ToString());

            MapData big = MapData.CreateBlank(Rules.MAX_WIDTH + 1, 10);
            Assert.IsTrue(MapValidator.Validate(big).Has(ValidationCode.SizeOutOfRange));

            MapData coins = MapData.CreateBlank(32, 18);
            for (int i = 0; i < Rules.MAX_COINS + 1; i++) coins.Coins.Add(new CellPos(2 + i % 28, 2 + i / 28));
            Assert.IsTrue(MapValidator.Validate(coins).Has(ValidationCode.TooManyCoins));
            coins.Coins.RemoveAt(0);
            Assert.IsTrue(MapValidator.Validate(coins).IsValid, "30개는 허용");

            MapData obs = MapData.CreateBlank(32, 18);
            for (int i = 0; i < Rules.MAX_OBSTACLES + 1; i++) obs.Obstacles.Add(ObstacleDef.PingPong(1, 0, TestUtil.P(10 + i % 20, 1 + i / 20)));
            Assert.IsTrue(MapValidator.Validate(obs).Has(ValidationCode.TooManyObstacles));
            obs.Obstacles.RemoveAt(0);
            Assert.IsTrue(MapValidator.Validate(obs).IsValid, MapValidator.Validate(obs).ToString());

            MapData path = MapData.CreateBlank(20, 10);
            var pts = new PathPoint[Rules.MAX_PATH_POINTS + 1];
            for (int i = 0; i < pts.Length; i++) pts[i] = TestUtil.P(5 + i, 3);
            path.Obstacles.Add(ObstacleDef.PingPong(1, 0, pts));
            Assert.IsTrue(MapValidator.Validate(path).Has(ValidationCode.ObstacleTooManyPoints));

            MapData speed = MapData.CreateBlank(20, 10);
            speed.Obstacles.Add(ObstacleDef.PingPong(4, 0, TestUtil.P(5, 3)));
            Assert.IsTrue(MapValidator.Validate(speed).Has(ValidationCode.ObstacleBadSpeed));

            MapData pal = MapData.CreateBlank(10, 10);
            pal.PaletteId = Rules.PALETTE_COUNT;
            Assert.IsTrue(MapValidator.Validate(pal).Has(ValidationCode.PaletteOutOfRange));
        }

        [Test]
        public void Validation_RequiresStartGoalAndConsistentItems()
        {
            MapData noGoal = MapData.CreateBlank(10, 6);
            noGoal.Goals.Clear();
            Assert.IsTrue(MapValidator.Validate(noGoal).Has(ValidationCode.NoGoal));

            MapData wallStart = MapData.CreateBlank(10, 6);
            wallStart.Start = new CellPos(0, 0);
            Assert.IsTrue(MapValidator.Validate(wallStart).Has(ValidationCode.StartNotWalkable));

            MapData door = MapData.CreateBlank(10, 6);
            door.Doors.Add(new DoorItem(new CellPos(4, 2), KeyColor.Blue));
            Assert.IsTrue(MapValidator.Validate(door).Has(ValidationCode.DoorWithoutKey));
            door.Keys.Add(new KeyItem(new CellPos(2, 2), KeyColor.Blue));
            Assert.IsTrue(MapValidator.Validate(door).IsValid);

            MapData overlap = MapData.CreateBlank(10, 6);
            overlap.Coins.Add(overlap.Goals[0]);
            Assert.IsTrue(MapValidator.Validate(overlap).Has(ValidationCode.ItemOverlap));

            MapData coinOnWall = MapData.CreateBlank(10, 6);
            coinOnWall.Coins.Add(new CellPos(0, 2));
            Assert.IsTrue(MapValidator.Validate(coinOnWall).Has(ValidationCode.ItemNotWalkable));

            MapData killer = MapData.CreateBlank(10, 6);
            killer.Obstacles.Add(ObstacleDef.PingPong(1, 0, PathPoint.CellCenter(killer.Start.X, killer.Start.Y)));
            Assert.IsTrue(MapValidator.Validate(killer).Has(ValidationCode.StartTouchesObstacle));
        }

        [Test]
        public void NameFilter_Hook_BlocksForbiddenWords_EvenWithSpacing()
        {
            IMapNameFilter old = MapNameFilter.Current;
            try
            {
                MapNameFilter.Current = new WordListNameFilter(new[] { "나쁜말" });
                MapData m = MapData.CreateBlank(10, 6);
                m.Name = "정말 나 쁜 말 맵!";
                Assert.IsTrue(MapValidator.Validate(m).Has(ValidationCode.NameForbidden));
                m.Name = "좋은 맵";
                Assert.IsTrue(MapValidator.Validate(m).IsValid);
                m.Name = new string('가', Rules.MAX_NAME_LENGTH + 1);
                Assert.IsTrue(MapValidator.Validate(m).Has(ValidationCode.NameTooLong));
            }
            finally
            {
                MapNameFilter.Current = old;
            }
        }

        [Test]
        public void Ascii_RoundTrip()
        {
            MapData m = RichMap();
            string ascii = MapAscii.ToAscii(m);
            MapData back = MapAscii.Parse(m.Name, m.PaletteId, ascii.Split('\n'));
            Assert.AreEqual(ascii, MapAscii.ToAscii(back));
            Assert.Throws<FormatException>(() => MapAscii.Parse("x", 0, "#?#"));
        }
    }
}
