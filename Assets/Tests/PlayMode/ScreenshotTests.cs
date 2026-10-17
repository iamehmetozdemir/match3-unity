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
    /// Not a real test: renders the running game into docs/ to check how things look.
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

        // Renders the main camera into a texture. (The IMGUI interface is not part of this image.)
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
        public IEnumerator CaptureLevels()
        {
            while (GameController.Instance == null) yield return null;
            GameController game = GameController.Instance;

            game.StartLevel(Levels.All.Count - 1);
            yield return new WaitForSeconds(0.3f);
            Capture("obstacles.png");

            // Play until some obstacles took damage, then capture again.
            for (int i = 0; i < 12 && game.Session.State == GameState.Playing; i++)
            {
                game.Session.Board.TryFindValidMove(out Vec2I a, out Vec2I b);
                game.TryPlay(a, b);
                while (game.IsBusy) yield return null;
            }
            Capture("obstacles-after-moves.png");

            game.StartLevel(0);
            yield return null;
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
