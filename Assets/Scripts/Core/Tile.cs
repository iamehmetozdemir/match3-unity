using System;

namespace Match3.Core
{
    public enum SpecialType
    {
        None = 0,
        RowRocket,      // clears its whole row
        ColumnRocket,   // clears its whole column
        Bomb,           // clears the 3x3 area around it
        ColorBomb       // clears every tile of one color
    }

    /// <summary>A single cell of the board. Immutable value type.</summary>
    public readonly struct Tile : IEquatable<Tile>
    {
        public const int EmptyColor = -1;
        /// <summary>Color value of a color bomb: it never matches by color.</summary>
        public const int WildColor = -2;

        public readonly int Color;
        public readonly SpecialType Special;

        public Tile(int color, SpecialType special)
        {
            Color = color;
            Special = special;
        }

        public static readonly Tile Empty = new Tile(EmptyColor, SpecialType.None);

        public static Tile Colored(int color) => new Tile(color, SpecialType.None);
        public static Tile MakeColorBomb() => new Tile(WildColor, SpecialType.ColorBomb);

        public bool IsEmpty => Color == EmptyColor;
        public bool IsSpecial => Special != SpecialType.None;
        /// <summary>True for tiles that can be part of a color match (rockets and bombs keep their color).</summary>
        public bool CanMatch => Color >= 0;

        public bool Equals(Tile other) => Color == other.Color && Special == other.Special;
        public override bool Equals(object obj) => obj is Tile other && Equals(other);
        public override int GetHashCode() => unchecked(Color * 31 + (int)Special);
        public static bool operator ==(Tile a, Tile b) => a.Equals(b);
        public static bool operator !=(Tile a, Tile b) => !a.Equals(b);

        public override string ToString()
        {
            if (IsEmpty) return ".";
            return Special == SpecialType.None ? Color.ToString() : Color + ":" + Special;
        }
    }
}
