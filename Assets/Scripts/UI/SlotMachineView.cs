using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LuckySeven.Slot
{
    /// <summary>
    /// Builds and drives the whole slot machine UI at runtime from the supplied
    /// artwork. Building the view in code keeps the scene empty and makes the
    /// project self-contained - no manual Inspector wiring is required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SlotMachineView : MonoBehaviour
    {
        private const float MachineScale = 1.5f;
        private const float MachineWidth = 816f * MachineScale;
        private const float MachineHeight = 624f * MachineScale;
        private const float MachineCenterY = -25f;
        private const float SymbolHeight = 70f * MachineScale;
        private const float SymbolSize = 96f;
        private const float ReelWidth = 108f * MachineScale;

        // Source-pixel X centres of the three windows in slot-machine4.png (816 px wide).
        private static readonly float[] ReelSourceX = { 282.5f, 413f, 543f };
        private const float ReelSourceCenterY = 351f;

        private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

        private SlotMachineSettings settings;
        private SlotMachineController controller;
        private AudioManager audioManager;
        private Reel[] reels;

        private Text creditsText;
        private Text betText;
        private Text jackpotText;
        private Text freeSpinText;
        private Text statusText;
        private Text winText;
        private Text popupTitle;
        private Text popupBody;
        private GameObject popupRoot;

        private Button spinButton;
        private Button betButton;
        private Image spinButtonImage;
        private Image betButtonImage;
        private Sprite spinEnabledSprite;
        private Sprite spinDisabledSprite;
        private Sprite betEnabledSprite;
        private Sprite betDisabledSprite;

        private Image blurOverlay;
        private RectTransform leverTransform;
        private float leverTimer = -1f;
        private float messageTimer;

        // Extra features: auto-spin, stats, best-win persistence, paytable and mute.
        private bool autoSpinEnabled;
        private float autoSpinTimer = -1f;
        private bool soundMuted;
        private Text statsText;
        private Text autoButtonLabel;
        private Text soundButtonLabel;
        private Button autoButton;
        private GameObject helpRoot;

        /// <summary>Entry point called by the bootstrap component.</summary>
        public void Initialize()
        {
            settings = SlotMachineSettings.CreateDefault();

            EnsureEventSystem();
            BuildSprites();

            GameObject canvasObject = new GameObject("SlotCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Transform canvasRoot = canvasObject.transform;

            BuildBackground(canvasRoot);
            BuildHeader(canvasRoot);
            BuildReels(canvasRoot);
            BuildMachineFrame(canvasRoot);
            BuildLever(canvasRoot);
            BuildInfoPanel(canvasRoot);
            BuildControls(canvasRoot);
            BuildSidePanel(canvasRoot);
            BuildPopup(canvasRoot);
            BuildHelpPanel(canvasRoot);

            audioManager = new AudioManager();
            audioManager.Initialize(gameObject);

            int seed = Environment.TickCount ^ (DateTime.Now.Millisecond * 7919);
            controller = new SlotMachineController(settings, seed);
            controller.AttachReels(controllerHolder);

            controller.StateChanged += RefreshState;
            controller.SpinStarted += OnSpinStarted;
            controller.SpinCompleted += OnSpinCompleted;
            controller.MessageRaised += ShowMessage;

            spinButton.onClick.AddListener(OnSpinClicked);
            betButton.onClick.AddListener(OnBetClicked);

            RefreshState();
            RefreshStats();
        }

        private void Update()
        {
            if (controller == null) return;

            controller.Tick(Time.deltaTime);
            UpdateEffects(Time.deltaTime);

            if (Input.GetKeyDown(KeyCode.Space)) OnSpinClicked();
            if (Input.GetKeyDown(KeyCode.B)) OnBetClicked();
            if (Input.GetKeyDown(KeyCode.A)) ToggleAutoSpin();
            if (Input.GetKeyDown(KeyCode.M)) ToggleSound();
            if (Input.GetKeyDown(KeyCode.H)) ToggleHelp();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (helpRoot != null) helpRoot.SetActive(false);
                else if (popupRoot != null) popupRoot.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- UI build

        /// <summary>Preloads all supplied artwork once and reports anything missing.</summary>
        private void BuildSprites()
        {
            string[] artwork =
            {
                "bg_gradient", "popup", "slot-machine1", "slot-machine2", "slot-machine3",
                "slot-machine4", "slot-machine5", "slot_machine_buttons-02",
                "slot_machine_buttons-03", "slot_machine_buttons-04",
                "slot_machine_Middle_box", "Yes_No_Btn"
            };
            for (int i = 0; i < artwork.Length; i++) GetSprite(artwork[i]);
        }

        private void BuildBackground(Transform parent)
        {
            Image background = CreateImage("Background", parent, GetSprite("bg_gradient"), Vector2.zero, Vector2.zero, false);
            RectTransform rect = background.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // A soft full-screen tint behind the machine for extra contrast.
            Image vignette = CreateImage("Vignette", parent, null, Vector2.zero, Vector2.zero, false);
            vignette.color = new Color(0.03f, 0.02f, 0.12f, 0.18f);
            Stretch(vignette.rectTransform);
        }

        private void BuildHeader(Transform parent)
        {
            Color gold = new Color(1f, 0.86f, 0.35f);
            creditsText = CreateText("Credits", parent, "CREDITS  0", 40, gold, new Vector2(560f, 70f), new Vector2(-650f, 480f), TextAnchor.MiddleLeft);
            jackpotText = CreateText("Jackpot", parent, "JACKPOT  0", 40, gold, new Vector2(560f, 70f), new Vector2(650f, 480f), TextAnchor.MiddleRight);
            freeSpinText = CreateText("FreeSpins", parent, "", 28, new Color(0.65f, 1f, 0.78f), new Vector2(560f, 50f), new Vector2(-650f, 430f), TextAnchor.MiddleLeft);

            Text title = CreateText("Title", parent, "LUCKY 7  SLOT", 30, new Color(0.85f, 0.88f, 1f), new Vector2(700f, 60f), new Vector2(0f, 482f), TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;

            statusText = CreateText("Status", parent, "", 30, new Color(1f, 0.95f, 0.8f), new Vector2(1400f, 60f), new Vector2(0f, -505f), TextAnchor.MiddleCenter);
            statusText.color = new Color(1f, 0.95f, 0.8f, 0f);
        }

        private void BuildReels(Transform parent)
        {
            // This root sits behind the machine frame; the frame's transparent
            // windows reveal the scrolling symbols underneath.
            RectTransform reelsRoot = CreateRect("ReelsRoot", parent, new Vector2(MachineWidth, MachineHeight), new Vector2(0f, MachineCenterY));

            WeightedRandom cosmeticRng = new WeightedRandom(settings.Rules, Environment.TickCount ^ 0x5f3a1c);
            reels = new Reel[3];

            for (int i = 0; i < 3; i++)
            {
                float x = (ReelSourceX[i] - 408f) * MachineScale;
                // Source pixel Y grows downward, Canvas Y grows upward.
                float y = (312f - ReelSourceCenterY) * MachineScale;

                RectTransform reelRect = CreateRect("Reel_" + (i + 1), reelsRoot, new Vector2(ReelWidth, SymbolHeight * 3f), new Vector2(x, y));
                reelRect.gameObject.AddComponent<RectMask2D>();

                Image window = CreateImage("Window", reelRect, null, new Vector2(ReelWidth, SymbolHeight * 3f), Vector2.zero);
                window.color = new Color(0.867f, 0.965f, 0.969f, 1f);

                Image[] symbolImages = new Image[3];
                for (int row = 0; row < 3; row++)
                {
                    symbolImages[row] = CreateImage("Symbol_" + row, reelRect, null, new Vector2(SymbolSize, SymbolSize), Vector2.zero);
                    symbolImages[row].preserveAspect = true;
                }

                ReelStrip strip = new ReelStrip(cosmeticRng, 30);
                Reel reel = new Reel();
                reel.Setup(reelRect, symbolImages, SymbolSprite, strip, SymbolHeight,
                           settings.MaxSpinSpeed, settings.AccelerationTime, settings.StopDuration);
                reel.Stopped += delegate { audioManager.Play(SoundCue.ReelStop); };
                reels[i] = reel;
            }

            controllerHolder = reels; // attached after the controller is constructed
        }

        // Temporary holder so reels created before the controller can be attached later.
        private Reel[] controllerHolder;

        private void BuildMachineFrame(Transform parent)
        {
            Image frame = CreateImage("MachineFrame", parent, GetSprite("slot-machine4"), new Vector2(MachineWidth, MachineHeight), new Vector2(0f, MachineCenterY));
            frame.preserveAspect = true;
            frame.raycastTarget = false;

            blurOverlay = CreateImage("SpinBlur", parent, GetSprite("slot-machine5"), new Vector2(MachineWidth, MachineHeight), new Vector2(0f, MachineCenterY));
            blurOverlay.preserveAspect = true;
            blurOverlay.raycastTarget = false;
            blurOverlay.color = new Color(1f, 1f, 1f, 0f);
        }

        private void BuildLever(Transform parent)
        {
            Image lever = CreateImage("Lever", parent, GetSprite("slot-machine2"), new Vector2(MachineWidth, MachineHeight), new Vector2(0f, MachineCenterY));
            lever.preserveAspect = true;
            lever.raycastTarget = false;

            // Pivot at the base of the lever handle (source pixel ~720, 560).
            lever.rectTransform.pivot = new Vector2(720f / 816f, 1f - (560f / 624f));
            leverTransform = lever.rectTransform;
            leverTransform.localRotation = Quaternion.identity;
        }

        private void BuildInfoPanel(Transform parent)
        {
            Image panel = CreateImage("InfoPanel", parent, GetSprite("slot_machine_Middle_box"), new Vector2(600f, 250f), new Vector2(0f, -420f));
            panel.preserveAspect = true;
            panel.raycastTarget = false;

            betText = CreateText("BetInfo", parent, "BET  10", 38, new Color(1f, 0.9f, 0.45f), new Vector2(520f, 60f), new Vector2(0f, -375f), TextAnchor.MiddleCenter);
            betText.fontStyle = FontStyle.Bold;
            winText = CreateText("WinInfo", parent, "WIN  0", 30, new Color(0.75f, 1f, 0.8f), new Vector2(520f, 60f), new Vector2(0f, -430f), TextAnchor.MiddleCenter);
        }

        private void BuildControls(Transform parent)
        {
            Texture2D arrows = GetTexture("slot_machine_buttons-02");
            spinEnabledSprite = Crop(arrows, 0, 256, 256, 256);   // bright purple arrow
            betEnabledSprite = Crop(arrows, 0, 0, 256, 256);     // deep purple arrow
            spinDisabledSprite = Crop(arrows, 0, 768, 256, 256); // grey arrow
            betDisabledSprite = Crop(arrows, 0, 768, 256, 256);

            spinButton = CreateButton("SpinButton", parent, spinEnabledSprite, new Vector2(190f, 190f), new Vector2(720f, -120f));
            betButton = CreateButton("BetButton", parent, betEnabledSprite, new Vector2(170f, 170f), new Vector2(725f, 110f));
            betButton.GetComponent<RectTransform>().localScale = new Vector3(-1f, 1f, 1f);

            spinButtonImage = spinButton.GetComponent<Image>();
            betButtonImage = betButton.GetComponent<Image>();

            Text spinLabel = CreateText("SpinLabel", parent, "SPIN", 30, Color.white, new Vector2(260f, 44f), new Vector2(720f, -250f), TextAnchor.MiddleCenter);
            spinLabel.fontStyle = FontStyle.Bold;
            Text betLabel = CreateText("BetLabel", parent, "BET", 26, Color.white, new Vector2(260f, 44f), new Vector2(725f, 5f), TextAnchor.MiddleCenter);
            betLabel.fontStyle = FontStyle.Bold;

            Text shortcut = CreateText("Shortcut", parent, "SPACE = SPIN   B = BET   A = AUTO   M = SOUND", 18, new Color(0.75f, 0.78f, 0.95f), new Vector2(560f, 40f), new Vector2(720f, -300f), TextAnchor.MiddleCenter);
            shortcut.color = new Color(0.75f, 0.78f, 0.95f, 0.8f);
        }

        /// <summary>Left-hand control column: statistics, auto-spin, sound and help.</summary>
        private void BuildSidePanel(Transform parent)
        {
            Image panel = CreateImage("StatsPanel", parent, null, new Vector2(320f, 360f), new Vector2(-790f, -60f));
            panel.color = new Color(0.04f, 0.03f, 0.16f, 0.72f);
            panel.raycastTarget = false;

            statsText = CreateText("Stats", parent, "", 24, new Color(0.75f, 0.86f, 1f), new Vector2(300f, 320f), new Vector2(-790f, -60f), TextAnchor.UpperLeft);
            statsText.horizontalOverflow = HorizontalWrapMode.Wrap;

            autoButton = CreateLabeledButton("AutoButton", parent, "AUTO: OFF", new Vector2(300f, 70f), new Vector2(-790f, 390f));
            autoButton.onClick.AddListener(ToggleAutoSpin);
            autoButtonLabel = autoButton.GetComponentInChildren<Text>();

            Button soundButton = CreateLabeledButton("SoundButton", parent, "SOUND: ON", new Vector2(300f, 70f), new Vector2(-790f, 300f));
            soundButton.onClick.AddListener(ToggleSound);
            soundButtonLabel = soundButton.GetComponentInChildren<Text>();

            Button helpButton = CreateLabeledButton("HelpButton", parent, "PAYTABLE (H)", new Vector2(300f, 70f), new Vector2(-790f, 210f));
            helpButton.onClick.AddListener(ToggleHelp);
        }

        private void BuildHelpPanel(Transform parent)
        {
            helpRoot = new GameObject("HelpPanel", typeof(RectTransform));
            helpRoot.transform.SetParent(parent, false);

            Image dimmer = CreateImage("Dimmer", helpRoot.transform, null, Vector2.zero, Vector2.zero, false);
            dimmer.color = new Color(0f, 0f, 0f, 0.72f);
            Stretch(dimmer.rectTransform);

            Image panel = CreateImage("Panel", helpRoot.transform, GetSprite("popup"), new Vector2(1000f, 620f), Vector2.zero);
            panel.preserveAspect = true;

            Text title = CreateText("Title", helpRoot.transform, "HOW TO PLAY / PAYTABLE", 44, new Color(1f, 0.88f, 0.42f), new Vector2(900f, 70f), new Vector2(0f, 220f), TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;

            string rules =
                "CENTRE ROW IS THE PAYLINE\n\n" +
                "7 - 7 - 7 ........ PROGRESSIVE JACKPOT\n" +
                "BELL x3 .......... 20x BET + 2 FREE SPINS\n" +
                "BAR x3 ........... 10x BET\n" +
                "CHERRY x3 ........ 5x BET\n" +
                "TWO CHERRIES ..... 2x BET\n\n" +
                "BETS: 10 / 50 / 100     START: 1000 CREDITS\n" +
                "SPACE SPIN   B BET   A AUTO   M SOUND   H HELP";
            CreateText("Rules", helpRoot.transform, rules, 26, Color.white, new Vector2(900f, 360f), new Vector2(0f, 20f), TextAnchor.MiddleCenter);

            Button closeButton = CreateLabeledButton("CloseButton", helpRoot.transform, "CLOSE (ESC)", new Vector2(300f, 80f), new Vector2(0f, -230f));
            closeButton.onClick.AddListener(delegate { helpRoot.SetActive(false); });
            helpRoot.SetActive(false);
        }

        private void BuildPopup(Transform parent)
        {
            popupRoot = new GameObject("WinPopup", typeof(RectTransform));
            popupRoot.transform.SetParent(parent, false);

            Image dimmer = CreateImage("Dimmer", popupRoot.transform, null, Vector2.zero, Vector2.zero, false);
            dimmer.color = new Color(0f, 0f, 0f, 0.68f);
            Stretch(dimmer.rectTransform);

            Image panel = CreateImage("Panel", popupRoot.transform, GetSprite("popup"), new Vector2(865f, 437f), Vector2.zero);
            panel.preserveAspect = true;

            popupTitle = CreateText("Title", popupRoot.transform, "YOU WIN!", 64, new Color(1f, 0.9f, 0.4f), new Vector2(760f, 90f), new Vector2(0f, 120f), TextAnchor.MiddleCenter);
            popupTitle.fontStyle = FontStyle.Bold;
            popupBody = CreateText("Body", popupRoot.transform, "", 32, Color.white, new Vector2(760f, 120f), new Vector2(0f, 10f), TextAnchor.MiddleCenter);

            Texture2D yesNo = GetTexture("Yes_No_Btn");
            Sprite yesSprite = Crop(yesNo, 70, 70, 370, 140);
            Sprite noSprite = Crop(yesNo, 550, 70, 370, 140);

            Button yesButton = CreateButton("YesButton", popupRoot.transform, yesSprite, new Vector2(280f, 100f), new Vector2(-190f, -130f));
            Button noButton = CreateButton("NoButton", popupRoot.transform, noSprite, new Vector2(280f, 100f), new Vector2(190f, -130f));
            yesButton.onClick.AddListener(OnPlayAgain);
            noButton.onClick.AddListener(delegate { popupRoot.SetActive(false); audioManager.Play(SoundCue.Button); });

            popupRoot.SetActive(false);
        }

        // ------------------------------------------------------------- interactions

        private void OnSpinClicked()
        {
            if (popupRoot != null && popupRoot.activeSelf) return;
            if (helpRoot != null && helpRoot.activeSelf) return;
            audioManager.Play(SoundCue.Button);
            controller.Spin();
        }

        private void OnBetClicked()
        {
            if (popupRoot != null && popupRoot.activeSelf) return;
            if (helpRoot != null && helpRoot.activeSelf) return;
            audioManager.Play(SoundCue.Button);
            controller.CycleBet();
        }

        private void OnPlayAgain()
        {
            popupRoot.SetActive(false);
            audioManager.Play(SoundCue.Button);
            if (controller.CanSpin) controller.Spin();
        }

        private void OnSpinStarted()
        {
            audioManager.Play(SoundCue.Spin);
            leverTimer = 0f;
            statusText.color = new Color(1f, 0.95f, 0.8f, 1f);
            statusText.text = "Reels spinning...";
            messageTimer = 0f;
        }

        private void OnSpinCompleted(SpinResult result)
        {
            RefreshState();
            RefreshStats();
            winText.text = "WIN  " + result.Payout;
            statusText.text = result.Describe();
            statusText.color = new Color(1f, 0.95f, 0.8f, 1f);
            messageTimer = 2.2f;

            string body = result.Describe();
            if (result.FreeSpinsAwarded > 0)
            {
                body += "\nBONUS: " + result.FreeSpinsAwarded + " FREE SPINS!";
                audioManager.Play(SoundCue.FreeSpin);
            }

            if (result.IsWin)
            {
                audioManager.Play(result.IsJackpot ? SoundCue.Jackpot : SoundCue.Win);
                StartCoroutine(HighlightPayline());
            }
            else
            {
                winText.text = "WIN  0";
            }

            // Auto-play skips the blocking popup so spins can run back-to-back.
            if (autoSpinEnabled)
            {
                autoSpinTimer = 0.55f;
                return;
            }

            popupTitle.text = result.IsJackpot ? "JACKPOT!!!" : (result.IsWin ? "YOU WIN!" : "TRY AGAIN");
            popupBody.text = body;
            popupRoot.SetActive(true);
        }

        /// <summary>Pulses the three payline symbols gold after a win.</summary>
        private IEnumerator HighlightPayline()
        {
            Color gold = new Color(1f, 0.85f, 0.25f);
            float elapsed = 0f;
            while (elapsed < 0.9f)
            {
                elapsed += Time.deltaTime;
                float wave = Mathf.Sin(Mathf.Clamp01(elapsed / 0.9f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                Color tint = Color.Lerp(Color.white, gold, wave * 0.85f);
                for (int i = 0; i < reels.Length; i++)
                {
                    Image image = reels[i].CentreImage;
                    if (image != null) image.color = tint;
                }
                yield return null;
            }
            for (int i = 0; i < reels.Length; i++)
            {
                Image image = reels[i].CentreImage;
                if (image != null) image.color = Color.white;
            }
        }

        private void ToggleAutoSpin()
        {
            audioManager.Play(SoundCue.Button);
            autoSpinEnabled = !autoSpinEnabled;
            autoSpinTimer = autoSpinEnabled && controller.CanSpin ? 0.35f : -1f;
            if (autoButtonLabel != null) autoButtonLabel.text = "AUTO: " + (autoSpinEnabled ? "ON" : "OFF");
            Image image = autoButton.GetComponent<Image>();
            image.color = autoSpinEnabled
                ? new Color(0.12f, 0.55f, 0.30f, 0.95f)
                : new Color(0.28f, 0.19f, 0.62f, 0.92f);
            if (popupRoot != null) popupRoot.SetActive(false);
        }

        private void ToggleSound()
        {
            soundMuted = !soundMuted;
            audioManager.Muted = soundMuted;
            if (!soundMuted) audioManager.Play(SoundCue.Button);
            if (soundButtonLabel != null) soundButtonLabel.text = "SOUND: " + (soundMuted ? "OFF" : "ON");
        }

        private void ToggleHelp()
        {
            audioManager.Play(SoundCue.Button);
            helpRoot.SetActive(!helpRoot.activeSelf);
        }

        /// <summary>Refreshes the session stats panel and stores the best win permanently.</summary>
        private void RefreshStats()
        {
            if (controller == null || statsText == null) return;

            int savedBest = PlayerPrefs.GetInt("Lucky7.BestWin", 0);
            if (controller.BiggestWin > savedBest)
            {
                PlayerPrefs.SetInt("Lucky7.BestWin", controller.BiggestWin);
                PlayerPrefs.Save();
            }
            int best = Mathf.Max(savedBest, controller.BiggestWin);

            statsText.text = "SESSION STATS\n\n" +
                             "SPINS      " + controller.SpinsPlayed + "\n" +
                             "WAGERED    " + controller.TotalWagered + "\n" +
                             "WON        " + controller.TotalWon + "\n" +
                             "BEST WIN   " + best + "\n" +
                             "JACKPOTS   " + controller.JackpotsWon;
        }

        private void ShowMessage(string message)
        {
            statusText.text = message;
            statusText.color = new Color(1f, 0.8f, 0.5f, 1f);
            messageTimer = 2.2f;
        }

        private void RefreshState()
        {
            if (controller == null) return;
            creditsText.text = "CREDITS  " + controller.Credits;
            jackpotText.text = "JACKPOT  " + controller.Jackpot;
            freeSpinText.text = controller.FreeSpins > 0 ? "FREE SPINS  " + controller.FreeSpins : "";
            betText.text = "BET  " + controller.CurrentBet + (controller.FreeSpins > 0 ? "   (FREE SPIN)" : "");

            bool canSpin = controller.CanSpin;
            spinButtonImage.sprite = canSpin ? spinEnabledSprite : spinDisabledSprite;
            betButtonImage.sprite = controller.IsSpinning ? betDisabledSprite : betEnabledSprite;
        }

        private void UpdateEffects(float deltaTime)
        {
            // Auto-play schedules the next spin shortly after the previous one finishes.
            if (autoSpinTimer > 0f)
            {
                autoSpinTimer -= deltaTime;
                if (autoSpinTimer <= 0f)
                {
                    autoSpinTimer = -1f;
                    if (controller.CanSpin)
                    {
                        controller.Spin();
                    }
                    else
                    {
                        autoSpinEnabled = false;
                        if (autoButtonLabel != null) autoButtonLabel.text = "AUTO: OFF";
                        Image image = autoButton.GetComponent<Image>();
                        image.color = new Color(0.28f, 0.19f, 0.62f, 0.92f);
                        ShowMessage("Auto-spin stopped - not enough credits.");
                    }
                }
            }

            // Motion-blur strip fades in while the reels are fast, out as they settle.
            float fastest = 0f;
            for (int i = 0; i < reels.Length; i++) fastest = Mathf.Max(fastest, reels[i].Speed);
            float blur = Mathf.Clamp01(fastest / settings.MaxSpinSpeed) * 0.85f;
            blurOverlay.color = new Color(1f, 1f, 1f, blur);

            // Lever pull: swing down and spring back while the spin starts.
            if (leverTimer >= 0f)
            {
                leverTimer += deltaTime;
                float t = Mathf.Clamp01(leverTimer / 0.65f);
                float angle = -32f * Mathf.Sin(t * Mathf.PI);
                leverTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                if (t >= 1f)
                {
                    leverTransform.localRotation = Quaternion.identity;
                    leverTimer = -1f;
                }
            }

            if (messageTimer > 0f)
            {
                messageTimer -= deltaTime;
                if (messageTimer <= 0f) statusText.color = new Color(1f, 0.95f, 0.8f, 0f);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                UnityEngine.Object.DontDestroyOnLoad(eventSystem);
            }
        }

        private Sprite SymbolSprite(SymbolType type)
        {
            string key = "symbol_" + type;
            Sprite cached;
            if (spriteCache.TryGetValue(key, out cached)) return cached;
            Sprite sprite = LoadSprite(SymbolArtwork.ResourcePath(type));
            spriteCache[key] = sprite;
            return sprite;
        }

        private Sprite GetSprite(string resourceName)
        {
            string key = "full_" + resourceName;
            Sprite cached;
            if (spriteCache.TryGetValue(key, out cached)) return cached;
            Sprite sprite = LoadSprite("Sprites/" + resourceName);
            spriteCache[key] = sprite;
            return sprite;
        }

        private Sprite LoadSprite(string path)
        {
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogError("[SlotMachine] Missing artwork: " + path);
                return null;
            }
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = path;
            return sprite;
        }

        private Texture2D GetTexture(string resourceName)
        {
            return Resources.Load<Texture2D>("Sprites/" + resourceName);
        }

        /// <summary>Crops a sprite sheet cell given top-left pixel coordinates.</summary>
        private static Sprite Crop(Texture2D texture, int x, int yFromTop, int width, int height)
        {
            if (texture == null) return null;
            Rect rect = new Rect(x, texture.height - yFromTop - height, width, height);
            return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 position, bool centered = true)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            if (centered)
            {
                RectTransform rect = image.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.anchoredPosition = position;
            }
            return image;
        }

        private static Text CreateText(string name, Transform parent, string content, int fontSize,
                                       Color color, Vector2 size, Vector2 position, TextAnchor alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = ResolveStaticFont();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static Font ResolveStaticFont()
        {
            try
            {
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font != null) return font;
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch (Exception) { return null; }
        }

        /// <summary>A simple labelled purple button used by the extra feature controls.</summary>
        private static Button CreateLabeledButton(string name, Transform parent, string label, Vector2 size, Vector2 position)
        {
            Button button = CreateButton(name, parent, null, size, position);
            Image image = button.GetComponent<Image>();
            image.color = new Color(0.28f, 0.19f, 0.62f, 0.92f);
            CreateText("Label", button.transform, label, 24, Color.white, size, Vector2.zero, TextAnchor.MiddleCenter);
            return button;
        }

        private static Button CreateButton(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 position)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            button.colors = colors;
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
