// Renders react-icons glyphs to PNGs in ../assets (icon-<name>.png).
const path = require("path");
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const sharp = require("sharp");
const fa = require("react-icons/fa6");

const ICONS = {
  sliders: fa.FaSliders, star: fa.FaStar, plane: fa.FaLayerGroup,
  desktop: fa.FaDesktop, export: fa.FaFileExport, gpu: fa.FaMicrochip,
  search: fa.FaMagnifyingGlass, ring: fa.FaBullseye, filter: fa.FaFilter, ruler: fa.FaRulerCombined,
  curve: fa.FaChartLine, stars: fa.FaWandMagicSparkles, step: fa.FaShoePrints, play: fa.FaPlay,
  hand: fa.FaHandPointer, moves: fa.FaArrowsUpDownLeftRight, undo: fa.FaRotateLeft,
  github: fa.FaGithub, discord: fa.FaDiscord, book: fa.FaBookOpen, check: fa.FaCircleCheck,
};

async function main(color = "#FFFFFF") {
  for (const [name, Icon] of Object.entries(ICONS)) {
    if (!Icon) throw new Error("missing icon " + name);
    const svg = renderToStaticMarkup(React.createElement(Icon, { color, size: 256 }));
    await sharp(Buffer.from(svg)).resize(256, 256, { fit: "contain", background: { r: 0, g: 0, b: 0, alpha: 0 } }).png()
      .toFile(path.resolve(__dirname, "../assets", `icon-${name}.png`));
  }
  // pink cross for the pass/fail graphic
  const x = renderToStaticMarkup(React.createElement(fa.FaCircleXmark, { color: "#FF4D9D", size: 256 }));
  await sharp(Buffer.from(x)).png().toFile(path.resolve(__dirname, "../assets", "icon-cross-pink.png"));
  // green variant of the check mark
  const svg = renderToStaticMarkup(React.createElement(fa.FaCircleCheck, { color: "#34D399", size: 256 }));
  await sharp(Buffer.from(svg)).png().toFile(path.resolve(__dirname, "../assets", "icon-check-green.png"));
}
module.exports = { main };
if (require.main === module) main().catch((e) => { console.error(e); process.exit(1); });
