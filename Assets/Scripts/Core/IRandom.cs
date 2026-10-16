using System;

namespace Match3.Core
{
    /// <summary>Random source abstraction so the game logic stays deterministic and testable.</summary>
    public interface IRandom
    {
        /// <summary>Returns an integer in [0, maxExclusive).</summary>
        int Next(int maxExclusive);
    }

    public sealed class SystemRandom : IRandom
    {
        private readonly Random _random;

        public SystemRandom(int seed)
        {
            _random = new Random(seed);
        }

        public int Next(int maxExclusive) => _random.Next(maxExclusive);
    }
}
