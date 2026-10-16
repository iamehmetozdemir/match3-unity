using System.Collections;
using Match3.Core;
using Match3.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Match3.PlayTests
{
    /// <summary>Starts the real game in Play Mode and plays it, to catch runtime errors in the view layer.</summary>
    public class GameSmokeTests
    {
        private static IEnumerator WaitForController()
        {
            float timeout = 5f;
            while (GameController.Instance == null && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            Assert.IsNotNull(GameController.Instance, "GameController did not bootstrap itself");
            yield return null;
        }

        private static IEnumerator WaitUntilIdle()
        {
            float timeout = 20f;
            while (GameController.Instance.IsBusy && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            Assert.IsFalse(GameController.Instance.IsBusy, "turn animation never finished");
        }

        [UnityTest]
        public IEnumerator GameStartsItselfAndShowsAFullBoard()
        {
            yield return WaitForController();
            GameController game = GameController.Instance;

            Assert.AreEqual(GameState.Playing, game.Session.State);
            Assert.AreEqual(game.Session.Board.Width * game.Session.Board.Height, game.BoardView.TileCount);
            Assert.IsNotNull(Camera.main);
        }

        [UnityTest]
        public IEnumerator PlayingManyTurnsKeepsViewAndBoardInSync()
        {
            yield return WaitForController();
            GameController game = GameController.Instance;
            int played = 0;

            for (int i = 0; i < 25 && game.Session.State == GameState.Playing; i++)
            {
                Assert.IsTrue(game.Session.Board.TryFindValidMove(out Vec2I a, out Vec2I b));
                Assert.IsTrue(game.TryPlay(a, b));
                yield return WaitUntilIdle();
                played++;

                Board board = game.Session.Board;
                Assert.AreEqual(board.Width * board.Height, game.BoardView.TileCount, "tile views out of sync after turn " + played);
            }

            Assert.Greater(played, 3);
            Assert.Greater(game.Session.Score, 0);
        }

        [UnityTest]
        public IEnumerator InvalidSwapIsAnimatedBackAndCostsNothing()
        {
            yield return WaitForController();
            GameController game = GameController.Instance;
            int movesBefore = game.Session.MovesLeft;

            // Find an adjacent pair that does not make a match.
            Board board = game.Session.Board;
            Vec2I a = default, b = default;
            bool found = false;
            for (int y = 0; y < board.Height && !found; y++)
            {
                for (int x = 0; x + 1 < board.Width && !found; x++)
                {
                    Board copy = board.Clone();
                    if (!MatchEngine.TrySwap(copy, new Vec2I(x, y), new Vec2I(x + 1, y)).Valid)
                    {
                        a = new Vec2I(x, y);
                        b = new Vec2I(x + 1, y);
                        found = true;
                    }
                }
            }
            Assert.IsTrue(found);

            Assert.IsTrue(game.TryPlay(a, b));
            yield return WaitUntilIdle();

            Assert.AreEqual(movesBefore, game.Session.MovesLeft);
            Assert.AreEqual(board.Width * board.Height, game.BoardView.TileCount);
        }
    }
}
