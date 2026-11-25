namespace LuckySeven.Slot
{
    /// <summary>
    /// The (looping) sequence of symbols painted on one physical reel.
    ///
    /// Results are decided before the animation starts with the weighted RNG.
    /// Once a target is known it is written into the strip a few symbols ahead of
    /// the current position, so the reel visibly stops on the predecided result
    /// while still looking like a natural, continuous spin.
    /// </summary>
    public sealed class ReelStrip
    {
        private readonly SymbolType[] symbols;
        private readonly WeightedRandom rng;

        public ReelStrip(WeightedRandom rng, int length)
        {
            this.rng = rng;
            symbols = rng.NextStrip(length);
        }

        public int Length { get { return symbols.Length; } }

        /// <summary>Returns a symbol with safe wrap-around for negative indices.</summary>
        public SymbolType At(int index)
        {
            int n = symbols.Length;
            int wrapped = ((index % n) + n) % n;
            return symbols[wrapped];
        }

        /// <summary>
        /// Writes a guaranteed result 3-6 symbols in front of the current position
        /// and returns the exact index the reel should settle on.
        /// </summary>
        public int PlaceAhead(SymbolType symbol, int currentIndex)
        {
            int index = currentIndex + 3 + rng.NextInt(0, 4);
            symbols[((index % symbols.Length) + symbols.Length) % symbols.Length] = symbol;
            return index;
        }
    }
}
