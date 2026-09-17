# Hocus Focus v4 presentation — outline + build plan

## Context

George is preparing a follow-up talk for The Astro Imaging Channel (online, 30–45 min, questions expected
throughout) on what's new in Hocus Focus v4. It follows his 2023 NEAIC/TAIC talk *"The Science of Star
Measurement for Auto-Focusing and Tilt Correction"*. Deliverable: a `.pptx` in a new `presentations/`
folder, with the 2023 deck copied alongside. He asked for a **proposed outline first** (this document),
then the build.

**Decisions made:** TAIC online only · fresh dark night-sky design (nods to the 2023 navy/purple/pink
palette, not its template) · intuition-first, **major points only — no deep specifics on slides**; each
feature slide carries a **"Read more" link to its docs page** · speaker notes on every slide ·
slides + **one live demo** (sensor model on a saved AF run, at the "every star matters" punchline).

**Feedback incorporated (round 1):**
1. GPU acceleration: mention it exists, nothing about the implementation.
2. Noise clipping / adaptive binarization: one light slide, no sweep tables; link to docs for depth. Same
   treatment for the other detection improvements.
3. Credits: **do not** credit Frank again (done in 2023). **Do** credit **Steve Smith** (RANSAC star
   registration) and **Josh Jones of ASG** (provided the EAT that made automation possible).
4. "Every star matters" → **live demo** of a sensor model on a saved AF run (it runs quickly).
5. Settings slide: say they are **all fully documented now** + link; keep the categorization; headline
   points only.

## Key findings from research

- **2023 deck found locally** — `/mnt/c/Users/ghili/OneDrive/Documents/NEAIC 2023 - Star Measurement.pptx`
  (38 slides, 16:9). Its *Future Work* slide promised **"Guidance for which screws to turn"**, **"Calculate
  tilt adjuster screw locations"**, **"Motorized tilt adjusters?"** — all three shipped in v4 → opening callback.
- **Docs site** `https://ghilios.github.io/hocus-focus/` (source `documentation/docs/`): 39 generated
  figures (`assets/figures/`, white-background matplotlib), 25 cropped screenshots, 18 full-res raw captures
  (`assets/screenshots/raw/`, 1600×1040, NINA light theme). Re-crop from `raw/` for slides.
- **Visual gaps** (no images in the manual): AF frame review, Tilt Adapter Wizard step-by-step, ASG EAT
  automation, both simulators, export/import dialog. The existing guidance-table capture shows a near-zero
  tilt plus a pitch-mismatch warning — not a hero shot. → **Shot list below.**
- **Facts to keep straight:**
  - Optimizer objective is dominated by **σ_focus** — the uncertainty of the best-focus position from the
    all-star average-HFR curve. Slide wording: "minimize the error in the calculated best-focus position".
  - Runtime: manual says only "can take a while"; slide will say **"20–30 min on a typical imaging PC"** per
    George (*confirm wording*).
  - Noise-clipping default was reverted to 4.0 (interim); adaptive binarization ships ON. Slide only claims
    the adaptive idea and "more stars, especially in the corners".
- **Tooling:** node 24 present; `pptxgenjs` not installed (local `npm install`); no LibreOffice in WSL.
  **PowerPoint is installed on Windows** → render slides to PNG via `powershell.exe` COM automation for
  visual QA (true font rendering, so Segoe UI is safe). No `windows-mcp` in this session → new NINA captures
  come from George (or a later session with that MCP, per `.claude/docs/nina-mcp-screenshots.md`).

