/*
 * Lucky 7 Slot - browser preview build.
 * Mirrors the Unity C# rules: same weights, paytable, staggered reel stops,
 * progressive jackpot and free-spin bonus. All artwork is loaded from ./assets.
 */
(function () {
  "use strict";

  var SYMBOLS = ["slot-symbol1", "slot-symbol2", "slot-symbol3", "slot-symbol4"];
  var NAMES = ["LUCKY 7", "CHERRY", "BELL", "BAR"];
  var WEIGHTS = [8, 35, 25, 32];
  var TOTAL_WEIGHT = WEIGHTS.reduce(function (a, b) { return a + b; }, 0);

  var BETS = [10, 50, 100];
  var START_CREDITS = 1000;
  var START_JACKPOT = 1000;
  var GROWTH = 0.1;
  var FREE_SPINS_FOR_BELLS = 2;
  var MAX_SPEED = 1500;
  var ACCEL_TIME = 0.25;
  var MIN_SPIN_TIME = 0.9;
  var STOP_DURATION = 0.65;
  var STAGGER = 0.3;

  var REEL_X = [772.5, 967.5, 1162.5];
  var REEL_Y = 623.5;
  var SYMBOL_HEIGHT = 105;
  var STRIP_LENGTH = 30;

  var credits = START_CREDITS;
  var jackpot = START_JACKPOT;
  var freeSpins = 0;
  var betIndex = 0;
  var spinning = false;
  var spinElapsed = 0;
  var messageTimer = 0;

  // Extra features: auto-spin, sound mute and persistent statistics.
  var autoSpinEnabled = false;
  var autoTimer = -1;
  var muted = false;
  var stats = { spins: 0, wagered: 0, won: 0, jackpots: 0 };
  var bestWin = 0;

  var pendingPayline = null;
  var pendingBet = 0;
  var pendingPayout = 0;
  var pendingFreeSpins = 0;
  var jackpotBefore = 0;
  var pendingWasFree = false;

  var $ = function (id) { return document.getElementById(id); };

  // ------------------------------------------------------------------ RNG
  function randomSymbol() {
    var roll = Math.random() * TOTAL_WEIGHT;
    for (var i = 0; i < WEIGHTS.length; i++) {
      roll -= WEIGHTS[i];
      if (roll < 0) return i;
    }
    return WEIGHTS.length - 1;
  }

  function randomStrip() {
    var strip = [];
    for (var i = 0; i < STRIP_LENGTH; i++) strip.push(randomSymbol());
    return strip;
  }

  function wrapped(strip, index) {
    return strip[((index % STRIP_LENGTH) + STRIP_LENGTH) % STRIP_LENGTH];
  }

  // ------------------------------------------------------------ paytable
  function evaluatePayout(line, bet, pot) {
    if (line[0] === line[1] && line[1] === line[2]) {
      if (line[0] === 0 && pot > 0) return pot;         // progressive jackpot
      if (line[0] === 0) return 50 * bet;
      if (line[0] === 1) return 5 * bet;
      if (line[0] === 2) return 20 * bet;
      if (line[0] === 3) return 10 * bet;
    }
    var cherries = line.filter(function (s) { return s === 1; }).length;
    return cherries === 2 ? 2 * bet : 0;
  }

  function evaluateFreeSpins(line) {
    return (line[0] === 2 && line[1] === 2 && line[2] === 2) ? FREE_SPINS_FOR_BELLS : 0;
  }

  // ---------------------------------------------------------------- audio
  var audioContext = null;
  function beep(frequency, duration, volume, slideTo) {
    if (muted) return;
    try {
      if (!audioContext) audioContext = new (window.AudioContext || window.webkitAudioContext)();
      var osc = audioContext.createOscillator();
      var gain = audioContext.createGain();
      var now = audioContext.currentTime;
      osc.type = "triangle";
      osc.frequency.setValueAtTime(frequency, now);
      if (slideTo) osc.frequency.exponentialRampToValueAtTime(slideTo, now + duration);
      gain.gain.setValueAtTime(volume, now);
      gain.gain.exponentialRampToValueAtTime(0.001, now + duration);
      osc.connect(gain);
      gain.connect(audioContext.destination);
      osc.start(now);
      osc.stop(now + duration);
    } catch (e) { /* audio is optional */ }
  }

  function arpeggio(notes, spacing) {
    notes.forEach(function (n, i) { setTimeout(function () { beep(n, 0.16, 0.16); }, i * spacing); });
  }

  // ---------------------------------------------------------------- reels
  var reels = [];

  function buildReels() {
    for (var i = 0; i < 3; i++) {
      var el = document.createElement("div");
      el.className = "reel";
      el.style.left = REEL_X[i] + "px";
      el.style.top = REEL_Y + "px";
      el.style.width = "162px";
      el.style.height = (SYMBOL_HEIGHT * 3) + "px";
      $("reels").appendChild(el);

      var symbols = [];
      for (var r = 0; r < 3; r++) {
        var symbol = document.createElement("div");
        symbol.className = "symbol";
        el.appendChild(symbol);
        symbols.push(symbol);
      }

      reels.push({
        el: el, symbols: symbols, strip: randomStrip(), pos: 0, speed: 0,
        state: "idle", stopFrom: 0, stopTo: 0, stopT: 0, stopRequested: false
      });
    }
  }

  function renderReel(reel) {
    var base = Math.floor(reel.pos);
    var fraction = reel.pos - base;
    var stretch = 1 + Math.min(reel.speed / MAX_SPEED, 1) * 0.08;
    for (var row = 0; row < 3; row++) {
      var offset = 1 - row;
      var symbolIndex = wrapped(reel.strip, base + offset);
      var element = reel.symbols[row];
      element.style.backgroundImage = 'url("assets/' + SYMBOLS[symbolIndex] + '.png")';
      element.style.transform = "translateY(" + ((offset - fraction) * SYMBOL_HEIGHT) + "px) scaleY(" + stretch + ")";
    }
  }

  function easeOutCubic(t) { var u = t - 1; return 1 + u * u * u; }
  function easeOutBack(t) {
    var c = 1.25, u = t - 1;
    return 1 + (c + 1) * u * u * u + c * u * u;
  }

  function requestStop(reel, target) {
    reel.stopFrom = reel.pos;
    var destination = Math.ceil(reel.pos) + 3 + Math.floor(Math.random() * 4);
    reel.strip[((destination % STRIP_LENGTH) + STRIP_LENGTH) % STRIP_LENGTH] = target;
    reel.stopTo = destination;
    reel.stopT = 0;
    reel.state = "stopping";
  }

  function updateReel(reel, dt) {
    if (reel.state === "spinning") {
      reel.speed = Math.min(MAX_SPEED, reel.speed + (MAX_SPEED / ACCEL_TIME) * dt);
      reel.pos += (reel.speed * dt) / SYMBOL_HEIGHT;
    } else if (reel.state === "stopping") {
      reel.stopT += dt;
      var t = Math.min(1, reel.stopT / STOP_DURATION);
      reel.pos = reel.stopFrom + (reel.stopTo - reel.stopFrom) * easeOutBack(t);
      reel.speed = MAX_SPEED * (1 - easeOutCubic(t)) * 0.85;
      if (t >= 1) {
        reel.pos = reel.stopTo;
        reel.speed = 0;
        reel.state = "idle";
        beep(260, 0.08, 0.12, 120);
      }
    }
    renderReel(reel);
  }

  // -------------------------------------------------------------- actions
  function currentBet() { return BETS[Math.max(0, Math.min(betIndex, BETS.length - 1))]; }

  function showMessage(text) {
    $("status").textContent = text;
    $("status").style.opacity = 1;
    messageTimer = 2.2;
  }

  function updateUI() {
    $("credits").textContent = "CREDITS " + credits;
    $("jackpot").textContent = "JACKPOT " + jackpot;
    $("betInfo").textContent = "BET " + currentBet() + (freeSpins > 0 ? "  (FREE SPIN)" : "");
    $("freeSpins").textContent = freeSpins > 0 ? "FREE SPINS " + freeSpins : "";
    var canSpin = !spinning && (freeSpins > 0 || credits >= currentBet());
    $("spinBtn").className = "ui arrow " + (canSpin ? "spin-normal" : "spin-off");
    $("betBtn").className = "ui arrow " + (spinning ? "bet-off" : "bet-normal");
  }

  function updateStats() {
    $("stats").textContent =
      "SESSION STATS\n\n" +
      "SPINS      " + stats.spins + "\n" +
      "WAGERED    " + stats.wagered + "\n" +
      "WON        " + stats.won + "\n" +
      "BEST WIN   " + bestWin + "\n" +
      "JACKPOTS   " + stats.jackpots;
  }

  function toggleAuto() {
    autoSpinEnabled = !autoSpinEnabled;
    autoTimer = autoSpinEnabled && !spinning && (freeSpins > 0 || credits >= currentBet()) ? 0.35 : -1;
    var button = $("autoBtn");
    button.textContent = "AUTO: " + (autoSpinEnabled ? "ON" : "OFF");
    button.classList.toggle("active", autoSpinEnabled);
    $("popupDim").style.display = "none";
    beep(520, 0.06, 0.12);
  }

  function toggleSound() {
    muted = !muted;
    $("soundBtn").textContent = "SOUND: " + (muted ? "OFF" : "ON");
    if (!muted) beep(520, 0.06, 0.12);
  }

  function toggleHelp() {
    var help = $("helpDim");
    help.style.display = help.style.display === "block" ? "none" : "block";
    beep(520, 0.06, 0.12);
  }

  function cycleBet() {
    if ($("helpDim").style.display === "block") return;
    if (spinning) { showMessage("Wait for the reels to stop first."); return; }
    betIndex = (betIndex + 1) % BETS.length;
    beep(520, 0.06, 0.12);
    updateUI();
  }

  function pullLever() {
    var lever = $("lever");
    var start = performance.now();
    function step(now) {
      var t = Math.min(1, (now - start) / 650);
      lever.style.transform = "translate(-50%, -50%) rotate(" + (-32 * Math.sin(t * Math.PI)) + "deg)";
      if (t < 1) requestAnimationFrame(step);
      else lever.style.transform = "translate(-50%, -50%)";
    }
    requestAnimationFrame(step);
  }

  function spin() {
    if ($("popupDim").style.display === "block" || $("helpDim").style.display === "block") return;
    if (spinning) { showMessage("The reels are still spinning!"); return; }

    var usesFree = freeSpins > 0;
    var bet = currentBet();
    if (!usesFree && credits < bet) { showMessage("Not enough credits - lower your bet."); return; }

    if (!usesFree) credits -= bet; else freeSpins--;
    stats.spins++;
    if (!usesFree) stats.wagered += bet;
    jackpotBefore = jackpot;
    if (!usesFree) jackpot += Math.round(bet * GROWTH);

    pendingPayline = [randomSymbol(), randomSymbol(), randomSymbol()];
    pendingBet = bet;
    pendingWasFree = usesFree;
    pendingPayout = evaluatePayout(pendingPayline, bet, jackpot);
    pendingFreeSpins = evaluateFreeSpins(pendingPayline);

    spinning = true;
    spinElapsed = 0;
    reels.forEach(function (r) { r.state = "spinning"; r.speed = 0; r.stopRequested = false; });
    $("winInfo").textContent = "WIN 0";
    $("status").textContent = "Reels spinning...";
    $("status").style.opacity = 1;
    messageTimer = 0;
    beep(180, 0.3, 0.12, 420);
    pullLever();
    updateUI();
  }

  function finishSpin() {
    var isJackpot = pendingPayline[0] === 0 && pendingPayline[1] === 0 && pendingPayline[2] === 0 && pendingPayout > 0;
    if (pendingPayout > 0) {
      credits += pendingPayout;
      stats.won += pendingPayout;
      if (pendingPayout > bestWin) {
        bestWin = pendingPayout;
        try { localStorage.setItem("lucky7.bestWin", String(bestWin)); } catch (e) { /* storage optional */ }
      }
    }
    if (isJackpot) { jackpot = START_JACKPOT; stats.jackpots++; }
    freeSpins += pendingFreeSpins;

    $("winInfo").textContent = "WIN " + pendingPayout;
    spinning = false;
    updateStats();

    // Celebrate by pulsing the three payline symbols.
    if (pendingPayout > 0) {
      reels.forEach(function (reel) {
        var symbol = reel.symbols[1];
        symbol.classList.remove("win-glow");
        void symbol.offsetWidth; // force reflow so the animation restarts
        symbol.classList.add("win-glow");
        setTimeout(function () { symbol.classList.remove("win-glow"); }, 950);
      });
    }

    var title = isJackpot ? "JACKPOT!!!" : (pendingPayout > 0 ? "YOU WIN!" : "TRY AGAIN");
    var body;
    var threeOfAKind = pendingPayline[0] === pendingPayline[1] && pendingPayline[1] === pendingPayline[2];
    if (isJackpot) body = "JACKPOT! All three LUCKY 7s!";
    else if (pendingPayout > 0 && threeOfAKind) body = "Three " + NAMES[pendingPayline[0]] + "s pays " + pendingPayout + " credits!";
    else if (pendingPayout > 0) body = "Two CHERRIES pays " + pendingPayout + " credits!";
    else body = "No win this time - try again!";
    if (pendingFreeSpins > 0) body += "\nBONUS: " + pendingFreeSpins + " FREE SPINS!";

    $("popupTitle").textContent = title;
    $("popupBody").textContent = body;
    $("popupBody").style.whiteSpace = "pre-line";
    $("status").textContent = body;
    $("status").style.opacity = 1;

    if (isJackpot) arpeggio([523, 659, 784, 1047, 1319], 110);
    else if (pendingPayout > 0) arpeggio([523, 659, 784], 110);
    if (pendingFreeSpins > 0) setTimeout(function () { arpeggio([880, 1175], 120); }, 500);

    // Auto-play skips the popup so spins can run back-to-back.
    if (autoSpinEnabled) {
      autoTimer = 0.55;
    } else {
      $("popupDim").style.display = "block";
    }
    updateUI();
  }

  // ----------------------------------------------------------------- loop
  var lastTime = 0;
  function frame(now) {
    var dt = lastTime ? Math.min((now - lastTime) / 1000, 0.05) : 0;
    lastTime = now;

    // Auto-play schedules the next spin shortly after the previous one ends.
    if (autoTimer > 0) {
      autoTimer -= dt;
      if (autoTimer <= 0) {
        autoTimer = -1;
        if (!spinning && (freeSpins > 0 || credits >= currentBet())) {
          spin();
        } else {
          autoSpinEnabled = false;
          $("autoBtn").textContent = "AUTO: OFF";
          $("autoBtn").classList.remove("active");
          showMessage("Auto-spin stopped - not enough credits.");
        }
      }
    }

    if (spinning) {
      spinElapsed += dt;
      reels.forEach(function (reel, i) {
        if (!reel.stopRequested && spinElapsed >= MIN_SPIN_TIME + i * STAGGER) {
          reel.stopRequested = true;
          requestStop(reel, pendingPayline[i]);
        }
      });
    }

    var fastest = 0;
    reels.forEach(function (reel) {
      updateReel(reel, dt);
      fastest = Math.max(fastest, reel.speed);
    });
    $("blur").style.opacity = String(Math.min(1, fastest / MAX_SPEED) * 0.85);

    if (spinning && reels.every(function (r) { return r.state === "idle"; })) finishSpin();

    if (messageTimer > 0) {
      messageTimer -= dt;
      if (messageTimer <= 0) $("status").style.opacity = 0;
    }
    requestAnimationFrame(frame);
  }

  // ------------------------------------------------------------- startup
  function fitStage() {
    var scale = Math.min(window.innerWidth / 1920, window.innerHeight / 1080);
    $("stage").style.transform = "translate(-50%, -50%) scale(" + scale + ")";
  }

  function init() {
    buildReels();
    reels.forEach(renderReel);
    $("popupDim").style.display = "none";
    $("helpDim").style.display = "none";
    try { bestWin = parseInt(localStorage.getItem("lucky7.bestWin") || "0", 10) || 0; } catch (e) { /* optional */ }
    updateStats();

    $("spinBtn").addEventListener("click", spin);
    $("betBtn").addEventListener("click", cycleBet);
    $("autoBtn").addEventListener("click", toggleAuto);
    $("soundBtn").addEventListener("click", toggleSound);
    $("helpBtn").addEventListener("click", toggleHelp);
    $("closeHelpBtn").addEventListener("click", function () { $("helpDim").style.display = "none"; });
    $("yesBtn").addEventListener("click", function () {
      $("popupDim").style.display = "none";
      if (!spinning && (freeSpins > 0 || credits >= currentBet())) spin();
    });
    $("noBtn").addEventListener("click", function () { $("popupDim").style.display = "none"; });
    document.addEventListener("keydown", function (event) {
      if (event.code === "Space") { event.preventDefault(); spin(); }
      else if (event.key === "b" || event.key === "B") cycleBet();
      else if (event.key === "a" || event.key === "A") toggleAuto();
      else if (event.key === "m" || event.key === "M") toggleSound();
      else if (event.key === "h" || event.key === "H") toggleHelp();
      else if (event.key === "Escape")
      {
        if ($("helpDim").style.display === "block") $("helpDim").style.display = "none";
        else $("popupDim").style.display = "none";
      }
    });
    window.addEventListener("resize", fitStage);
    fitStage();
    updateUI();
    requestAnimationFrame(frame);
  }

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
  else init();
})();
