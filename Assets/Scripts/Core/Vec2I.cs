using System;

namespace Match3.Core
{
    /// <summary>Integer grid position. X is the column (left to right), Y the row (bottom to top).</summary>
    public readonly struct Vec2I : IEquatable<Vec2I>
    {
        public readonly int X;
        public readonly int Y;

        public Vec2I(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool IsAdjacentTo(Vec2I other)
        {
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y) == 1;
        }

        public bool Equals(Vec2I other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Vec2I other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
        public static bool operator ==(Vec2I a, Vec2I b) => a.Equals(b);
        public static bool operator !=(Vec2I a, Vec2I b) => !a.Equals(b);
        public override string ToString() => "(" + X + "," + Y + ")";
    }
}
