using System;
using UnityEngine;
using UnityEngine.UI;

namespace LuckySeven.Slot
{
    /// <summary>
    /// Drives a single 3-symbol reel window.
    ///
    /// The reel uses a continuous "position" measured in symbol units. Each frame
    /// the three visible symbol images are laid out around that position, which
    /// makes acceleration, long spins and a soft eased stop all derive from one
    /// simple number - no Animator or baked animation clips are required.
    /// </summary>
    public sealed class Reel
    {
        public enum ReelState { Idle, Spinning, Stopping }

        private RectTransform stripRoot;
        private Image[] symbolImages;           // [0] top, [1] centre (payline), [2] bottom
        private Func<SymbolType, Sprite> spriteLookup;
        private ReelStrip strip;
        private float symbolHeight;

        private float maxSpeed;
        private float accelerationTime;
        private float stopDuration;

        private float position;                 // grows downward, in symbol units
        private float speed;                    // pixels / second
        private float stopFrom;
        private float stopTo;
        private float stopTimer;

        public ReelState State { get; private set; }
        public float Speed { get { return speed; } }
        public SymbolType CentreSymbol { get { return strip == null ? SymbolType.Cherry : strip.At(Mathf.RoundToInt(position)); } }

        /// <summary>The payline symbol image, used by the view's win highlight.</summary>
        public Image CentreImage { get { return symbolImages == null ? null : symbolImages[1]; } }

        /// <summary>Raised once the reel has fully settled on its result.</summary>
        public event Action Stopped;

        public void Setup(RectTransform stripRoot, Image[] symbolImages,
                          Func<SymbolType, Sprite> spriteLookup, ReelStrip strip,
                          float symbolHeight, float maxSpeed, float accelerationTime, float stopDuration)
        {
            this.stripRoot = stripRoot;
            this.symbolImages = symbolImages;
            this.spriteLookup = spriteLookup;
            this.strip = strip;
            this.symbolHeight = symbolHeight;
            this.maxSpeed = maxSpeed;
            this.accelerationTime = Mathf.Max(0.05f, accelerationTime);
            this.stopDuration = Mathf.Max(0.1f, stopDuration);

            State = ReelState.Idle;
            speed = 0f;
            position = 0f;
            Render();
        }

        /// <summary>Spins up the reel from rest.</summary>
        public void StartSpin()
        {
            State = ReelState.Spinning;
            speed = 0f;
        }

        /// <summary>
        /// Locks in a predecided symbol and begins a smooth, staggered deceleration
        /// that ends with that symbol exactly on the centre payline.
        /// </summary>
        public void RequestStop(SymbolType target)
        {
            if (State == ReelState.Idle) return;

            stopFrom = position;
            stopTo = strip.PlaceAhead(target, Mathf.CeilToInt(position));
            stopTimer = 0f;
            State = ReelState.Stopping;
        }

        /// <summary>Called once per frame by the controller.</summary>
        public void Tick(float deltaTime)
        {
            switch (State)
            {
                case ReelState.Spinning:
                    // Ease into top speed, then keep the strip travelling.
                    speed = Mathf.MoveTowards(speed, maxSpeed, (maxSpeed / accelerationTime) * deltaTime);
                    position += (speed * deltaTime) / symbolHeight;
                    break;

                case ReelState.Stopping:
                    stopTimer += deltaTime;
                    float t = Mathf.Clamp01(stopTimer / stopDuration);
                    position = Mathf.Lerp(stopFrom, stopTo, EaseOutBack(t));
                    // Speed is cosmetic here - it drives the motion-blur amount.
                    speed = maxSpeed * (1f - EaseOutCubic(t)) * 0.85f;
                    if (t >= 1f)
                    {
                        position = stopTo;
                        speed = 0f;
                        State = ReelState.Idle;
                        if (Stopped != null) Stopped();
                    }
                    break;
            }

            Render();
        }

        /// <summary>Repositions and re-skins the three visible symbols.</summary>
        private void Render()
        {
            if (stripRoot == null || symbolImages == null || spriteLookup == null) return;

            int baseIndex = Mathf.FloorToInt(position);
            float fraction = position - baseIndex;
            float stretch = 1f + Mathf.Clamp01(speed / maxSpeed) * 0.08f;

            for (int row = 0; row < 3; row++)
            {
                int offset = 1 - row;                       // top=+1, centre=0, bottom=-1
                int index = baseIndex + offset;
                Image image = symbolImages[row];
                image.sprite = spriteLookup(strip.At(index));
                image.rectTransform.anchoredPosition = new Vector2(0f, (offset - fraction) * symbolHeight);
                image.rectTransform.localScale = new Vector3(1f, stretch, 1f);
            }
        }

        public static float EaseOutCubic(float t)
        {
            float u = t - 1f;
            return 1f + u * u * u;
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.25f;
            float u = t - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }
    }
}
