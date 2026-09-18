const T = require("../theme");
const { C, FONT, W, H, MX, TOP, BOTTOM, COLW, COLGAP, colX, DOCS, asset } = T;
const { chapterSlide } = require("./common");

module.exports = function ch3(pres) {
  // 18 — Chapter divider
  chapterSlide(pres, 3, "Flat sensors", "Calibrating a tilt adapter  ·  guided and automatic correction  ·  simulators", "plane",
    `Pause for questions on detection and autofocus.
     Chapter three is the big one: tilt and backfocus. In 2023 Hocus Focus could measure them. In v4 it tells you exactly what to do about them — and with the right hardware, does it for you.`);

  // 19 — Recap: the sensor model
  let s = T.newSlide(pres, {
    chapter: 3, title: "The sensor model, in one minute",
    docs: DOCS.sensorModel,
    notes: `Quick recap from 2023. If you measure where best focus falls across the sensor, you get a surface. That surface is the sum of two things: a tilted plane — that's sensor tilt, one side reaching focus before the other — and a symmetric bowl — that's field curvature, which is how a backfocus spacing error shows up. Blue and pink here are just "focuses early" and "focuses late".
      On the right is the real thing in the Aberration Inspector: the fitted surface for one of my runs, in microns.
      In 2023 this told you THAT you had tilt, and roughly which way. What it didn't tell you was what to do about it: which screw, which direction, how far. That's what v4 adds.
      Docs: ${DOCS.sensorModel}`,
  });
  const a19 = T.art(s, asset("art-decomp.png"), { x: MX, y: TOP + 0.1, w: 7.3, h: 2.0, alignX: "left", alignY: "top" });
  const sq = a19.w * (300 / 1140), gp = a19.w * (120 / 1140);
  ["+", "="].forEach((op, i) => T.text(s, op, { x: a19.x + (i + 1) * sq + i * gp, y: a19.y, w: gp, h: a19.h }, { fontFace: FONT.light, fontSize: 40, align: "center", valign: "middle", color: C.text }));
  [["Tilt", "one side focuses first"], ["Curvature", "corners vs. center: backfocus"], ["Your sensor", "best focus across the frame"]].forEach(([h, b], i) => {
    const x = a19.x + i * (sq + gp) - 0.3;
    T.text(s, h, { x, y: a19.y + a19.h + 0.1, w: sq + 0.6, h: 0.4 }, { fontFace: FONT.head, fontSize: 18, align: "center", valign: "middle" });
    T.text(s, b, { x, y: a19.y + a19.h + 0.5, w: sq + 0.6, h: 0.4 }, { fontSize: 14, color: C.muted, align: "center" });
  });
  const c19 = T.card(s, asset("doc-sensor-surface.png"), { x: 8.35, y: TOP + 0.1, w: 4.28, h: 2.85, alignX: "right", alignY: "top" }, { caption: "In the Aberration Inspector" });
  T.panel(s, { x: MX, y: 5.0, w: W - 2 * MX, h: 1.4 });
  T.text(s, [{ text: "In 2023 it told you " }, { text: "that", options: { bold: true, color: C.pink } }, { text: " you had tilt. It didn't tell you what to do about it." }],
    { x: MX + 0.4, y: 5.0, w: W - 2 * MX - 0.8, h: 1.4 }, { fontFace: FONT.head, fontSize: 24, valign: "middle" });

  // 20 — Tilt Adapter Calibration Wizard
  s = T.newSlide(pres, {
    chapter: 3, title: "Which screw, which way, how far?",
    docs: DOCS.wizard,
    notes: `Those are the three questions. A tilt measurement only becomes instructions once the software knows your adapter. To answer them, the software has to know your tilt adapter: where the screws are relative to the image, and how much each one moves the sensor. That's what the Tilt Adapter Calibration Wizard measures.
      Setup is short. Pick your adapter from the presets — Neumann CTU, the ASG Photon Cage family, the motorized ASG EAT, the OGMA tilters — or choose Manual and type in the numbers. If your adapter isn't in the list, message me with its thread pitch, screw count and screw radius and I'll add a preset. Three-screw and four-screw adapters are both supported. You can name the screws whatever you call them, and those names are used everywhere afterwards.
      Docs: ${DOCS.wizard}`,
  });
  // Shot left, points right (slide 6's idiom). The wizard's settings pane is portrait -- roughly 1.4:1
  // with the adapter rows, the motorized connection and the screw labels all open -- so the original
  // full-width 3.3in strip letterboxed it by 62%. Sizing the card to the real panel keeps the capture
  // honest instead of cropping away the rows the three points below actually refer to.
  const c20 = T.shot(s, "wizard-setup.png", { x: MX, y: TOP, w: 6.45, h: BOTTOM - TOP, alignX: "left", alignY: "top" }, { fallback: "doc-wizard-setup.png" });
  T.kicker(s, "TELL IT ABOUT YOUR ADAPTER", { x: 7.55, y: c20.y, w: 5.1 });
  T.bullets(s, [
    [{ text: "Presets for popular adapters", bold: true }, " \u2014 Neumann, ASG, OGMA and more. Not listed? Message me and I'll add yours"],
    [{ text: "3 or 4 screws, by hand or motor", bold: true }, " \u2014 manual adapters and the ASG EAT"],
    [{ text: "Thread pitch and screw radius", bold: true }, " \u2014 filled in by the preset, or typed from your adapter's spec"],
    [{ text: "Name the screws", bold: true }, " whatever you call them, and those names are used everywhere afterwards"],
  ], { x: 7.55, y: c20.y + 0.4, w: 5.08, h: c20.h - 0.4 }, { size: 18 });

  // 21 — The calibration, step by step
  s = T.newSlide(pres, {
    chapter: 3, title: "Turn a screw, watch the tilt plane move",
    docs: DOCS.wizard,
    notes: `Here's the process. Step one: a baseline measurement. Step two: the wizard asks you to turn screw 1 by a known amount — say one full turn clockwise — and measures again. Step three: put it back and re-measure. Step four: same thing with screw 2. Step five: a final re-measure.
      Every one of those steps is a full focus sweep followed by a sensor model fit. The wizard is watching how the tilt plane moves in response to a known input. The re-measurements in between aren't wasted — they cancel out slow drift, like temperature changing during the run.
      Two optional extra steps turn all screws clockwise together, which works out which direction your adapter moves — toward the camera or toward the telescope — instead of assuming it.
      Four-screw adapters only need two screws measured: opposite screws are mechanically coupled.
      Docs: ${DOCS.wizard}`,
  });
  const nodes = [["1", "Baseline"], ["2", "Turn screw 1"], ["3", "Re-measure"], ["4", "Turn screw 2"], ["5", "Re-measure"], ["6", "All clockwise"], ["7", "Return"]];
  const d = 0.66, span = W - 2 * MX, stepX = span / 7, y0 = 1.98;
  T.panel(s, { x: MX, y: TOP, w: span, h: 1.7 });
  s.addShape("line", { x: MX + stepX / 2, y: y0 + d / 2, w: stepX * 4, h: 0, line: { color: C.purple, width: 2.5 } });
  s.addShape("line", { x: MX + stepX * 4.5, y: y0 + d / 2, w: stepX * 2, h: 0, line: { color: C.dim, width: 2, dashType: "dash" } });
  nodes.forEach(([n, label], i) => {
    const cx = MX + stepX * (i + 0.5), opt = i > 4, you = i === 1 || i === 3 || i === 5 || i === 6;
    if (opt) {
      s.addShape("ellipse", { x: cx - d / 2, y: y0, w: d, h: d, fill: { color: C.panel }, line: { color: C.pink, width: 2, dashType: "dash" } });
      T.text(s, n, { x: cx - d / 2, y: y0, w: d, h: d }, { fontFace: FONT.head, fontSize: 18, align: "center", valign: "middle", color: C.muted });
    } else {
      T.numberDot(s, n, cx - d / 2, y0, d, you ? C.pink : C.purple);
    }
    T.text(s, label, { x: cx - stepX / 2 + 0.05, y: y0 + d + 0.08, w: stepX - 0.1, h: 0.4 }, { fontSize: 15, align: "center", valign: "middle", color: opt ? C.muted : C.text, bold: you && !opt });
  });
  s.addShape("ellipse", { x: MX + 0.3, y: 3.2, w: 0.2, h: 0.2, fill: { color: C.pink } });
  T.text(s, "you turn", { x: MX + 0.58, y: 3.12, w: 1.1, h: 0.36 }, { fontSize: 14, color: C.muted, valign: "middle" });
  s.addShape("ellipse", { x: MX + 1.75, y: 3.2, w: 0.2, h: 0.2, fill: { color: C.purple } });
  T.text(s, "the wizard measures", { x: MX + 2.03, y: 3.12, w: 2.4, h: 0.36 }, { fontSize: 14, color: C.muted, valign: "middle" });
  T.text(s, "optional: finds which way the adapter moves", { x: MX + stepX * 4.6, y: 3.12, w: stepX * 2.4 - 0.2, h: 0.36 }, { fontSize: 14, color: C.muted, align: "center", valign: "middle" });
  const py = 3.8, ph = BOTTOM - py, pw21 = 5.5;
  T.shot(s, "wizard-step.png", { x: MX, y: py, w: pw21, h: ph }, { label: "Tight crop: the step's instruction sentence + the screw diagram" });
  T.bullets(s, [
    [{ text: "Every step is a full focus sweep", bold: true }],
    "A known turn in, a measured tilt change out",
    "Re-measuring cancels thermal drift",
  ], { x: MX + pw21 + 0.5, y: py, w: span - pw21 - 0.5, h: ph }, { size: 19, gap: 10 });

  // 22 — What comes out
  s = T.newSlide(pres, {
    chapter: 3, title: "A measured map of your adapter",
    docs: DOCS.wizard,
    notes: `Two answers come out of the calibration.
      One: where each screw sits, as the sensor sees it. This is derived from the measurements, not from looking at the adapter — which matters, because a star diagonal, a mirror, or a rotator can flip or rotate the image relative to the hardware. The wizard doesn't care; it measured the response in image space.
      Two: how far each screw moves the tilt plane — effectively microns per turn, or per step for a motorized adapter. It's cross-checked against the adapter's mechanical spec, and the wizard warns you if they disagree.
      You can save a calibration's data and replay it indoors later, and there's a manual entry path if you already know your geometry.
      But notice: every one of those calibration steps was a sensor model. And a sensor model is a very demanding measurement — which brings us to the punchline.
      Docs: ${DOCS.wizard}`,
  });
  T.shot(s, "wizard-result.png", { x: MX, y: TOP, w: 5.9, h: BOTTOM - TOP }, { fallback: "doc-wizard-result.png", fillBox: true });
  const rx = 7.0, rw = W - MX - rx;
  [["1", "Where each screw sits, as the sensor sees it", "Measured in the image, so mirrors, diagonals and rotators don't matter"],
   ["2", "How far each screw moves the tilt plane", "Microns per turn (or per motor step), cross-checked against the adapter's spec"]].forEach(([n, h, b], i) => {
    const y = TOP + i * 1.75;
    T.panel(s, { x: rx, y, w: rw, h: 1.55 });
    T.numberDot(s, n, rx + 0.3, y + 0.25, 0.62);
    T.text(s, h, { x: rx + 1.15, y: y + 0.15, w: rw - 1.4, h: 0.6 }, { fontFace: FONT.head, fontSize: 18, valign: "middle" });
    T.text(s, b, { x: rx + 1.15, y: y + 0.75, w: rw - 1.4, h: 0.7 }, { fontSize: 14, color: C.muted });
  });
  T.text(s, [{ text: "Measured, not assumed. ", options: { bold: true, color: C.text } }, { text: "But every step was a sensor model, and that is a demanding measurement…", options: { color: C.muted } }],
    { x: rx, y: TOP + 3.55, w: rw, h: 1.0 }, { fontSize: 17, valign: "middle" });

  // 23 — The punchline
  s = T.newSlide(pres, {
    chapter: 3, title: "In a sensor model, every star matters", snr: true,
    notes: `Here's the payoff for "why so many settings".
      Compare the two measurements. Autofocus: ONE curve, where each point is the average of hundreds of stars. Averaging gives you high signal-to-noise for free.
      Sensor model: HUNDREDS of curves — one per star — where each point is ONE star in ONE frame. No averaging to save you. Low SNR. So every single star has to be found and measured well, in every frame, including the out-of-focus ends of the sweep — and the stars you need most are in the corners, where they're faintest and most distorted.
      How it works, along the bottom. Step one: match the same star across every frame of the focus sweep, from badly out of focus, through focus, and out the other side. Frames shift and stars change size, so this uses a RANSAC-based registration — thank you to Steve Smith for that implementation. Step two: fit a focus curve for each individual star, so each star gets its own best-focus position. Step three: fit a 3D surface through all of those positions across the sensor. Tilt and curvature fall out of that surface.
      Every star matters, and getting enough signal out of each one is hard. Let me show you what that looks like.
      Backup: a star must be matched in at least 5 frames with a good individual curve fit; the surface fit rejects outliers robustly. Docs: ${DOCS.sensorModel}`,
  });
  const hw = (W - 2 * MX - 0.34) / 2, hy = 1.8, hh = 2.8;
  [["AUTOFOCUS", "One curve", "Every point averages hundreds of stars", "High SNR, for free", "art-onecurve.png", false],
   ["SENSOR MODEL", "Hundreds of curves", "Every point is one star, in one frame, even far out of focus", "Low SNR: every star must be measured well", "art-manycurves.png", true]].forEach(([k, big, sub, snr, img, hi], i) => {
    const x = MX + i * (hw + 0.34);
    T.panel(s, { x, y: hy, w: hw, h: hh }, hi ? { accent: C.amber } : {});
    T.kicker(s, k, { x: x + 0.35, y: hy + 0.2, w: 3 }, hi ? C.amber : C.lav);
    T.text(s, big, { x: x + 0.35, y: hy + 0.5, w: 3.6, h: 0.8 }, { fontFace: FONT.head, fontSize: 26, valign: "middle", color: hi ? C.amber : C.text });
    T.text(s, sub, { x: x + 0.35, y: hy + 1.3, w: 3.2, h: 0.85 }, { fontSize: 15, color: C.text });
    T.text(s, snr, { x: x + 0.35, y: hy + hh - 0.95, w: 3.3, h: 0.65 }, { fontSize: 15, bold: true, color: C.amber, valign: "bottom" });
    T.art(s, asset(img), { x: x + 3.8, y: hy + 0.3, w: hw - 4.05, h: hh - 0.6 });
  });
  const sy = 4.9, sh = 1.85;
  [["1", "Match stars", "Across every frame. Star registration by Steve Smith", "art-match.png"],
   ["2", "Fit each star", "Every star gets its own focus curve", "art-curves.png"],
   ["3", "Fit a surface", "Tilt and curvature fall out of it", "art-surface.png"]].forEach(([n, h, b, img], i) => {
    const x = colX(i);
    T.art(s, asset(img), { x, y: sy + 0.05, w: 1.6, h: sh - 0.35 });
    T.numberDot(s, n, x + 1.75, sy + 0.16, 0.42);
    T.text(s, h, { x: x + 2.27, y: sy + 0.16, w: COLW - 2.27, h: 0.42 }, { fontFace: FONT.head, fontSize: 17, valign: "middle" });
    T.text(s, b, { x: x + 1.75, y: sy + 0.72, w: COLW - 1.85, h: 0.7 }, { fontSize: 14, color: C.muted });
    if (i < 2) T.arrowRight(s, x + COLW - 0.02, sy + 0.75, COLGAP - 0.14);
  });

  // 24 — LIVE DEMO
  s = T.newSlide(pres, {
    hero: true,
    notes: `LIVE DEMO — sensor model on a saved autofocus run. About 5 minutes.
      Before the talk: NINA open on the Imaging tab with the Aberration Inspector dock visible and wide; "Sensor Curve Model Enabled" ON in the inspector options; saved run folder picked out in advance (dense star field, visible tilt); do one dry run so you know how long it takes.
      Script:
      1. Aberration Inspector → Load Saved AF → pick the AutoFocus_<date> folder. Mention: no equipment needed, this is a replay.
      2. While it runs: every frame is being re-detected, stars matched across frames, one curve fit per star.
      3. When it lands: point at "Points in Model" — that's how many individual stars survived. Every one had to be found in most frames of the sweep.
      4. Show the 3D surface: the slope is tilt, the bowl is curvature/backfocus. Rotate it.
      5. Show Model Properties and the Model Analysis checklist: tilt effect and curvature effect in microns, graded against the critical focus zone.
      6. Tease the Tilt Adapter Guidance section — we'll come back to it in a few slides.
      If the demo misbehaves: the next slide (hidden) has screenshots of the same kind of result — unhide or jump to it.`,
  });
  s.addShape("roundRect", { x: MX, y: 1.7, w: 2.5, h: 0.6, rectRadius: 0.08, fill: { color: C.pink } });
  s.addImage({ path: asset("icon-play.png"), x: MX + 0.28, y: 1.86, w: 0.28, h: 0.28 });
  T.text(s, "LIVE DEMO", { x: MX + 0.7, y: 1.7, w: 1.7, h: 0.6 }, { fontFace: FONT.head, fontSize: 18, charSpacing: 3, valign: "middle", color: C.white });
  T.text(s, "A sensor model from a saved autofocus run", { x: MX, y: 2.5, w: W - 2 * MX, h: 1.0 }, { fontFace: FONT.head, fontSize: 38, valign: "middle" });
  T.panel(s, { x: MX, y: 3.65, w: 7.6, h: 0.7 });
  T.text(s, "Imaging  →  Aberration Inspector  →  Load Saved AF", { x: MX + 0.3, y: 3.65, w: 7.2, h: 0.7 }, { fontSize: 20, color: C.text, valign: "middle" });
  ["How many stars make it into the model", "One focus curve per star", "The 3D surface: tilt and curvature, in microns"].forEach((t, i) => {
    const x = colX(i);
    T.panel(s, { x, y: 4.95, w: COLW, h: 1.35 });
    T.kicker(s, "WATCH FOR", { x: x + 0.3, y: 5.07, w: 3 });
    T.text(s, t, { x: x + 0.3, y: 5.42, w: COLW - 0.6, h: 0.8 }, { fontSize: 17, valign: "middle" });
  });

  // 25 — Demo backup (hidden) — every crop is from the same run
  s = T.newSlide(pres, {
    chapter: 3, title: "One fitted sensor model (demo backup)",
    notes: `Hidden slide — only needed if the live demo fails. All three crops are from the same run.
      1: the fitted 3D surface — slope is tilt, the bowl is curvature.
      2: "Points in Model" is the count of individual stars that survived matching and per-star curve fitting.
      3: the analysis grades each effect against the critical focus zone: here curvature passes, tilt fails.`,
  });
  s.hidden = true;
  const b1 = T.card(s, asset("doc-backup-surface.png"), { x: MX, y: TOP, w: 6.3, h: 3.75, alignX: "left", alignY: "top" });
  T.numberDot(s, 1, MX, b1.y + b1.h + 0.2, 0.46);
  T.text(s, "The slope is tilt; the bowl is curvature", { x: MX + 0.62, y: b1.y + b1.h + 0.2, w: 5.6, h: 0.46 }, { fontSize: 17, valign: "middle" });
  const b2 = T.card(s, asset("doc-backup-properties.png"), { x: 7.25, y: TOP, w: 5.38, h: 1.5, alignX: "left", alignY: "top" });
  T.numberDot(s, 2, 7.25, b2.y + b2.h + 0.15, 0.46);
  T.text(s, "Points in Model: the stars that survived", { x: 7.87, y: b2.y + b2.h + 0.15, w: 4.8, h: 0.46 }, { fontSize: 16, valign: "middle" });
  const b3 = T.card(s, asset("doc-backup-checklist.png"), { x: 7.25, y: b2.y + b2.h + 0.85, w: 2.2, h: 2.2, alignX: "left", alignY: "top" });
  T.numberDot(s, 3, b3.x + b3.w + 0.3, b3.y + 0.1, 0.46);
  T.text(s, "Each effect is graded against the critical focus zone. Here curvature passes and tilt fails.", { x: b3.x + b3.w + 0.92, y: b3.y + 0.08, w: W - MX - b3.x - b3.w - 0.92, h: 1.6 }, { fontSize: 16 });

  // 26 — That's why there are so many settings
  s = T.newSlide(pres, {
    chapter: 3, title: "That's why there are so many settings", snr: true,
    docs: DOCS.settings,
    notes: `So back to that wall of settings. Every optical system is different: focal ratio, central obstruction, corrector, pixel scale, sky, filters. A one-size-fits-all detector works most of the time for autofocus, because averaging hides its mistakes. It does not measure precisely enough for a sensor model, where every star is measured on its own.
      The settings fall into four jobs. Find faint stars: the thresholds and structure settings, adaptive binarization. Survive defocus: donut detection and the defocus-aware gates. Reject junk: hot pixels, saturated stars, distorted shapes, stars contaminated by a neighbour. Measure precisely: PSF fitting, background estimation, how measurements are averaged.
      I want to be clear that none of these was added on a hunch. Each one exists because I found a real difference between real setups in the autofocus runs people sent me, and one fixed value got it wrong for some of them.
      And this isn't only for tilt work. A properly tuned detector pins down best focus with very low error — and that error is now reported with every autofocus result, so you can see how good your focus actually is. That's why the optimizer exists — remember the "Optimize for aberration inspection" toggle on its start page? That tells it to favor finding many more stars across the frame, which is what the sensor model needs.
      All documented: ${DOCS.settings}`,
  });
  const w26 = T.card(s, asset("settings-wall.png"), { x: MX, y: TOP, w: 3.0, h: 3.4, alignX: "left", alignY: "top" });
  T.text(s, "40+ settings, four jobs", { x: MX, y: w26.y + w26.h + 0.2, w: w26.w, h: 0.4 }, { fontSize: 15, color: C.muted, align: "center" });
  const gx = 4.2, gw = W - MX - gx;
  T.text(s, "Every optical system is different", { x: gx, y: TOP - 0.1, w: gw, h: 0.8 }, { fontFace: FONT.head, fontSize: 30, color: C.amber, valign: "middle" });
  T.text(s, "One-size-fits-all detection works most of the time for autofocus. It isn't precise enough for a sensor model.", { x: gx, y: TOP + 0.75, w: gw, h: 0.85 }, { fontSize: 17, color: C.text });
  const chipW = (gw - 3 * 0.2) / 4;
  [["search", "Find faint stars"], ["ring", "Survive defocus"], ["filter", "Reject junk"], ["ruler", "Measure precisely"]].forEach(([icon, h], i) => {
    const x = gx + i * (chipW + 0.2), y = TOP + 1.75;
    T.panel(s, { x, y, w: chipW, h: 0.72 });
    s.addShape("ellipse", { x: x + 0.15, y: y + 0.13, w: 0.46, h: 0.46, fill: { color: C.purple } });
    s.addImage({ path: asset(`icon-${icon}.png`), x: x + 0.27, y: y + 0.25, w: 0.22, h: 0.22 });
    T.text(s, h, { x: x + 0.72, y, w: chipW - 0.8, h: 0.72 }, { fontFace: FONT.head, fontSize: 15, valign: "middle" });
  });
  T.text(s, "Each of these settings exists because I found a real difference between real setups. None was added on a hunch.", { x: gx, y: TOP + 2.65, w: gw, h: 0.75 }, { fontSize: 16, color: C.muted });
  T.panel(s, { x: gx, y: TOP + 3.5, w: gw, h: BOTTOM - TOP - 3.5 }, { accent: C.amber });
  T.kicker(s, "FOR EVERYONE, NOT JUST TILT", { x: gx + 0.3, y: TOP + 3.6, w: 6 }, C.amber);
  T.text(s, "A properly tuned detector pins down best focus with very low error. That error is now reported with every autofocus result.", { x: gx + 0.3, y: TOP + 3.95, w: gw - 0.6, h: BOTTOM - TOP - 4.0 }, { fontSize: 16, valign: "middle" });

  // 27 — Signal Amplification
  s = T.newSlide(pres, {
    chapter: 3, title: "“Signal Amplification”: more frames, more signal", snr: true,
    docs: DOCS.inspector,
    notes: `There's one more way to get more signal, and it has nothing to do with detection settings: take more frames.
      Signal Amplification divides your autofocus step size by N and multiplies the number of steps by N. The sweep covers exactly the same focus range — it just samples it more finely. On the left, a normal sweep; on the right, the same sweep at 2x — which is what the wizard screenshot below is set to.
      Two benefits. Every star's individual curve gets more points, so its best-focus estimate is tighter. And the jump in defocus between neighbouring frames is smaller, so stars are easier to match from one frame to the next.
      The cost is time: N times the exposures per sweep — and a tilt adapter calibration is five to seven sweeps. The wizard shows a live estimate of how many images you're signing up for. It applies to live captures only.
      Docs: ${DOCS.inspector}`,
  });
  const a27 = T.art(s, asset("art-sigamp.png"), { x: MX, y: TOP, w: 7.4, h: 2.75, alignX: "left", alignY: "top" });
  const cw27 = a27.w * (580 / 1320);
  T.text(s, "Normal sweep", { x: a27.x, y: a27.y + a27.h + 0.08, w: cw27, h: 0.38 }, { fontSize: 15, color: C.muted, align: "center", valign: "middle" });
  T.text(s, "star size vs. focuser position", { x: a27.x, y: a27.y + a27.h + 0.45, w: cw27, h: 0.32 }, { fontSize: 14, color: C.dim, align: "center" });
  T.text(s, [{ text: "2×", options: { color: C.amber, bold: true } }, { text: ": same range, half the step, twice the frames", options: { color: C.muted } }], { x: a27.x + a27.w - cw27 - 0.9, y: a27.y + a27.h + 0.08, w: cw27 + 1.3, h: 0.38 }, { fontSize: 15, align: "center", valign: "middle" });
  const c27 = T.card(s, asset("doc-wizard-sigamp.png"), { x: MX, y: 5.55, w: 4.9, h: 0.75, alignX: "left", alignY: "top" });
  T.text(s, "One number, in the inspector and the wizard", { x: c27.x + c27.w + 0.25, y: c27.y, w: 2.3, h: c27.h }, { fontSize: 14, color: C.muted, valign: "middle" });
  T.bullets(s, [
    [{ text: "More points on every star's curve", bold: true }],
    "Smaller focus jumps between frames, so stars are easier to match",
    [{ text: "The cost is exposures:", bold: true }, " a calibration is several sweeps"],
  ], { x: 8.6, y: TOP, w: 4.03, h: BOTTOM - TOP }, { size: 19, gap: 14 });

  // 28 — Guided adjustments
  s = T.newSlide(pres, {
    chapter: 3, title: "Now it tells you how far to turn each screw",
    docs: DOCS.inspector,
    notes: `Now the payoff of calibration. Once the adapter is calibrated, every Detailed Analysis in the Aberration Inspector comes with a Tilt Adapter Guidance section.
      One: the legend — what the symbols mean, and your units. You can display turns, degrees, or minutes on a clock face; a motorized adapter shows motor steps.
      Two: the arrows — for each screw, which way that corner of the adapter needs to move; a bigger arrow means a bigger move.
      Three: the numbers — how far to turn each screw, split into three rows: the part that fixes tilt, the part that fixes backfocus, and the total you actually apply. The rotation symbol tells you clockwise or counter-clockwise.
      Docs: ${DOCS.inspector}`,
  });
  const c28 = T.shot(s, "guidance.png", { x: MX + 0.6, y: TOP, w: W - 2 * MX - 0.6, h: 2.6, alignX: "left", alignY: "top" }, { fallback: "doc-guidance.png" });
  [0.1, 0.42, 0.8].forEach((f, i) => T.numberDot(s, i + 1, MX, c28.y + c28.h * f - 0.23, 0.46));
  T.columns3(s, [
    ["The legend, in your units", "Turns, degrees or clock-minutes. Motor steps for motorized adapters"],
    ["Arrows: which way", "The direction each corner of the adapter needs to move"],
    ["Numbers: how far", "Per screw, split into Tilt, Backfocus, and the Total you apply"],
  ], c28.y + c28.h + 0.45, { numbered: true });

  // 29 — Tilt vs backfocus
  s = T.newSlide(pres, {
    chapter: 3, title: "Tilt and backfocus are separate fixes",
    docs: DOCS.inspector,
    notes: `Why split it into tilt and backfocus? Because they're different motions of the same screws. In both pictures the dashed line is where the focal plane wants the sensor to be, the purple bar is the sensor, and the pink arrows are the fix.
      Tilt is corrected by turning screws differently — one side up, the other side down — so the plane tips.
      Backfocus is corrected by turning all the screws the same way, moving the whole sensor along the optical axis. If it needs more travel than the adapter has, that's a spacer.
      Seeing them separately means you can choose: fix tilt tonight, order a spacer for the backfocus.
      And whatever remains after both is simply your optics — the residual curvature of your corrector. The Model Analysis grades each against your critical focus zone — tilt within a quarter of it, curvature within one and a half times — so you know when to stop.
      Docs: ${DOCS.inspector}`,
  });
  [["art-tilt.png", "Tilt", "The fix: turn the screws differently"], ["art-backfocus.png", "Backfocus", "The fix: turn them all the same way, or add a spacer"]].forEach(([img, h, b], i) => {
    const x = MX + i * 4.1, w = 3.85, ph29 = 3.95;
    T.panel(s, { x, y: TOP, w, h: ph29 });
    const a = T.art(s, asset(img), { x: x + 0.3, y: TOP + 0.15, w: w - 0.6, h: 2.05 });
    T.text(s, h, { x: x + 0.35, y: TOP + 2.3, w: w - 0.7, h: 0.55 }, { fontFace: FONT.head, fontSize: 26, valign: "middle" });
    T.text(s, b, { x: x + 0.35, y: TOP + 2.9, w: w - 0.7, h: 1.0 }, { fontSize: 17, color: C.text });
  });
  T.text(s, "Dashed: where focus wants the sensor.   Bar: the sensor.   Arrows: the fix.", { x: MX, y: TOP + 4.05, w: 7.95, h: 0.4 }, { fontSize: 14, color: C.muted, valign: "middle" });
  const x29 = 8.95, w29 = W - MX - x29;
  T.text(s, "What's left is your optics", { x: x29, y: TOP, w: w29, h: 0.5 }, { fontFace: FONT.head, fontSize: 22, valign: "middle" });
  T.text(s, "Each effect is graded against your critical focus zone, so you know when you're done.", { x: x29, y: TOP + 0.55, w: w29, h: 1.1 }, { fontSize: 16, color: C.muted });
  [["check-green", "Model fit", "explains the data"], ["check-green", "Curvature", "within tolerance"], ["cross-pink", "Tilt", "too large: adjust"]].forEach(([icon, h, b], i) => {
    const y = TOP + 1.8 + i * 0.745;
    T.panel(s, { x: x29, y, w: w29, h: 0.66 });
    s.addImage({ path: asset(`icon-${icon}.png`), x: x29 + 0.2, y: y + 0.15, w: 0.36, h: 0.36 });
    T.text(s, h, { x: x29 + 0.75, y, w: 1.4, h: 0.66 }, { fontFace: FONT.head, fontSize: 17, valign: "middle" });
    T.text(s, b, { x: x29 + 2.05, y, w: w29 - 2.15, h: 0.66 }, { fontSize: 14, color: C.muted, valign: "middle" });
  });

  // 30 — Measure, turn, re-measure (mirrored layout: text left, image right)
  s = T.newSlide(pres, {
    chapter: 3, title: "Measure, turn, re-measure: watch the tilt converge",
    docs: DOCS.inspector,
    notes: `In practice it's a loop: run a Detailed Analysis, turn the screws by the amounts shown, run it again.
      The inspector keeps a history of every measurement in the session, so you can watch the tilt come down. [Talk through the screenshot: first measurement, after one adjustment, after two.]
      You stop when the tilt effect is inside the tolerance the analysis checks against the critical focus zone.
      [If you have real before/after corner-star crops, this is the slide to show them.]
      Docs: ${DOCS.inspector}`,
  });
  T.bullets(s, [
    [{ text: "Each round, the correction gets smaller", bold: true }],
    "The history keeps every measurement of the session",
    "Stop when tilt is inside your critical focus zone",
  ], { x: MX, y: TOP, w: 4.3, h: BOTTOM - TOP }, { size: 20 });
  T.shot(s, "tilt-history.png", { x: 5.35, y: TOP, w: 7.28, h: BOTTOM - TOP }, { label: "Tilt Measurement History after 2–3 measure/adjust rounds" });

  // 31 — ASG EAT: hands-off calibration
  s = T.newSlide(pres, {
    chapter: 3, title: "With an ASG EAT, calibration runs itself",
    docs: DOCS.motorized,
    notes: `The 2023 slide said "Motorized tilt adjusters?" with a question mark. The ASG EAT is exactly that: an electronic tilt adapter with four stepper motors instead of four screws. Hocus Focus now drives it directly from NINA.
      A huge thank-you to Josh Jones at ASG, who provided an EAT so I could build and test this.
      Connect to it from the Tilt Adapter Wizard, and calibration becomes hands-off: press Auto Run All and the wizard moves the motors, runs each sweep, and works through every step on its own. Go have dinner.
      If you cancel part-way, it puts the motors back where they started.
      Backup: motor positions live in the adapter's own memory and survive power cycles; the plugin never zeroes them. Safety limits cap steps per command and total travel.
      Docs: ${DOCS.motorized}`,
  });
  T.shot(s, "eat-connection.png", { x: MX, y: TOP, w: 6.4, h: BOTTOM - TOP }, { label: "Motorized Device Connection + motor positions grid (Simulator port is fine)" });
  const x31 = 7.55, w31 = W - MX - x31;
  T.text(s, [{ text: "Press Auto Run All.", options: { breakLine: true } }, { text: "Walk away.", options: { color: C.pink } }], { x: x31, y: TOP, w: w31, h: 1.5 }, { fontFace: FONT.head, fontSize: 32, valign: "middle" });
  T.text(s, "Four motors driven directly from N.I.N.A. The wizard makes every move and every measurement itself. Cancel at any time and it puts the motors back.", { x: x31, y: TOP + 1.6, w: w31, h: 1.5 }, { fontSize: 17, color: C.muted });
  T.panel(s, { x: x31, y: BOTTOM - 1.25, w: w31, h: 1.25 }, { accent: C.purple });
  T.kicker(s, "THANK YOU", { x: x31 + 0.3, y: BOTTOM - 1.15, w: 3 });
  T.text(s, [{ text: "Josh Jones at ASG", options: { bold: true } }, { text: ", for the EAT that made this automation possible" }], { x: x31 + 0.3, y: BOTTOM - 0.82, w: w31 - 0.6, h: 0.75 }, { fontSize: 16, valign: "middle" });

  // 32 — ASG EAT: automatic adjustment (mirrored)
  s = T.newSlide(pres, {
    chapter: 3, title: "Then it corrects tilt and backfocus for you",
    docs: DOCS.motorized,
    notes: `With a calibrated, connected EAT, the guidance section gets one more button: Automatic Adjustment.
      It works out the motor moves from the current measurement and shows you this review dialog first. Nothing moves until you approve. You can choose to apply tilt, backfocus, or both; you see each move, and what residual will be left.
      A full tilt-plus-backfocus correction is at most three motor commands.
      Afterwards you re-measure. If tilt somehow got worse — stale calibration, camera rotated since — you get a banner with a one-click Revert that undoes the moves.
      There's also a manual pad for nudging corners, and you can return to the motor positions of any earlier measurement in the session.
      Docs: ${DOCS.motorized}`,
  });
  const y32 = TOP + (BOTTOM - TOP - 3.9) / 2;
  T.iconRow(s, "hand", "You approve every move", "Review each motor command first. Apply tilt, backfocus, or both", MX, y32 + 0.1, 5.0);
  T.iconRow(s, "moves", "At most three moves", "For a complete tilt and backfocus correction", MX, y32 + 1.5, 5.0);
  T.iconRow(s, "undo", "Got worse? Revert", "One click undoes the moves. Then measure again", MX, y32 + 2.9, 5.0);
  T.shot(s, "eat-review-commands.png", { x: 6.0, y: TOP, w: 6.63, h: BOTTOM - TOP }, { label: "“Review motor commands” approval dialog" });

  // 33 — Simulators
  s = T.newSlide(pres, {
    chapter: 3, title: "Rehearse all of it indoors",
    docs: DOCS.simulator,
    notes: `One more thing, mostly for the curious. Building all of this needed a way to test without clear skies, so Hocus Focus now includes a camera simulator — and it's surprisingly sophisticated.
      It renders real catalog stars for wherever your mount is pointing, through a model of your optics and a real sensor — IMX455, 571, 533 and so on — with filters including narrowband, sky brightness, seeing, and realistic noise. Defocus it and you get donuts.
      Most importantly, you can inject tilt and backfocus error. Pair it with the simulated tilt adapter — virtual screws you click — and you can run autofocus, a full tilt adapter calibration, guided correction, even the EAT automation, entirely indoors on a cloudy day. When you've got it right, the panel reads "approximately flat".
      It's the best way to get a feel for the process before you touch a real screw. The documentation walks you through it.
      Docs: ${DOCS.simulator}`,
  });
  const h33 = 2.75;
  T.shot(s, "sim-frame-focused.png", { x: colX(0), y: TOP, w: COLW, h: h33 }, { label: "Simulated frame, in focus" });
  T.shot(s, "sim-frame-defocused.png", { x: colX(1), y: TOP, w: COLW, h: h33 }, { label: "Simulated frame, defocused donuts" });
  T.shot(s, "sim-tilt-panel.png", { x: colX(2), y: TOP, w: COLW, h: h33 }, { label: "Simulator Tilt Adapter panel at ✓ ≈ flat" });
  T.columns3(s, [
    ["A camera simulator with real stars", "Catalog stars for wherever the mount points, with real sensors, filters, seeing and noise"],
    ["It defocuses like your scope", "Donuts included. Inject tilt and backfocus error on purpose"],
    ["Virtual screws, or a virtual EAT", "Run autofocus, calibration and correction on a cloudy day"],
  ], TOP + h33 + 0.3, { headSize: 17, bodySize: 14 });
};