### Docs links used on slides ("Read more" footer chip + in notes)
| Topic | URL (under `https://ghilios.github.io/hocus-focus/`) |
|---|---|
| All settings, documented | `settings/` |
| Optimization wizard | `optimization/` |
| Export / import settings | `settings/` (§ Exporting and importing) |
| Donut detection | `settings/donut-aware/` |
| Adaptive binarization | `settings/adaptive-binarization/` |
| Hyperbolic fitting | `overview/hyperbola-fitting/` |
| Autofocus + frame review | `overview/autofocus/` |
| Sensor model | `overview/sensor-model/` |
| Tilt Adapter Wizard | `overview/tilt-adapter-wizard/` |
| Aberration Inspector / guidance | `overview/tilt-aberration-inspector/` |
| Motorized tilt adapter (ASG EAT) | `overview/motorized-tilt-adapter/` |
| Camera simulator | `overview/camera-simulator/` |
(Verify each URL resolves against the built `site/` folder before finalizing.)

---

## Proposed outline

Working title: **"Hocus Focus v4 — Every Star Matters"**
Subtitle: *Star detection optimization, tilt adapter calibration, and guided tilt correction.*

Narrative device: a small amber **SNR** tag appears where the question is planted (slide 6) and returns on
the slides that pay it off (18, 24–27). Chapter dividers double as **question pauses**.
📷 = needs a new screenshot (shot list below). Every feature slide: 3–4 short bullets max + a docs link.

### Opening (≈3 min)
| # | Slide | Content / visual |
|---|---|---|
| 1 | Title | Title, name, TAIC, date. Starfield background. |
| 2 | Where we left off | The 2023 *Future Work* slide as an image, with ✔ stamped on "which screws to turn", "screw locations", "motorized tilt adjusters". One-line recap of the 2023 talk. |
| 3 | What's new in v4 | The talk as 3 chapters: **Fewer decisions** (optimizer) → **More stars** (detection, fits, review) → **Flat sensors** (wizard, guidance, EAT, simulators). |

### Chapter 1 — "There are too many settings" (≈7 min)
| # | Slide | Content / visual |
|---|---|---|
| 4 | The #1 complaint | Wall of the Advanced Star Detector list (`raw/plugin-advanced-*.png`). Big number "~30 settings". Good news #1: **every one is now fully documented** → `settings/` link + QR. |
| 5 | Good news #2: one button | Star Detection Optimization Wizard start page (📷 refresh). Point it at a saved AF run (or capture a live one) and it tunes everything for your rig. |
| 6 | So why so many settings? | Teaser. "It comes down to something everyone here already obsesses over…" → **SNR** tag. "Hold that thought." |
| 7 | How it works | Simple loop diagram (native shapes): saved AF frames → try settings → re-detect stars → fit focus curve → score → repeat. `figures/compass-search.png` as the visual. Never hands back anything worse than what you have. |
| 8 | What you get back | Summary page (📷 refresh with a strong before/after): tighter focus curve, more stars, plus recommended step size / exposure. |
| 9 | It takes a while | "20–30 min on an imaging PC" — it re-detects every frame hundreds of times. Three helpers, one line each: **run it on another computer** from a saved AF folder · **Export / Import** the result (📷) · **GPU acceleration** (exists; one toggle). |

### Chapter 2 — More stars, better curves (≈7 min)
| # | Slide | Content / visual |
|---|---|---|
| 10 | Chapter divider | "More stars" — pause for questions |
| 11 | Donut detection | `figures/defocused-donut.png` + 📷 donut frame OFF vs ON. Out-of-focus reflector/SCT stars are hollow rings that the old detector threw away; v4 recognises them. Opt-in toggle. → docs link |
| 12 | Locally adaptive binarization | `figures/adaptive-binarization.png`. One brightness threshold can't suit a frame with vignetting, gradients or amp glow; v4 adapts it across the frame → **more stars, especially in the corners** (plant: "remember the corners"). On by default. → docs link |
| 13 | Better hyperbolic fits | `figures/hyperbola-asymmetric-bias.png` (+ `hyperbola-variants.png` small). Real focus curves aren't symmetric; forcing a symmetric V shifts best focus. **Hybrid (Best Fit)** tries several shapes and only picks an asymmetric one when the data justify it. → docs link |
| 14 | Autofocus frame review | 📷 AF panel controls (*Keep frames for review*, *Review Frames*) + 📷 review window on a good frame. |
| 15 | Spot the bad frame | 📷 review window on a problem frame. See exactly what the detector saw at each focuser position; feed it back to the optimizer. → docs link |
| 16 | What the optimizer is chasing (bridge) | `figures/af-vcurve.png`: minimize the error in the best-focus position. Each point **averages every star in the frame** → lots of signal → autofocus is forgiving. **SNR** tag: "Autofocus is the easy case. Tilt is not." |

