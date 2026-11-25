# Lucky 7 Slot — Unity Slot Game Assignment

## 🎮 Game Overview

A complete, playable 3-reel slot machine built in Unity. The player bets credits, pulls the lever (or presses SPIN), watches three reels spin and stop one after another, and wins when the centre payline shows a winning combination. All artwork supplied with the assignment is used: the JACKPOT machine frame, four symbols, lever, arrow buttons, result popup, YES/NO buttons, payout panel and gradient background.

The project is self-contained: the entire UI is created at runtime, so opening the included scene and pressing **Play** is enough — no Inspector wiring is required.

## ✅ Features

| Requirement | Implementation |
|---|---|
| Winning logic | Three identical symbols on the centre payline wins; two cherries pay a smaller consolation prize. |
| Smooth reel animations | Continuous-position reel with acceleration, motion-blur overlay, staggered stops and a springy ease-out landing. |
| Clean symbol display | Symbols are cropped, aspect-preserved and clipped to the original machine windows with `RectMask2D`. |
| Randomized outcomes | Weighted `System.Random` RNG (seeded per session); each reel is an independent draw. |
| Winning combinations & payouts | Configurable paytable in `SlotMachineSettings`, progressive jackpot, credit/bet bookkeeping. |
| Bonus features | Progressive jackpot, free spins for three bells, two-cherry consolation, procedural sound effects, keyboard shortcuts. |
| Extra features | Auto-spin mode, in-game paytable/help panel, session statistics with persistent best win, winning-symbol celebration highlight, sound mute toggle. |
| Clean structure | OOP separation into Core / Game / UI / Audio / Bootstrap namespaces with documented code. |

## 🕹 Controls

- **SPIN** button or **SPACE** — spin the reels (costs the current bet, unless a free spin is available)
- **BET** button or **B** — cycle bet amounts: 10 → 50 → 100 credits
- **A** or **AUTO** button — toggle continuous auto-spin (spins run back-to-back and stop automatically if credits run out)
- **M** or **SOUND** button — mute/unmute the procedural sound effects
- **H** or **PAYTABLE** button — open the in-game help/paytable panel
- **ESC** — close the help panel, then the result popup
- **YES / NO** on the popup — spin again or close

The left-hand panel shows live session stats (spins, wagered, won, best win, jackpots). Your **best win is saved permanently** with `PlayerPrefs` in Unity and `localStorage` in the browser build.

## 💰 Paytable (at bet 10)

| Payline | Payout |
|---|---|
| 7 · 7 · 7 | Progressive jackpot (starts at 1000, grows with every paid spin, then resets) |
| BELL · BELL · BELL | 20 × bet **+ 2 free spins** |
| BAR · BAR · BAR | 10 × bet |
| CHERRY · CHERRY · CHERRY | 5 × bet |
| Two cherries (anywhere on payline) | 2 × bet |
| Anything else | 0 |

Symbol weights: 7 = 8, CHERRY = 35, BELL = 25, BAR = 32 (out of 100).

## 📁 Folder Structure

```
Assets/
  Animations/        Reserved for animation clips (reels are code-driven)
  Editor/            Build menu helper
  Prefabs/           Reserved for prefabs
  Resources/
    Sprites/         All supplied artwork
    Audio/           Reserved (sounds are synthesised at runtime)
  Scenes/
    SlotMachine.unity
  Scripts/
    Core/            SymbolType, settings, weighted RNG, paytable, spin result
    Game/            ReelStrip, Reel, SlotMachineController
    UI/              SlotMachineView (runtime UI builder)
    Audio/           AudioManager (procedural sound bank)
    Bootstrap/       GameBootstrap (auto-start)
  Sounds/ UI/        Documentation folders for future extensions
Build/WebGL/         Playable browser build + copied assets
ProjectSettings/     Minimal Unity project settings
```

## ▶️ Running in Unity

1. Open the project folder (`game intern`) with **Unity Hub → Add → Add Project from Disk**.
2. Open `Assets/Scenes/SlotMachine.unity`.
3. Press **Play**. `GameBootstrap` creates the game automatically.
4. To build: **File → Build Settings → WebGL → Build**, or use **Lucky Seven → Build WebGL**.

The project was authored for Unity 2022.3 LTS and uses only the built-in UI system (no external packages). Text uses Unity's built-in font, so it also works in WebGL.

## 🌐 Running the bundled WebGL preview

A ready-to-play browser build is included at `Build/WebGL/index.html` so the game can be reviewed without opening Unity.

- Easiest: open `Build/WebGL/index.html` in a desktop browser.
- Or serve the folder locally, e.g. `cd Build/WebGL && python -m http.server 8000`, then visit `http://localhost:8000`.

The browser build mirrors the Unity rules, weights, payouts and animation timing exactly. It is a hand-authored HTML5 preview; run the Unity WebGL build from the menu above for the official Unity output.

## 🎁 Extra Features

- **Auto-spin** — enable once and the machine keeps spinning every ~0.5 s after the previous result. The blocking win popup is suppressed in auto mode, and auto-play stops itself when credits are too low.
- **Paytable / Help overlay** — explains the rules, payouts and every keyboard shortcut without leaving the game.
- **Session statistics** — spins played, total wagered, total won, best win and jackpots won; best win persists between sessions.
- **Win celebration** — the three payline symbols pulse gold, and the win sound/arpeggio plays (bigger arpeggio for a jackpot).
- **Sound toggle** — mute the procedural audio bank at any time.

## 🧠 Thought Process / Approach

1. **Decide first, animate second.** Each spin's three symbols are drawn by the weighted RNG and the payout is calculated *before* animation starts. The chosen symbol is then written into each reel's cosmetic strip a few positions ahead, so the reel can spin freely but still stop exactly on the predetermined result. This avoids "rerolling" after the visual stop and keeps RNG and presentation independent.
2. **One number per reel.** Instead of baking animation clips, each reel tracks a continuous `position` (in symbol units). Speed ramps up, then the position is eased to an integer target with a slight overshoot. Rendering the three visible symbols around that position gives acceleration, long spins, blur and a soft landing from a single, deterministic update.
3. **Separation of concerns.** `SlotMachineController` owns rules/state and raises events; `SlotMachineView` owns layout and reacts; `Reel` owns motion; `Paytable`/`WeightedRandom` are pure logic. This makes each part readable and replaceable.
4. **Artwork-driven layout.** The original GIF showed a 3×3 machine with a centre payline, so symbol positions were measured in source pixels from `slot-machine4.png` and scaled to a 1920×1080 canvas. The machine frame's transparent windows let the reels show through; `slot-machine5.png` is faded in as a spin-blur overlay.
5. **Self-contained setup.** Runtime UI construction plus a bootstrap attribute means the reviewer never has to wire references manually, and missing assets are reported clearly in the console.

## 📤 Submission notes

To publish: create a public GitHub repository, commit this folder (the repository already contains meaningful incremental commits), push, and submit the repository link. The `Build/WebGL/` folder is included as required.
