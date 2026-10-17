using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// The level list. Move counts were tuned with bot simulations (see README): a "greedy" bot that
    /// always takes the move with the most goal progress should win most of the time on early levels
    /// and less often later, while a random bot should usually lose.
    /// Colors: 0 red, 1 blue, 2 green, 3 yellow, 4 purple, 5 orange.
    /// </summary>
    public static class Levels
    {
        public static readonly IReadOnlyList<LevelConfig> All = new List<LevelConfig>
        {
            new LevelConfig("Level 1", 6, 6, 4, 12, new[] { Goal.Score(1500) }),

            new LevelConfig("Level 2", 7, 7, 5, 15, new[] { Goal.Collect(0, 20), Goal.Collect(1, 20) }),

            new LevelConfig("Level 3", 7, 8, 5, 12, new[] { Goal.ClearIce() }, new[]
            {
                ".......",
                ".......",
                "..iii..",
                ".iiiii.",
                ".iiiii.",
                "..iii..",
                ".......",
                ".......",
            }),

            new LevelConfig("Level 4", 8, 8, 5, 15, new[] { Goal.ClearIce(), Goal.Collect(2, 20) }, new[]
            {
                "........",
                "........",
                "..IIII..",
                "..IIII..",
                "..IIII..",
                "..IIII..",
                "........",
                "........",
            }),

            new LevelConfig("Level 5", 8, 8, 5, 18, new[] { Goal.BreakBoxes() }, new[]
            {
                "........",
                "........",
                "........",
                ".bBBBBb.",
                "........",
                "........",
                "b......b",
                "bb....bb",
            }),

            new LevelConfig("Level 6", 8, 9, 6, 18, new[] { Goal.BreakBoxes() }, new[]
            {
                "........",
                "........",
                ".bbbbbb.",
                ".b....b.",
                ".b....b.",
                ".b....b.",
                ".bbbbbb.",
                "........",
                "........",
            }),

            new LevelConfig("Level 7", 8, 9, 6, 30, new[] { Goal.ClearIce(), Goal.Collect(3, 20) }, new[]
            {
                "........",
                "........",
                "........",
                "........",
                "........",
                "........",
                "........",
                "iiiiiiii",
                "iiiiiiii",
            }),

            new LevelConfig("Level 8", 9, 9, 6, 34, new[] { Goal.BreakBoxes(), Goal.ClearIce(), Goal.Collect(4, 15) }, new[]
            {
                ".........",
                ".........",
                "bb.....bb",
                ".........",
                "..iiiii..",
                "..iIIIi..",
                "..iiiii..",
                ".........",
                "BB.....BB",
            }),
        };
    }
}