### Chapter 3 — Flat sensors (≈20 min incl. demo)
| # | Slide | Content / visual |
|---|---|---|
| 17 | Chapter divider | "Tilt & backfocus" — pause for questions |
| 18 | 60-second recap: the sensor model | `figures/sensor-surface-decomposition.png` + real 3D surface (`raw/inspector-sensor-model.png`). Surface = tilt plane + curvature (backfocus). In 2023 it told you *that* you had tilt — not what to do about it. |
| 19 | Tilt Adapter Calibration Wizard | The question: *which screw, which way, how far?* 📷 wizard setup (device presets, screw labels). |
| 20 | The calibration walk-through | Native-shape timeline: **Baseline → turn screw 1 → re-measure → turn screw 2 → re-measure** (+ optional direction check). Each step is a full focus sweep + sensor model. 📷 step prompt with screw diagram. |
| 21 | What comes out | 📷 wizard result. Two answers: **where each screw sits as the sensor sees it** and **how far each screw moves the tilt plane**. Measured, not assumed — mirrors/diagonals/rotators don't matter. → docs link |
| 22 | **The punchline: every star matters** | New 3-panel diagram: ① match the same star across every frame (credit **Steve Smith — RANSAC registration**) → ② a focus curve **per star** → ③ a 3D surface through all of them. AF = one curve from hundreds of stars; sensor model = hundreds of curves from **one star each**, including the out-of-focus ends and the faint corners. **SNR** tag, large. |
| 23 | **LIVE DEMO** | Demo card slide: "Sensor model on a saved AF run" — *Aberration Inspector → Load Saved AF*. Notes carry the demo script + talking points (star count, per-star curves, surface, tilt/curvature numbers). |
| 24 | Demo backup | `raw/inspector-sensor-model.png` + `raw/inspector-model-analysis.png` — shown only if the demo misbehaves (hidden slide by default). |
| 25 | *That's* why there are so many settings | Callback to slide 4's wall, now **categorized by purpose**: find faint stars · survive defocus · reject junk · measure precisely. All in service of per-star SNR across the whole frame. The optimizer has an "optimize for aberration inspection" objective for exactly this. Docs link repeated. |
| 26 | Signal Amplification | Diagram: same sweep range, step ÷ N, frames × N. More points per star curve, smaller jumps between frames → easier matching. Cost: more exposures. Existing screenshot (`raw/wizard-measurement-section.png`). |
| 27 | Guided adjustments | 📷 Tilt Adapter Guidance with a meaningful correction: direction arrows + **Tilt / Backfocus / Total** turns per screw. |
| 28 | Tilt vs. backfocus, separated | Diagram: tilt = turn screws **differently**; backfocus = turn them **all the same way** (or add spacers). What's left is your optics. Pass/fail vs critical focus zone (`raw/inspector-model-analysis.png`). |
| 29 | Measure → turn → re-measure | 📷 Tilt Measurement History converging over 2–3 rounds. → docs link |
| 30 | ASG EAT: fully automated | 📷 Motorized connection + motor grid; hardware photo if available. Hands-off calibration (*Auto Run All*). **Thank you Josh Jones (ASG)** for providing an EAT. |
| 31 | ASG EAT: automatic adjustment | 📷 "Review motor commands" dialog. Nothing moves until you approve; tilt + backfocus in ≤3 moves; "tilt got worse → Revert". → docs link |
| 32 | Camera & tilt adapter simulators | 📷 simulated frames (focused / donut) + 📷 Simulator Tilt Adapter panel at `✓ ≈ flat`. Real catalog stars, real sensor models, injected tilt/backfocus. Rehearse AF, calibration and EAT automation indoors on a cloudy day. → docs link |

