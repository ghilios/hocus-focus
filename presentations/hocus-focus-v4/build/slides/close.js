const T = require("../theme");
const { C, FONT, W, H, MX, TOP, COLW, colX, DOCS, asset } = T;

module.exports = function close(pres) {
  // 34 — The story in one slide
  let s = T.newSlide(pres, {
    title: "Why every star matters",
    notes: `To wrap up. The complaint was "too many settings". The optimizer means you don't have to set them. But they exist for a reason, and the reason is signal-to-noise: a sensor model needs a clean measurement of every individual star, across the whole frame, in and out of focus.
      Get that right, and you can calibrate your tilt adapter — and once it's calibrated, Hocus Focus tells you exactly how far to turn each screw, separately for tilt and backfocus. With an ASG EAT, it turns them for you.
      Everything tonight is in the documentation — scan the code or go to ${DOCS.home}. There's a quick start, a page per feature, and the simulator walkthrough if you want to try this indoors first.`,
  });
  [["01  FEWER DECISIONS", "Too many settings? The optimizer sets them for you."],
   ["02  MORE STARS", "Donuts, adaptive thresholds, better fits, frame review."],
   ["03  FLAT SENSORS", "Calibrate the adapter, then guided or automatic correction."]].forEach(([k, t], i) => {
    const x = colX(i);
    T.panel(s, { x, y: TOP + 0.1, w: COLW, h: 1.35 });
    T.kicker(s, k, { x: x + 0.3, y: TOP + 0.22, w: COLW - 0.6 });
    T.text(s, t, { x: x + 0.3, y: TOP + 0.6, w: COLW - 0.6, h: 0.75 }, { fontSize: 16, valign: "top" });
  });
  s.addShape("roundRect", { x: MX, y: 3.55, w: W - 2 * MX, h: 0.7, rectRadius: 0.1, fill: { color: C.amber } });
  T.text(s, [{ text: "SNR   ", options: { bold: true, charSpacing: 3 } }, { text: "A sensor model needs every star measured well. That is why the settings exist." }],
    { x: MX + 0.35, y: 3.55, w: W - 2 * MX - 0.7, h: 0.7 }, { fontFace: FONT.head, fontSize: 18, color: C.ink, valign: "middle" });
  T.panel(s, { x: MX, y: 4.5, w: W - 2 * MX, h: 2.2 });
  s.addShape("roundRect", { x: MX + 0.3, y: 4.7, w: 1.8, h: 1.8, rectRadius: 0.08, fill: { color: C.white } });
  s.addImage({ path: asset("qr-home.png"), x: MX + 0.38, y: 4.78, w: 1.64, h: 1.64 });
  T.kicker(s, "IT'S ALL DOCUMENTED", { x: 3.2, y: 4.75, w: 6 });
  T.text(s, [{ text: "ghilios.github.io/hocus-focus", options: { hyperlink: { url: DOCS.home } } }], { x: 3.2, y: 5.1, w: 9, h: 0.7 }, { fontFace: FONT.head, fontSize: 32, valign: "middle" });
  T.text(s, "Quick start  ·  every setting  ·  the optimizer  ·  tilt adapter wizard  ·  motorized adapters  ·  simulator", { x: 3.2, y: 5.85, w: 9.2, h: 0.6 }, { fontSize: 15, color: C.muted });

  // 35 — Thank you (stays up through Q&A, so it carries the docs link too)
  s = T.newSlide(pres, {
    hero: true, number: false,
    notes: `Thank you. Credits: Steve Smith for the RANSAC star registration that makes matching stars across a focus sweep work; Josh Jones at ASG for providing the EAT; and everyone in the community who sent me saved autofocus runs — that bank of real data is what all of this was tuned and tested against.
      I'm jokogeo on the NINA Discord and ghilios on Cloudy Nights. The plugin is open source on GitHub. The docs link stays on screen. Questions?`,
  });
  T.text(s, "Thank you", { x: MX, y: 1.2, w: 6.2, h: 1.3 }, { fontFace: FONT.head, fontSize: 66, valign: "middle" });
  T.text(s, "Questions?", { x: MX, y: 2.45, w: 6.2, h: 0.8 }, { fontFace: FONT.light, fontSize: 36, color: C.lav, valign: "middle" });
  T.iconRow(s, "discord", "jokogeo", "N.I.N.A. Discord", MX, 4.0, 5.8, { d: 0.62, headSize: 19, subSize: 14, subH: 0.4 });
  T.iconRow(s, "github", "github.com/ghilios/hocus-focus", "Open source (MPL 2.0)  ·  ghilios on Cloudy Nights", MX, 5.1, 5.8, { d: 0.62, headSize: 19, subSize: 14, subH: 0.4 });
  const px = 7.1, pw = W - MX - px;
  T.panel(s, { x: px, y: 1.2, w: pw, h: 2.15 }, { accent: C.purple });
  s.addShape("roundRect", { x: px + 0.25, y: 1.4, w: 1.75, h: 1.75, rectRadius: 0.08, fill: { color: C.white } });
  s.addImage({ path: asset("qr-home.png"), x: px + 0.33, y: 1.48, w: 1.59, h: 1.59 });
  T.kicker(s, "DOCS AND QUICK START", { x: px + 2.3, y: 1.6, w: 3.2 });
  T.text(s, [{ text: "ghilios.github.io/", options: { breakLine: true, hyperlink: { url: DOCS.home } } }, { text: "hocus-focus", options: { hyperlink: { url: DOCS.home } } }], { x: px + 2.3, y: 1.95, w: pw - 2.5, h: 1.1 }, { fontFace: FONT.head, fontSize: 24, valign: "middle" });
  T.panel(s, { x: px, y: 3.6, w: pw, h: 3.1 });
  T.kicker(s, "WITH THANKS TO", { x: px + 0.35, y: 3.75, w: 4.5 });
  [["Steve Smith", "for the RANSAC star registration"],
   ["Josh Jones, ASG", "for the EAT that made the automation possible"],
   ["The community", "for saved autofocus runs, bug reports, and patience"]].forEach(([who, what], i) => {
    const y = 4.15 + i * 0.83;
    T.text(s, who, { x: px + 0.35, y, w: pw - 0.7, h: 0.42 }, { fontFace: FONT.head, fontSize: 19, valign: "middle" });
    T.text(s, what, { x: px + 0.35, y: y + 0.4, w: pw - 0.7, h: 0.36 }, { fontSize: 14, color: C.muted, valign: "middle" });
  });
};
