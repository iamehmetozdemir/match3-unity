using System.Collections.Generic;
using Match3.Core;

namespace Match3.Tests
{
    /// <summary>Returns a fixed repeating sequence instead of real random numbers.</summary>
    public sealed class ScriptedRandom : IRandom
    {
        private readonly int[] _values;
        private int _index;

        public ScriptedRandom(params int[] values)
        {
            _values = values;
        }

        public int Next(int maxExclusive)
        {
            int v = _values[_index++ % _values.Length];
            return v % maxExclusive;
        }
    }

    public static class TestHelpers
    {
        /// <summary>
        /// Colors 0-3 are used by test boards; refills draw from 4-7 (cycling), so freshly dropped tiles
        /// never match the hand-made layout.
        /// </summary>
        public static Board BoardFrom(params string[] rowsTopFirst)
        {
            return Board.FromRows(new ScriptedRandom(4, 5, 6, 7), 8, rowsTopFirst);
        }

        public static int CountTiles(Board board, int color)
        {
            int n = 0;
            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                    if (board[x, y].Color == color) n++;
            return n;
        }

        public static List<Vec2I> ClearedPositions(CascadeStep step)
        {
            var list = new List<Vec2I>();
            foreach (ClearedTile c in step.Cleared) list.Add(c.Position);
            return list;
        }
    }
}
