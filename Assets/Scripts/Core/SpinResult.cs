namespace LuckySeven.Slot
{
    /// <summary>
    /// Immutable snapshot of one completed spin, passed to the view and audio
    /// layers so they never need to read mutable controller state.
    /// </summary>
    public sealed class SpinResult
    {
        /// <summary>The three centre-row symbols (the active payline).</summary>
        public readonly SymbolType[] Payline;

        public readonly int Bet;
        public readonly int Payout;
        public readonly int JackpotBefore;
        public readonly int JackpotAfter;
        public readonly int FreeSpinsAwarded;
        public readonly bool WasFreeSpin;

        public bool IsWin { get { return Payout > 0; } }
        public bool IsJackpot { get { return JackpotBefore > 0 && Payout == JackpotBefore; } }

        public SpinResult(SymbolType[] payline, int bet, int payout,
                          int jackpotBefore, int jackpotAfter, int freeSpinsAwarded, bool wasFreeSpin)
        {
            Payline = payline;
            Bet = bet;
            Payout = payout;
            JackpotBefore = jackpotBefore;
            JackpotAfter = jackpotAfter;
            FreeSpinsAwarded = freeSpinsAwarded;
            WasFreeSpin = wasFreeSpin;
        }

        /// <summary>Human-readable outcome text used by the win popup.</summary>
        public string Describe()
        {
            if (IsJackpot) return "JACKPOT! All three LUCKY 7s!";
            if (Payout > 0)
            {
                bool threeOfAKind = Payline[0] == Payline[1] && Payline[1] == Payline[2];
                if (threeOfAKind)
                    return "Three " + SymbolArtwork.DisplayName(Payline[0]) + "s pays " + Payout + " credits!";
                return "Two CHERRIES pays " + Payout + " credits!";
            }
            return "No win this time - try again!";
        }
    }
}
