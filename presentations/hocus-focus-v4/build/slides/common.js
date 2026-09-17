const T = require("../theme");
const { C, FONT, W, MX, asset } = T;

// Chapter divider: big numeral, title, a faint oversized chapter icon on the right, optional question pause.
function chapterSlide(pres, n, title, sub, icon, notes, pause = true) {
  const s = T.newSlide(pres, { hero: true, notes });
  s.addShape("ellipse", { x: 8.95, y: 1.85, w: 3.7, h: 3.7, fill: { color: C.purple, transparency: 82 }, line: { color: C.purple, width: 1.25 } });
  s.addImage({ path: asset(`icon-${icon}.png`), x: 9.9, y: 2.8, w: 1.8, h: 1.8, transparency: 25 });
  T.text(s, `0${n}`, { x: MX, y: 1.7, w: 4, h: 1.6 }, { fontFace: FONT.light, fontSize: 110, color: C.purple, valign: "middle" });
  T.text(s, title, { x: MX, y: 3.35, w: 8.0, h: 1.0 }, { fontFace: FONT.head, fontSize: 44, valign: "middle" });
  T.text(s, sub, { x: MX, y: 4.45, w: 8.15, h: 0.5 }, { fontSize: 18, color: C.muted, valign: "middle" });
  if (pause) T.text(s, "Pause: questions so far?", { x: MX, y: 5.75, w: 6, h: 0.45 }, { fontSize: 18, color: C.lav, valign: "middle" });
  return s;
}
module.exports = { chapterSlide };
