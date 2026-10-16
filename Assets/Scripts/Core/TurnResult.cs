using System.Collections.Generic;

namespace Match3.Core
{
    // Plain data describing what happened, so a view layer can animate it step by step.

    public readonly struct ClearedTile
    {
        public readonly Vec2I Position;
        public readonly Tile Tile;
        public ClearedTile(Vec2I position, Tile tile) { Position = position; Tile = tile; }
    }

    /// <summary>A special tile that was created by a 4+ match (it replaces the tile at Position).</summary>
    public readonly struct CreatedTile
    {
        public readonly Vec2I Position;
        public readonly Tile Tile;
        public CreatedTile(Vec2I position, Tile tile) { Position = position; Tile = tile; }
    }

    public readonly struct SpecialActivation
    {
        public readonly Vec2I Origin;
        public readonly SpecialType Type;
        public SpecialActivation(Vec2I origin, SpecialType type) { Origin = origin; Type = type; }
    }

    /// <summary>An existing tile that fell down.</summary>
    public readonly struct TileMove
    {
        public readonly Vec2I From;
        public readonly Vec2I To;
        public readonly Tile Tile;
        public TileMove(Vec2I from, Vec2I to, Tile tile) { From = from; To = to; Tile = tile; }
    }

    /// <summary>A brand new tile that falls in from above the board. FromY is its starting row (>= board height).</summary>
    public readonly struct TileDrop
    {
        public readonly Vec2I To;
        public readonly Tile Tile;
        public readonly int FromY;
        public TileDrop(Vec2I to, Tile tile, int fromY) { To = to; Tile = tile; FromY = fromY; }
    }

    /// <summary>One round of "clear matches, apply gravity, refill".</summary>
    public sealed class CascadeStep
    {
        public int Level;
        public int Score;
        public readonly List<ClearedTile> Cleared = new List<ClearedTile>();
        public readonly List<CreatedTile> Created = new List<CreatedTile>();
        public readonly List<SpecialActivation> Activations = new List<SpecialActivation>();
        public readonly List<TileMove> Moves = new List<TileMove>();
        public readonly List<TileDrop> Drops = new List<TileDrop>();
    }

    public sealed class TurnResult
    {
        /// <summary>False if the swap was rejected (nothing changed on the board).</summary>
        public bool Valid;
        public int TotalScore;
        /// <summary>True if the board had no moves left after the turn and was shuffled.</summary>
        public bool Reshuffled;
        public readonly List<CascadeStep> Steps = new List<CascadeStep>();
    }
}
