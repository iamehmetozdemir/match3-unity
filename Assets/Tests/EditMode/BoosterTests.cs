using System.Linq;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    public class BoosterTests
    {
        // (x + 2y) % 4 pattern: no matches anywhere.
        private static Board Base()
        {
            return TestHelpers.BoardFrom(
                "01230",
                "23012",
                "01230",
                "23012",
                "01230");
        }

        [Test]
        public void Hammer_ClearsOneTile_AndTheColumnRefills()
        {
            Board board = Base();
            TurnResult result = MatchEngine.Hammer(board, new Vec2I(2, 2));

            Assert.IsTrue(result.Valid);
            CascadeStep step = result.Steps[0];
            Assert.AreEqual(1, step.Cleared.Count);
            Assert.AreEqual(new Vec2I(2, 2), step.Cleared[0].Position);
            Assert.AreEqual(2, step.Moves.Count, "the two tiles above fall one row");
            Assert.AreEqual(1, step.Drops.Count);
            Assert.IsFalse(board[2, 2].IsEmpty);
        }

        [Test]
        public void Hammer_OnABox_TakesOneHealth()
        {
            Board board = Base();
            board.SetBox(new Vec2I(1, 1), 2);

            TurnResult result = MatchEngine.Hammer(board, new Vec2I(1, 1));

            Assert.IsTrue(result.Valid);
            CollectionAssert.AreEqual(new[] { new ObstacleHit(new Vec2I(1, 1), 1) }, result.Steps[0].BoxesHit);
            Assert.AreEqual(0, result.Steps[0].Cleared.Count);
            Assert.AreEqual(1, board.BoxAt(1, 1));
        }

        [Test]
        public void Hammer_CracksIceUnderTheTile()
        {
            Board board = Base();
            board.SetIce(new Vec2I(3, 0), 2);

            TurnResult result = MatchEngine.Hammer(board, new Vec2I(3, 0));

            CollectionAssert.AreEqual(new[] { new ObstacleHit(new Vec2I(3, 0), 1) }, result.Steps[0].IceCracked);
        }

        [Test]
        public void Hammer_OnASpecialTile_FiresIt()
        {
            Board board = Base();
            board.Set(new Vec2I(2, 3), new Tile(0, SpecialType.RowRocket));

            TurnResult result = MatchEngine.Hammer(board, new Vec2I(2, 3));

            CascadeStep step = result.Steps[0];
            Assert.AreEqual(SpecialType.RowRocket, step.Activations.Single().Type);
            Assert.AreEqual(board.Width, step.Cleared.Count);
            Assert.IsTrue(step.Cleared.All(c => c.Position.Y == 3));
        }

        [Test]
        public void Hammer_OutsideTheBoard_IsRejected()
        {
            Board board = Base();
            string before = board.ToString();
            Assert.IsFalse(MatchEngine.Hammer(board, new Vec2I(9, 9)).Valid);
            Assert.AreEqual(before, board.ToString());
        }

        [Test]
        public void SessionBoosters_CostNoMove_AndRunOut()
        {
            var level = new LevelConfig("t", 6, 6, 4, 10, 1000000) { Hammers = 1, Shuffles = 1 };
            var session = new GameSession(level, new SystemRandom(3));

            Assert.IsTrue(session.UseHammer(new Vec2I(2, 2)).Valid);
            Assert.AreEqual(0, session.Hammers);
            Assert.AreEqual(10, session.MovesLeft);
            Assert.Greater(session.Score, 0);
            Assert.IsFalse(session.UseHammer(new Vec2I(2, 2)).Valid, "no hammers left");

            TurnResult shuffle = session.UseShuffle();
            Assert.IsTrue(shuffle.Valid);
            Assert.IsTrue(shuffle.Reshuffled);
            Assert.AreEqual(0, session.Shuffles);
            Assert.AreEqual(10, session.MovesLeft);
            Assert.IsFalse(MatchFinder.HasAnyMatch(session.Board));
            Assert.IsFalse(session.UseShuffle().Valid, "no shuffles left");
        }

        [Test]
        public void HammeringTheLastBox_WinsTheLevel()
        {
            var level = new LevelConfig("t", 6, 6, 4, 10, new[] { Goal.BreakBoxes() }, new[]
            {
                "......",
                "......",
                "..b...",
                "......",
                "......",
                "......",
            });
            var session = new GameSession(level, new SystemRandom(5));

            session.UseHammer(new Vec2I(2, 3));

            Assert.AreEqual(1, session.Goals[0].Current);
            Assert.AreEqual(GameState.Won, session.State);
        }
    }
}
