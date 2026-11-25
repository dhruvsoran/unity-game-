namespace LuckySeven.Slot
{
    /// <summary>
    /// Pure win-evaluation logic. Kept separate from the controller so the payout
    /// rules are easy to read, test and tune.
    ///
    /// Rules (centre row is the payline):
    ///   3x LUCKY 7  -> progressive jackpot (resets after it is won)
    ///   3x BELL     -> line multiplier + free spins
    ///   3x CHERRY   -> line multiplier
    ///   3x BAR      -> line multiplier
    ///   2x CHERRY   -> small consolation payout
    /// </summary>
    public static class Paytable
    {
        public static int EvaluatePayout(SymbolType[] payline, int bet, int jackpot, SlotMachineSettings settings)
        {
            if (payline == null || payline.Length < 3 || settings == null || bet <= 0) return 0;

            if (payline[0] == payline[1] && payline[1] == payline[2])
            {
                SymbolType symbol = payline[0];
                if (symbol == SymbolType.LuckySeven && jackpot > 0)
                    return jackpot; // progressive jackpot overrides the line multiplier

                SymbolRule rule = settings.RuleFor(symbol);
                return rule == null ? 0 : rule.ThreeOfAKindMultiplier * bet;
            }

            // Classic slot consolation prize: two cherries anywhere on the payline.
            int cherries = 0;
            for (int i = 0; i < 3; i++)
                if (payline[i] == SymbolType.Cherry) cherries++;

            if (cherries == 2)
            {
                SymbolRule rule = settings.RuleFor(SymbolType.Cherry);
                return rule == null ? 0 : rule.TwoOfAKindMultiplier * bet;
            }

            return 0;
        }

        public static int EvaluateFreeSpins(SymbolType[] payline, SlotMachineSettings settings)
        {
            if (payline == null || payline.Length < 3 || settings == null) return 0;
            if (payline[0] == SymbolType.Bell && payline[1] == SymbolType.Bell && payline[2] == SymbolType.Bell)
                return settings.FreeSpinsForThreeBells;
            return 0;
        }
    }
}
