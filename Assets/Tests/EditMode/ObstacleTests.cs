using System.Linq;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    public class ObstacleTests
    {
        // Bottom row "11310": swapping (2,0) with (3,0) matches (0,0), (1,0), (2,0).
        private static Board SimpleMatchBoard()
        {
            return TestHelpers.BoardFrom(
                "01230",
                "23012",
                "01230",
                "23012",
                "11310");
        }

        private static TurnResult PlaySimpleMatch(Board board) => MatchEngine.TrySwap(board, new Vec2I(2, 0), new Vec2I(3, 0));

        [Test]
        public void Layout_PlacesIceAndBoxes_AndBoxCellsHaveNoTile()
        {
            var board = new Board(5, 3, 4, new SystemRandom(1), new[]
            {
                "b....",
                ".iI..",
                "....B",
            });

            Assert.AreEqual(1, board.BoxAt(0, 2));
            Assert.AreEqual(2, board.BoxAt(4, 0));
            Assert.AreEqual(1, board.IceAt(1, 1));
            Assert.AreEqual(2, board.IceAt(2, 1));
            Assert.AreEqual(2, board.BoxCount);
            Assert.AreEqual(2, board.IceCellCount);
            Assert.IsTrue(board[0, 2].IsEmpty);
            Assert.IsTrue(board[4, 0].IsEmpty);
            Assert.IsFalse(board[1, 1].IsEmpty, "ice cells still hold a tile");
        }

        [Test]
        public void ClearingATile_CracksOneLayerOfIceUnderIt()
        {
            Board board = SimpleMatchBoard();
            board.SetIce(new Vec2I(0, 0), 1);
            board.SetIce(new Vec2I(1, 0), 2);
            board.SetIce(new Vec2I(4, 4), 1); // not touched

            TurnResult result = PlaySimpleMatch(board);

            CascadeStep step = result.Steps[0];
            CollectionAssert.AreEquivalent(
                new[] { new ObstacleHit(new Vec2I(0, 0), 0), new ObstacleHit(new Vec2I(1, 0), 1) },
                step.IceCracked);
            Assert.AreEqual(0, board.IceAt(0, 0));
            Assert.AreEqual(1, board.IceAt(1, 0));
            Assert.AreEqual(1, board.IceAt(4, 4));
            Assert.AreEqual(3 * MatchEngine.PointsPerTile + 2 * MatchEngine.PointsPerIceLayer, step.Score);
        }

        [Test]
        public void TileTurningIntoASpecial_AlsoCracksIce()
        {
            Board board = TestHelpers.BoardFrom(
                "01230",
                "23012",
                "01230",
                "23102",
                "11013");
            board.SetIce(new Vec2I(2, 0), 1);

            TurnResult result = MatchEngine.TrySwap(board, new Vec2I(2, 1), new Vec2I(2, 0));

            Assert.AreEqual(new Vec2I(2, 0), result.Steps[0].Created[0].Position);
            Assert.IsTrue(result.Steps[0].IceCracked.Contains(new ObstacleHit(new Vec2I(2, 0), 0)));
        }

        [Test]
        public void MatchNextToABox_DamagesIt_AndABrokenBoxLetsTilesFallThrough()
        {
            Board board = SimpleMatchBoard();
            board.SetBox(new Vec2I(0, 1), 1); // right above the matched (0,0)

            TurnResult result = PlaySimpleMatch(board);

            CascadeStep step = result.Steps[0];
            CollectionAssert.AreEqual(new[] { new ObstacleHit(new Vec2I(0, 1), 0) }, step.BoxesHit);
            Assert.IsFalse(board.HasBox(new Vec2I(0, 1)));
            // With the box gone, column 0 is one segment again and refills from above the board.
            foreach (TileDrop drop in step.Drops.Where(d => d.To.X == 0))
                Assert.GreaterOrEqual(drop.FromY, board.Height);
            Assert.IsFalse(board[0, 1].IsEmpty);
        }

        [Test]
        public void ABoxThatSurvives_BlocksFallingTiles_AndTheCellUnderItRefillsInPlace()
        {
            Board board = SimpleMatchBoard();
            board.SetBox(new Vec2I(0, 1), 2);

            TurnResult result = PlaySimpleMatch(board);

            CascadeStep step = result.Steps[0];
            CollectionAssert.AreEqual(new[] { new ObstacleHit(new Vec2I(0, 1), 1) }, step.BoxesHit);
            Assert.IsTrue(board.HasBox(new Vec2I(0, 1)));
            Assert.IsTrue(board[0, 1].IsEmpty, "a box cell never holds a tile");
            Assert.IsFalse(step.Moves.Any(m => m.To.X == 0), "nothing can fall through the box");
            TileDrop under = step.Drops.Single(d => d.To.X == 0);
            Assert.AreEqual(new Vec2I(0, 0), under.To);
            Assert.AreEqual(0, under.FromY, "the new tile appears in place under the box");
        }

        [Test]
        public void ARocketHitsBoxesInItsLine_AndSkipsTheirCells()
        {
            Board board = TestHelpers.BoardFrom(
                "01230",
                "23012",
                "01230",
                "23012",
                "01230");
            board.SetBox(new Vec2I(4, 3), 1);
            board.Set(new Vec2I(1, 2), new Tile(1, SpecialType.RowRocket));

            TurnResult result = MatchEngine.TrySwap(board, new Vec2I(1, 2), new Vec2I(1, 3));

            CascadeStep step = result.Steps[0];
            Assert.IsTrue(step.BoxesHit.Contains(new ObstacleHit(new Vec2I(4, 3), 0)));
            Assert.IsFalse(step.Cleared.Any(c => c.Position == new Vec2I(4, 3)));
        }

        [Test]
        public void AStrongBox_LosesOneHealthPerHit()
        {
            Board board = SimpleMatchBoard();
            board.SetBox(new Vec2I(1, 1), 3); // right above the matched (1,0)

            TurnResult result = PlaySimpleMatch(board);

            Assert.AreEqual(1, result.Steps[0].BoxesHit.Count);
            Assert.AreEqual(2, board.BoxAt(1, 1));
        }

        [Test]
        public void Shuffle_KeepsBoxesAndIceInPlace()
        {
            var board = new Board(6, 6, 4, new SystemRandom(3), new[]
            {
                "......",
                ".b..i.",
                "......",
                "..I...",
                "....B.",
                "......",
            });

            board.Shuffle();

            Assert.AreEqual(1, board.BoxAt(1, 4));
            Assert.AreEqual(2, board.BoxAt(4, 1));
            Assert.AreEqual(1, board.IceAt(4, 4));
            Assert.AreEqual(2, board.IceAt(2, 2));
            Assert.IsTrue(board[1, 4].IsEmpty);
            Assert.IsTrue(board[4, 1].IsEmpty);
            Assert.IsFalse(MatchFinder.HasAnyMatch(board));
            Assert.IsTrue(board.HasValidMove());
        }

        [Test]
        public void BoardsWithObstacles_StayConsistentOverManyRandomTurns()
        {
            foreach (LevelConfig level in Levels.All.Where(l => l.Layout != null))
            {
                for (int seed = 0; seed < 8; seed++)
                {
                    var board = new Board(level.Width, level.Height, level.ColorCount, new SystemRandom(seed), level.Layout);
                    for (int turn = 0; turn < 30; turn++)
                    {
                        Assert.IsTrue(board.TryFindValidMove(out Vec2I a, out Vec2I b), level.Name + " has no move");
                        Assert.IsTrue(MatchEngine.TrySwap(board, a, b).Valid);
                        Assert.IsFalse(MatchFinder.HasAnyMatch(board), level.Name + ": matches left on board");
                        for (int y = 0; y < board.Height; y++)
                            for (int x = 0; x < board.Width; x++)
                                Assert.AreEqual(board.HasBox(x, y), board[x, y].IsEmpty,
                                    level.Name + ": cell (" + x + "," + y + ") must hold a tile unless it is a box");
                    }
                }
            }
        }
    }

    public class GoalTests
    {
        [Test]
        public void EveryLevel_StartsPlayable_WithReachableGoals()
        {
            foreach (LevelConfig level in Levels.All)
            {
                for (int seed = 0; seed < 10; seed++)
                {
                    var session = new GameSession(level, new SystemRandom(seed));
                    Assert.IsFalse(MatchFinder.HasAnyMatch(session.Board), level.Name);
                    Assert.IsTrue(session.Board.HasValidMove(), level.Name);
                    foreach (GoalProgress goal in session.Goals)
                    {
                        Assert.Greater(goal.Target, 0, level.Name + " has an empty goal");
                        Assert.AreEqual(0, goal.Current);
                        if (goal.Goal.Type == GoalType.Collect) Assert.Less(goal.Goal.Color, level.ColorCount, level.Name);
                    }
                }
            }
        }

        [Test]
        public void IceAndBoxGoals_TargetEverythingOnTheBoard()
        {
            var level = new LevelConfig("t", 6, 6, 4, 10, new[] { Goal.ClearIce(), Goal.BreakBoxes() }, new[]
            {
                "......",
                ".iI...",
                "......",
                "...b..",
                "....B.",
                "i.....",
            });
            var session = new GameSession(level, new SystemRandom(1));
            Assert.AreEqual(3, session.Goals[0].Target);
            Assert.AreEqual(2, session.Goals[1].Target);
        }

        [Test]
        public void CollectGoal_CountsClearedTilesOfItsColor()
        {
            var level = new LevelConfig("t", 7, 7, 5, 30, new[] { Goal.Collect(0, 1000), Goal.Collect(3, 1000) });
            var session = new GameSession(level, new SystemRandom(4));
            int red = 0, yellow = 0;

            for (int i = 0; i < 10; i++)
            {
                session.Board.TryFindValidMove(out Vec2I a, out Vec2I b);
                TurnResult result = session.TryMove(a, b);
                foreach (CascadeStep step in result.Steps)
                    foreach (ClearedTile c in step.Cleared)
                    {
                        if (c.Tile.Color == 0) red++;
                        if (c.Tile.Color == 3) yellow++;
                    }
            }

            Assert.AreEqual(red, session.Goals[0].Current);
            Assert.AreEqual(yellow, session.Goals[1].Current);
            Assert.Greater(red + yellow, 0);
        }

        [Test]
        public void WinNeedsEveryGoal()
        {
            var easy = new LevelConfig("t", 6, 6, 4, 5, new[] { Goal.Score(1), Goal.Score(2) });
            var session = new GameSession(easy, new SystemRandom(2));
            session.Board.TryFindValidMove(out Vec2I a, out Vec2I b);
            session.TryMove(a, b);
            Assert.AreEqual(GameState.Won, session.State);

            var hard = new LevelConfig("t", 6, 6, 4, 5, new[] { Goal.Score(1), Goal.Score(1000000) });
            session = new GameSession(hard, new SystemRandom(2));
            session.Board.TryFindValidMove(out a, out b);
            session.TryMove(a, b);
            Assert.AreEqual(GameState.Playing, session.State);
            Assert.IsTrue(session.Goals[0].Done);
            Assert.IsFalse(session.Goals[1].Done);
        }

        [Test]
        public void GoalProgressNeverGoesAboveTarget()
        {
            var level = new LevelConfig("t", 6, 6, 4, 5, new[] { Goal.Score(5), Goal.Score(1000000) });
            var session = new GameSession(level, new SystemRandom(2));
            session.Board.TryFindValidMove(out Vec2I a, out Vec2I b);
            session.TryMove(a, b);
            Assert.AreEqual(5, session.Goals[0].Current);
            Assert.AreEqual(0, session.Goals[0].Remaining);
        }

        [Test]
        public void Stars_DependOnMovesLeft()
        {
            var session = new GameSession(new LevelConfig("t", 6, 6, 4, 10, 1), new SystemRandom(2));
            Assert.AreEqual(0, session.Stars, "no stars before winning");
            session.Board.TryFindValidMove(out Vec2I a, out Vec2I b);
            session.TryMove(a, b);
            Assert.AreEqual(3, session.Stars, "won with 9 of 10 moves left");

            session = new GameSession(new LevelConfig("t", 6, 6, 4, 1, 1), new SystemRandom(2));
            session.Board.TryFindValidMove(out a, out b);
            session.TryMove(a, b);
            Assert.AreEqual(GameState.Won, session.State);
            Assert.AreEqual(1, session.Stars, "won on the last move");
        }
    }
}
