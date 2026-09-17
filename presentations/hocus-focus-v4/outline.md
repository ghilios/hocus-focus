# Hocus Focus v4 — talk outline

Living outline for the TAIC talk **"Hocus Focus v4 — Every star matters"** (35 slides, ~30 min of slides + ~5 min
live demo + question pauses). Deck source lives in `build/`; full plan: `plans/hocus-focus-v4-presentation-plan.md`.

- Rebuild: `cd build && npm install && node build.js` (add `--skip-assets` to reuse `assets/`). The build log lists
  which slides still use DRAFT doc images or placeholders.
- Render to PNG for review (uses Windows PowerPoint): `build/render.sh "Hocus Focus v4.pptx" render/v4 ["5,8,11"]`.
- Slide 25 (demo backup) is hidden; unhide it in PowerPoint if the live demo misbehaves.
- Drop captures into `screenshots/` with the exact file names below; they replace the placeholder / DRAFT image on
  the next build. Nothing else needs editing.

## Design rules (so edits stay consistent)

- Dark night-sky theme. **Purple** = default accent · **pink** = something *you* do / emphasis · **amber** = the SNR
  thread **only** (title tagline, the SNR pill on 7/17/23/26/27, the bridge bar on 34) · green = done/pass marks.
- The big filled pill shape is reserved for the SNR token. Two panel styles: neutral, and accent-bordered.
- No text under 14 pt (placeholder labels excepted). Content stops at y = 6.4 in on slides with a docs chip.
- Titles are messages in sentence case; feature names go in the small lavender kicker.
- Screenshots go in white rounded cards; explanatory diagrams are drawn for the dark theme (`build/assets.js`) with
  native PowerPoint labels — no matplotlib figures with small print.

## Slides

| # | Title | What's on it |
|---|---|---|
| 1 | Hocus Focus v4 — Every star matters | Title, logo |
| 2 | Where we left off | The 2023 "Future Work" slide with ✔ on the three tilt promises |
| 3 | What's new in v4 | Three chapter cards |
| 4 | 01 Fewer decisions | Chapter divider |
| 5 | "There are too many settings" | Wall of settings, "40+", good news #1: all documented (QR → settings docs) |
| 6 | Good news #2: you don't have to set them | 📷 `optimizer-start` + "remember these two" switches |
| 7 | So why are there so many settings? | **SNR** planted. "Hold that thought." |
| 8 | It tries a few hundred combinations for you | Loop diagram + search-hill art |
| 9 | You get a tighter curve, and advice | 📷 `optimizer-summary` + three outcomes |
| 10 | It's a lot of computing | "20–30 min"; another computer · export/import · GPU; 📷 `settings-import` |
| 11 | 02 More stars, better curves | Chapter divider — pause for questions |
| 12 | Out-of-focus donut stars are no longer thrown away | Donut art; 📷 `donut-frame-off` / `donut-frame-on` |
| 13 | One brightness threshold can't fit the whole frame | Frame + row-trace art with legend (adaptive binarization) |
| 14 | Real focus curves aren't symmetric | Lopsided-curve art (Hybrid Best Fit) |
| 15 | See what autofocus saw, frame by frame | 3 steps; 📷 `af-review-controls`, `af-review-good` |
| 16 | Spot the bad frame | 📷 `af-review-bad` centered, statements either side |
| 17 | The optimizer chases a precise best focus | **SNR** returns. "Autofocus is the easy case. Tilt is not." |
| 18 | 03 Flat sensors | Chapter divider — pause for questions |
| 19 | The sensor model, in one minute | Tilt + curvature = your sensor; real 3D surface |
| 20 | Which screw, which way, how far? | Tilt Adapter Calibration Wizard setup; 📷 `wizard-setup` |
| 21 | Turn a screw, watch the tilt plane move | 5 (+2 optional) step timeline; 📷 `wizard-step` |
| 22 | A measured map of your adapter | 📷 `wizard-result`; the two outcomes |
| 23 | In a sensor model, every star matters | **SNR** punchline: one curve vs hundreds; 3-step pipeline; credit Steve Smith |
| 24 | LIVE DEMO | Sensor model on a saved AF run (script in notes) |
| 25 | One fitted sensor model *(hidden demo backup)* | One fitted model, three takeaways |
| 26 | That's why there are so many settings | **SNR** callback: every optical system is different; four jobs; low focus error for everyone |
| 27 | "Signal Amplification": more frames, more signal | **SNR**: normal vs 2× sweep art |
| 28 | Now it tells you how far to turn each screw | 📷 `guidance` with numbered markers |
| 29 | Tilt and backfocus are separate fixes | Side-view art + pass/fail graphic |
| 30 | Measure, turn, re-measure: watch the tilt converge | 📷 `tilt-history` |
| 31 | With an ASG EAT, calibration runs itself | 📷 `eat-connection`; thank-you to Josh Jones (ASG) |
| 32 | Then it corrects tilt and backfocus for you | 📷 `eat-review-commands` |
| 33 | Rehearse all of it indoors | 📷 `sim-frame-focused`, `sim-frame-defocused`, `sim-tilt-panel` |
| 34 | Why every star matters | Three chapters + SNR bridge; docs QR |
| 35 | Thank you / Questions? | Docs QR + URL, contacts, credits (Steve Smith, Josh Jones/ASG, community) |

