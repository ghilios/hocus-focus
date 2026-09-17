// Prepares every image the deck needs into ../assets: doc-figure copies, screenshot crops,
// starfield backgrounds, diagram art (SVG -> PNG) and QR codes. Deterministic; safe to re-run.
const fs = require("fs");
const path = require("path");
const sharp = require("sharp");
const QRCode = require("qrcode");
const { C, DOCS } = require("./theme");

const ROOT = path.resolve(__dirname, "../../..");
const OUT = path.resolve(__dirname, "../assets");
const FIG = path.join(ROOT, "documentation/docs/assets/figures");
const RAW = path.join(ROOT, "documentation/docs/assets/screenshots/raw");
const DECK2023 = path.resolve(__dirname, "../render/2023");

// Small deterministic PRNG so backgrounds and art never change between builds.
function rng(seed) {
  let s = seed >>> 0;
  return () => {
    s = (s + 0x6d2b79f5) >>> 0;
    let t = s;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

async function svgToPng(svg, file, width, trim = false) {
  let img = sharp(Buffer.from(svg), { density: 192 }).resize({ width });
  if (trim) img = sharp(await img.png().toBuffer()).trim();
  await img.png().toFile(path.join(OUT, file));
}

// ---------- backgrounds ----------
function starfieldSvg({ w, h, count, seed, glow }) {
  const r = rng(seed);
  let stars = "";
  for (let i = 0; i < count; i++) {
    const x = r() * w, y = r() * h, m = r();
    const rad = m > 0.985 ? 2.6 : m > 0.93 ? 1.7 : m > 0.6 ? 1.0 : 0.7;
    const op = (0.18 + r() * 0.5) * (glow ? 1 : 0.6);
    const tint = r() > 0.8 ? "#C9B8FF" : r() > 0.6 ? "#BFD4FF" : "#FFFFFF";
    stars += `<circle cx="${x.toFixed(1)}" cy="${y.toFixed(1)}" r="${rad}" fill="${tint}" opacity="${op.toFixed(2)}"/>`;
    if (rad > 2) stars += `<circle cx="${x.toFixed(1)}" cy="${y.toFixed(1)}" r="7" fill="${tint}" opacity="0.08"/>`;
  }
  const nebula = glow
    ? `<ellipse cx="${w * 0.82}" cy="${h * 0.2}" rx="${w * 0.45}" ry="${h * 0.55}" fill="url(#neb)"/>
       <ellipse cx="${w * 0.1}" cy="${h * 0.95}" rx="${w * 0.4}" ry="${h * 0.45}" fill="url(#neb2)"/>`
    : `<ellipse cx="${w * 0.9}" cy="${h * 0.05}" rx="${w * 0.4}" ry="${h * 0.45}" fill="url(#neb)" opacity="0.55"/>`;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">
    <defs>
      <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
        <stop offset="0" stop-color="#0B1026"/><stop offset="1" stop-color="#070A1A"/>
      </linearGradient>
      <radialGradient id="neb"><stop offset="0" stop-color="#6D3FE0" stop-opacity="0.34"/><stop offset="1" stop-color="#6D3FE0" stop-opacity="0"/></radialGradient>
      <radialGradient id="neb2"><stop offset="0" stop-color="#FF4D9D" stop-opacity="0.13"/><stop offset="1" stop-color="#FF4D9D" stop-opacity="0"/></radialGradient>
    </defs>
    <rect width="${w}" height="${h}" fill="url(#bg)"/>${nebula}${stars}</svg>`;
}

// ---------- diagram art (transparent background, labels stay native in PowerPoint) ----------
const hyp = (x, x0, a, b, y0) => (a / b) * Math.sqrt((x - x0) ** 2 + b * b) + y0;

function vcurveSvgPart({ ox, oy, w, h, x0 = 0.5, n = 7, color = "#8B5CF6", pt = "#F2F0FF", seed = 1, noise = 0.03, axis = true, mark = "#FF4D9D", ptR = 5, lw = 3.5 }) {
  const r = rng(seed);
  const f = (t) => hyp(t, x0, 0.62, 0.16, 0.0);
  const fmax = Math.max(f(0), f(1));
  const X = (t) => ox + t * w, Y = (v) => oy + h - 8 - (v / fmax) * (h - 24);
  let d = "";
  for (let i = 0; i <= 60; i++) { const t = i / 60; d += `${i ? "L" : "M"}${X(t).toFixed(1)},${Y(f(t)).toFixed(1)}`; }
  let s = "";
  if (axis) s += `<path d="M${ox},${oy} V${oy + h} H${ox + w}" fill="none" stroke="#5B6399" stroke-width="2"/>`;
  s += `<path d="${d}" fill="none" stroke="${color}" stroke-width="${lw}" stroke-linecap="round"/>`;
  for (let i = 0; i < n; i++) {
    const t = 0.04 + (0.92 * i) / (n - 1);
    const v = f(t) + (r() - 0.5) * 2 * noise * fmax;
    s += `<circle cx="${X(t).toFixed(1)}" cy="${Y(Math.max(v, 0.01)).toFixed(1)}" r="${ptR}" fill="${pt}"/>`;
  }
  if (mark) s += `<path d="M${X(x0)},${oy + h - 2} V${Y(f(x0)) + 6}" stroke="${mark}" stroke-width="3" stroke-dasharray="5 5"/>`;
  return s;
}

function artMatchSvg() {
  // Filmstrip: the same star field at three focuser positions; one star traced through every frame.
  const W = 900, H = 640, fw = 272, fh = 400, gap = 28, top = 120;
  const stars = [[0.22, 0.2], [0.7, 0.14], [0.82, 0.55], [0.3, 0.8], [0.52, 0.45], [0.14, 0.52], [0.75, 0.86]];
  const frames = [{ ring: 17 }, { ring: 0 }, { ring: 12 }];
  let s = "";
  const track = [];
  frames.forEach((f, fi) => {
    const ox = 14 + fi * (fw + gap);
    s += `<rect x="${ox}" y="${top}" width="${fw}" height="${fh}" rx="14" fill="#10173A" stroke="#3A4380" stroke-width="2.5"/>`;
    stars.forEach(([u, v], i) => {
      const cx = ox + u * fw, cy = top + v * fh, hi = i === 4;
      const col = hi ? "#FFB020" : "#F2F0FF";
      if (f.ring) {
        s += `<circle cx="${cx}" cy="${cy}" r="${f.ring}" fill="none" stroke="${col}" stroke-width="${f.ring * 0.5}" opacity="${hi ? 1 : 0.62}"/>`;
      } else {
        s += `<circle cx="${cx}" cy="${cy}" r="13" fill="${col}" opacity="0.22"/><circle cx="${cx}" cy="${cy}" r="5.5" fill="${col}"/>`;
      }
      if (hi) track.push([cx, cy]);
    });
  });
  const d = track.map((p, i) => `${i ? "L" : "M"}${p[0]},${p[1]}`).join("");
  s = `<path d="${d}" fill="none" stroke="#FFB020" stroke-width="3.5" stroke-dasharray="9 8" opacity="0.9"/>` + s.replace(/^/, "");
  // draw the track on top of the frames but under nothing else
  s = s.replace(/^<path[^>]*\/>/, "") + `<path d="${d}" fill="none" stroke="#FFB020" stroke-width="3.5" stroke-dasharray="9 8" opacity="0.9"/>`;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">${s}</svg>`;
}

function artCurvesSvg() {
  // A grid of per-star V-curves, each bottoming out at a slightly different focuser position.
  const W = 900, H = 640, cols = 3, rows = 2, cw = 250, ch = 250, gx = 45, gy = 50;
  const x0s = [0.4, 0.47, 0.55, 0.44, 0.52, 0.6];
  let s = "";
  for (let i = 0; i < 6; i++) {
    const c = i % cols, r = Math.floor(i / cols), hi = i === 1;
    const ox = 30 + c * (cw + gx), oy = 30 + r * (ch + gy);
    s += `<rect x="${ox - 14}" y="${oy - 14}" width="${cw + 28}" height="${ch + 28}" rx="14" fill="#10173A" stroke="${hi ? "#FFB020" : "#3A4380"}" stroke-width="${hi ? 3.5 : 2}"/>`;
    s += vcurveSvgPart({ ox, oy, w: cw, h: ch, x0: x0s[i], n: 7, seed: 11 + i, noise: 0.07, color: hi ? "#FFB020" : "#8B5CF6", ptR: 5.5 });
  }
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">${s}</svg>`;
}

function artSurfaceSvg() {
  // Wireframe of a tilted, curved best-focus surface with the per-star points scattered around it.
  const W = 900, H = 640, n = 12, r = rng(7);
  const z = (u, v) => 0.55 * u + 0.18 * v - 0.6 * (u * u + v * v);
  const P = (u, v, zz) => [450 + (u - v) * 210, 270 + (u + v) * 95 - zz * 150];
  let s = "";
  for (let i = 0; i <= n; i++) {
    const t = -1 + (2 * i) / n;
    let a = "", b = "";
    for (let j = 0; j <= 40; j++) {
      const q = -1 + (2 * j) / 40;
      const p1 = P(t, q, z(t, q)), p2 = P(q, t, z(q, t));
      a += `${j ? "L" : "M"}${p1[0].toFixed(1)},${p1[1].toFixed(1)}`;
      b += `${j ? "L" : "M"}${p2[0].toFixed(1)},${p2[1].toFixed(1)}`;
    }
    s += `<path d="${a}" fill="none" stroke="#8B5CF6" stroke-width="2" opacity="0.75"/><path d="${b}" fill="none" stroke="#8B5CF6" stroke-width="2" opacity="0.75"/>`;
  }
  for (let i = 0; i < 95; i++) {
    const u = -0.95 + r() * 1.9, v = -0.95 + r() * 1.9;
    const off = (r() - 0.5) * 0.22, hi = i === 40;
    const p = P(u, v, z(u, v) + off), q = P(u, v, z(u, v));
    s += `<path d="M${p[0].toFixed(1)},${p[1].toFixed(1)} L${q[0].toFixed(1)},${q[1].toFixed(1)}" stroke="#A9B0D6" stroke-width="1.2" opacity="0.5"/>`;
    s += `<circle cx="${p[0].toFixed(1)}" cy="${p[1].toFixed(1)}" r="${hi ? 9 : 5}" fill="${hi ? "#FFB020" : "#F2F0FF"}" opacity="${hi ? 1 : 0.9}"/>`;
  }
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">${s}</svg>`;
}

function artSigAmpSvg() {
  const W = 1400, H = 520;
  let s = vcurveSvgPart({ ox: 40, oy: 30, w: 580, h: 440, n: 7, seed: 3, noise: 0.02, ptR: 10, lw: 5.5 });
  s += vcurveSvgPart({ ox: 780, oy: 30, w: 580, h: 440, n: 13, seed: 5, noise: 0.02, ptR: 8, lw: 5.5, pt: "#FFB020" });
  s += `<path d="M655,250 H735 M715,228 L738,250 L715,272" fill="none" stroke="#A9B0D6" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/>`;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">${s}</svg>`;
}

function artTiltBackfocusSvg(kind) {
  // Side view: dashed line = where the focal plane wants the sensor; solid bar = the sensor; arrows = screw moves.
  const W = 640, H = 420;
  const arrow = (x, y1, y2, col) => {
    const dir = y2 < y1 ? -1 : 1;
    return `<path d="M${x},${y1} V${y2} M${x - 13},${y2 - dir * 16} L${x},${y2} L${x + 13},${y2 - dir * 16}" fill="none" stroke="${col}" stroke-width="6" stroke-linecap="round" stroke-linejoin="round"/>`;
  };
  let s = `<path d="M60,150 H580" stroke="#A9B0D6" stroke-width="3" stroke-dasharray="12 9"/>`;
  if (kind === "tilt") {
    s += `<g transform="rotate(-9 320 210)"><rect x="90" y="196" width="460" height="28" rx="8" fill="#8B5CF6"/></g>`;
    s += arrow(130, 330, 262, "#FF4D9D") + arrow(510, 250, 318, "#FF4D9D");
    s = s.replace('M60,150 H580', 'M60,210 H580');
  } else {
    s += `<rect x="90" y="246" width="460" height="28" rx="8" fill="#8B5CF6"/>`;
    s += arrow(130, 370, 300, "#FF4D9D") + arrow(320, 370, 300, "#FF4D9D") + arrow(510, 370, 300, "#FF4D9D");
  }
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">${s}</svg>`;
}


// ---------- slide-specific explanatory art (replaces small-print matplotlib figures) ----------
const svgWrap = (W, H, body, defs = "") => `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}"><defs>${defs}</defs>${body}</svg>`;

function artSearchSvg() {
  // An objective "hill" with a coarse grid of probes and a stepped path walking to the best score.
  const W = 760, H = 540, cx = 300, cy = 330;
  let s = `<rect x="4" y="4" width="${W - 8}" height="${H - 8}" rx="16" fill="#10173A" stroke="#3A4380" stroke-width="2.5"/>`;
  const rings = [[330, 215, "#2A1F66"], [270, 175, "#3F2A8C"], [210, 135, "#5B36B8"], [150, 97, "#8B5CF6"], [95, 60, "#C05BD8"], [48, 30, "#FF4D9D"]];
  s += `<g clip-path="url(#clip)">` + rings.map(([rx, ry, c]) => `<ellipse cx="${cx}" cy="${cy}" rx="${rx}" ry="${ry}" fill="${c}" transform="rotate(-18 ${cx} ${cy})"/>`).join("") + `</g>`;
  for (let i = 0; i < 4; i++) for (let j = 0; j < 4; j++) s += `<rect x="${110 + i * 180 - 6}" y="${80 + j * 125 - 6}" width="12" height="12" fill="#A9B0D6" opacity="0.8"/>`;
  const path = [[650, 205], [470, 205], [470, 270], [380, 270], [380, 315], [325, 315], [325, 330], [300, 330]];
  s += `<path d="${path.map((p, i) => `${i ? "L" : "M"}${p[0]},${p[1]}`).join("")}" fill="none" stroke="#FFFFFF" stroke-width="4.5" stroke-linejoin="round"/>`;
  path.slice(0, -1).forEach((p) => { s += `<circle cx="${p[0]}" cy="${p[1]}" r="7" fill="#FFFFFF"/>`; });
  const star = (x, y, r) => { let d = ""; for (let k = 0; k < 10; k++) { const a = -Math.PI / 2 + (k * Math.PI) / 5, rr = k % 2 ? r * 0.45 : r; d += `${k ? "L" : "M"}${(x + rr * Math.cos(a)).toFixed(1)},${(y + rr * Math.sin(a)).toFixed(1)}`; } return `<path d="${d}Z" fill="#FFFFFF" stroke="#0B1026" stroke-width="2"/>`; };
  s += star(cx, cy, 20);
  return svgWrap(W, H, s, `<clipPath id="clip"><rect x="6" y="6" width="${W - 12}" height="${H - 12}" rx="14"/></clipPath>`);
}

// Shared geometry for the adaptive-threshold art so the slide can place native callouts on it.
const THRESH = { W: 1500, H: 520, plotX: 560, plotW: 920, plotY: 20, plotH: 470, ymax: 0.82, faintT: 0.24, glowT: 0.9, global: 0.33, margin: 0.115, rowV: 0.55 };
function threshBackground(t) { return 0.12 + 0.1 * t + 0.42 * Math.exp(-(((t - 0.88) / 0.14) ** 2)); }
function artThresholdSvg() {
  const { W, H, plotX, plotW, plotY, plotH, ymax, faintT, glowT, global } = THRESH;
  const r = rng(21);
  const X = (t) => plotX + t * plotW, Y = (v) => plotY + plotH - (v / ymax) * plotH;
  const bump = (t, c, w, a) => a * Math.exp(-(((t - c) / w) ** 2));
  const sig = (t) => threshBackground(t) + bump(t, faintT, 0.012, 0.16) + bump(t, 0.47, 0.014, 0.3) + bump(t, glowT, 0.008, 0.03);
  // left: the frame, with a corner glow and the scan row
  let s = `<rect x="4" y="20" width="480" height="470" rx="14" fill="#05070F" stroke="#3A4380" stroke-width="2.5"/>
    <g clip-path="url(#fclip)"><rect x="4" y="20" width="480" height="470" fill="url(#lin)"/><circle cx="430" cy="${20 + 0.55 * 470}" r="300" fill="url(#glow)"/>`;
  const stars = [[0.24, 0.55, 3, 0.55], [0.47, 0.55, 5, 1], [0.15, 0.2, 4, 0.9], [0.62, 0.78, 4, 0.9], [0.33, 0.85, 3, 0.7], [0.7, 0.3, 4, 0.9], [0.1, 0.72, 3, 0.6], [0.55, 0.25, 3.5, 0.8], [0.85, 0.85, 3, 0.7]];
  stars.forEach(([u, v, rad, op]) => { const x = 4 + u * 480, y = 20 + v * 470; s += `<circle cx="${x}" cy="${y}" r="${rad * 3}" fill="#fff" opacity="${op * 0.2}"/><circle cx="${x}" cy="${y}" r="${rad}" fill="#fff" opacity="${op}"/>`; });
  s += `</g><path d="M4,${20 + 0.55 * 470} H484" stroke="#C4B5FD" stroke-width="2.5"/>`;
  // right: that row as a trace, with the two thresholds
  s += `<path d="M${plotX},${plotY} V${plotY + plotH} H${plotX + plotW}" fill="none" stroke="#5B6399" stroke-width="2.5"/>`;
  let tr = "", ad = "";
  for (let i = 0; i <= 300; i++) { const t = i / 300; tr += `${i ? "L" : "M"}${X(t).toFixed(1)},${Y(sig(t) + (r() - 0.5) * 0.022).toFixed(1)}`; }
  for (let i = 0; i <= 80; i++) { const t = i / 80; ad += `${i ? "L" : "M"}${X(t).toFixed(1)},${Y(threshBackground(t) + THRESH.margin).toFixed(1)}`; }
  s += `<path d="${tr}" fill="none" stroke="#A9B0D6" stroke-width="2.5" stroke-linejoin="round"/>`;
  s += `<path d="M${X(0)},${Y(global)} H${X(1)}" stroke="#FFFFFF" stroke-width="4" stroke-dasharray="14 10"/>`;
  s += `<path d="${ad}" fill="none" stroke="#8B5CF6" stroke-width="6" stroke-linecap="round"/>`;
  s += `<circle cx="${X(faintT)}" cy="${Y(sig(faintT))}" r="13" fill="none" stroke="#34D399" stroke-width="5"/>`;
  const gx = X(glowT), gy = Y(sig(glowT));
  s += `<path d="M${gx - 11},${gy - 11} L${gx + 11},${gy + 11} M${gx + 11},${gy - 11} L${gx - 11},${gy + 11}" stroke="#FF4D9D" stroke-width="6" stroke-linecap="round"/>`;
  const defs = `<clipPath id="fclip"><rect x="4" y="20" width="480" height="470" rx="14"/></clipPath>
    <linearGradient id="lin" x1="0" y1="1" x2="1" y2="0"><stop offset="0" stop-color="#05070F"/><stop offset="1" stop-color="#2A2F45"/></linearGradient>
    <radialGradient id="glow"><stop offset="0" stop-color="#FFFFFF" stop-opacity="0.85"/><stop offset="0.35" stop-color="#C8CCE0" stop-opacity="0.4"/><stop offset="1" stop-color="#C8CCE0" stop-opacity="0"/></radialGradient>`;
  return svgWrap(W, H, s, defs);
}

const ASYM = { W: 900, H: 560, ox: 40, oy: 20, w: 830, h: 500, trueMin: 0.56, symMin: 0.43 };
function artAsymSvg() {
  // A lopsided focus curve: the symmetric fit's minimum lands away from the true minimum.
  const { W, H, ox, oy, w, h, trueMin, symMin } = ASYM, r = rng(9);
  const tru = (t) => { const u = t - trueMin, k = u < 0 ? 0.75 : 1.9; return Math.sqrt(0.0064 + (k * u) ** 2); };
  const sym = (t) => Math.sqrt(0.0064 + (1.15 * (t - symMin)) ** 2) + 0.012;
  const fmax = 0.9, X = (t) => ox + t * w, Y = (v) => oy + h - 10 - (v / fmax) * (h - 30);
  const line = (f) => { let d = ""; for (let i = 0; i <= 80; i++) { const t = i / 80; d += `${i ? "L" : "M"}${X(t).toFixed(1)},${Y(Math.min(f(t), fmax)).toFixed(1)}`; } return d; };
  let s = `<path d="M${ox},${oy} V${oy + h} H${ox + w}" fill="none" stroke="#5B6399" stroke-width="2.5"/>`;
  s += `<path d="${line(sym)}" fill="none" stroke="#A9B0D6" stroke-width="4.5" stroke-dasharray="14 10"/>`;
  s += `<path d="${line(tru)}" fill="none" stroke="#8B5CF6" stroke-width="6" stroke-linecap="round"/>`;
  for (let i = 0; i < 9; i++) { const t = 0.05 + (0.9 * i) / 8; s += `<circle cx="${X(t)}" cy="${Y(tru(t) + (r() - 0.5) * 0.03)}" r="9" fill="#F2F0FF"/>`; }
  s += `<path d="M${X(symMin)},${oy + h} V${Y(sym(symMin)) + 8}" stroke="#A9B0D6" stroke-width="3.5" stroke-dasharray="7 6"/>`;
  s += `<path d="M${X(trueMin)},${oy + h} V${Y(tru(trueMin)) + 8}" stroke="#8B5CF6" stroke-width="3.5" stroke-dasharray="7 6"/>`;
  const ya = oy + h - 26, a = X(symMin) + 6, b = X(trueMin) - 6;
  s += `<path d="M${a},${ya} H${b} M${a + 13},${ya - 11} L${a},${ya} L${a + 13},${ya + 11} M${b - 13},${ya - 11} L${b},${ya} L${b - 13},${ya + 11}" fill="none" stroke="#FF4D9D" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/>`;
  return svgWrap(W, H, s);
}

const SIGMA = { W: 900, H: 560, ox: 40, oy: 20, w: 830, h: 500, x0: 0.5, half: 0.045 };
function artSigmaSvg() {
  const { W, H, ox, oy, w, h, x0, half } = SIGMA;
  let s = `<rect x="${ox + (x0 - half) * w}" y="${oy}" width="${2 * half * w}" height="${h}" fill="#FF4D9D" opacity="0.28"/>`;
  s += vcurveSvgPart({ ox, oy, w, h, x0, n: 9, seed: 4, noise: 0.025, ptR: 10, lw: 6, mark: "#FF4D9D" });
  return svgWrap(W, H, s);
}

function artDecompSvg() {
  // tilt plane + curvature bowl = the sensor's best-focus surface (three heat maps; operators are native text)
  const S = 300, gap = 120, W = 3 * S + 2 * gap, H = S;
  const sq = (i, fills) => `<g transform="translate(${i * (S + gap)},0)"><rect width="${S}" height="${S}" rx="12" fill="#1E2650"/>${fills.map((f) => `<rect width="${S}" height="${S}" rx="12" fill="${f[0]}" opacity="${f[1]}"/>`).join("")}<rect width="${S}" height="${S}" rx="12" fill="none" stroke="#3A4380" stroke-width="2.5"/></g>`;
  const defs = `<linearGradient id="tilt" x1="0" y1="1" x2="1" y2="0"><stop offset="0" stop-color="#38BDF8"/><stop offset="0.5" stop-color="#1E2650"/><stop offset="1" stop-color="#FF4D9D"/></linearGradient>
    <radialGradient id="bowl" cx="0.5" cy="0.5" r="0.75"><stop offset="0" stop-color="#38BDF8"/><stop offset="0.55" stop-color="#1E2650"/><stop offset="1" stop-color="#FF4D9D"/></radialGradient>
    <radialGradient id="bowl2" cx="0.3" cy="0.7" r="0.95"><stop offset="0" stop-color="#38BDF8"/><stop offset="0.5" stop-color="#1E2650"/><stop offset="1" stop-color="#FF4D9D"/></radialGradient>`;
  return svgWrap(W, H, sq(0, [["url(#tilt)", 1]]) + sq(1, [["url(#bowl)", 1]]) + sq(2, [["url(#bowl2)", 1]]), defs);
}

function artDonutSvg() {
  const S = 320, gap = 60, W = 2 * S + gap, H = S;
  const defs = `<radialGradient id="pt"><stop offset="0" stop-color="#fff"/><stop offset="0.35" stop-color="#fff" stop-opacity="0.55"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>
    <radialGradient id="ring"><stop offset="0" stop-color="#fff" stop-opacity="0"/><stop offset="0.38" stop-color="#fff" stop-opacity="0.03"/><stop offset="0.6" stop-color="#fff" stop-opacity="0.95"/><stop offset="0.8" stop-color="#fff" stop-opacity="0.85"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>`;
  const frame = (x) => `<rect x="${x + 2}" y="2" width="${S - 4}" height="${S - 4}" rx="14" fill="#05070F" stroke="#3A4380" stroke-width="2.5"/>`;
  const body = frame(0) + `<circle cx="${S / 2}" cy="${S / 2}" r="34" fill="url(#pt)"/>` + frame(S + gap) + `<circle cx="${S + gap + S / 2}" cy="${S / 2}" r="125" fill="url(#ring)"/>`;
  return svgWrap(W, H, body, defs);
}

function artOneCurveSvg() {
  return svgWrap(640, 400, vcurveSvgPart({ ox: 20, oy: 10, w: 600, h: 370, n: 9, seed: 8, noise: 0.012, ptR: 9, lw: 5.5 }));
}

function artManyCurvesSvg() {
  // "Hundreds of curves": a field of small, visibly noisier single-star curves.
  const W = 640, H = 400, cols = 5, rows = 3, cw = 108, ch = 108;
  let s = "";
  for (let i = 0; i < cols * rows; i++) {
    const c = i % cols, r = Math.floor(i / cols);
    s += vcurveSvgPart({ ox: 14 + c * (cw + 18), oy: 10 + r * (ch + 22), w: cw, h: ch, x0: 0.38 + ((i * 37) % 25) / 100, n: 7, seed: 100 + i, noise: 0.13, ptR: 4.2, lw: 3, color: "#FFB020", mark: null });
  }
  return svgWrap(W, H, s);
}

// ---------- screenshot crops ----------
async function crop(src, file, box) {
  await sharp(path.join(RAW, src)).extract(box).png().toFile(path.join(OUT, file));
}

async function settingsWall() {
  // Two columns: the top and bottom halves of the advanced Star Detector list, side by side.
  const colW = 290, h1 = 650;
  const a = await sharp(path.join(RAW, "plugin-advanced-top.png")).extract({ left: 434, top: 355, width: colW, height: h1 }).toBuffer();
  const b = await sharp(path.join(RAW, "plugin-advanced-bottom.png")).extract({ left: 434, top: 523, width: colW, height: 477 }).toBuffer();
  await sharp({ create: { width: colW * 2 + 30, height: h1, channels: 4, background: "#FFFFFF" } })
    .composite([{ input: a, left: 0, top: 0 }, { input: b, left: colW + 30, top: 0 }])
    .png().toFile(path.join(OUT, "settings-wall.png"));
}

async function futureWork2023() {
  // The 2023 "Future Work" slide with check marks stamped on the three promises that shipped in v4.
  const src = path.join(DECK2023, "slide-37.png");
  if (!fs.existsSync(src)) { console.warn("! 2023 slide render missing; run render.sh on the 2023 deck (slides 37)"); return; }
  const check = (x, y) => `<g transform="translate(${x},${y})"><circle r="19" fill="#16A34A"/><path d="M-9,1 L-3,8 L10,-7" fill="none" stroke="#fff" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/></g>`;
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="900">${check(876, 352)}${check(938, 394)}${check(938, 467)}</svg>`;
  const meta = await sharp(src).metadata();
  const overlay = await sharp(Buffer.from(svg)).resize(meta.width, meta.height, { fit: "fill" }).png().toBuffer();
  // sharp applies extract before composite, so stamp first and crop in a second pass.
  const stamped = await sharp(src).composite([{ input: overlay, left: 0, top: 0 }]).png().toBuffer();
  await sharp(stamped).extract({ left: 90, top: 40, width: Math.min(1460, meta.width - 90), height: 640 })
    .png().toFile(path.join(OUT, "future-work-2023.png"));
}

async function main() {
  fs.mkdirSync(OUT, { recursive: true });
  await svgToPng(starfieldSvg({ w: 2667, h: 1500, count: 520, seed: 42, glow: true }), "bg-hero.png", 2667);
  await svgToPng(starfieldSvg({ w: 2667, h: 1500, count: 260, seed: 1234, glow: false }), "bg-content.png", 2667);

  fs.copyFileSync(path.join(ROOT, "Joko.NINA.Plugins/Joko.NINA.Plugins.HocusFocus/Resources/HocusFocus.jpg"), path.join(OUT, "logo.jpg"));

  await settingsWall();
  await futureWork2023();
  await crop("inspector-sensor-model.png", "doc-sensor-surface.png", { left: 330, top: 405, width: 670, height: 415 });
  // demo backup: every crop comes from the SAME run (inspector-model-analysis.png)
  await crop("inspector-model-analysis.png", "doc-backup-surface.png", { left: 380, top: 140, width: 720, height: 410 });
  await crop("inspector-model-analysis.png", "doc-backup-properties.png", { left: 385, top: 588, width: 605, height: 128 });
  await crop("inspector-model-analysis.png", "doc-backup-checklist.png", { left: 278, top: 786, width: 178, height: 198 });
  await crop("inspector-populated.png", "doc-guidance.png", { left: 268, top: 642, width: 990, height: 166 });
  await crop("wizard-measurement-section.png", "doc-wizard-setup.png", { left: 268, top: 98, width: 990, height: 247 });
  await crop("wizard-measurement-section.png", "doc-wizard-sigamp.png", { left: 268, top: 388, width: 440, height: 36 });
  await crop("wizard-measurement-section.png", "doc-wizard-result.png", { left: 770, top: 630, width: 490, height: 185 });
  await sharp(path.join(RAW, "optimizer-wizard-start.png")).extract({ left: 0, top: 0, width: 662, height: 300 }).toFile(path.join(OUT, "doc-optimizer-start.png"));
  await sharp(path.join(RAW, "optimizer-wizard-summary.png")).extract({ left: 0, top: 0, width: 842, height: 432 }).toFile(path.join(OUT, "doc-optimizer-summary.png"));

  await svgToPng(artMatchSvg(), "art-match.png", 1350, true);
  await svgToPng(artCurvesSvg(), "art-curves.png", 1350, true);
  await svgToPng(artSurfaceSvg(), "art-surface.png", 1350, true);
  await svgToPng(artSigAmpSvg(), "art-sigamp.png", 2100, true);
  await svgToPng(artSearchSvg(), "art-search.png", 1140);
  await svgToPng(artThresholdSvg(), "art-threshold.png", 2250);
  await svgToPng(artAsymSvg(), "art-asym.png", 1350);
  await svgToPng(artSigmaSvg(), "art-sigma.png", 1350);
  await svgToPng(artDecompSvg(), "art-decomp.png", 1710);
  await svgToPng(artDonutSvg(), "art-donut.png", 1050);
  await svgToPng(artOneCurveSvg(), "art-onecurve.png", 960);
  await svgToPng(artManyCurvesSvg(), "art-manycurves.png", 960);
  await svgToPng(artTiltBackfocusSvg("tilt"), "art-tilt.png", 960);
  await svgToPng(artTiltBackfocusSvg("backfocus"), "art-backfocus.png", 960);

  for (const [key, url] of Object.entries({ home: DOCS.home, settings: DOCS.settings })) {
    await QRCode.toFile(path.join(OUT, `qr-${key}.png`), url, { margin: 1, width: 600, color: { dark: "#0B1026", light: "#FFFFFF" } });
  }
  console.log("assets ready:", fs.readdirSync(OUT).length, "files");
}

module.exports = { main, THRESH, ASYM, SIGMA, threshBackground };
if (require.main === module) main().catch((e) => { console.error(e); process.exit(1); });
