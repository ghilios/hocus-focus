// Design system for the Hocus Focus v4 deck: palette, type, and layout helpers.
const fs = require("fs");
const path = require("path");

// Color roles: purple = default accent · pink = something YOU do / emphasis · amber = the SNR thread ONLY
// (plus the title tagline that seeds it) · green = check marks only · lav = small labels.
const C = {
  bg: "0B1026", panel: "151B3A", panelHi: "1E2650", line: "3A4380",
  text: "F2F0FF", muted: "B4BBE0", dim: "8088B8",
  purple: "8B5CF6", lav: "C4B5FD", pink: "FF4D9D", amber: "FFB020", green: "34D399", white: "FFFFFF", ink: "0B1026",
};
const FONT = { head: "Segoe UI Semibold", body: "Segoe UI", light: "Segoe UI Light" };
const W = 13.333, H = 7.5, MX = 0.7;
const TOP = 1.85, BOTTOM = 6.4;          // content zone; the docs chip lives below BOTTOM
const COLW = 3.75, COLGAP = (W - 2 * MX - 3 * COLW) / 2;   // standard 3-column grid
const colX = (i) => MX + i * (COLW + COLGAP);

const BASE = "https://ghilios.github.io/hocus-focus/";
const DOCS = {
  home: BASE,
  settings: BASE + "settings/",
  optimization: BASE + "optimization/",
  donut: BASE + "settings/donut-aware/",
  adaptive: BASE + "settings/adaptive-binarization/",
  hyperbola: BASE + "overview/hyperbola-fitting/",
  autofocus: BASE + "overview/autofocus/",
  sensorModel: BASE + "overview/sensor-model/",
  wizard: BASE + "overview/tilt-adapter-wizard/",
  inspector: BASE + "overview/tilt-aberration-inspector/",
  motorized: BASE + "overview/motorized-tilt-adapter/",
  simulator: BASE + "overview/camera-simulator/",
};

const ASSETS = path.resolve(__dirname, "../assets");
const SHOTS = path.resolve(__dirname, "../screenshots");
const CHAPTERS = ["FEWER DECISIONS", "MORE STARS", "FLAT SENSORS"];

const state = { dims: {}, missing: [], drafts: [] };
const asset = (f) => path.join(ASSETS, f);
const shadow = () => ({ type: "outer", color: "000000", opacity: 0.45, blur: 14, offset: 4, angle: 90 });

function newSlide(pres, { chapter = 0, title, hero = false, snr = false, docs, notes, number = true } = {}) {
  const s = pres.addSlide();
  s.background = { path: asset(hero ? "bg-hero.png" : "bg-content.png") };
  if (chapter) {
    const runs = [];
    CHAPTERS.forEach((c, i) => {
      const on = i + 1 === chapter;
      runs.push({ text: `0${i + 1}  ${c}`, options: { color: on ? C.text : C.dim, bold: on } });
      if (i < 2) runs.push({ text: "      ", options: { color: C.dim } });
    });
    s.addText(runs, { x: MX, y: 0.3, w: 9, h: 0.32, fontFace: FONT.body, fontSize: 11, charSpacing: 2, margin: 0, isTextBox: true });
  }
  if (title) {
    s.addText(title, { x: MX, y: 0.68, w: snr ? 10.2 : W - 2 * MX, h: 0.95, fontFace: FONT.head, fontSize: 32, color: C.text, margin: 0, valign: "middle", isTextBox: true, fit: "shrink" });
  }
  if (snr) snrTag(s, W - MX - 1.5, 0.84, 1);
  if (docs) docsChip(s, docs);
  if (number) s.slideNumber = { x: W - 1.2, y: H - 0.62, w: 0.5, h: 0.32, fontFace: FONT.body, fontSize: 12, color: C.muted, align: "right" };
  if (notes) s.addNotes(notes.trim().replace(/\n[ \t]+/g, "\n"));
  return s;
}

// The one big filled pill in the deck: the SNR narrative token.
function snrTag(s, x, y, scale = 1) {
  const w = 1.5 * scale, h = 0.62 * scale;
  s.addShape("roundRect", { x, y, w, h, rectRadius: h / 2, fill: { color: C.amber }, shadow: shadow() });
  s.addText("SNR", { x, y, w, h, align: "center", valign: "middle", fontFace: FONT.head, fontSize: 20 * scale, bold: true, color: C.ink, charSpacing: 3, margin: 0, isTextBox: true });
}

