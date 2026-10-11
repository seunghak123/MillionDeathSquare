using NUnit.Framework;
using Seunghak.Game.MillionDeathSquare;

namespace Seunghak.Game.MillionDeathSquare.Tests
{
    public class CampaignTests
    {
        [Test]
        public void Campaign_Has5WorldsOf10_World1IsReal_OthersPlaceholders()
        {
            int count = 0;
            foreach (CampaignStage s in Campaign.All())
            {
                count++;
                Assert.AreEqual(s.World != 1, s.IsPlaceholder, s.Id);
                Assert.AreEqual(Campaign.PaletteOfWorld(s.World), s.Map.PaletteId, s.Id + " 월드 팔레트");
            }
            Assert.AreEqual(50, count);
        }

        [Test]
        public void World1_UsesOnlyPingPongObstacles_World2IntroducesCircles()
        {
            for (int s = 1; s <= 10; s++)
            {
                MapData m = Campaign.Get(1, s).Map;
                Assert.Greater(m.Obstacles.Count, 0, "1-" + s);
                foreach (ObstacleDef o in m.Obstacles) Assert.AreEqual(PathMode.PingPong, o.Mode, "1-" + s);
                Assert.AreEqual(0, m.Keys.Count + m.Doors.Count + m.CountTiles(TileType.Ice) + m.CountTiles(TileType.Vanishing));
            }
            bool circle = false;
            foreach (ObstacleDef o in Campaign.Get(2, 1).Map.Obstacles) circle |= o.Mode == PathMode.Circle;
            Assert.IsTrue(circle);
            Assert.Greater(Campaign.Get(3, 1).Map.Keys.Count, 0);
            Assert.Greater(Campaign.Get(4, 1).Map.CountTiles(TileType.Ice), 0);
            Assert.Greater(Campaign.Get(5, 1).Map.CountTiles(TileType.Vanishing), 0);
        }

        [Test]
        public void NextWorld_UnlocksAfterSevenClears_EvenWithStuckStages()
        {
            var p = new CampaignProgress();
            Assert.IsTrue(p.IsWorldUnlocked(1));
            Assert.IsFalse(p.IsWorldUnlocked(2));
            Assert.IsTrue(p.IsStageUnlocked(1, 10), "열린 월드의 스테이지는 모두 도전 가능");
            // 스테이지 3, 6, 9에서 막혔다고 치고 나머지 7개 클리어
            int[] cleared = { 1, 2, 4, 5, 7, 8 };
            foreach (int s in cleared)
            {
                ClearOutcome o = p.RecordClear(1, s, 3, 600);
                Assert.AreEqual(0, o.UnlockedWorld);
            }
            Assert.IsFalse(p.IsWorldUnlocked(2));
            ClearOutcome last = p.RecordClear(1, 10, 0, 900);
            Assert.AreEqual(2, last.UnlockedWorld);
            Assert.AreEqual(EditorDevice.PathObstacle, last.UnlockedDevice, "월드 1을 깨면 경로 장애물 해금");
            Assert.IsTrue(p.IsWorldUnlocked(2));
            Assert.IsFalse(p.IsWorldUnlocked(3));
            Assert.AreEqual(7, p.ClearedCount(1));
        }

        [Test]
        public void DeviceUnlocks_FollowWorldClears()
        {
            var p = new CampaignProgress();
            Assert.AreEqual(EditorDevice.None, p.UnlockedDevices);
            EditorDevice[] expect = { EditorDevice.PathObstacle, EditorDevice.CircleObstacle, EditorDevice.KeysDoors, EditorDevice.IceConveyor, EditorDevice.Vanishing };
            EditorDevice acc = EditorDevice.None;
            for (int w = 1; w <= 5; w++)
            {
                for (int s = 1; s <= 7; s++) p.RecordClear(w, s, 1, 100);
                acc |= expect[w - 1];
                Assert.AreEqual(acc, p.UnlockedDevices, "월드 " + w);
            }
            Assert.AreEqual(EditorDevice.All, p.UnlockedDevices);
            var dev = new CampaignProgress { UnlockAll = true };
            Assert.AreEqual(EditorDevice.All, dev.UnlockedDevices);
            Assert.IsTrue(dev.IsWorldUnlocked(5));
        }

        [Test]
        public void Records_KeepBestDeathsTimeAndZeroDeath()
        {
            var p = new CampaignProgress();
            ClearOutcome a = p.RecordClear(1, 1, 5, 1000);
            Assert.IsTrue(a.FirstClear && a.NewBestDeaths && a.NewBestTime);
            Assert.IsFalse(a.FirstZeroDeath);
            ClearOutcome b = p.RecordClear(1, 1, 7, 800);
            Assert.IsFalse(b.FirstClear);
            Assert.IsFalse(b.NewBestDeaths);
            Assert.IsTrue(b.NewBestTime);
            ClearOutcome c = p.RecordClear(1, 1, 0, 1200);
            Assert.IsTrue(c.NewBestDeaths && c.FirstZeroDeath);
            StageRecord r = p.Get(1, 1);
            Assert.AreEqual(0, r.BestDeaths);
            Assert.AreEqual(800, r.BestTicks);
            Assert.IsTrue(r.ZeroDeath);
            Assert.AreEqual(3, r.Clears);
            p.RecordDeaths(1, 1, 4);
            Assert.AreEqual(5 + 7 + 0 + 4, p.Get(1, 1).TotalDeaths);
        }

        [Test]
        public void Progress_SerializeRoundTrip_AndToleratesGarbage()
        {
            var p = new CampaignProgress();
            p.RecordClear(1, 3, 2, 456);
            p.RecordClear(2, 10, 0, 789);
            p.RecordDeaths(1, 4, 9);
            string s = p.Serialize();
            CampaignProgress q = CampaignProgress.Parse(s);
            Assert.AreEqual(s, q.Serialize());
            Assert.AreEqual(456, q.Get(1, 3).BestTicks);
            Assert.IsTrue(q.Get(2, 10).ZeroDeath);
            Assert.IsFalse(q.Get(1, 4).Cleared);
            Assert.AreEqual(0, CampaignProgress.Parse("garbage").TotalCleared);
            Assert.AreEqual(1, CampaignProgress.Parse("MDSC1|u=0|1-1=1,0,10,1,1,0|9-9=1,1,1,1,1,1|1-2=x").TotalCleared);
        }

        [Test]
        public void StageIds_Parse()
        {
            int w, s;
            Assert.IsTrue(Campaign.TryParseStageId("3-7", out w, out s));
            Assert.AreEqual(3, w);
            Assert.AreEqual(7, s);
            Assert.IsFalse(Campaign.TryParseStageId("6-1", out w, out s));
            Assert.IsFalse(Campaign.TryParseStageId("1-11", out w, out s));
            Assert.IsFalse(Campaign.TryParseStageId("abc", out w, out s));
        }

        [Test]
        public void PlaceholderStages_AreAlsoClearable()
        {
            for (int w = 2; w <= 5; w++)
            {
                for (int s = 1; s <= 10; s++)
                {
                    string script = CampaignSolutions.Get(w, s);
                    Assert.IsNotNull(script, w + "-" + s + " 풀이 없음");
                    MapData map = Campaign.Get(w, s).Map;
                    ReplayResult r = ReplayVerifier.Run(map, InputRecording.FromScript(map.ContentHash(), script));
                    Assert.IsTrue(r.IsValid, w + "-" + s + ": " + r);
                }
            }
        }
    }
}