### Close (≈2 min)
| # | Slide | Content / visual |
|---|---|---|
| 33 | The story in one slide | Settings → optimizer → SNR → every star matters → calibrated adapter → guided / automatic correction. Docs URL + QR. |
| 34 | Thank you / Q&A | Credits: **Steve Smith** (RANSAC), **Josh Jones / ASG** (EAT), community AF-data contributors. Contact (Discord jokogeo, CN ghilios), GitHub. *(Optional "What's next" slide if George supplies items.)* |

**Timing:** 33 shown slides (≈28–32 min) + ≈5 min live demo + question pauses → 35–45 min.
Flex: slides 15 and 28 can be merged into their neighbours if time runs short.

---

## Screenshot shot list for George (all doable indoors with the simulators)

Capture at 100% display scaling, NINA window ≥1600 px wide, PNG, no hover tooltips; drop into
`presentations/hocus-focus-v4/screenshots/` using these names. The draft deck carries clearly-marked
placeholders until they arrive (auto-swapped by file name on rebuild).

| File name | What | Slide |
|---|---|---|
| `optimizer-start.png` | Wizard start page, current build (objectives + GPU toggle visible) | 5 |
| `optimizer-summary.png` | Summary from a run with a visibly improved curve + recommendations | 8 |
| `settings-import.png` | Import confirmation table (Setting / Current / Imported) | 9 |
| `donut-frame-off.png`, `donut-frame-on.png` | A heavily defocused frame annotated with donut detection OFF vs ON (real data preferred, else simulator) | 11 |
| `af-review-controls.png` | AF panel showing Replay Saved AF / Keep frames / Review Frames | 14 |
| `af-review-good.png`, `af-review-bad.png` | Review Frames window: healthy frame and problem frame | 14–15 |
| `wizard-setup.png` | Tilt Adapter Wizard: device preset, screw labels, measurement options | 19 |
| `wizard-step.png` | A mid-calibration step prompt with the screw diagram | 20 |
| `wizard-result.png` | Finished calibration: screw angles + measured hardware panel | 21 |
| `guidance.png` | Inspector Tilt Adapter Guidance with a real correction (≈0.5–1.5 turns), arrows + Tilt/Backfocus/Total | 27 |
| `tilt-history.png` | Tilt Measurement History after 2–3 measure/adjust rounds | 29 |
| `eat-connection.png` | Motorized Device Connection + motor positions grid (Simulator port is fine) | 30 |
| `eat-review-commands.png` | "Review motor commands" approval dialog | 31 |
| `sim-frame-focused.png`, `sim-frame-defocused.png` | Simulator frames | 32 |
| `sim-tilt-panel.png` | Simulator Tilt Adapter panel showing `✓ ≈ flat` | 32 |
| *(optional)* hardware photos | ASG EAT / a manual tilt adapter on a camera — only images George owns or has permission to use | 19, 30 |

