using System.Collections;
using System.IO;
using Match3.Core;
using Match3.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Match3.PlayTests
{
    /// <summary>
    /// Not a real test: renders the running game into docs/ so the README can show it.
    /// Run on demand with the "Screenshots" category.
    /// </summary>
    [Category("Screenshots")]
    public class ScreenshotTests
    {
        private static string DocsPath(string fileName)
        {
            string docs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs"));
            Directory.CreateDirectory(docs);
            return Path.Combine(docs, fileName);
        }

        // Renders the main camera into a texture. (The on-screen HUD is IMGUI and is not part of this image.)
        private static void Capture(string fileName)
        {
            Camera cam = Camera.main;
            var rt = new RenderTexture(720, 1000, 24);
            RenderTexture previous = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            shot.Apply();
            cam.targetTexture = null;
            RenderTexture.active = previous;
            File.WriteAllBytes(DocsPath(fileName), shot.EncodeToPNG());
            Object.Destroy(shot);
            rt.Release();
        }

        [UnityTest]
        public IEnumerator CaptureGameplay()
        {
            while (GameController.Instance == null) yield return null;
            yield return new WaitForSeconds(0.5f);
            GameController game = GameController.Instance;

            Capture("board.png");

            // Play a few turns, then capture a mid-game state.
            for (int i = 0; i < 4; i++)
            {
                game.Session.Board.TryFindValidMove(out Vec2I a, out Vec2I b);
                game.TryPlay(a, b);
                while (game.IsBusy) yield return null;
            }
            Capture("board-after-moves.png");

            // Show every special tile type.
            Board board = game.Session.Board;
            board.Set(new Vec2I(1, 4), new Tile(board[1, 4].Color, SpecialType.RowRocket));
            board.Set(new Vec2I(3, 4), new Tile(board[3, 4].Color, SpecialType.ColumnRocket));
            board.Set(new Vec2I(1, 1), new Tile(board[1, 1].Color, SpecialType.Bomb));
            board.Set(new Vec2I(4, 1), Tile.MakeColorBomb());
            game.BoardView.Resync(board);
            yield return null;
            Capture("specials.png");
        }
    }
}
