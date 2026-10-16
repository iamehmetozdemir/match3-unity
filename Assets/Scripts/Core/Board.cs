using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// The match-3 grid. Coordinates are (x = column, y = row) with y = 0 at the bottom,
    /// so gravity pulls tiles towards smaller y.
    /// </summary>
    public sealed class Board
    {
        private readonly Tile[,] _cells;
        private readonly IRandom _random;

        public int Width { get; }
        public int Height { get; }
        public int ColorCount { get; }

        public Board(int width, int height, int colorCount, IRandom random)
        {
            if (width < 3 || height < 3) throw new ArgumentException("Board must be at least 3x3.");
            if (colorCount < 3) throw new ArgumentException("At least 3 colors are required.");
            Width = width;
            Height = height;
            ColorCount = colorCount;
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _cells = new Tile[width, height];
            Generate();
        }

        private Board(Board source)
        {
            Width = source.Width;
            Height = source.Height;
            ColorCount = source.ColorCount;
            _random = source._random;
            _cells = (Tile[,])source._cells.Clone();
        }

        /// <summary>
        /// Builds a board from text rows listed top to bottom. Digits are colors, '.' is an empty cell.
        /// Mostly useful for tests.
        /// </summary>
        public static Board FromRows(IRandom random, int colorCount, params string[] rowsTopFirst)
        {
            int height = rowsTopFirst.Length;
            int width = rowsTopFirst[0].Length;
            var board = new Board(width, height, colorCount, random, skipGenerate: true);
            for (int row = 0; row < height; row++)
            {
                string text = rowsTopFirst[row];
                if (text.Length != width) throw new ArgumentException("All rows must have the same length.");
                for (int x = 0; x < width; x++)
                {
                    char c = text[x];
                    board._cells[x, height - 1 - row] = c == '.' ? Tile.Empty : Tile.Colored(c - '0');
                }
            }
            return board;
        }

        private Board(int width, int height, int colorCount, IRandom random, bool skipGenerate)
        {
            Width = width;
            Height = height;
            ColorCount = colorCount;
            _random = random;
            _cells = new Tile[width, height];
        }

        public Tile this[int x, int y] => _cells[x, y];
        public Tile this[Vec2I p] => _cells[p.X, p.Y];

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool InBounds(Vec2I p) => InBounds(p.X, p.Y);

        public void Set(Vec2I p, Tile tile) => _cells[p.X, p.Y] = tile;

        public void Swap(Vec2I a, Vec2I b)
        {
            Tile tmp = _cells[a.X, a.Y];
            _cells[a.X, a.Y] = _cells[b.X, b.Y];
            _cells[b.X, b.Y] = tmp;
        }

        public Board Clone() => new Board(this);

        public int RandomColor() => _random.Next(ColorCount);

        /// <summary>Fills the board with random tiles so that no match exists and at least one move is possible.</summary>
        public void Generate()
        {
            do
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        int start = RandomColor();
                        for (int i = 0; i < ColorCount; i++)
                        {
                            int color = (start + i) % ColorCount;
                            if (!WouldCompleteRun(x, y, color))
                            {
                                _cells[x, y] = Tile.Colored(color);
                                break;
                            }
                        }
                    }
                }
            } while (!HasValidMove());
        }

        // Only looks left and down, because cells are filled in that order.
        private bool WouldCompleteRun(int x, int y, int color)
        {
            bool horizontal = x >= 2 && _cells[x - 1, y].Color == color && _cells[x - 2, y].Color == color;
            bool vertical = y >= 2 && _cells[x, y - 1].Color == color && _cells[x, y - 2].Color == color;
            return horizontal || vertical;
        }

        /// <summary>Is there any swap the player can make? Swaps involving a special tile always count.</summary>
        public bool HasValidMove() => TryFindValidMove(out _, out _);

        public bool TryFindValidMove(out Vec2I from, out Vec2I to)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var a = new Vec2I(x, y);
                    if (x + 1 < Width && IsValidSwap(a, new Vec2I(x + 1, y))) { from = a; to = new Vec2I(x + 1, y); return true; }
                    if (y + 1 < Height && IsValidSwap(a, new Vec2I(x, y + 1))) { from = a; to = new Vec2I(x, y + 1); return true; }
                }
            }
            from = default;
            to = default;
            return false;
        }

        private bool IsValidSwap(Vec2I a, Vec2I b)
        {
            Tile ta = this[a];
            Tile tb = this[b];
            if (ta.IsEmpty || tb.IsEmpty) return false;
            if (ta.IsSpecial || tb.IsSpecial) return true;
            Swap(a, b);
            bool matches = MatchFinder.HasAnyMatch(this);
            Swap(a, b);
            return matches;
        }

        /// <summary>
        /// Rearranges the existing tiles until no match exists and a move is possible.
        /// Special tiles are kept. Falls back to a fresh board if shuffling keeps failing.
        /// </summary>
        public void Shuffle()
        {
            var tiles = new List<Tile>();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (!_cells[x, y].IsEmpty) tiles.Add(_cells[x, y]);

            for (int attempt = 0; attempt < 100; attempt++)
            {
                for (int i = tiles.Count - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    Tile tmp = tiles[i];
                    tiles[i] = tiles[j];
                    tiles[j] = tmp;
                }

                int index = 0;
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                        _cells[x, y] = index < tiles.Count ? tiles[index++] : Tile.Empty;

                if (!MatchFinder.HasAnyMatch(this) && HasValidMove()) return;
            }

            Generate();
        }

        public override string ToString()
        {
            var lines = new string[Height];
            for (int row = 0; row < Height; row++)
            {
                var chars = new List<string>();
                for (int x = 0; x < Width; x++) chars.Add(_cells[x, Height - 1 - row].ToString());
                lines[row] = string.Join(" ", chars);
            }
            return string.Join("\n", lines);
        }
    }
}
