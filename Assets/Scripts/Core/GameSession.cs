using System.Collections.Generic;

namespace Match3.Core
{
    public sealed class LevelConfig
    {
        public string Name;
        public int Width;
        public int Height;
        public int ColorCount;
        public int Moves;
        public int TargetScore;

        public LevelConfig(string name, int width, int height, int colorCount, int moves, int targetScore)
        {
            Name = name;
            Width = width;
            Height = height;
            ColorCount = colorCount;
            Moves = moves;
            TargetScore = targetScore;
        }
    }

    public static class Levels
    {
        // Targets were tuned with bot simulations (random-move bot vs. greedy bot, 300 games per level).
        // A target sits between what a random player and a greedy player score in the given moves,
        // getting closer to the greedy score on later levels.
        public static readonly IReadOnlyList<LevelConfig> All = new List<LevelConfig>
        {
            new LevelConfig("Level 1", 6, 6, 4, 15, 2000),
            new LevelConfig("Level 2", 7, 7, 5, 18, 2300),
            new LevelConfig("Level 3", 7, 8, 5, 20, 3500),
            new LevelConfig("Level 4", 8, 8, 5, 22, 4200),
            new LevelConfig("Level 5", 8, 9, 6, 30, 4800),
        };
    }

    public enum GameState
    {
        Playing,
        Won,
        Lost
    }

    /// <summary>One play-through of a level: the board plus moves left, score and win/lose state.</summary>
    public sealed class GameSession
    {
        public LevelConfig Level { get; }
        public Board Board { get; }
        public int MovesLeft { get; private set; }
        public int Score { get; private set; }
        public GameState State { get; private set; } = GameState.Playing;

        public GameSession(LevelConfig level, IRandom random)
        {
            Level = level;
            Board = new Board(level.Width, level.Height, level.ColorCount, random);
            MovesLeft = level.Moves;
        }

        /// <summary>Tries a swap. Invalid swaps cost nothing.</summary>
        public TurnResult TryMove(Vec2I a, Vec2I b)
        {
            if (State != GameState.Playing) return new TurnResult();

            TurnResult result = MatchEngine.TrySwap(Board, a, b);
            if (!result.Valid) return result;

            MovesLeft--;
            Score += result.TotalScore;

            if (Score >= Level.TargetScore) State = GameState.Won;
            else if (MovesLeft <= 0) State = GameState.Lost;
            return result;
        }
    }
}
