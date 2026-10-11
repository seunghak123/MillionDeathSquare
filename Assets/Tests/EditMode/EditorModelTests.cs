using NUnit.Framework;
using Seunghak.Game.MillionDeathSquare;

namespace Seunghak.Game.MillionDeathSquare.Tests
{
    public class EditorModelTests
    {
        private static MapEditorModel NewEditor(EditorDevice devices = EditorDevice.All)
        {
            return new MapEditorModel(MapData.CreateBlank(12, 8), devices);
        }

        [Test]
        public void PlaceAndErase_TilesAndItems()
        {
            MapEditorModel e = NewEditor();
            Assert.AreEqual(EditResult.Changed, e.Apply(EditorTool.Wall, 5, 3));
            Assert.AreEqual(TileType.Wall, e.Map.GetTile(5, 3));
            Assert.AreEqual(EditResult.NoChange, e.Apply(EditorTool.Wall, 5, 3));
            Assert.AreEqual(EditResult.NotAllowedHere, e.Apply(EditorTool.Coin, 5, 3), "벽 위에는 동전 불가");
            Assert.AreEqual(EditResult.Changed, e.Apply(EditorTool.Coin, 6, 3));
            Assert.AreEqual(1, e.Map.Coins.Count);
            // 같은 칸에 다른 아이템 → 교체
            Assert.AreEqual(EditResult.Changed, e.Apply(EditorTool.Checkpoint, 6, 3));
            Assert.AreEqual(0, e.Map.Coins.Count);
            Assert.AreEqual(1, e.Map.Checkpoints.Count);
            // 아이템 칸을 벽으로 바꾸면 아이템도 지워짐
            Assert.AreEqual(EditResult.Changed, e.Apply(EditorTool.Wall, 6, 3));
            Assert.AreEqual(0, e.Map.Checkpoints.Count);
            // 지우개: 벽 → 바닥
            Assert.AreEqual(EditResult.Changed, e.Erase(5, 3));
            Assert.AreEqual(TileType.Floor, e.Map.GetTile(5, 3));
            // 시작점은 벽으로 덮을 수 없음
            Assert.AreEqual(EditResult.NotAllowedHere, e.Apply(EditorTool.Wall, e.Map.Start.X, e.Map.Start.Y));
            Assert.AreEqual(EditResult.OutOfBounds, e.Apply(EditorTool.Wall, 99, 0));
        }

        [Test]
        public void UndoRedo_RestoresSnapshots_AndDragIsOneStep()
        {
            MapEditorModel e = NewEditor();
            e.BeginStroke();
            for (int x = 2; x <= 8; x++) e.Apply(EditorTool.Wall, x, 2);
            e.EndStroke();
            Assert.AreEqual(1, e.UndoCount, "드래그 한 번 = 되돌리기 한 단계");
            e.Apply(EditorTool.Coin, 3, 4);
            Assert.AreEqual(2, e.UndoCount);

            Assert.IsTrue(e.Undo());
            Assert.AreEqual(0, e.Map.Coins.Count);
            Assert.AreEqual(TileType.Wall, e.Map.GetTile(5, 2));
            Assert.IsTrue(e.Undo());
            Assert.AreEqual(TileType.Floor, e.Map.GetTile(5, 2));
            Assert.IsFalse(e.Undo());
            Assert.IsTrue(e.Redo());
            Assert.AreEqual(TileType.Wall, e.Map.GetTile(8, 2));
            Assert.IsTrue(e.Redo());
            Assert.AreEqual(1, e.Map.Coins.Count);
            Assert.IsFalse(e.Redo());
            // 새 편집은 다시하기 기록을 지움
            e.Undo();
            e.Apply(EditorTool.Goal, 9, 5);
            Assert.IsFalse(e.CanRedo);
        }

        [Test]
        public void Tools_AreLockedUntilDevicesUnlocked()
        {
            MapEditorModel e = NewEditor(EditorDevice.None);
            Assert.IsTrue(e.IsToolUnlocked(EditorTool.Wall));
            Assert.IsTrue(e.IsToolUnlocked(EditorTool.Coin));
            Assert.AreEqual(EditResult.Locked, e.Apply(EditorTool.Obstacle, 4, 4));
            Assert.AreEqual(EditResult.Locked, e.Apply(EditorTool.Ice, 4, 4));
            Assert.AreEqual(EditResult.Locked, e.Apply(EditorTool.KeyRed, 4, 4));
            Assert.AreEqual(EditResult.Locked, e.Apply(EditorTool.Vanishing, 4, 4));
            e.Devices = EditorDevice.PathObstacle;
            Assert.AreEqual(EditResult.Changed, e.Apply(EditorTool.Obstacle, 4, 4));
            Assert.AreEqual(EditResult.Locked, e.SetPathMode(PathMode.Circle), "원 회전은 월드 2 해금 필요");
            Assert.AreEqual(EditResult.Changed, e.SetPathMode(PathMode.Loop));
        }

