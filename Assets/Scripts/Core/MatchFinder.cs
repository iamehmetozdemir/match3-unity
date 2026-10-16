using System.Collections.Generic;

namespace Match3.Core
{
    public enum MatchOrientation
    {
        Horizontal,
        Vertical
    }

    /// <summary>
    /// A set of same-colored tiles connected through runs of 3 or more.
    /// An L or T shape (a horizontal and a vertical run sharing a tile) is a single group.
    /// </summary>
    public sealed class MatchGroup
    {
        public int Color;
        public List<Vec2I> Cells = new List<Vec2I>();
        public bool HasHorizontal;
        public bool HasVertical;
        public int LongestRun;
        public MatchOrientation LongestOrientation;
        /// <summary>Middle tile of the longest run, used as the default place for a created special tile.</summary>
        public Vec2I RunCenter;

        /// <summary>The special tile this match creates (None for a plain 3-match).</summary>
        public SpecialType Reward
        {
            get
            {
                if (LongestRun >= 5) return SpecialType.ColorBomb;
                if (HasHorizontal && HasVertical) return SpecialType.Bomb;
                if (LongestRun == 4)
                    return LongestOrientation == MatchOrientation.Horizontal ? SpecialType.RowRocket : SpecialType.ColumnRocket;
                return SpecialType.None;
            }
        }
    }

    public static class MatchFinder
    {
        private struct Run
        {
            public int Color;
            public int X;
            public int Y;
            public int Length;
            public bool Horizontal;

            public Vec2I Cell(int i) => Horizontal ? new Vec2I(X + i, Y) : new Vec2I(X, Y + i);
        }

        /// <summary>Cheap check used for move validation and shuffling.</summary>
        public static bool HasAnyMatch(Board board)
        {
            int w = board.Width, h = board.Height;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Tile t = board[x, y];
                    if (!t.CanMatch) continue;
                    if (x + 2 < w && board[x + 1, y].Color == t.Color && board[x + 2, y].Color == t.Color) return true;
                    if (y + 2 < h && board[x, y + 1].Color == t.Color && board[x, y + 2].Color == t.Color) return true;
                }
            }
            return false;
        }

        public static List<MatchGroup> FindGroups(Board board)
        {
            var runs = CollectRuns(board);
            var groups = new List<MatchGroup>();
            if (runs.Count == 0) return groups;

            // Union runs that share a tile.
            var parent = new int[runs.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;

            var owner = new Dictionary<int, int>();
            for (int i = 0; i < runs.Count; i++)
            {
                for (int k = 0; k < runs[i].Length; k++)
                {
                    Vec2I c = runs[i].Cell(k);
                    int key = c.Y * board.Width + c.X;
                    if (owner.TryGetValue(key, out int other)) Union(parent, i, other);
                    else owner[key] = i;
                }
            }

            var groupOfRoot = new Dictionary<int, MatchGroup>();
            var cellSets = new Dictionary<MatchGroup, HashSet<Vec2I>>();
            for (int i = 0; i < runs.Count; i++)
            {
                Run run = runs[i];
                int root = Find(parent, i);
                if (!groupOfRoot.TryGetValue(root, out MatchGroup group))
                {
                    group = new MatchGroup { Color = run.Color };
                    groupOfRoot[root] = group;
                    cellSets[group] = new HashSet<Vec2I>();
                    groups.Add(group);
                }

                for (int k = 0; k < run.Length; k++) cellSets[group].Add(run.Cell(k));
                if (run.Horizontal) group.HasHorizontal = true; else group.HasVertical = true;
                if (run.Length > group.LongestRun)
                {
                    group.LongestRun = run.Length;
                    group.LongestOrientation = run.Horizontal ? MatchOrientation.Horizontal : MatchOrientation.Vertical;
                    group.RunCenter = run.Cell(run.Length / 2);
                }
            }

            foreach (MatchGroup group in groups)
            {
                group.Cells.AddRange(cellSets[group]);
                group.Cells.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            }
            return groups;
        }

        private static List<Run> CollectRuns(Board board)
        {
            var runs = new List<Run>();
            int w = board.Width, h = board.Height;

            for (int y = 0; y < h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    Tile t = board[x, y];
                    if (!t.CanMatch) { x++; continue; }
                    int end = x + 1;
                    while (end < w && board[end, y].Color == t.Color) end++;
                    if (end - x >= 3) runs.Add(new Run { Color = t.Color, X = x, Y = y, Length = end - x, Horizontal = true });
                    x = end;
                }
            }

            for (int x = 0; x < w; x++)
            {
                int y = 0;
                while (y < h)
                {
                    Tile t = board[x, y];
                    if (!t.CanMatch) { y++; continue; }
                    int end = y + 1;
                    while (end < h && board[x, end].Color == t.Color) end++;
                    if (end - y >= 3) runs.Add(new Run { Color = t.Color, X = x, Y = y, Length = end - y, Horizontal = false });
                    y = end;
                }
            }
            return runs;
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a), rb = Find(parent, b);
            if (ra != rb) parent[rb] = ra;
        }
    }
}
