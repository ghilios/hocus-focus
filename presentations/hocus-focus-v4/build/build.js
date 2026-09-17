// Builds "Hocus Focus v4.pptx". Usage: node build.js [--skip-assets]
const fs = require("fs");
const path = require("path");
const sharp = require("sharp");
const pptxgen = require("pptxgenjs");
const T = require("./theme");

async function loadDims(dir) {
  if (!fs.existsSync(dir)) return;
  for (const f of fs.readdirSync(dir).filter((f) => /\.(png|jpe?g)$/i.test(f))) {
    const m = await sharp(path.join(dir, f)).metadata();
    T.state.dims[path.join(dir, f)] = { width: m.width, height: m.height };
  }
}

(async () => {
  if (!process.argv.includes("--skip-assets")) {
    await require("./assets").main();
    await require("./icons").main();
  }
  await loadDims(path.resolve(__dirname, "../assets"));
  await loadDims(path.resolve(__dirname, "../screenshots"));

  const pres = new pptxgen();
  pres.layout = "LAYOUT_WIDE";
  pres.author = "George Hilios";
  pres.title = "Hocus Focus v4 — Every Star Matters";

  for (const mod of ["opening", "ch1", "ch2", "ch3", "close"]) {
    const file = path.join(__dirname, "slides", mod + ".js");
    if (fs.existsSync(file)) require(file)(pres);
  }

  const out = path.resolve(__dirname, "../Hocus Focus v4.pptx");
  await pres.writeFile({ fileName: out });
  console.log("wrote", out);
  if (T.state.drafts.length) console.log("DRAFT images (doc fallback in use):\n  " + T.state.drafts.join("\n  "));
  if (T.state.missing.length) console.log("MISSING screenshots (placeholder shown):\n  " + T.state.missing.join("\n  "));
})().catch((e) => { console.error(e); process.exit(1); });