        [Test]
        public void ObstaclePathEditing()
        {
            MapEditorModel e = NewEditor();
            Assert.AreEqual(EditResult.NoSelection, e.AddPathPoint(TestUtil.P(1, 1)));
            e.Apply(EditorTool.Obstacle, 3, 2);
            Assert.AreEqual(0, e.SelectedObstacle);
            for (int i = 0; i < Rules.MAX_PATH_POINTS - 1; i++)
                Assert.AreEqual(EditResult.Changed, e.AddPathPoint(TestUtil.P(4 + i % 6, 2 + i / 6)));
            Assert.AreEqual(Rules.MAX_PATH_POINTS, e.Map.Obstacles[0].Points.Length);
            Assert.AreEqual(EditResult.LimitReached, e.AddPathPoint(TestUtil.P(1, 6)), "경로 지점 최대 8개");
            Assert.AreEqual(EditResult.Changed, e.RemoveLastPathPoint());
            Assert.AreEqual(EditResult.Changed, e.SetSpeed(3));
            Assert.AreEqual(EditResult.Changed, e.SetDelay(5));
            Assert.AreEqual(EditResult.Changed, e.DuplicateSelected(5));
            Assert.AreEqual(2, e.Map.Obstacles.Count);
            Assert.AreEqual(10, e.Map.Obstacles[1].DelayTenths, "기차: 같은 경로 + 지연 추가");
            Assert.AreEqual(EditResult.Changed, e.SetPathMode(PathMode.Circle));
            Assert.AreEqual(1, e.Map.Obstacles[1].Points.Length, "원 회전은 중심 하나");
            Assert.AreEqual(EditResult.Changed, e.SetCircle(3, 4, false));
            Assert.IsTrue(e.Validate().IsValid, e.Validate().ToString());
            // 지우개로 장애물 삭제 (첫 지점 칸)
            Assert.AreEqual(EditResult.Changed, e.Erase(3, 2));
            Assert.AreEqual(1, e.Map.Obstacles.Count);
        }

        [Test]
        public void UploadGate_OpensOnlyAfterVerifiedClear_AndResetsOnAnyEdit()
        {
            var map = MapAscii.Parse("내 맵", 5,
                "########",
                "#@...E.#",
                "########");
            var e = new MapEditorModel(map, EditorDevice.All);
            Assert.IsFalse(e.CanUpload, "깨기 전에는 업로드 불가");

            // 실패한 시험 플레이
            ReplayResult fail = e.SubmitTestPlay(InputRecording.FromScript(e.Map.ContentHash(), "R10"));
            Assert.IsFalse(fail.IsValid);
            Assert.IsFalse(e.CanUpload);

            // 다른 맵에서 만든 기록으로는 안 됨
            ReplayResult wrong = e.SubmitTestPlay(InputRecording.FromScript(12345u, "R53"));
            Assert.AreEqual(ReplayVerdict.MapMismatch, wrong.Verdict);

            // 직접 클리어 (시작 (1,1) → 골인 (5,1): 4칸 = 중심이 골인 칸에 들어설 때까지 53틱)
            MapData test = e.CreateTestPlayMap();
            var sim = new Simulation(test);
            var rec = new InputRecording(test.ContentHash());
            while (!sim.IsCleared && rec.Length < 600) { rec.Add(InputBits.Right); sim.Step(InputBits.Right); }
            ReplayResult ok = e.SubmitTestPlay(rec);
            Assert.IsTrue(ok.IsValid, ok.ToString());
            Assert.IsTrue(e.CanUpload);
            Assert.IsNotNull(e.CreatorClear);
            Assert.AreEqual(0, e.CreatorClear.Deaths);

            // 어떤 편집이든 다시 잠김 — 칸, 되돌리기, 팔레트, 이름
            e.Apply(EditorTool.Coin, 3, 1);
            Assert.IsFalse(e.CanUpload);
            e.Undo();
            Assert.IsFalse(e.CanUpload, "되돌려서 같은 맵이 돼도 다시 깨야 함");
            e.SubmitTestPlay(rec);
            Assert.IsTrue(e.CanUpload);
            e.SetPalette(6);
            Assert.IsFalse(e.CanUpload);
            e.SetPalette(5);
            e.SubmitTestPlay(rec);
            Assert.IsTrue(e.CanUpload);
            string err;
            Assert.IsTrue(e.SetName("새 이름", out err));
            Assert.IsFalse(e.CanUpload);
        }

        [Test]
        public void SetName_UsesForbiddenWordFilter()
        {
            IMapNameFilter old = MapNameFilter.Current;
            try
            {
                MapNameFilter.Current = new WordListNameFilter(new[] { "금지" });
                MapEditorModel e = NewEditor();
                string err;
                Assert.IsFalse(e.SetName("금 지 어 맵", out err));
                Assert.IsNotNull(err);
                Assert.IsTrue(e.SetName("괜찮은 맵", out err));
                Assert.AreEqual("괜찮은 맵", e.Map.Name);
            }
            finally
            {
                MapNameFilter.Current = old;
            }
        }

        [Test]
        public void Resize_KeepsInsideContent()
        {
            MapEditorModel e = NewEditor();
            e.Apply(EditorTool.Coin, 3, 3);
            e.Apply(EditorTool.Coin, 10, 6);
            Assert.AreEqual(EditResult.Changed, e.Resize(8, 6));
            Assert.AreEqual(8, e.Map.Width);
            Assert.AreEqual(1, e.Map.Coins.Count);
            Assert.IsTrue(e.Undo());
            Assert.AreEqual(12, e.Map.Width);
            Assert.AreEqual(2, e.Map.Coins.Count);
        }
    }
}
