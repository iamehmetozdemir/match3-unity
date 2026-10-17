using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// The match-3 grid. Coordinates are (x = column, y = row) with y = 0 at the bottom,
    /// so gravity pulls tiles towards smaller y.
    ///
    /// Besides tiles, a cell can have two kinds of obstacles:
    /// - Ice: a layer under the tile. Clearing the tile on top cracks one layer.
    /// - Box: fills the cell instead of a tile. It does not move, blocks falling tiles,
    ///   and loses one health when a neighbouring tile is matched or a special tile hits it.
    /// </summary>
    public sealed class Board
    {
        private readonly Tile[,] _cells;
        private readonly int[,] _ice;
        private readonly int[,] _box;
        private readonly IRandom _random;

        public int Width { get; }
        public int Height { get; }
        public int ColorCount { get; }

        /// <param name="layoutTopFirst">
        /// Optional obstacle layout, one string per row from the top: '.' normal cell,
        /// 'i' / 'I' ice with 1 / 2 layers, 'b' / 'B' box with 1 / 2 health.
        /// </param>
        public Board(int width, int height, int colorCount, IRandom random, string[] layoutTopFirst = null)
        {
            if (width < 3 || height < 3) throw new ArgumentException("Board must be at least 3x3.");
            if (colorCount < 3) throw new ArgumentException("At least 3 colors are required.");
            Width = width;
            Height = height;
            ColorCount = colorCount;
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _cells = new Tile[width, height];
            _ice = new int[width, height];
            _box = new int[width, height];
            if (layoutTopFirst != null) ApplyLayout(layoutTopFirst);
            Generate();
        }

        private Board(Board source)
        {
            Width = source.Width;
            Height = source.Height;
            ColorCount = source.ColorCount;
            _random = source._random;
            _cells = (Tile[,])source._cells.Clone();
            _ice = (int[,])source._ice.Clone();
            _box = (int[,])source._box.Clone();
        }

        private Board(int width, int height, int colorCount, IRandom random, bool skipGenerate)
        {
            Width = width;
            Height = height;
            ColorCount = colorCount;
            _random = random;
            _cells = new Tile[width, height];
            _ice = new int[width, height];
            _box = new int[width, height];
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

        private void ApplyLayout(string[] rowsTopFirst)
        {
            if (rowsTopFirst.Length != Height) throw new ArgumentException("Layout must have one row per board row.");
            for (int row = 0; row < Height; row++)
            {
                string text = rowsTopFirst[row];
                if (text.Length != Width) throw new ArgumentException("Layout rows must match the board width.");
                int y = Height - 1 - row;
                for (int x = 0; x < Width; x++)
                {
                    switch (text[x])
                    {
                        case 'i': _ice[x, y] = 1; break;
                        case 'I': _ice[x, y] = 2; break;
                        case 'b': _box[x, y] = 1; break;
                        case 'B': _box[x, y] = 2; break;
                    }
                }
            }
        }

        // ---------- access ----------

        public Tile this[int x, int y] => _cells[x, y];
        public Tile this[Vec2I p] => _cells[p.X, p.Y];

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool InBounds(Vec2I p) => InBounds(p.X, p.Y);

        public void Set(Vec2I p, Tile tile) => _cells[p.X, p.Y] = tile;

        public int IceAt(int x, int y) => _ice[x, y];
        public int IceAt(Vec2I p) => _ice[p.X, p.Y];
        public void SetIce(Vec2I p, int layers) => _ice[p.X, p.Y] = layers;

        public int BoxAt(int x, int y) => _box[x, y];
        public int BoxAt(Vec2I p) => _box[p.X, p.Y];
        public bool HasBox(int x, int y) => _box[x, y] > 0;
        public bool HasBox(Vec2I p) => _box[p.X, p.Y] > 0;

        /// <summary>Puts a box with the given health in a cell (removing its tile), or removes it with 0.</summary>
        public void SetBox(Vec2I p, int health)
        {
            _box[p.X, p.Y] = health;
            if (health > 0) _cells[p.X, p.Y] = Tile.Empty;
        }

        /// <summary>Number of cells that still have ice.</summary>
        public int IceCellCount
        {
            get
            {
                int n = 0;
                foreach (int layers in _ice) if (layers > 0) n++;
                return n;
            }
        }

        public int BoxCount
        {
            get
            {
                int n = 0;
                foreach (int health in _box) if (health > 0) n++;
                return n;
            }
        }

        public void Swap(Vec2I a, Vec2I b)
        {
            Tile tmp = _cells[a.X, a.Y];
            _cells[a.X, a.Y] = _cells[b.X, b.Y];
            _cells[b.X, b.Y] = tmp;
        }

        public Board Clone() => new Board(this);

        public int RandomColor() => _random.Next(ColorCount);

        // ---------- generation ----------

        /// <summary>Fills every non-box cell with random tiles so that no match exists and at least one move is possible.</summary>
        public void Generate()
        {
            do
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        if (HasBox(x, y)) { _cells[x, y] = Tile.Empty; continue; }
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

        // ---------- moves ----------

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

        /// <summary>All swaps that would be accepted, each listed once.</summary>
        public List<KeyValuePair<Vec2I, Vec2I>> AllValidMoves()
        {
            var moves = new List<KeyValuePair<Vec2I, Vec2I>>();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var a = new Vec2I(x, y);
                    if (x + 1 < Width && IsValidSwap(a, new Vec2I(x + 1, y))) moves.Add(new KeyValuePair<Vec2I, Vec2I>(a, new Vec2I(x + 1, y)));
                    if (y + 1 < Height && IsValidSwap(a, new Vec2I(x, y + 1))) moves.Add(new KeyValuePair<Vec2I, Vec2I>(a, new Vec2I(x, y + 1)));
                }
            }
            return moves;
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
        /// Special tiles, ice and boxes stay. Falls back to a fresh board if shuffling keeps failing.
        /// </summary>
        public void Shuffle()
        {
            var tiles = new List<Tile>();
            var slots = new List<Vec2I>();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (HasBox(x, y)) continue;
                    slots.Add(new Vec2I(x, y));
                    if (!_cells[x, y].IsEmpty) tiles.Add(_cells[x, y]);
                }
            }

            for (int attempt = 0; attempt < 100; attempt++)
            {
                for (int i = tiles.Count - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    Tile tmp = tiles[i];
                    tiles[i] = tiles[j];
                    tiles[j] = tmp;
                }

                for (int i = 0; i < slots.Count; i++)
                    _cells[slots[i].X, slots[i].Y] = i < tiles.Count ? tiles[i] : Tile.Empty;

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
                int y = Height - 1 - row;
                for (int x = 0; x < Width; x++) chars.Add(HasBox(x, y) ? "#" : _cells[x, y].ToString());
                lines[row] = string.Join(" ", chars);
            }
            return string.Join("\n", lines);
        }
    }
}
