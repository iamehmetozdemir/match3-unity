using System.Collections.Generic;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    public class EngineTests
    {
        // Base layout without any match: color = (x + 2y) % 4 on a 5x5 board.
        private static Board Base()
        {
            return TestHelpers.BoardFrom(
                "01230", // y = 4
                "23012", // y = 3
                "01230", // y = 2
                "23012", // y = 1
                "01230"); // y = 0
        }

        [Test]
        public void BaseLayoutHasNoMatches()
        {
            Assert.IsFalse(MatchFinder.HasAnyMatch(Base()));
        }

        [Test]
        public void SwapNotCreatingAMatch_IsRejectedAndBoardUnchanged()
        {
            Board board = Base();
            string before = board.ToString();
            TurnResult result = MatchEngine.TrySwap(board, new Vec2I(0, 0), new Vec2I(1, 0));
            Assert.IsFalse(result.Valid);
            Assert.AreEqual(before, board.ToString());
        }

        [Test]
        public void NonAdjacentOrOutOfBoundsSwaps_AreRejected()
        {
            Board board = Base();
            Assert.IsFalse(MatchEngine.TrySwap(board, new Vec2I(0, 0), new Vec2I(2, 0)).Valid);
            Assert.IsFalse(MatchEngine.TrySwap(board, new Vec2I(0, 0), new Vec2I(1, 1)).Valid);
            Assert.IsFalse(MatchEngine.TrySwap(board, new Vec2I(4, 4), new Vec2I(5, 4)).Valid);
        }

        [Test]
        public void SimpleMatch_ClearsThreeTilesScoresAndRefills()
        {
            // Bottom row "11310": swapping (2,0) with (3,0) lines up three 1s.
            Board board = TestHelpers.BoardFrom(
                "01230",
                "23012",
                "01230",
                "23012",
                "11310");

            TurnResult result = MatchEngine.TrySwap(board, new Vec2I(2, 0), new Vec2I(3, 0));

            Assert.IsTrue(result.Valid);
            Assert.AreEqual(1, result.Steps.Count);
            CascadeStep step = result.Steps[0];
            Assert.AreEqual(3, step.Cleared.Count);
            Assert.AreEqual(3 * MatchEngine.PointsPerTile, step.Score);
            Assert.AreEqual(step.Score, result.TotalScore);
            Assert.AreEqual(3, step.Drops.Count);
            Assert.AreEqual(3 * 4, step.Moves.Count); // the 4 tiles above each cleared tile fall one row
            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                    Assert.IsFalse(board[x, y].IsEmpty);
        }

        [Test]
        public void Gravity_MovesTilesDown_AndFreshTilesStartAboveTheBoard()
        {
            Board board = TestHelpers.BoardFrom(
                "01230",
                "23012",
                "01230",
                "23012",
                "11310");

            TurnResult result = MatchEngine.TrySwap(board, new Vec2I(2, 0), new Vec2I(3, 0));
            CascadeStep step = result.Steps[0];

            foreach (TileMove move in step.Moves)
            {
                Assert.AreEqual(move.From.X, move.To.X);
                Assert.Less(move.To.Y, move.From.Y);
            }
            foreach (TileDrop drop in step.Drops)
                Assert.GreaterOrEqual(drop.FromY, board.Height);
        }

        [Test]
        public void FourInARow_CreatesRowRocketAtTheSwappedTile()
        {
            // Bottom row "11013"; moving the 1 from (2,1) down to (2,0) gives 1111.
            Board board = TestHelpers.BoardFrom(
                "01230",
                "23012",
                "01230",
                "23102",
                "11013");
            Assert.IsFalse(MatchFinder.HasAnyMatch(board));

            TurnResult result = MatchEngine.TrySwap(board, new Vec2I(2, 1), new Vec2I(2, 0));

            Assert.IsTrue(result.Valid);
            CascadeStep step = result.Steps[0];
            Assert.AreEqual(1, step.Created.Count);
            Assert.AreEqual(SpecialType.RowRocket, step.Created[0].Tile.Special);
            Assert.AreEqual(new Vec2I(2, 0), step.Created[0].Position);
            Assert.AreEqual(3, step.Cleared.Count); // 4 matched, 1 of them became the rocket
        }

        [Test]
        public void RowRocket_ClearsItsWholeRow_WhenSwapped()
        {
            Board board = Base();
            board.Set(new Vec2I(1, 2), new Tile(1, SpecialType.RowRocket));

            TurnResult result = MatchEngine.TrySwap(board, new Vec2I(1, 2), new Vec2I(1, 3));

            Assert.IsTrue(result.Valid);
            CascadeStep step = result.Steps[0];
            // The rocket moved to (1,3) and clears row 3.
            Assert.AreEqual(1, step.Activations.Count);
            Assert.AreEqual(SpecialType.RowRocket, step.Activations[0].Type);
            Assert.AreEqual(board.Width, step.Cleared.Count);
            foreach (Vec2I p in TestHelpers.ClearedPositions(step)) Assert.AreEqual(3, p.Y);
        }

        [Test]
        public void ColumnRocket_ClearsItsWholeColumn()
        {
            Board board = Base();
            board.Set(new Vec2I(2, 2), new Tile(2, SpecialType.ColumnRocket));

            TurnResult result = MatchEngine.TrySwap(board, new Vec2I(2, 2), new Vec2I(3, 2));

            Assert.IsTrue(result.Valid);
            CascadeStep step = result.Steps[0];
            Assert.AreEqual(board.Height, step.Cleared.Count);
            foreach (Vec2I p in TestHelpers.ClearedPositions(step)) Assert.AreEqual(3, p.X);
        }

        [Test]
        public void Bomb_ClearsThreeByThreeArea()
        {
            Board board = Base();
            // Color 3 so that the bomb does not accidentally line up with its neighbors.
            board.Set(new Vec2I(2, 2), new Tile(3, SpecialType.Bomb));

            TurnResult result = MatchEngine.TrySwap(board, new Vec2I(2, 2), new Vec2I(2, 3));

            CascadeStep step = result.Steps[0];
            Assert.AreEqual(9, step.Cleared.Count);
            foreach (Vec2I p in TestHelpers.ClearedPositions(step))
            {
                Assert.LessOrEqual(System.Math.Abs(p.X - 2), 1);
                Assert.LessOrEqual(System.Math.Abs(p.Y - 3), 1);
            }
        }

        [Test]
        public void ColorBomb_ClearsEveryTileOfTheSwappedColor()
        {
            Board board = Base();
            int expectedColor = board[3, 2].Color;
            board.Set(new Vec2I(2, 2), Tile.MakeColorBomb());
            // Count after placing the bomb, because it replaced one tile.
            int total = TestHelpers.CountTiles(board, expectedColor);

            TurnResult result = MatchEngine.TrySwap(board, new Vec2I(2, 2), new Vec2I(3, 2));

            Assert.IsTrue(result.Valid);
            CascadeStep step = result.Steps[0];
            Assert.AreEqual(total + 1, step.Cleared.Count); // all tiles of that color + the bomb itself
            foreach (ClearedTile c in step.Cleared)
                Assert.IsTrue(c.Tile.Color == expectedColor || c.Tile.Special == SpecialType.ColorBomb);
        }

        [Test]
        public void TwoColorBombs_ClearTheWholeBoard()
        {
            Board board = Base();
            board.Set(new Vec2I(1, 1), Tile.MakeColorBomb());
            board.Set(new Vec2I(2, 1), Tile.MakeColorBomb());

            TurnResult result = MatchEngine.TrySwap(board, new Vec2I(1, 1), new Vec2I(2, 1));

            Assert.AreEqual(board.Width * board.Height, result.Steps[0].Cleared.Count);
        }

        [Test]
        public void SpecialTilesChainReact()
        {
            Board board = Base();
            // The column rocket ends up in column 3, where it hits the row rocket at (3,4), which must fire too.
            board.Set(new Vec2I(2, 2), new Tile(2, SpecialType.ColumnRocket));
            board.Set(new Vec2I(3, 4), new Tile(3, SpecialType.RowRocket));

            TurnResult result = MatchEngine.TrySwap(board, new Vec2I(2, 2), new Vec2I(3, 2));

            CascadeStep step = result.Steps[0];
            Assert.AreEqual(2, step.Activations.Count);
            // Column 3 (5 tiles) + row 4 (5 tiles) share (3,4).
            Assert.AreEqual(9, step.Cleared.Count);
        }

        [Test]
        public void CascadeSteps_AreNumberedAndScoredPerLevel()
        {
            // Whatever cascades happen, each step must be numbered in order and scored
            // as cleared tiles * points * step number.
            Board board = TestHelpers.BoardFrom(
                "20102",
                "13013",
                "00300",
                "21121");
            Assert.IsFalse(MatchFinder.HasAnyMatch(board));
            bool found = board.TryFindValidMove(out Vec2I a, out Vec2I b);
            Assert.IsTrue(found);
            TurnResult result = MatchEngine.TrySwap(board, a, b);
            Assert.IsTrue(result.Valid);
            for (int i = 0; i < result.Steps.Count; i++)
            {
                Assert.AreEqual(i + 1, result.Steps[i].Level);
                Assert.AreEqual(result.Steps[i].Cleared.Count * MatchEngine.PointsPerTile * (i + 1), result.Steps[i].Score);
            }
        }

        [Test]
        public void BoardIsAlwaysPlayableAndFullAfterManyRandomTurns()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var rng = new SystemRandom(seed);
                var board = new Board(7, 8, 5, rng);
                for (int turn = 0; turn < 40; turn++)
                {
                    Assert.IsTrue(board.TryFindValidMove(out Vec2I a, out Vec2I b), "no move at seed " + seed + " turn " + turn);
                    TurnResult result = MatchEngine.TrySwap(board, a, b);
                    Assert.IsTrue(result.Valid);
                    Assert.IsFalse(MatchFinder.HasAnyMatch(board), "matches left on board");
                    for (int y = 0; y < board.Height; y++)
                        for (int x = 0; x < board.Width; x++)
                            Assert.IsFalse(board[x, y].IsEmpty);
                }
            }
        }

        [Test]
        public void SameSeed_GivesSameResult()
        {
            int Play(int seed)
            {
                var board = new Board(7, 7, 5, new SystemRandom(seed));
                int score = 0;
                for (int i = 0; i < 10; i++)
                {
                    board.TryFindValidMove(out Vec2I a, out Vec2I b);
                    score += MatchEngine.TrySwap(board, a, b).TotalScore;
                }
                return score;
            }
            Assert.AreEqual(Play(5), Play(5));
        }
    }

    public class GameSessionTests
    {
        private static LevelConfig Tiny(int moves, int target) => new LevelConfig("test", 6, 6, 4, moves, target);

        [Test]
        public void InvalidMove_DoesNotCostAMove()
        {
            var session = new GameSession(Tiny(5, 100000), new SystemRandom(1));
            TurnResult result = session.TryMove(new Vec2I(0, 0), new Vec2I(3, 3));
            Assert.IsFalse(result.Valid);
            Assert.AreEqual(5, session.MovesLeft);
        }

        [Test]
        public void ValidMove_CostsOneMoveAndAddsScore()
        {
            var session = new GameSession(Tiny(5, 100000), new SystemRandom(1));
            session.Board.TryFindValidMove(out Vec2I a, out Vec2I b);
            TurnResult result = session.TryMove(a, b);
            Assert.IsTrue(result.Valid);
            Assert.AreEqual(4, session.MovesLeft);
            Assert.AreEqual(result.TotalScore, session.Score);
            Assert.Greater(session.Score, 0);
        }

        [Test]
        public void RunningOutOfMoves_LosesAndStopsAcceptingMoves()
        {
            var session = new GameSession(Tiny(1, 100000), new SystemRandom(2));
            session.Board.TryFindValidMove(out Vec2I a, out Vec2I b);
            session.TryMove(a, b);
            Assert.AreEqual(GameState.Lost, session.State);

            session.Board.TryFindValidMove(out a, out b);
            Assert.IsFalse(session.TryMove(a, b).Valid);
        }

        [Test]
        public void ReachingTargetOnTheLastMove_IsAWin()
        {
            var session = new GameSession(Tiny(1, 10), new SystemRandom(2));
            session.Board.TryFindValidMove(out Vec2I a, out Vec2I b);
            session.TryMove(a, b);
            Assert.AreEqual(GameState.Won, session.State);
        }
    }
}
