using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    public class BoardTests
    {
        [Test]
        public void GeneratedBoards_HaveNoMatchesAndAtLeastOneMove()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var board = new Board(7, 8, 4 + seed % 3, new SystemRandom(seed));
                Assert.IsFalse(MatchFinder.HasAnyMatch(board), "seed " + seed + " started with a match");
                Assert.IsTrue(board.HasValidMove(), "seed " + seed + " has no move");
                for (int y = 0; y < board.Height; y++)
                    for (int x = 0; x < board.Width; x++)
                        Assert.IsFalse(board[x, y].IsEmpty);
            }
        }

        [Test]
        public void Shuffle_ProducesPlayableBoardAndKeepsTiles()
        {
            var board = new Board(6, 6, 4, new SystemRandom(7));
            board.Set(new Vec2I(0, 0), new Tile(2, SpecialType.Bomb));

            int bombs = 0;
            board.Shuffle();
            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                    if (board[x, y].Special == SpecialType.Bomb) bombs++;

            Assert.AreEqual(1, bombs);
            Assert.IsFalse(MatchFinder.HasAnyMatch(board));
            Assert.IsTrue(board.HasValidMove());
        }

        [Test]
        public void TryFindValidMove_ReturnsAnActualWorkingSwap()
        {
            var board = new Board(6, 6, 5, new SystemRandom(3));
            Assert.IsTrue(board.TryFindValidMove(out Vec2I a, out Vec2I b));
            Assert.IsTrue(a.IsAdjacentTo(b));
            var result = MatchEngine.TrySwap(board, a, b);
            Assert.IsTrue(result.Valid);
        }
    }

    public class MatchFinderTests
    {
        [Test]
        public void FindsHorizontalAndVerticalRuns()
        {
            var board = TestHelpers.BoardFrom(
                "01230",
                "01231",
                "11123",
                "01230");

            var groups = MatchFinder.FindGroups(board);
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(1, groups[0].Color);
            // A row of 3 at y=1 and a column of 4 at x=1 share the tile (1,1): 3 + 4 - 1 = 6 cells.
            Assert.AreEqual(6, groups[0].Cells.Count);
            Assert.AreEqual(SpecialType.Bomb, groups[0].Reward);
        }

        [Test]
        public void TwoSeparateRunsOfSameColor_AreTwoGroups()
        {
            var board = TestHelpers.BoardFrom(
                "000.3",
                "12312",
                "000.3");
            var groups = MatchFinder.FindGroups(board);
            Assert.AreEqual(2, groups.Count);
        }

        [Test]
        public void FiveInARow_RewardsColorBomb_FourRewardsRocket_LShapeRewardsBomb()
        {
            var five = TestHelpers.BoardFrom("00000");
            Assert.AreEqual(SpecialType.ColorBomb, MatchFinder.FindGroups(five)[0].Reward);

            var fourH = TestHelpers.BoardFrom("0000.");
            Assert.AreEqual(SpecialType.RowRocket, MatchFinder.FindGroups(fourH)[0].Reward);

            var fourV = TestHelpers.BoardFrom("0", "0", "0", "0");
            Assert.AreEqual(SpecialType.ColumnRocket, MatchFinder.FindGroups(fourV)[0].Reward);

            var lShape = TestHelpers.BoardFrom(
                "0..",
                "0..",
                "000");
            Assert.AreEqual(SpecialType.Bomb, MatchFinder.FindGroups(lShape)[0].Reward);

            var plain = TestHelpers.BoardFrom("000");
            Assert.AreEqual(SpecialType.None, MatchFinder.FindGroups(plain)[0].Reward);
        }

        [Test]
        public void SpecialTilesKeepTheirColorForMatching_ColorBombNever()
        {
            var board = TestHelpers.BoardFrom("000");
            board.Set(new Vec2I(1, 0), new Tile(0, SpecialType.RowRocket));
            Assert.AreEqual(1, MatchFinder.FindGroups(board).Count);

            board.Set(new Vec2I(1, 0), Tile.MakeColorBomb());
            Assert.AreEqual(0, MatchFinder.FindGroups(board).Count);
        }
    }
}
