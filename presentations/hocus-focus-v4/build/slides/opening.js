const T = require("../theme");
const { C, FONT, W, H, MX, TOP, BOTTOM, COLW, colX, DOCS, asset } = T;
const { chapterSlide } = require("./common");

module.exports = function opening(pres) {
  // 1 — Title (the amber tagline deliberately seeds the SNR thread; nothing else here is amber)
  let s = T.newSlide(pres, {
    hero: true, number: false,
    notes: `Welcome back. In 2023 I gave a talk here called "The Science of Star Measurement" — how Hocus Focus detects and measures stars, fits focus curves, and models your sensor's tilt and backfocus.
      Tonight is the sequel: what's new in Hocus Focus v4. The title is the spoiler — "every star matters" — and by the end you'll see why that one idea explains most of what changed.
      Please interrupt with questions; I've also left natural pauses between the three chapters.`,
  });
  T.kicker(s, "THE ASTRO IMAGING CHANNEL", { x: MX, y: 1.5, w: 7 }, C.lav);
  T.text(s, "Hocus Focus v4", { x: MX, y: 2.0, w: 8, h: 1.2 }, { fontFace: FONT.head, fontSize: 66, valign: "middle" });
  T.text(s, "Every star matters", { x: MX, y: 3.15, w: 8, h: 0.9 }, { fontFace: FONT.light, fontSize: 42, color: C.amber, valign: "middle" });
  T.text(s, "Star detection optimization, tilt adapter calibration, and guided tilt correction for N.I.N.A.",
    { x: MX, y: 4.25, w: 6.6, h: 0.9 }, { fontSize: 19, color: C.muted });
  T.text(s, "George Hilios", { x: MX, y: 5.7, w: 6, h: 0.45 }, { fontFace: FONT.head, fontSize: 24 });
  T.text(s, "jokogeo on Discord  ·  ghilios on Cloudy Nights", { x: MX, y: 6.2, w: 7, h: 0.4 }, { fontSize: 16, color: C.muted });
  s.addShape("ellipse", { x: 8.55, y: 1.65, w: 4.2, h: 4.2, fill: { color: C.purple, transparency: 55 }, line: { color: C.purple, width: 1.5 } });
  s.addImage({ path: asset("logo.jpg"), x: 8.75, y: 1.85, w: 3.8, h: 3.8, rounding: true });

  // 2 — Where we left off
  s = T.newSlide(pres, {
    title: "Where we left off",
    notes: `This is the actual last content slide from the 2023 talk — my "future work" list.
      Three of those bullets were about tilt: tell people which screws to turn, work out where the screws are, and — with a question mark — motorized tilt adjusters.
      All three shipped in v4, and they're the back half of tonight's talk.
      If you missed 2023: that talk covered how stars are detected and measured (HFR, FWHM/PSF fitting), how the hyperbolic focus curve is fit, and how the sensor model separates tilt from backfocus. The recording is on TAIC's channel. I'll do a 60-second recap of the sensor model when we get there.`,
  });
  const o = T.card(s, asset("future-work-2023.png"), { x: MX, y: TOP + 0.25, w: 7.9, h: 4.3, alignX: "left", alignY: "top" }, { caption: "The last slide of the 2023 talk" });
  const rx = 9.0, rw = W - MX - rx;
  T.kicker(s, "THE 2023 TALK", { x: rx, y: o.y + 0.1, w: rw });
  T.text(s, "How stars are detected and measured, how focus curves are fit, and how a sensor model separates tilt from backfocus.",
    { x: rx, y: o.y + 0.5, w: rw, h: 1.55 }, { fontSize: 16, color: C.muted });
  T.text(s, [{ text: "Three promises from 2023.", options: { breakLine: true } }, { text: "All three shipped in v4.", options: { color: C.green } }],
    { x: rx, y: o.y + 2.1, w: rw, h: 1.5 }, { fontFace: FONT.head, fontSize: 24, valign: "middle" });

  // 3 — What's new in v4
  s = T.newSlide(pres, {
    title: "What's new in v4",
    notes: `Three chapters.
      One — fewer decisions: the Star Detection Optimization Wizard, which answers the number-one complaint I hear: too many settings.
      Two — more stars: donut detection, locally adaptive binarization, better hyperbolic fits, and a way to review every frame of an autofocus run.
      Three — flat sensors: the Tilt Adapter Calibration Wizard, guided and fully automated tilt and backfocus correction, and simulators so you can practice all of it indoors.
      I'll pause for questions between chapters.`,
  });
  const chapters = [
    { n: "01", icon: "sliders", title: "Fewer decisions", items: ["The Optimization Wizard", "Export and import", "Every setting documented"] },
    { n: "02", icon: "star", title: "More stars", items: ["Donut detection", "Adaptive binarization", "Better fits, frame review"] },
    { n: "03", icon: "plane", title: "Flat sensors", items: ["The Calibration Wizard", "Guided and automatic fixes", "Indoor simulators"] },
  ];
  chapters.forEach((c, i) => {
    const x = colX(i), y = 2.2, h = 3.75;
    T.panel(s, { x, y, w: COLW, h });
    s.addShape("ellipse", { x: x + 0.35, y: y + 0.35, w: 0.85, h: 0.85, fill: { color: C.purple } });
    s.addImage({ path: asset(`icon-${c.icon}.png`), x: x + 0.58, y: y + 0.58, w: 0.39, h: 0.39 });
    T.text(s, c.n, { x: x + COLW - 1.3, y: y + 0.35, w: 0.95, h: 0.85 }, { fontFace: FONT.light, fontSize: 40, color: C.muted, align: "right", valign: "middle" });
    T.text(s, c.title, { x: x + 0.35, y: y + 1.4, w: COLW - 0.7, h: 0.55 }, { fontFace: FONT.head, fontSize: 26, valign: "middle" });
    T.bullets(s, c.items, { x: x + 0.35, y: y + 2.1, w: COLW - 0.6, h: 1.45 }, { size: 17, gap: 9, color: C.muted, valign: "top" });
  });

  // 4 — Chapter 01 divider
  chapterSlide(pres, 1, "Fewer decisions", "The Star Detection Optimization Wizard, and why the settings exist at all", "sliders",
    `Chapter one: the optimizer. I'll start with the complaint it answers.`, false);
};
