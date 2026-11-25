using System;
using UnityEngine;

namespace LuckySeven.Slot
{
    /// <summary>
    /// Tunable rules for one symbol. Weight controls how often the symbol lands;
    /// the multiplier fields control the payout for three-of-a-kind and for the
    /// classic "two cherries" consolation prize.
    /// </summary>
    [Serializable]
    public sealed class SymbolRule
    {
        public SymbolType Symbol;
        [Range(0, 100)] public int Weight;
        public int ThreeOfAKindMultiplier;
        public int TwoOfAKindMultiplier;

        public SymbolRule(SymbolType symbol, int weight, int threeMultiplier, int twoMultiplier)
        {
            Symbol = symbol;
            Weight = weight;
            ThreeOfAKindMultiplier = threeMultiplier;
            TwoOfAKindMultiplier = twoMultiplier;
        }
    }

    /// <summary>
    /// All balance/config values for the slot game. Exposed in the Inspector so
    /// the game can be tuned without touching code; a sensible default preset is
    /// created automatically when the component is first added.
    /// </summary>
    [Serializable]
    public sealed class SlotMachineSettings
    {
        [Header("Economy")]
        public int StartingCredits = 1000;
        public int[] BetAmounts = { 10, 50, 100 };
        public int StartingJackpot = 1000;

        [Header("Bonus")]
        [Range(0f, 1f)] public float JackpotGrowthPerBet = 0.1f;
        public int FreeSpinsForThreeBells = 2;

        [Header("Reel Feel")]
        public float MaxSpinSpeed = 1500f;   // pixels per second
        public float AccelerationTime = 0.25f;
        public float MinimumSpinTime = 0.9f; // brief fast spin before the first reel stops
        public float StopDuration = 0.65f;
        public float StopStagger = 0.3f;     // delay between reel 1 and reel 3 stopping

        [Header("Symbols / Paytable")]
        public SymbolRule[] Rules;

        /// <summary>Creates the default, playable balance preset.</summary>
        public static SlotMachineSettings CreateDefault()
        {
            return new SlotMachineSettings
            {
                StartingCredits = 1000,
                BetAmounts = new[] { 10, 50, 100 },
                StartingJackpot = 1000,
                JackpotGrowthPerBet = 0.1f,
                FreeSpinsForThreeBells = 2,
                MaxSpinSpeed = 1500f,
                AccelerationTime = 0.25f,
                MinimumSpinTime = 0.9f,
                StopDuration = 0.65f,
                StopStagger = 0.3f,
                Rules = new[]
                {
                    // Rare "7" pays the progressive jackpot, so its line multiplier is only a fallback.
                    new SymbolRule(SymbolType.LuckySeven, 8, 50, 0),
                    new SymbolRule(SymbolType.Cherry, 35, 5, 2),
                    new SymbolRule(SymbolType.Bell, 25, 20, 0),
                    new SymbolRule(SymbolType.Bar, 32, 10, 0)
                }
            };
        }

        /// <summary>Returns the rule for a symbol, or null when not configured.</summary>
        public SymbolRule RuleFor(SymbolType symbol)
        {
            if (Rules == null) return null;
            for (int i = 0; i < Rules.Length; i++)
            {
                if (Rules[i].Symbol == symbol) return Rules[i];
            }
            return null;
        }
    }
}
