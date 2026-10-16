using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Resolves a player swap on a board: validates it, clears matches, triggers special tiles,
    /// applies gravity and refills, and repeats until the board settles.
    /// The board is mutated; the returned <see cref="TurnResult"/> describes every step for animation.
    /// </summary>
    public static class MatchEngine
    {
        public const int PointsPerTile = 10;
        private const int AnyColor = -3;
        private const int MaxCascadeSteps = 100;

        private readonly struct Pending
        {
            public readonly Vec2I Origin;
            public readonly SpecialType Type;
            public readonly int TargetColor;
            public Pending(Vec2I origin, SpecialType type, int targetColor) { Origin = origin; Type = type; TargetColor = targetColor; }
        }

        public static TurnResult TrySwap(Board board, Vec2I a, Vec2I b)
        {
            var result = new TurnResult();
            if (!board.InBounds(a) || !board.InBounds(b) || !a.IsAdjacentTo(b)) return result;

            Tile ta = board[a];
            Tile tb = board[b];
            if (ta.IsEmpty || tb.IsEmpty) return result;

            board.Swap(a, b);

            // After the swap, ta sits at b and tb sits at a.
            var pending = new List<Pending>();
            AddSwapEffect(pending, ta, b, tb);
            AddSwapEffect(pending, tb, a, ta);

            if (pending.Count == 0 && !MatchFinder.HasAnyMatch(board))
            {
                board.Swap(a, b); // no effect: undo
                return result;
            }

            result.Valid = true;
            var preferred = new[] { a, b };
            int level = 1;

            while (level <= MaxCascadeSteps)
            {
                List<MatchGroup> groups = MatchFinder.FindGroups(board);
                if (groups.Count == 0 && pending.Count == 0) break;

                CascadeStep step = ResolveStep(board, groups, pending, level == 1 ? preferred : new Vec2I[0], level);
                result.Steps.Add(step);
                result.TotalScore += step.Score;
                pending.Clear();
                level++;
            }

            if (!board.HasValidMove())
            {
                board.Shuffle();
                result.Reshuffled = true;
            }
            return result;
        }

        private static void AddSwapEffect(List<Pending> pending, Tile moved, Vec2I position, Tile other)
        {
            if (!moved.IsSpecial) return;
            if (moved.Special == SpecialType.ColorBomb)
            {
                int target = other.Special == SpecialType.ColorBomb ? AnyColor : other.Color;
                pending.Add(new Pending(position, SpecialType.ColorBomb, target));
            }
            else
            {
                pending.Add(new Pending(position, moved.Special, moved.Color));
            }
        }

        private static CascadeStep ResolveStep(Board board, List<MatchGroup> groups, List<Pending> pending, IList<Vec2I> preferred, int level)
        {
            var step = new CascadeStep { Level = level };
            var clear = new HashSet<Vec2I>();
            var queue = new Queue<Vec2I>();
            var activated = new HashSet<Vec2I>();
            var spawns = new Dictionary<Vec2I, Tile>();

            void AddCell(Vec2I p)
            {
                if (!board.InBounds(p) || board[p].IsEmpty) return;
                if (clear.Add(p)) queue.Enqueue(p);
            }

            void Activate(Vec2I origin, SpecialType type, int targetColor)
            {
                if (!activated.Add(origin)) return;
                step.Activations.Add(new SpecialActivation(origin, type));
                AddCell(origin);
                switch (type)
                {
                    case SpecialType.RowRocket:
                        for (int x = 0; x < board.Width; x++) AddCell(new Vec2I(x, origin.Y));
                        break;
                    case SpecialType.ColumnRocket:
                        for (int y = 0; y < board.Height; y++) AddCell(new Vec2I(origin.X, y));
                        break;
                    case SpecialType.Bomb:
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                                AddCell(new Vec2I(origin.X + dx, origin.Y + dy));
                        break;
                    case SpecialType.ColorBomb:
                        for (int y = 0; y < board.Height; y++)
                            for (int x = 0; x < board.Width; x++)
                            {
                                Tile t = board[x, y];
                                if (t.IsEmpty) continue;
                                if (targetColor == AnyColor || t.Color == targetColor) AddCell(new Vec2I(x, y));
                            }
                        break;
                }
            }

            foreach (MatchGroup group in groups)
            {
                foreach (Vec2I cell in group.Cells) AddCell(cell);

                SpecialType reward = group.Reward;
                if (reward != SpecialType.None)
                {
                    Vec2I at = PickSpawnPosition(group, preferred);
                    spawns[at] = reward == SpecialType.ColorBomb ? Tile.MakeColorBomb() : new Tile(group.Color, reward);
                }
            }

            foreach (Pending p in pending) Activate(p.Origin, p.Type, p.TargetColor);

            // Tiles that are cleared and special trigger their own effect (chain reactions).
            while (queue.Count > 0)
            {
                Vec2I p = queue.Dequeue();
                Tile t = board[p];
                if (!t.IsSpecial) continue;
                int target = t.Special == SpecialType.ColorBomb ? DominantColor(board) : t.Color;
                Activate(p, t.Special, target);
            }

            // A tile that turns into a special survives this step.
            foreach (KeyValuePair<Vec2I, Tile> spawn in spawns) clear.Remove(spawn.Key);

            var clearedList = new List<Vec2I>(clear);
            clearedList.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.X.CompareTo(q.X));
            foreach (Vec2I p in clearedList)
            {
                step.Cleared.Add(new ClearedTile(p, board[p]));
                board.Set(p, Tile.Empty);
            }
            foreach (KeyValuePair<Vec2I, Tile> spawn in spawns)
            {
                step.Created.Add(new CreatedTile(spawn.Key, spawn.Value));
                board.Set(spawn.Key, spawn.Value);
            }

            step.Score = clearedList.Count * PointsPerTile * level;
            ApplyGravityAndRefill(board, step);
            return step;
        }

        private static Vec2I PickSpawnPosition(MatchGroup group, IList<Vec2I> preferred)
        {
            foreach (Vec2I p in preferred)
                if (group.Cells.Contains(p)) return p;
            return group.RunCenter;
        }

        private static int DominantColor(Board board)
        {
            var counts = new int[board.ColorCount];
            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    Tile t = board[x, y];
                    if (t.CanMatch && t.Color < counts.Length) counts[t.Color]++;
                }

            int best = 0;
            for (int c = 1; c < counts.Length; c++)
                if (counts[c] > counts[best]) best = c;
            return best;
        }

        private static void ApplyGravityAndRefill(Board board, CascadeStep step)
        {
            for (int x = 0; x < board.Width; x++)
            {
                int writeY = 0;
                for (int y = 0; y < board.Height; y++)
                {
                    Tile t = board[x, y];
                    if (t.IsEmpty) continue;
                    if (y != writeY)
                    {
                        var from = new Vec2I(x, y);
                        var to = new Vec2I(x, writeY);
                        board.Set(to, t);
                        board.Set(from, Tile.Empty);
                        step.Moves.Add(new TileMove(from, to, t));
                    }
                    writeY++;
                }

                int fresh = 0;
                for (int y = writeY; y < board.Height; y++)
                {
                    Tile t = Tile.Colored(board.RandomColor());
                    var to = new Vec2I(x, y);
                    board.Set(to, t);
                    step.Drops.Add(new TileDrop(to, t, board.Height + fresh));
                    fresh++;
                }
            }
        }
    }
}
