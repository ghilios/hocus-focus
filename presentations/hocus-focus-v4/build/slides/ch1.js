const T = require("../theme");
const { C, FONT, W, H, MX, TOP, BOTTOM, DOCS, asset } = T;

module.exports = function ch1(pres) {
  // 5 — The #1 complaint
  let s = T.newSlide(pres, {
    chapter: 1, title: "“There are too many settings”",
    notes: `This is the number-one complaint about Hocus Focus, and it's fair. This is the advanced Star Detector list — it doesn't even fit on one screen.
      Two pieces of good news. First: every one of these is now fully documented — what it does, when to change it, and what it costs you. The QR code goes straight there.
      Don't try to read the list. We'll come back to this wall of settings later, and it will make a lot more sense.
      Docs: ${DOCS.settings}`,
  });
  const wall = T.card(s, asset("settings-wall.png"), { x: MX, y: TOP, w: 4.9, h: BOTTOM - TOP + 0.25, alignX: "left", alignY: "top" });
  const bottom = wall.y + wall.h;
  T.text(s, "40+", { x: 6.1, y: TOP, w: 3.0, h: 1.3 }, { fontFace: FONT.head, fontSize: 84, color: C.pink, valign: "middle" });
  T.text(s, "star detection settings in Advanced mode. The #1 complaint from the community.", { x: 9.0, y: TOP, w: 3.63, h: 1.3 }, { fontSize: 18, color: C.muted, valign: "middle" });
  const py = 3.4;
  T.panel(s, { x: 6.1, y: py, w: 6.53, h: bottom - py });
  T.kicker(s, "GOOD NEWS #1", { x: 6.45, y: py + 0.25, w: 4 });
  T.text(s, "Every setting is now fully documented", { x: 6.45, y: py + 0.65, w: 3.9, h: 1.0 }, { fontFace: FONT.head, fontSize: 24, valign: "top" });
  T.text(s, "What it does, when to touch it, and what it trades off.", { x: 6.45, y: py + 1.7, w: 3.9, h: 0.7 }, { fontSize: 16, color: C.muted });
  T.text(s, [{ text: "ghilios.github.io/hocus-focus/settings", options: { hyperlink: { url: DOCS.settings } } }], { x: 6.45, y: bottom - 0.7, w: 4.0, h: 0.4 }, { fontSize: 15, color: C.text });
  s.addShape("roundRect", { x: 10.6, y: py + 0.35, w: 1.75, h: 1.75, rectRadius: 0.08, fill: { color: C.white } });
  s.addImage({ path: asset("qr-settings.png"), x: 10.68, y: py + 0.43, w: 1.59, h: 1.59 });
  T.text(s, "scan for the docs", { x: 10.45, y: py + 2.18, w: 2.05, h: 0.32 }, { fontSize: 14, color: C.muted, align: "center" });

  // 6 — Good news #2
  s = T.newSlide(pres, {
    chapter: 1, title: "Good news #2: you don't have to set them",
    docs: DOCS.optimization,
    notes: `The second piece of good news is the Star Detection Optimization Wizard. One button on the Star Detector options page.
      You point it at a saved autofocus run — a folder of frames you already captured — or have it capture a fresh sweep live. It re-runs star detection on those frames with many combinations of settings and keeps the combination that best pins down focus for your telescope, camera and sky.
      Options on this page: recover out-of-focus donut stars (for reflectors and SCTs), and optimize for aberration inspection — remember that one, it matters later. With per-filter star detection enabled you can tune each filter separately.
      Docs: ${DOCS.optimization}`,
  });
  // Height is capped so the "remember these two switches" panel below always has room. The box used to
  // be the full TOP..BOTTOM span, which only worked because the old doc crop was wide (2.2:1) and so
  // fitted short. A capture that matches the box aspect fills it, the panel's derived height goes
  // negative, and the deck stops opening in PowerPoint -- see assertBox in theme.js.
  const P6H = 1.25, c6 = T.shot(s, "optimizer-start.png", { x: MX, y: TOP, w: 7.0, h: BOTTOM - TOP - P6H - 0.3, alignX: "left", alignY: "top" }, { fallback: "doc-optimizer-start.png" });
  T.kicker(s, "THE OPTIMIZATION WIZARD", { x: 8.1, y: c6.y, w: 4.5 });
  T.bullets(s, [
    [{ text: "Point it at a saved autofocus run", bold: true }, ", or let it capture one live"],
    "It re-runs star detection on those frames with many combinations of settings",
    [{ text: "It keeps what best pins down focus", bold: true }, " for your rig and your sky"],
  ], { x: 8.1, y: c6.y + 0.4, w: 4.53, h: c6.h - 0.4 }, { size: 19 });
  const p6 = BOTTOM - P6H, h6 = P6H, half6 = (W - 2 * MX - 0.7) / 2;
  T.panel(s, { x: MX, y: p6, w: W - 2 * MX, h: h6 });
  T.kicker(s, "REMEMBER THESE TWO SWITCHES", { x: MX + 0.35, y: p6 + 0.1, w: 6 });
  ["Recover out-of-focus donut stars", "Optimize for aberration inspection"].forEach((t, i) =>
    T.text(s, t, { x: MX + 0.35 + i * half6, y: p6 + 0.42, w: half6 - 0.2, h: h6 - 0.5 }, { fontSize: 17, bold: true, valign: "middle" }));

  // 7 — Teaser (statement slide: no breadcrumb)
  s = T.newSlide(pres, {
    hero: true,
    notes: `But that raises the obvious question: if a wizard can pick them, why do all those settings exist in the first place?
      I'm going to hold the full answer until later, but here's the hint: it comes down to something every one of you already obsesses over when you plan an imaging night — signal-to-noise ratio. SNR.
      Hold that thought. When this amber tag comes back, we're paying it off.`,
  });
  T.text(s, "So why are there so many settings?", { x: 1.2, y: 1.5, w: W - 2.4, h: 1.2 }, { fontFace: FONT.head, fontSize: 46, align: "center", valign: "middle" });
  T.text(s, "It comes down to something everyone here already obsesses over…", { x: 1.2, y: 2.75, w: W - 2.4, h: 0.6 }, { fontSize: 22, color: C.muted, align: "center", valign: "middle" });
  T.snrTag(s, W / 2 - 1.875, 3.75, 2.5);
  T.text(s, "Hold that thought.", { x: 1.2, y: 5.75, w: W - 2.4, h: 0.6 }, { fontSize: 24, color: C.text, align: "center", valign: "middle" });

  // 8 — How it works
  s = T.newSlide(pres, {
    chapter: 1, title: "It tries a few hundred combinations for you",
    docs: DOCS.optimization,
    notes: `Conceptually it's a loop. Take the saved autofocus frames. Pick a candidate set of detection settings. Re-detect stars in every frame. Fit the focus curve. Score how good that result is. Then move to the next candidate — a few hundred times.
      The search is methodical rather than random. The picture shows the idea with just two settings: brighter means a better score, the grey squares are a coarse first pass, and the white path is the search walking uphill and taking smaller steps as it closes in. Same input, same answer, every time.
      And it has a safety rail: it will never hand you back settings that score worse than what you already have. Worst case it says "no change".
      Backup detail if asked: about a dozen settings are searched (around 20 with donut recovery on), budget of roughly 250–400 evaluations, and results are cached so repeat visits are free. Optional: you can label missed or bogus stars and re-optimize with that feedback.`,
  });
  const steps = ["Saved autofocus frames", "Try a set of settings", "Re-detect stars in every frame", "Fit the focus curve", "Score it"];
  const bw = 2.05, bg = (W - 2 * MX - 5 * bw) / 4, by = 1.9, bh = 0.95;
  steps.forEach((t, i) => {
    const x = MX + i * (bw + bg);
    T.panel(s, { x, y: by, w: bw, h: bh }, i === 4 ? { accent: C.purple } : {});
    T.text(s, t, { x: x + 0.12, y: by, w: bw - 0.24, h: bh }, { fontSize: 16, align: "center", valign: "middle", bold: i === 4 });
    if (i < 4) T.arrowRight(s, x + bw + 0.06, by + bh / 2, bg - 0.12);
  });
  const xEnd = MX + 4 * (bw + bg) + bw / 2, xStart = MX + 1 * (bw + bg) + bw / 2, yLoop = 3.25;
  s.addShape("line", { x: xEnd, y: by + bh, w: 0, h: yLoop - by - bh, line: { color: C.purple, width: 2.25 } });
  s.addShape("line", { x: xStart, y: yLoop, w: xEnd - xStart, h: 0, line: { color: C.purple, width: 2.25 } });
  s.addShape("line", { x: xStart, y: by + bh + 0.04, w: 0, h: yLoop - by - bh - 0.04, line: { color: C.purple, width: 2.25, beginArrowType: "triangle" } });
  const lx = (xStart + xEnd) / 2 - 1.75;
  s.addShape("roundRect", { x: lx, y: yLoop - 0.22, w: 3.5, h: 0.44, rectRadius: 0.08, fill: { color: C.purple } });
  T.text(s, "repeat a few hundred times", { x: lx, y: yLoop - 0.22, w: 3.5, h: 0.44 }, { fontSize: 15, bold: true, align: "center", valign: "middle", color: C.white });
  const a8 = T.art(s, asset("art-search.png"), { x: MX, y: 3.7, w: 3.8, h: BOTTOM - 3.7, alignX: "left", alignY: "top" });
  const x8 = a8.x + a8.w + 0.5;
  T.bullets(s, [
    [{ text: "A methodical search.", bold: true }, " No randomness, so the same frames give the same answer"],
    "Each candidate is judged by how precisely the frames pin down best focus",
    [{ text: "It never hands back anything worse", bold: true }, " than what you have now"],
  ], { x: x8, y: 3.7, w: W - MX - x8, h: BOTTOM - 3.7 }, { size: 18, gap: 9, note: "Brighter = better score  ·  dots = a coarse first pass  ·  white path = the search" });

  // 9 — What you get back
  s = T.newSlide(pres, {
    chapter: 1, title: "You get a tighter curve, and advice",
    docs: DOCS.optimization,
    notes: `When it finishes you get a summary. You can flip between the focus curve with your current settings and the optimized one, see the focus precision, and see stars per frame at every focuser position.
      It also recommends an autofocus step size — and, when the frames are starved of signal, a longer exposure.
      From here: Accept applies it. Review frames lets you look at the detections and label mistakes. Continue optimizing runs another pass.
      Docs: ${DOCS.optimization}`,
  });
  const c9 = T.shot(s, "optimizer-summary.png", { x: MX, y: TOP + 0.25, w: 7.3, h: BOTTOM - TOP - 0.25, alignX: "left", alignY: "top" }, { fallback: "doc-optimizer-summary.png" });
  const r9 = c9.y + (c9.h - 3.75) / 2;
  T.iconRow(s, "curve", "A tighter focus curve", "Before and after, with the focus precision for each", 8.35, r9 + 0.1, 4.28);
  // Not "more stars": on real runs the default (focus) objective usually keeps FEWER, better-measured
  // stars -- 111 -> 81 at best focus on the captured example -- because it drops marginal detections that
  // add noise to every HFR. More stars is what the "optimize for aberration inspection" switch buys
  // (same run: 111 -> 123), which is slide 26's material, not this slide's.
  T.iconRow(s, "stars", "Stars per frame, position by position", "Counted at every focuser position, not just at best focus", 8.35, r9 + 1.4, 4.28);
  T.iconRow(s, "step", "Autofocus advice", "A step size that suits your curve, and a longer exposure when the frames need it", 8.35, r9 + 2.7, 4.28);

  // 10 — The catch
  s = T.newSlide(pres, {
    chapter: 1, title: "It's a lot of computing",
    docs: DOCS.settings,
    notes: `The honest catch: this is heavy. Every candidate means re-detecting stars in every frame of the run, a few hundred times over. On a typical imaging mini-PC, plan on 20 to 30 minutes — longer for big sensors.
      Three things help. One: you don't have to run it at the telescope. Copy a saved autofocus folder to your desktop and optimize there. Two: Export and Import move the optimized settings back to the imaging PC as a single file; import shows you exactly what will change before anything is applied. Three: there's now GPU acceleration — one toggle on the wizard start page.
      Docs (export/import section is on the settings page): ${DOCS.settings}`,
  });
  T.panel(s, { x: MX, y: TOP, w: 6.6, h: 1.45 });
  T.text(s, "20–30 min", { x: MX + 0.3, y: TOP, w: 3.4, h: 1.45 }, { fontFace: FONT.head, fontSize: 46, color: C.pink, valign: "middle" });
  T.text(s, "on a typical imaging PC. Every frame is re-detected a few hundred times.", { x: MX + 3.6, y: TOP, w: 2.85, h: 1.45 }, { fontSize: 15, color: C.muted, valign: "middle" });
  const rowOpt = { d: 0.66, headSize: 19, subSize: 15, subH: 0.4 };
  T.iconRow(s, "desktop", "Run it on another computer", "Copy a saved autofocus folder to your desktop PC", MX, 3.6, 6.6, rowOpt);
  T.iconRow(s, "export", "Export, then import", "Carry the optimized settings back as a single file", MX, 4.58, 6.6, rowOpt);
  T.iconRow(s, "gpu", "GPU acceleration", "New: one toggle on the wizard's start page", MX, 5.56, 6.6, rowOpt);
  T.shot(s, "settings-import.png", { x: 7.75, y: TOP, w: 4.88, h: 3.66 }, { label: "Import confirmation dialog (Setting / Current / Imported)" });
  T.text(s, "Import shows exactly what will change before anything is applied", { x: 7.75, y: TOP + 3.78, w: 4.88, h: 0.7 }, { fontSize: 15, color: C.muted, align: "center" });
};