## Screenshot shot list

**The single biggest risk to legibility is a full NINA window shrunk onto a slide.** Capture at 100 % display scaling,
then **crop tight to just the controls named below** — UI text should end up ≥ 12 px tall when the slide is viewed at
1600 px wide (the `optimizer-summary` and Signal Amplification crops in the draft are the model). PNG, no hover
tooltips. All of these can be produced indoors with the simulators.

| File (in `screenshots/`) | Slide | Crop to… | Box shape |
|---|---|---|---|
| `optimizer-start.png` | 6 | The populated part of the wizard start page (source, objectives, GPU toggle) — not the empty lower half | ~2:1 |
| `optimizer-summary.png` | 9 | Curve + Current/Optimized toggle + focus-precision line, from a run with a visible improvement | ~2:1 |
| `settings-import.png` | 10 | The import confirmation table (Setting / Current / Imported) | 4:3 |
| `donut-frame-off.png`, `donut-frame-on.png` | 12 | The *same* region of a heavily defocused frame, annotated, donut detection OFF vs ON | 3:2 each |
| `af-review-controls.png` | 15 | Only the three controls under the AF chart | wide strip ~3:1 |
| `af-review-good.png` | 15 | Review Frames window on a healthy frame | 16:10 |
| `af-review-bad.png` | 16 | Roughly square crop of a problem frame: the image with its annotations + the Detected stars / HFR readout (e.g. Detected stars: 0) | ~1.2:1 |
| `wizard-setup.png` | 20 | Device preset row through Turns applied per screw (include the Device row so the presets statement maps to it) | very wide ~4:1 |
| `wizard-step.png` | 21 | One step's instruction sentence + the screw diagram | ~2:1 |
| `wizard-result.png` | 22 | Finished calibration: screw angles + diagram + Measured Adapter Hardware | ~4:3 |
| `guidance.png` | 28 | Tilt Adapter Guidance: legend line, arrow rows, Tilt/Backfocus/Total rows — with a real correction (≈0.5–1.5 turns), a non-empty Backfocus row, and **no warning banner**. Markers on the slide sit at 10 % / 42 % / 80 % of the card height (legend / arrows / numbers); nudge in `ch3.js` if your crop differs | very wide ~5:1 |
| `tilt-history.png` | 30 | Tilt Measurement History after 2–3 measure/adjust rounds | 16:10 |
| `eat-connection.png` | 31 | Motorized Device Connection + motor-positions grid (Simulator port is fine) | ~4:3 |
| `eat-review-commands.png` | 32 | The "Review motor commands" dialog | ~3:2 |
| `sim-frame-focused.png`, `sim-frame-defocused.png` | 33 | Simulator frames, same region | 4:3 each |
| `sim-tilt-panel.png` | 33 | Simulator Tilt Adapter panel showing `✓ ≈ flat` | 4:3 |

Optional: real before/after corner-star crops for slide 30; a hardware photo of the ASG EAT for slide 31 (only images you
own or have permission to use).

**Live-demo prep (slide 24):** pick a saved AF run with a dense field and visible tilt, confirm *Sensor Curve Model
Enabled* is on, widen the Aberration Inspector dock, and time one dry run.

## Open items

- Confirm the "20–30 min" wording (slide 10) and "40+ settings" (slide 5; the Advanced list shows 41 rows).
- Date/venue line for slide 1. Optional "What's next" slide before the close.