function docsChip(s, url) {
  const label = url.replace("https://", "").replace(/\/$/, "");
  const size = 13;   // one size deck-wide; only the width varies
  const w = 0.75 + label.length * 0.087, y = H - 0.82, h = 0.46;
  s.addShape("roundRect", { x: MX, y, w, h, rectRadius: 0.08, fill: { color: C.panelHi }, line: { color: C.line, width: 0.75 } });
  s.addImage({ path: asset("icon-book.png"), x: MX + 0.18, y: y + 0.12, w: 0.22, h: 0.22 });
  s.addText([{ text: label, options: { hyperlink: { url } } }], { x: MX + 0.52, y, w: w - 0.6, h, valign: "middle", fontFace: FONT.body, fontSize: size, color: C.text, margin: 0, isTextBox: true });
}

// Fit an image of known pixel size inside a box, preserving aspect ratio.
function fit(file, box) {
  const d = state.dims[file];
  if (!d) throw new Error("no dimensions for " + file);
  const ar = d.width / d.height;
  let w = box.w, h = box.w / ar;
  if (h > box.h) { h = box.h; w = box.h * ar; }
  const ax = box.alignX === "left" ? 0 : box.alignX === "right" ? 1 : 0.5;
  const ay = box.alignY === "top" ? 0 : box.alignY === "bottom" ? 1 : 0.5;
  return { x: box.x + (box.w - w) * ax, y: box.y + (box.h - h) * ay, w, h };
}

// Light-background screenshots sit in white rounded "evidence cards". Returns the OUTER card rect.
function card(s, file, box, { pad = 0.12, caption, fillBox = false } = {}) {
  const inner = { ...box, x: box.x + pad, y: box.y + pad, w: box.w - 2 * pad, h: box.h - 2 * pad - (caption ? 0.45 : 0) };
  const r = fit(file, inner);
  const o = fillBox ? { x: box.x, y: box.y, w: box.w, h: box.h - (caption ? 0.45 : 0) } : { x: r.x - pad, y: r.y - pad, w: r.w + 2 * pad, h: r.h + 2 * pad };
  s.addShape("roundRect", { ...o, rectRadius: 0.1, fill: { color: C.white }, shadow: shadow() });
  s.addImage({ path: file, ...r });
  if (caption) s.addText(caption, { x: o.x, y: o.y + o.h + 0.1, w: o.w, h: 0.35, fontFace: FONT.body, fontSize: 14, color: C.muted, align: "center", margin: 0, isTextBox: true });
  return o;
}

// Transparent art (no card).
function art(s, file, box) { const r = fit(file, box); s.addImage({ path: file, ...r }); return r; }

// A screenshot George supplies. Falls back to a doc image (flagged DRAFT) or a dashed placeholder.
function shot(s, name, box, { fallback, label, fillBox = false } = {}) {
  const real = path.join(SHOTS, name);
  if (fs.existsSync(real)) return card(s, real, box, { fillBox });
  if (fallback) {
    state.drafts.push(name);
    const o = card(s, asset(fallback), box, { fillBox });
    const tw = 2.7;
    s.addShape("roundRect", { x: o.x + o.w - tw - 0.08, y: o.y + o.h - 0.36, w: tw, h: 0.28, rectRadius: 0.06, fill: { color: C.pink } });
    s.addText(`DRAFT IMAGE → ${name}`, { x: o.x + o.w - tw - 0.08, y: o.y + o.h - 0.36, w: tw, h: 0.28, align: "center", valign: "middle", fontFace: FONT.body, fontSize: 9, bold: true, color: C.white, margin: 0, isTextBox: true });
    return o;
  }
  state.missing.push(name);
  s.addShape("roundRect", { x: box.x, y: box.y, w: box.w, h: box.h, rectRadius: 0.12, fill: { color: C.panel, transparency: 25 }, line: { color: C.pink, width: 1.5, dashType: "dash" } });
  s.addText([
    { text: "SCREENSHOT NEEDED", options: { fontSize: 11, bold: true, color: C.pink, charSpacing: 3, breakLine: true } },
    { text: `screenshots/${name}`, options: { fontSize: 15, color: C.text, fontFace: "Consolas", breakLine: true } },
    { text: label || "", options: { fontSize: 13, color: C.muted } },
  ], { x: box.x + 0.2, y: box.y, w: box.w - 0.4, h: box.h, align: "center", valign: "middle", fontFace: FONT.body, paraSpaceAfter: 8, isTextBox: true });
  return box;
}

