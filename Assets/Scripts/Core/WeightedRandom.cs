using System;
using System.Collections.Generic;

namespace LuckySeven.Slot
{
    /// <summary>
    /// A weighted random-number generator used for both reel strips and outcomes.
    ///
    /// A single System.Random instance is seeded from the current tick count so two
    /// sessions behave differently, while every reel in a spin is drawn from the
    /// same independent distribution (no shared state / no visible patterns).
    /// </summary>
    public sealed class WeightedRandom
    {
        private readonly Random random;
        private readonly SymbolType[] symbols;
        private readonly int[] cumulativeWeights;
        private readonly int totalWeight;

        public WeightedRandom(IList<SymbolRule> rules, int seed)
        {
            if (rules == null || rules.Count == 0)
                throw new ArgumentException("At least one symbol rule is required.", "rules");

            random = new Random(seed);
            symbols = new SymbolType[rules.Count];
            cumulativeWeights = new int[rules.Count];

            int running = 0;
            for (int i = 0; i < rules.Count; i++)
            {
                symbols[i] = rules[i].Symbol;
                running += Math.Max(0, rules[i].Weight);
                cumulativeWeights[i] = running;
            }

            // Guard against a misconfigured (all zero) paytable.
            totalWeight = running <= 0 ? rules.Count : running;
            if (running <= 0)
            {
                for (int i = 0; i < cumulativeWeights.Length; i++) cumulativeWeights[i] = i + 1;
            }
        }

        /// <summary>Draws one symbol according to the configured weights.</summary>
        public SymbolType NextSymbol()
        {
            int roll = random.Next(totalWeight); // [0, totalWeight)
            int low = 0;
            int high = cumulativeWeights.Length - 1;

            // Binary search keeps the draw O(log n) even for large paytables.
            while (low < high)
            {
                int mid = (low + high) >> 1;
                if (roll < cumulativeWeights[mid]) high = mid;
                else low = mid + 1;
            }
            return symbols[low];
        }

        /// <summary>Convenience for generating a whole strip of symbols.</summary>
        public SymbolType[] NextStrip(int length)
        {
            SymbolType[] strip = new SymbolType[length];
            for (int i = 0; i < length; i++) strip[i] = NextSymbol();
            return strip;
        }

        /// <summary>Plain integer helper used for cosmetic variation (audio pitch, etc.).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            return random.Next(minInclusive, maxExclusive);
        }
    }
}
