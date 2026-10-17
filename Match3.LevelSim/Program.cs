using System;
using System.Collections.Generic;
using System.Linq;
using Match3.Core;

// Plays each level 200 times with a random bot and a greedy bot (picks the move with the most
// immediate goal progress) and prints win rates. I used it to tune the move counts in Levels.cs.
static class P
{
    // Value of a move = goal progress (weighted) + a little score.
    static double Value(GameSession s, TurnResult r)
    {
        double v = r.TotalScore * 0.01;
        foreach (var g in s.Goals)
        {
            if (g.Done) continue;
            int gain = 0;
            foreach (var st in r.Steps)
            {
                switch (g.Goal.Type)
                {
                    case GoalType.Collect: gain += st.Cleared.Count(c => c.Tile.Color == g.Goal.Color); break;
                    case GoalType.Ice: gain += st.IceCracked.Count; break;
                    case GoalType.Boxes: gain += st.BoxesHit.Count; break;
                    case GoalType.Score: gain += r.TotalScore / 50; break;
                }
            }
            v += Math.Min(gain, g.Remaining * 2) * 10;
        }
        return v;
    }

    static (bool won, int movesLeft) Play(LevelConfig lv, int seed, bool greedy)
    {
        var s = new GameSession(lv, new SystemRandom(seed));
        var rnd = new Random(seed * 31 + 7);
        while (s.State == GameState.Playing)
        {
            var moves = s.Board.AllValidMoves();
            KeyValuePair<Vec2I, Vec2I> pick = moves[rnd.Next(moves.Count)];
            if (greedy)
            {
                double best = -1;
                foreach (var m in moves)
                {
                    var c = s.Board.Clone();
                    double val = Value(s, MatchEngine.TrySwap(c, m.Key, m.Value)) + rnd.NextDouble() * 0.01;
                    if (val > best) { best = val; pick = m; }
                }
            }
            s.TryMove(pick.Key, pick.Value);
        }
        return (s.State == GameState.Won, s.MovesLeft);
    }

    static void Main(string[] args)
    {
        int n = 200;
        foreach (var lv in Levels.All)
        {
            int rw = 0, gw = 0; var left = new List<int>();
            for (int seed = 1; seed <= n; seed++)
            {
                if (Play(lv, seed, false).won) rw++;
                var g = Play(lv, seed, true);
                if (g.won) { gw++; left.Add(g.movesLeft); }
            }
            left.Sort();
            Console.WriteLine($"{lv.Name} ({lv.Moves} moves): random win {100.0 * rw / n:F0}%  greedy win {100.0 * gw / n:F0}%  greedy median moves left {(left.Count > 0 ? left[left.Count / 2] : -1)}");
        }
    }
}