function bullets(s, items, box, { size = 20, gap = 14, color = C.text, valign = "middle", note } = {}) {
  const runs = [];
  items.forEach((it, i) => {
    const parts = Array.isArray(it) ? it : [it];
    parts.forEach((p, j) => {
      const o = typeof p === "string" ? { text: p } : p;
      const last = j === parts.length - 1;
      runs.push({
        text: o.text,
        options: {
          bold: !!o.bold, color: o.color || color,
          ...(j === 0 ? { bullet: { characterCode: "25CF", indent: 22 }, paraSpaceAfter: gap } : {}),
          ...(last && i < items.length - 1 ? { breakLine: true } : {}),
        },
      });
    });
  });
  if (note) {
    runs[runs.length - 1].options.breakLine = true;
    runs.push({ text: note, options: { color: C.muted, fontSize: 14, paraSpaceBefore: 6 } });
  }
  s.addText(runs, { ...box, fontFace: FONT.body, fontSize: size, valign, margin: 0, isTextBox: true });
}

// Heading + supporting line in ONE text box, so the support always flows after the heading however it wraps.
function statement(s, head, body, box, { headSize = 19, bodySize = 15, valign = "middle", align = "left" } = {}) {
  s.addText([
    { text: head, options: { fontFace: FONT.head, fontSize: headSize, color: C.text, breakLine: true, paraSpaceAfter: 6 } },
    { text: body, options: { fontFace: FONT.body, fontSize: bodySize, color: C.muted } },
  ], { ...box, valign, align, margin: 0, isTextBox: true });
}

function text(s, str, box, o = {}) {
  s.addText(str, { fontFace: FONT.body, fontSize: 18, color: C.text, margin: 0, valign: "top", isTextBox: true, ...box, ...o });
}

// Small letter-spaced label ("kicker").
function kicker(s, str, box, color = C.lav) {
  text(s, str, { h: 0.32, ...box }, { fontSize: 13, bold: true, color, charSpacing: 3, valign: "middle" });
}

// Two panel styles only: neutral, and highlighted with one accent (amber only on SNR-thread slides).
function panel(s, box, { accent } = {}) {
  s.addShape("roundRect", { ...box, rectRadius: 0.14, fill: { color: accent ? C.panelHi : C.panel, transparency: accent ? 0 : 8 }, line: { color: accent || C.line, width: accent ? 1.5 : 0.75 } });
}

function numberDot(s, n, x, y, d = 0.62, color = C.purple, fg = C.white) {
  s.addShape("ellipse", { x, y, w: d, h: d, fill: { color } });
  s.addText(String(n), { x, y, w: d, h: d, align: "center", valign: "middle", fontFace: FONT.head, fontSize: d * 26, bold: true, color: fg, margin: 0, isTextBox: true });
}

// Icon in a purple circle + heading + one supporting line.
function iconRow(s, icon, heading, sub, x, y, w, { d = 0.78, headSize = 20, subSize = 15, subH = 0.75 } = {}) {
  s.addShape("ellipse", { x, y, w: d, h: d, fill: { color: C.purple } });
  s.addImage({ path: asset(`icon-${icon}.png`), x: x + d * 0.27, y: y + d * 0.27, w: d * 0.46, h: d * 0.46 });
  s.addText(heading, { x: x + d + 0.3, y: y - 0.06, w: w - d - 0.3, h: 0.45, fontFace: FONT.head, fontSize: headSize, color: C.text, margin: 0, valign: "middle", isTextBox: true });
  if (sub) s.addText(sub, { x: x + d + 0.3, y: y + 0.4, w: w - d - 0.3, h: subH, fontFace: FONT.body, fontSize: subSize, color: C.muted, margin: 0, valign: "top", isTextBox: true });
}

// Three statements on the standard grid: a heading and one supporting line each.
function columns3(s, cols, y, { headSize = 18, bodySize = 15, numbered = false, dotColor = C.purple } = {}) {
  cols.forEach(([h, b], i) => {
    const x = colX(i), off = numbered ? 0.62 : 0;
    if (numbered) numberDot(s, i + 1, x, y + 0.02, 0.46, dotColor);
    text(s, h, { x: x + off, y, w: COLW - off, h: 0.5 }, { fontFace: FONT.head, fontSize: headSize, valign: "middle" });
    if (b) text(s, b, { x: x + off, y: y + 0.55, w: COLW - off, h: 0.9 }, { fontSize: bodySize, color: C.muted });
  });
}

function arrowRight(s, x, y, w, color = C.muted) {
  s.addShape("line", { x, y, w, h: 0, line: { color, width: 2.25, endArrowType: "triangle" } });
}

module.exports = { C, FONT, W, H, MX, TOP, BOTTOM, COLW, COLGAP, colX, DOCS, CHAPTERS, state, asset, shadow, newSlide, snrTag, docsChip, fit, card, art, shot, bullets, text, kicker, panel, numberDot, iconRow, columns3, statement, arrowRight };
