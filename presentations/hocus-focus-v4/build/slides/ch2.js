const T = require("../theme");
const { C, FONT, W, H, MX, TOP, BOTTOM, COLW, colX, DOCS, asset } = T;
const { chapterSlide } = require("./common");
const { ASYM, SIGMA, THRESH } = require("../assets");

// Native axis label tucked right under the x-axis, right-aligned to the axis end.
function xAxisLabel(s, a, M, label = "focuser position →") {
  const xe = a.x + ((M.ox + M.w) / M.W) * a.w, ya = a.y + ((M.oy + M.h) / M.H) * a.h;
  T.text(s, label, { x: xe - 2.4, y: ya + 0.03, w: 2.4, h: 0.32 }, { fontSize: 14, color: C.muted, align: "right" });
  return ya;
}

module.exports = function ch2(pres) {
  // 11 — Chapter divider
  chapterSlide(pres, 2, "More stars, better curves", "Donut detection  ·  adaptive binarization  ·  hyperbolic fits  ·  frame review", "star",
    `Pause here for questions on the optimizer.
     Chapter two is a quick tour of four improvements to detection and autofocus itself. I'm going to keep each of these to the headline — every one has a full page in the documentation, and the link is at the bottom of each slide.`);

  // 12 — Donut detection
  let s = T.newSlide(pres, {
    chapter: 2, title: "Out-of-focus donut stars are no longer thrown away",
    docs: DOCS.donut,
    notes: `If you have a central obstruction — a Newtonian, an SCT, an RC — your out-of-focus stars aren't blobs, they're donuts: a bright ring with a hole in the middle.
      The old detector threw most of those away. The ring breaks up into arcs, so the pieces look too small; and a hollow ring fails the shape test, so it looks too distorted.
      v4 recognizes the ring for what it is and keeps the star. That gives autofocus real data at the ends of the sweep, where it used to be starved.
      It's opt-in: a toggle in the settings, or tick "Recover out-of-focus donut stars" in the optimizer and let it work out the details.
      Backup: recovery only affects which stars are accepted — it never changes a star's measured HFR. Truly faint rings with no core remain noise-limited for any detector.
      Docs: ${DOCS.donut}`,
  });
  T.kicker(s, "DONUT DETECTION", { x: MX, y: TOP - 0.05, w: 4 });
  const a12 = T.art(s, asset("art-donut.png"), { x: MX, y: TOP + 0.4, w: 4.1, h: 1.95, alignX: "left", alignY: "top" });
  const half = a12.w * (320 / 700);
  T.text(s, "In focus", { x: a12.x, y: a12.y + a12.h + 0.08, w: half, h: 0.35 }, { fontSize: 15, color: C.muted, align: "center" });
  T.text(s, "Out of focus", { x: a12.x + a12.w - half, y: a12.y + a12.h + 0.08, w: half, h: 0.35 }, { fontSize: 15, color: C.muted, align: "center" });
  T.text(s, "With a central obstruction (Newtonian, SCT, RC), a defocused star is a ring with a hole.", { x: MX, y: 4.75, w: 4.1, h: 1.3 }, { fontSize: 15, color: C.muted });
  const pw = 3.62, ph = pw / 1.5, px = 5.2;
  T.shot(s, "donut-frame-off.png", { x: px, y: TOP, w: pw, h: ph }, { label: "Defocused frame, donut detection OFF (3:2 crop)" });
  T.shot(s, "donut-frame-on.png", { x: px + pw + 0.19, y: TOP, w: pw, h: ph }, { label: "Same crop, donut detection ON" });
  T.bullets(s, [
    "The old detector rejected them as too fragmented or too “distorted”",
    [{ text: "v4 recognizes the ring and keeps the star", bold: true }],
    "Opt-in: flip one toggle, or let the optimizer decide",
  ], { x: px, y: 4.75, w: W - MX - px, h: BOTTOM - 4.75 }, { size: 18, gap: 8, valign: "top" });

  // 13 — Locally adaptive binarization
  s = T.newSlide(pres, {
    chapter: 2, title: "One brightness threshold can't fit the whole frame",
    docs: DOCS.adaptive,
    notes: `Step one of finding stars is deciding which pixels are bright enough to be "something". The old detector used one brightness threshold for the entire frame.
      But real frames aren't uniform: vignetting, sky gradients, amp glow. On the left is a frame with a glow along one side; on the right is the brightness along that dashed row. The white dashed line is a single global threshold: it misses the faint star on the left — the green circle — and lets noise through inside the glow — the pink cross.
      The purple line is what v4 does: the threshold follows the local background. The faint star is recovered, the glow noise is rejected.
      The result is more real stars — and especially more stars in the corners. Remember the corners; they come back in chapter three. It's on by default, nothing to configure. The feature is called "Locally Adaptive Binarization" in the settings.
      Docs — with the measurements behind this: ${DOCS.adaptive}`,
  });
  const a13 = T.art(s, asset("art-threshold.png"), { x: MX, y: TOP, w: 8.95, h: 3.1, alignX: "left", alignY: "top" });
  const lx = a13.x + a13.w + 0.35, lw = W - MX - lx;
  T.kicker(s, "LOCALLY ADAPTIVE BINARIZATION", { x: lx, y: TOP, w: lw + 0.2, h: 0.6 });
  const legend = [
    ["trace", C.muted, "Brightness along the marked row", false],
    ["line", C.white, "One global threshold", true],
    ["line", C.purple, "v4: adapts to the local background", false],
    ["ring", C.green, "Faint star: recovered", false],
    ["cross", C.pink, "Glow noise: rejected", false],
  ];
  legend.forEach(([kind, col, label, dash], i) => {
    const y = TOP + 0.7 + i * 0.5;
    if (kind === "trace") s.addShape("line", { x: lx, y: y + 0.2, w: 0.5, h: 0, line: { color: col, width: 1.5 } });
    if (kind === "line") s.addShape("line", { x: lx, y: y + 0.2, w: 0.5, h: 0, line: { color: col, width: 3, dashType: dash ? "dash" : "solid" } });
    if (kind === "ring") s.addShape("ellipse", { x: lx + 0.12, y: y + 0.07, w: 0.26, h: 0.26, fill: { color: C.bg, transparency: 100 }, line: { color: col, width: 2.5 } });
    if (kind === "cross") T.text(s, "✕", { x: lx, y: y - 0.02, w: 0.5, h: 0.44 }, { fontSize: 18, bold: true, color: col, align: "center", valign: "middle" });
    T.text(s, label, { x: lx + 0.65, y: y - 0.08, w: lw - 0.65, h: 0.56 }, { fontSize: 14, valign: "middle" });
  });
  T.text(s, "brightness along that row ↑", { x: a13.x + (THRESH.plotX / THRESH.W) * a13.w + 0.15, y: a13.y + 0.02, w: 3.2, h: 0.32 }, { fontSize: 14, color: C.muted });
  T.columns3(s, [
    ["Real frames aren't uniform", "Vignetting, gradients and amp glow change the background from center to corner"],
    ["v4 adapts the threshold locally", "Stricter where the frame is bright and noisy, more sensitive where it's clean"],
    ["More stars, especially in corners", "On by default. Remember the corners: they matter in chapter 3"],
  ], a13.y + a13.h + 0.2, { headSize: 17, bodySize: 14 });

  // 14 — Better hyperbolic fits
  s = T.newSlide(pres, {
    chapter: 2, title: "Real focus curves aren't symmetric",
    docs: DOCS.hyperbola,
    notes: `In 2023 I showed a slide called "Doesn't fit?" — real focus curves often aren't symmetric. Stars defocus differently inside and outside of focus.
      If you force a symmetric V onto a lopsided curve — the dashed line — the fitted minimum gets pulled toward the shallow side. The pink arrow is the error in your "best focus".
      v4 adds a Hybrid (Best Fit) mode, and it's the default. It fits several curve shapes to the same points, and it only picks a lopsided one when the data statistically justify the extra complexity. Clean symmetric data still gets a symmetric fit.
      Backup: outliers are only rejected when every candidate model agrees; candidates are ranked by how precisely they locate best focus.
      Docs: ${DOCS.hyperbola}`,
  });
  const a14 = T.art(s, asset("art-asym.png"), { x: MX, y: TOP, w: 6.6, h: 3.95, alignX: "left", alignY: "top" });
  const fx = (t) => a14.x + ((ASYM.ox + t * ASYM.w) / ASYM.W) * a14.w;
  const ya14 = xAxisLabel(s, a14, ASYM);
  T.text(s, "best focus shifts", { x: fx((ASYM.symMin + ASYM.trueMin) / 2) - 1.2, y: ya14 + 0.03, w: 2.4, h: 0.32 }, { fontSize: 15, bold: true, color: C.pink, align: "center" });
  T.text(s, "star size ↑", { x: a14.x + 0.45, y: ya14 - 0.75, w: 1.6, h: 0.32 }, { fontSize: 14, color: C.muted });
  s.addShape("line", { x: a14.x + 0.55, y: a14.y + 0.3, w: 0.5, h: 0, line: { color: C.muted, width: 3, dashType: "dash" } });
  T.text(s, "symmetric fit", { x: a14.x + 1.15, y: a14.y + 0.1, w: 2.5, h: 0.4 }, { fontSize: 15, color: C.muted, valign: "middle" });
  s.addShape("line", { x: a14.x + 0.55, y: a14.y + 0.75, w: 0.5, h: 0, line: { color: C.purple, width: 4 } });
  T.text(s, "fit that's allowed to lean", { x: a14.x + 1.15, y: a14.y + 0.55, w: 3.0, h: 0.4 }, { fontSize: 15, color: C.text, valign: "middle" });
  T.kicker(s, "HYBRID (BEST FIT), THE NEW DEFAULT", { x: 7.8, y: TOP + 0.45, w: 4.9 });
  T.bullets(s, [
    "Stars defocus differently on each side of focus",
    [{ text: "Force a symmetric V", bold: true }, " and best focus is pulled toward the shallow side"],
    "v4 tries several shapes, and only goes lopsided when the data justify it",
  ], { x: 7.8, y: TOP + 0.95, w: 4.83, h: 3.0 }, { size: 19, valign: "top" });

  // 15 — Autofocus frame review
  s = T.newSlide(pres, {
    chapter: 2, title: "See what autofocus saw, frame by frame",
    docs: DOCS.autofocus,
    notes: `Until now, an autofocus run gave you a curve and you had to trust it. If one point looked wrong, you couldn't see why.
      New controls under the autofocus chart: turn on "Keep frames for review", run autofocus from the panel, then hit "Review Frames". You can step through every frame in the sweep, with detected stars drawn on top, plus the focuser position, star count and HFR for that frame.
      Keeping frames only applies to runs you start from this panel — autofocus inside a sequence doesn't hold frames in memory. And there's "Replay Saved AF" for re-analysing a run from disk with no equipment connected.
      Docs: ${DOCS.autofocus}`,
  });
  T.kicker(s, "AUTOFOCUS FRAME REVIEW", { x: MX, y: TOP - 0.05, w: 4.3 });
  ["Turn on Keep frames for review", "Run autofocus from the panel", "Click Review Frames"].forEach((t, i) => {
    const y = TOP + 0.5 + i * 0.85;
    T.numberDot(s, i + 1, MX, y, 0.56, C.pink);
    T.text(s, t, { x: MX + 0.8, y, w: 3.5, h: 0.56 }, { fontSize: 18, valign: "middle" });
  });
  T.shot(s, "af-review-controls.png", { x: MX, y: BOTTOM - 1.6, w: 4.3, h: 1.6 }, { label: "AF panel controls (wide strip)" });
  T.shot(s, "af-review-good.png", { x: 5.35, y: TOP, w: 7.28, h: BOTTOM - TOP }, { label: "Review Frames window on a healthy frame (16:10)" });

  // 16 — Spot the bad frame
  s = T.newSlide(pres, {
    chapter: 2, title: "Spot the bad frame",
    docs: DOCS.autofocus,
    notes: `This is what it's for. Here's a frame from the same run where detection went wrong — [describe: zero stars / donuts missed / junk detections].
      You can see exactly what the detector saw at each focuser position. It's the fastest way to find the one frame that came up empty, and to understand whether your settings are the problem.
      And it closes the loop with the optimizer: the wizard has the same kind of frame view, where you can box stars it missed or detections that are bogus, then choose "Optimize with feedback".
      Docs: ${DOCS.autofocus}`,
  });
  const w16 = 5.5, x16 = (W - w16) / 2, side = x16 - MX - 0.35;
  T.shot(s, "af-review-bad.png", { x: x16, y: TOP, w: w16, h: BOTTOM - TOP }, { label: "Review Frames window on a problem frame, e.g. Detected stars: 0 (tight crop)" });
  const h16 = BOTTOM - TOP, r16 = x16 + w16 + 0.35;
  T.statement(s, "What the detector saw", "at every focuser position", { x: MX, y: TOP + h16 * 0.25 - 0.85, w: side, h: 1.7 });
  T.statement(s, "Find the empty frame", "or the donuts it missed", { x: MX, y: TOP + h16 * 0.75 - 0.85, w: side, h: 1.7 });
  T.statement(s, "Label the mistakes", "and hand them back to the optimizer", { x: r16, y: TOP + h16 * 0.5 - 0.85, w: side, h: 1.7 });

  // 17 — Bridge: what the optimizer is chasing
  s = T.newSlide(pres, {
    chapter: 2, title: "The optimizer chases a precise best focus", snr: true,
    notes: `Let me connect this back to the optimizer, because it sets up everything that follows.
      For autofocus, the optimizer's goal is to minimize the error in the calculated best-focus position — the pink band around the minimum of this curve. The tighter that band, the more repeatable your focus.
      Now notice what each point on this curve is: the average HFR of every star in the frame — often hundreds of stars. Averaging is a fantastic noise killer. A few missed stars, a few bad ones… the average barely moves. That is high signal-to-noise.
      So autofocus is forgiving. It's the easy case. Tilt is not — and that's where SNR comes back.
      Backup: the score also rewards star count, fit quality and coverage of the sensor, but focus precision dominates.`,
  });
  const a17 = T.art(s, asset("art-sigma.png"), { x: MX, y: TOP + 0.5, w: 6.6, h: 3.7, alignX: "left", alignY: "top" });
  const bx = a17.x + ((SIGMA.ox + SIGMA.x0 * SIGMA.w) / SIGMA.W) * a17.w;
  T.text(s, "how precisely we know best focus", { x: bx - 1.9, y: TOP, w: 3.8, h: 0.36 }, { fontSize: 15, bold: true, color: C.pink, align: "center", valign: "middle" });
  s.addShape("line", { x: bx, y: TOP + 0.37, w: 0, h: a17.y + (SIGMA.oy / SIGMA.H) * a17.h - TOP - 0.37, line: { color: C.pink, width: 1.5 } });
  const ya17 = xAxisLabel(s, a17, SIGMA);
  T.text(s, "star size (HFR) ↑", { x: a17.x + 0.45, y: ya17 - 0.75, w: 2.2, h: 0.32 }, { fontSize: 14, color: C.muted });
  T.bullets(s, [
    [{ text: "The goal: ", bold: true }, "minimize the error in the calculated best-focus position"],
    ["Each point is the ", { text: "average of every star in the frame", bold: true }],
    [{ text: "Averaging hundreds of stars = high SNR", bold: true, color: C.amber }],
  ], { x: 7.8, y: TOP, w: 4.83, h: 2.85 }, { size: 19, gap: 12 });
  T.panel(s, { x: 7.8, y: 4.9, w: 4.83, h: 1.5 }, { accent: C.amber });
  T.text(s, [
    { text: "Autofocus is the easy case.", options: { breakLine: true, color: C.text } },
    { text: "Tilt is not.", options: { color: C.amber, bold: true } },
  ], { x: 8.1, y: 4.9, w: 4.3, h: 1.5 }, { fontFace: FONT.head, fontSize: 23, valign: "middle" });
};
