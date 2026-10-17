using System;
using System.Collections.Generic;

namespace Match3.Core
{
    public enum GoalType
    {
        Score,      // reach a score
        Collect,    // clear N tiles of one color
        Ice,        // clear all ice
        Boxes       // break all boxes
    }

    public sealed class Goal
    {
        public readonly GoalType Type;
        public readonly int Color;
        /// <summary>For Ice and Boxes, 0 means "all of them on the board".</summary>
        public readonly int Target;

        private Goal(GoalType type, int color, int target)
        {
            Type = type;
            Color = color;
            Target = target;
        }

        public static Goal Score(int points) => new Goal(GoalType.Score, -1, points);
        public static Goal Collect(int color, int count) => new Goal(GoalType.Collect, color, count);
        public static Goal ClearIce() => new Goal(GoalType.Ice, -1, 0);
        public static Goal BreakBoxes() => new Goal(GoalType.Boxes, -1, 0);
    }

    /// <summary>A goal with the session's progress towards it.</summary>
    public sealed class GoalProgress
    {
        public Goal Goal { get; }
        public int Target { get; }
        public int Current { get; internal set; }
        public bool Done => Current >= Target;
        public int Remaining => Math.Max(0, Target - Current);

        internal GoalProgress(Goal goal, int target)
        {
            Goal = goal;
            Target = target;
        }
    }

    public sealed class LevelConfig
    {
        public string Name;
        public int Width;
        public int Height;
        public int ColorCount;
        public int Moves;
        public Goal[] Goals;
        /// <summary>Obstacle layout (see <see cref="Board"/>), or null for a plain board.</summary>
        public string[] Layout;

        public LevelConfig(string name, int width, int height, int colorCount, int moves, Goal[] goals, string[] layout = null)
        {
            Name = name;
            Width = width;
            Height = height;
            ColorCount = colorCount;
            Moves = moves;
            Goals = goals;
            Layout = layout;
        }

        /// <summary>Shorthand for a level whose only goal is a score.</summary>
        public LevelConfig(string name, int width, int height, int colorCount, int moves, int targetScore)
            : this(name, width, height, colorCount, moves, new[] { Goal.Score(targetScore) })
        {
        }
    }

    public enum GameState
    {
        Playing,
        Won,
        Lost
    }

    /// <summary>One play-through of a level: the board plus moves left, score, goals and win/lose state.</summary>
    public sealed class GameSession
    {
        public LevelConfig Level { get; }
        public Board Board { get; }
        public int MovesLeft { get; private set; }
        public int Score { get; private set; }
        public GameState State { get; private set; } = GameState.Playing;
        public IReadOnlyList<GoalProgress> Goals => _goals;

        private readonly List<GoalProgress> _goals = new List<GoalProgress>();

        public GameSession(LevelConfig level, IRandom random)
        {
            Level = level;
            Board = new Board(level.Width, level.Height, level.ColorCount, random, level.Layout);
            MovesLeft = level.Moves;

            foreach (Goal goal in level.Goals)
            {
                int target = goal.Target;
                if (goal.Type == GoalType.Ice && target == 0) target = Board.IceCellCount;
                if (goal.Type == GoalType.Boxes && target == 0) target = Board.BoxCount;
                _goals.Add(new GoalProgress(goal, target));
            }
        }

        /// <summary>1-3 stars for a won level, depending on how many moves were left. 0 if not won.</summary>
        public int Stars
        {
            get
            {
                if (State != GameState.Won) return 0;
                int stars = 1;
                if (MovesLeft * 5 >= Level.Moves) stars++;      // at least 20% of the moves left
                if (MovesLeft * 5 >= Level.Moves * 2) stars++;  // at least 40% left
                return stars;
            }
        }

        /// <summary>Tries a swap. Invalid swaps cost nothing.</summary>
        public TurnResult TryMove(Vec2I a, Vec2I b)
        {
            if (State != GameState.Playing) return new TurnResult();

            TurnResult result = MatchEngine.TrySwap(Board, a, b);
            if (!result.Valid) return result;

            MovesLeft--;
            Score += result.TotalScore;
            UpdateGoals(result);

            if (AllGoalsDone()) State = GameState.Won;
            else if (MovesLeft <= 0) State = GameState.Lost;
            return result;
        }

        private void UpdateGoals(TurnResult result)
        {
            foreach (GoalProgress progress in _goals)
            {
                switch (progress.Goal.Type)
                {
                    case GoalType.Score:
                        progress.Current = Score;
                        break;
                    case GoalType.Collect:
                        foreach (CascadeStep step in result.Steps)
                            foreach (ClearedTile c in step.Cleared)
                                if (c.Tile.Color == progress.Goal.Color) progress.Current++;
                        break;
                    case GoalType.Ice:
                        foreach (CascadeStep step in result.Steps)
                            foreach (ObstacleHit hit in step.IceCracked)
                                if (hit.Remaining == 0) progress.Current++;
                        break;
                    case GoalType.Boxes:
                        foreach (CascadeStep step in result.Steps)
                            foreach (ObstacleHit hit in step.BoxesHit)
                                if (hit.Remaining == 0) progress.Current++;
                        break;
                }
                if (progress.Current > progress.Target) progress.Current = progress.Target;
            }
        }

        private bool AllGoalsDone()
        {
            foreach (GoalProgress progress in _goals)
                if (!progress.Done) return false;
            return true;
        }
    }
}