**Live-demo prep (slide 23):** pick a saved AF run with a dense star field and visible tilt (e.g. one from
`D:\Tilt Calibration Bank\astrodet\`), confirm *Sensor Curve Model Enabled* is on, and time one dry run so
the notes can quote the expected duration.

---

## Build plan

### Folder layout (new)
```
presentations/
  2023-neaic-star-measurement/NEAIC 2023 - Star Measurement.pptx   # copied from OneDrive
  hocus-focus-v4/
    outline.md                 # the outline + shot list above (living doc)
    Hocus Focus v4.pptx        # build output
    screenshots/               # George's captures (names above)
    assets/                    # re-cropped doc images, generated diagrams, starfield bg, QR codes
    build/                     # package.json, build.js, theme.js, slides/*.js, render.ps1
```
`presentations/**/node_modules/` added to `.gitignore`. Per repo convention, this plan is also saved as
`plans/hocus-focus-v4-presentation-plan.md`. No plugin code changes → dotnet test suite unaffected.
Nothing is committed unless George asks (then: `ghilios/<topic>` branch, privacy email, PR).

### Steps
1. Create `presentations/`, copy the 2023 deck, write `outline.md`, save the plan to `plans/`.
2. `npm init` + `npm install pptxgenjs sharp qrcode` inside `presentations/hocus-focus-v4/build/`.
3. **Design system (`theme.js`)** — 16:9 `LAYOUT_WIDE`. Deep night navy `0B1026` background with a subtle
   procedurally generated starfield PNG; panels `151B3A`; text `F2F0FF`; primary accent purple `8B5CF6`
   (2023 nod); emphasis pink `FF4D9D` (matches docs highlight colour); amber `FFB020` reserved for the
   **SNR** thread. Fonts: Segoe UI Semibold headings / Segoe UI body (QA'd in real PowerPoint).
   Motif: doc figures + NINA screenshots (both light-background) sit in **rounded white "evidence cards"**
   with soft shadow; a small 3-chapter breadcrumb top-left; a **"Read more → docs URL" chip** bottom-right on
   feature slides (clickable hyperlink). No title underlines, no accent stripes.
   Layout helpers: `titleSlide`, `chapterSlide`, `heroImage`, `imageLeftTextRight`, `bigNumbers`,
   `iconRows`, `flowSteps`, `twoUp`, `demoCard`, `docsChip(url)`, `placeholderShot(name)` (dashed frame +
   file name for missing 📷).
4. **Assets** — re-crop regions from `documentation/docs/assets/screenshots/raw/*.png` with `sharp` (crop
   boxes can be lifted from `documentation/figures/screenshots/*.json` sidecars); copy doc figures from
   `documentation/docs/assets/figures/`; export the 2023 *Future Work* slide to PNG via PowerPoint COM; draw
   new diagrams natively in pptxgenjs shapes (optimizer loop, calibration timeline, 3-panel "every star
   matters", settings categorization, signal amplification, tilt-vs-backfocus) so they stay editable.
5. **Slides** — one module per chapter in `build/slides/`; slide text stays at headline level (3–4 bullets);
   every slide gets `addNotes()` with a ~1-minute talk track, the supporting numbers/caveats from the docs
   (for answering questions), and the docs URL. Slide 23 notes hold the demo script; slide 24 is hidden.
6. **Build + QA loop** — `node build.js` → `render.ps1` (PowerPoint COM `Presentation.Export` to PNG via
   `powershell.exe`) → review every slide image for overflow/overlap/contrast → fix → re-render.
   Run the pptx skill's `validate.py` if its Python deps are available; opening cleanly in PowerPoint (no
   repair prompt) is the hard gate.
7. When George's screenshots arrive, `placeholderShot` swaps to the real file (exists-check by name) →
   rebuild → re-QA those slides.

### Verification
- Deck opens in PowerPoint with no repair dialog; all slides exported to PNG and visually reviewed.
- Build log lists remaining placeholder shots → reported to George as the outstanding list.
- Notes on every slide; docs links verified against the built `site/` tree; no Frank credit; Steve Smith and
  Josh Jones/ASG credited (slides 22, 30, 34); GPU mentioned only as a one-liner (slide 9).

### Open items for George (non-blocking)
- Confirm the "20–30 min" runtime wording and the final talk title/date for slide 1.
- Optional "What's next" slide — supply items if wanted.
- Any hardware photos or real-sky before/after corner-star images to feature (strong addition to slide 29).
