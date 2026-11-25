using System;
using UnityEngine;

namespace LuckySeven.Slot
{
    /// <summary>
    /// Owns all game rules and state (credits, bets, jackpot, free spins).
    /// It deliberately knows nothing about UI layout: it raises events that the
    /// view subscribes to, keeping presentation and gameplay cleanly separated.
    /// </summary>
    public sealed class SlotMachineController
    {
        private readonly SlotMachineSettings settings;
        private WeightedRandom rng;
        private Reel[] reels;
        private bool[] stopRequested;       // true once a reel has been told to stop
        private float spinElapsed;

        private SymbolType[] pendingPayline;
        private int pendingBet;
        private int pendingPayout;
        private int pendingFreeSpins;
        private int jackpotBefore;
        private bool pendingWasFreeSpin;

        public int Credits { get; private set; }
        public int Jackpot { get; private set; }
        public int FreeSpins { get; private set; }
        public int BetIndex { get; private set; }
        public bool IsSpinning { get; private set; }
        public SpinResult LastResult { get; private set; }

        public int CurrentBet
        {
            get
            {
                if (settings.BetAmounts == null || settings.BetAmounts.Length == 0) return 1;
                int index = BetIndex;
                if (index < 0) index = 0;
                if (index >= settings.BetAmounts.Length) index = settings.BetAmounts.Length - 1;
                return settings.BetAmounts[index];
            }
        }

        public bool CanSpin
        {
            get { return !IsSpinning && (FreeSpins > 0 || Credits >= CurrentBet); }
        }

        public event Action StateChanged;
        public event Action SpinStarted;
        public event Action<SpinResult> SpinCompleted;
        public event Action<string> MessageRaised;

        public SlotMachineController(SlotMachineSettings settings, int seed)
        {
            this.settings = settings ?? SlotMachineSettings.CreateDefault();
            rng = new WeightedRandom(this.settings.Rules, seed);

            Credits = this.settings.StartingCredits;
            Jackpot = this.settings.StartingJackpot;
            BetIndex = 0;
            FreeSpins = 0;
            IsSpinning = false;
        }

        /// <summary>Attaches the three animated reels built by the view.</summary>
        public void AttachReels(Reel[] gameReels)
        {
            reels = gameReels;
            for (int i = 0; i < reels.Length; i++)
            {
                int index = i;
                reels[i].Stopped += delegate { OnReelStopped(index); };
            }
        }

        /// <summary>Cycles through the configured bet amounts.</summary>
        public void CycleBet()
        {
            if (IsSpinning)
            {
                RaiseMessage("Wait for the reels to stop first.");
                return;
            }
            if (settings.BetAmounts == null || settings.BetAmounts.Length == 0) return;

            BetIndex = (BetIndex + 1) % settings.BetAmounts.Length;
            RaiseStateChanged();
        }

        /// <summary>
        /// Validates the wager, decides the outcome with the RNG, then starts all
        /// three reels. Stopping is staggered by the settings for a natural feel.
        /// </summary>
        public void Spin()
        {
            if (IsSpinning)
            {
                RaiseMessage("The reels are still spinning!");
                return;
            }

            bool usesFreeSpin = FreeSpins > 0;
            int bet = CurrentBet;

            if (!usesFreeSpin && Credits < bet)
            {
                RaiseMessage("Not enough credits - lower your bet.");
                return;
            }

            // Pay the wager, then feed the progressive jackpot with a slice of it.
            if (!usesFreeSpin) Credits -= bet;
            else FreeSpins--;

            jackpotBefore = Jackpot;
            if (!usesFreeSpin)
                Jackpot += Mathf.RoundToInt(bet * settings.JackpotGrowthPerBet);

            // THE random outcome. Each reel is an independent weighted draw.
            pendingPayline = new SymbolType[3];
            for (int i = 0; i < 3; i++) pendingPayline[i] = rng.NextSymbol();

            pendingBet = bet;
            pendingWasFreeSpin = usesFreeSpin;
            pendingPayout = Paytable.EvaluatePayout(pendingPayline, bet, Jackpot, settings);
            pendingFreeSpins = Paytable.EvaluateFreeSpins(pendingPayline, settings);

            IsSpinning = true;
            spinElapsed = 0f;
            stopRequested = new bool[reels.Length];

            for (int i = 0; i < reels.Length; i++)
            {
                reels[i].StartSpin();
                stopRequested[i] = false;
            }

            if (SpinStarted != null) SpinStarted();
            RaiseStateChanged();
        }

        /// <summary>Per-frame update, forwarded from the view's MonoBehaviour.</summary>
        public void Tick(float deltaTime)
        {
            if (reels == null) return;

            spinElapsed += deltaTime;
            if (IsSpinning)
            {
                // Reel 0 stops first, then 1, then 2 - classic cascading stop.
                for (int i = 0; i < reels.Length; i++)
                {
                    float stopTime = settings.MinimumSpinTime + i * settings.StopStagger;
                    if (!stopRequested[i] && spinElapsed >= stopTime)
                    {
                        stopRequested[i] = true;
                        reels[i].RequestStop(pendingPayline[i]);
                    }
                }
            }

            for (int i = 0; i < reels.Length; i++) reels[i].Tick(deltaTime);

            if (IsSpinning)
            {
                bool allStopped = true;
                for (int i = 0; i < reels.Length; i++)
                {
                    if (reels[i].State != Reel.ReelState.Idle) { allStopped = false; break; }
                }
                if (allStopped) FinishSpin();
            }
        }

        private void OnReelStopped(int reelIndex)
        {
            // Individual reel landing sound is handled by the view via Reel.Stopped.
        }

        private void FinishSpin()
        {
            bool isJackpot = pendingPayline[0] == SymbolType.LuckySeven &&
                             pendingPayline[1] == SymbolType.LuckySeven &&
                             pendingPayline[2] == SymbolType.LuckySeven &&
                             pendingPayout > 0;

            if (pendingPayout > 0) Credits += pendingPayout;
            if (isJackpot) Jackpot = settings.StartingJackpot; // progressive pot resets
            FreeSpins += pendingFreeSpins;

            int jackpotAfter = Jackpot;
            LastResult = new SpinResult(pendingPayline, pendingBet, pendingPayout,
                                        jackpotBefore, jackpotAfter, pendingFreeSpins, pendingWasFreeSpin);

            IsSpinning = false;
            if (SpinCompleted != null) SpinCompleted(LastResult);
            RaiseStateChanged();
        }

        private void RaiseStateChanged()
        {
            if (StateChanged != null) StateChanged();
        }

        private void RaiseMessage(string message)
        {
            if (MessageRaised != null) MessageRaised(message);
        }
    }
}
