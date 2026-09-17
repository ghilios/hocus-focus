# TestApp CLI — Headless Diagnostics & Optimizer Harnesses

Read this when you need to run TestApp to diagnose or tune the star detector / optimizer **without launching NINA** — the `contamination`, `optimize`, `review`, and `diagnose-labels` subcommands. For the contamination algorithm itself, see `star-detection-internals.md`.

All subcommands load the user's real NINA profile and build params through `HocusFocusStarDetection.BuildStarDetectorParams` (the single options→params source of truth). They are **read-only with respect to the profile/options** (they never call a settings setter or touch the options accessor — NINA auto-saves the active profile, so mutating options would silently rewrite the user's settings).

Build first:
```bash
cmd.exe /c "dotnet build Joko.NINA.Plugins\TestApp\TestApp.csproj -c Debug --nologo"
```

## Detection parity — every runner must detect on the image the app detects on

**Non-negotiable.** A headless result is only worth anything if it predicts what a user sees, so every detecting
runner loads `DiagnosticUtil.LoadRenderedImage` and detects through `StarDetector.Detect(IRenderedImage, …)` (or
the plugin's own `RunEvaluationLoader.HocusFocusSplitFrameDetector` on the optimizer/AF-fit paths). Spec:
`docs/headless-detection-parity-design.md`.

**Why it matters.** For a **bayered** run the live app CFA hot-pixel filters and debayers *inside* `Detect`, at the
caller's params. Detecting on the raw Bayer mosaic instead moved `bobp`'s landed `Sensitivity` from the wizard's
`10.000` (27–31 min stars) to `0.0` at the search floor (52) — the harness could drive the gate to its floor and
harvest unfiltered hot pixels as faint stars, an incentive that does not exist on the filtered image. **4 of 22**
bank runs are bayered (`SorenVance`, `bobp`, `bobp_m101`, `timmer`); the other 18 are mono and byte-identical
either way.

Rules, in order of how easy they are to get wrong:

- **Never CFA-filter or debayer at load time.** The representation is *params-dependent*: `HotpixelThreshold` and
  `HotpixelThresholdingEnabled` are **searched optimizer axes**, and a load-time filter silently turns them into
  no-ops (the probe watched the optimizer keep searching `0.0005 → 0.0015` against an image it could no longer
  affect). Let `Detect` decide.
- **Never use the "pre-filter, then set `HotpixelFiltering = false`" pattern.** It is not equivalent: live sets
  `hotpixelFilterAlreadyApplied = true`, which the pre-filter pattern cannot, so `StarDetector.cs:549` runs a
  **spatial** hot-pixel filter on the structure-detection source that live never applies — different structure map,
  different candidates, and a clobbered `metrics.HotpixelCount`. `Detect(Mat, …)` hard-codes that flag false too.
- **`saveLumChannel` must stay `false`** on every detection path (`RenderedImageLoading.ForDetection` pins it).
  `true` flips `CvImageUtility.ToOpenCVMat`'s guard and makes the hotpixel-filtering-OFF branch read luminance
  where live reads the mosaic. It belongs only to display helpers.
- **Not for detection:** `DiagnosticUtil.LoadFloatMat` returns the frame exactly as stored (the mosaic, for a
  bayered frame) and exists for the `.tif` carve-out — NINA's TIFF decoder normalizes by `1<<16` vs
  `ushort.MaxValue`, and a TIFF has no CFA anyway. `LoadDebayeredFloatMat` is the same debayer **without** the CFA
  filter, for surfaces a human/LLM reads (golden tiles, annotated overlays) and for `export-linear`, whose
  detector-**independent** blind spots are the point.
- `HeadlessDetectionParityGuardTests` enforces the above at the source level; `HeadlessDetectionParityTests` pins
  the mechanism (mono byte-identity, `SaveLumChannel == false`, the hot-pixel axis still biting).

**Costs, both inherent.** A bayered run holds an extra `Rgb48` `BitmapSource` (~+20% peak working set;
`bobp_m101` 6.0 → 7.2 GB). `bobp`'s optimize phase went 87.6 s → ~300 s, and `tilt`'s 4-corner phase ~20 s → ~215 s
per step (fixed params, five regions = five early keys, so one frame is filtered+debayered five times), because
the CFA filter + debayer now run per early-context build rather than once at load — **do not "optimize" that
back**, it is the same work the live wizard does, and hoisting it out of the loop is the load-time filtering the
spec rejects. The legitimate speedup, if it is ever needed, is caching the prepared source image per (frame,
hot-pixel params) inside `StarDetector`.

**Per-filter caveat.** With per-filter star detection enabled, live resolves the captured filter's snapshot and a
headless run cannot (it has no filter wheel, and seeding one would write to the profile). The runners warn to
stderr when the profile has the feature on; their numbers are then profile-level, not what the app would use.

## Contamination diagnostic

`TestApp` doubles as a self-contained, headless diagnostic for the contamination test. It runs detection with per-star diagnostics enabled and `RejectContaminatedStars=false` (so contaminated stars are retained for analysis).

**Run it** (WSL interop runs the Windows `.exe` directly, so paths with spaces quote cleanly):

```bash
./Joko.NINA.Plugins/TestApp/bin/Debug/net8.0-windows7.0/TestApp.exe \
  contamination --image "C:\path\to\image.xisf" --out "C:\temp\hf-diag"
```

- Args: `--image <path>` (req; `.xisf`/`.fits`/`.tif`), `--profile-id <guid>` (default: active profile),
  `--out <dir>` (default `%LOCALAPPDATA%\NINA\Logs\hf-diag\<timestamp>`), `--sensitivity <double>` (override),
  `--sensitivity-sweep <a,b,step>` (per-value CSVs → `sweep.csv`).
- No `--image`/`contamination` arg ⇒ TestApp launches its normal WPF GUI instead.

**Outputs** (in `--out`): `contamination_stars.csv` (one row per accepted star — center, HFR, background
(plane value at center), σ used, `ContaminationSuspected`, gradient-robust fields `GradientSlope`/
`LocalSigmaResidual`/`MaxSectorResidualOverSE`/`ResidualTrippingSector`, per-octant `resid*`/`residCount*`,
and per-star shape/proximity `Eccentricity`/`FWHMx`/`FWHMy`/`FWHMPixels`/`ThetaDeg`/`NearestNeighborDist`/
`NearestNeighborOverHfr`/`HasCloseNeighbor`/`PsfFitOk`); `contamination_summary.txt` (settings + flag rate +
a flagged-vs-clean profile + `MaxSectorResidualOverSE` distribution); `contamination_annotated.png` (green =
clean, magenta = flagged-with-close-neighbor, cyan = flagged-isolated); `gr_sweep.csv` (flag rate + flagged
set's median gradient slope/eccentricity vs sensitivity, from a single run); plus verbose TRACE in
`%LOCALAPPDATA%\NINA\Logs`.

**How the diagnostics hook works (off by default, zero overhead):** set
`StarDetectorParams.CollectContaminationDiagnostics = true` and read
`HocusFocusStarDetectorResult.ContaminationDiagnostics` (a `List<ContaminationDiagnosticRecord>`). When the
flag is false the detector fills no per-sector residual arrays and skips the diagnostic record (the plane fit
+ decision always run, since they are the production background/contamination path).

## Per-star measurement probe (`star-probe`)

Use when a star's HFR or FWHM looks wrong and you need to know *why*: it dumps every quantity each measurement
was derived from, which `contamination`'s CSV does not carry. Worked example:
`docs/saturated-star-fwhm-investigation-results.md`.

```bash
dotnet Joko.NINA.Plugins/TestApp/bin/Debug/net8.0-windows7.0/TestApp.dll star-probe \
  --image "C:\path\frame.xisf" --settings "C:\path\harness_settings.json" \
  --out "C:\temp\probe" --near 4707,3262 --radius 260 --psf-sweep
```

**Pass a HARNESS settings file, not a plugin export** — see `convert-settings` below for why an export is
silently ignored.

- Args: `--image` (req), `--settings` / `--profile-id` (same pinning rules as every harness runner), `--out`
  (default `%LOCALAPPDATA%\NINA\Logs\hf-diag\star-probe\<timestamp>`), `--near <x,y>` + `--radius <px>`
  (default 250) to select the console table, `--top <n>` (default 25; brightest first), `--psf-sweep`.
- Detects through `DetectionSource` like every other runner, forces `ModelPSF` on, applies the options'
  `DetectionBinning` the way `ApplyDetectionImageContext` does, and takes the pixel scale from the frame header.
- **`star_probe.csv`** (one row per accepted star): centre, structure box, background, peak, HFR and the HFR
  aperture (`min(boxW, boxH)/2`), the full PSF fit (FWHM px/arcsec, sigmas, theta, eccentricity, R^2, reduced
  chi^2, fitted amplitude and background, beta), the PSF sampling step, and a census of the PSF sample grid —
  how many samples the saturation mask dropped, and saturated pixels in the box.
- **`--psf-sweep`** refits each selected star at `PSFResolution` 30/60, without the saturation mask, and over a
  halved box, using the detector's own measurement noise sigma. Its `base` column must reproduce the detected
  `fwhmPx`; if it does not, the reconstructed measurement image is wrong and the other columns mean nothing.
- **Unbinned mono only for the census and the sweep.** They run against a reconstruction (display Mat + the same
  hot-pixel filter). A bayered frame is CFA-filtered before its debayer and a `DetectionBinning > 1` frame is
  fitted in binned pixels, so for those the census columns are left empty and the sweep is skipped.
- The amplitude bound shows up here: a fitted `psfPeak` of exactly `2` (with `psfBackground` 0) is a fit pinned
  at its limits, not a measurement.
- **The rejection census** (candidates, and the per-gate rejection counts) prints under the detected count, so a
  change in detected stars can be attributed to a gate rather than guessed at.
- **`--hotpixel-census`** reports, for the RAW measurement image, how many pixels sit above the local background
  by 3/5/8 local sigmas and how many of those are isolated at ratios 3/2/1. That is the population the
  measurement path's isolation repair acts on; it costs a full-frame scan, hence the flag. The
  "reconstruction repaired N" line must equal the detector's own `measurementRepaired`.

## `inspect-align --sensor-diagnostics`

Dumps the sensor model's own intermediates so a before/after pair can be compared at the level the fit actually
consumes, rather than at its reported tilt angle:

```bash
TestApp inspect-align --runs "D:\Autofocus Bank\Panos\attempt01" --params default \
  --sensor-diagnostics "C:\temp\sensor" --sensor-diagnostics-label panos_after
```

Writes `<label>_stars.csv` (one row per registered star, its sweep fit and how it entered the data-point list),
`<label>_points.csv` (the paraboloid's `x_um, y_um, z_um, sigma_um, predicted, residual, enabled`) and
`<label>_iterations.csv` (the winsorized solve's trajectory). Purely observational — the fit is bit-identical
with the seam on. This is what settled whether a change had moved the recovered tilt or only the harness.

## Pin `--settings` on EVERY arm of a comparison

`HarnessSettingsStore` falls back to a DEFAULT PATH when `--settings` is absent, and that path resolves per
machine and **per binary directory**. Two arms of a before/after run launched from two build outputs therefore
read two different files, silently, each printing a plausible `Settings: <path>` banner. It cost a whole
`bank-verify` before/after pair here: the before arm read a 68-key bag exported from profile `astrodet`, the
after arm a 44-key bag from `Default`, and the two reports also recorded different `profileId`s. The visible
symptom was a "uniform 4x" change in the sensor model that did not exist.

- **Always pass `--settings <one file>` to every arm**, and check the `Settings:` banner and `profileId` agree.
- `bank-verify` records `settingsPath`, `settingsPinned` and the profile in its JSON and report header, prints a
  comparability line, and warns on stderr when `--settings` was omitted. **Two reports are comparable only when
  those lines match.**
- `fitInputs` is NOT sufficient: it carries four AF-fit values that can agree across two different files.

## Plugin settings export -> harness option bag (`convert-settings`)

**The trap this exists for.** A harness `--settings` file is `{ Options: { "PSFResolution": "20", ... } }` —
a flat bag of plugin option keys. The plugin's own settings EXPORT (the file the Star Detection options page
and the per-filter store write, `fileType: HocusFocusStarDetectionSettings`) nests typed values under
`starDetection`. Newtonsoft deserializes an export into the harness shape **without complaining** and yields
an EMPTY bag, so the run silently uses stock defaults while appearing to honour the file. On the frame in
`docs/saturated-star-fwhm-investigation-results.md` that was the difference between 1737 and 1615 detections.

```bash
TestApp convert-settings --import "C:\path\O_settings.json" --out "C:\temp\harness_O.json" \
  [--set PSFResolution=20] [--set UsePSFAbsoluteDeviation=True]
```

Goes through `StarDetectionOptions.ApplyImportedSnapshot` (the Import button's own path), so the key mapping
cannot drift. `--set` overrides a key in the produced bag, which is how one arm of a comparison pins a single
knob. It prints the keys that matter most so you can eyeball the result.

**Only keys that DIFFER from a freshly constructed `StarDetectionOptions` are written** (the accessor records
a key when its setter fires). Absent keys fall back to the build's own default — so when you produce a file
with one build and read it with another whose defaults differ, spell the affected keys out with `--set`.

## PSF modelling across the AF bank (`psf-bank`)

One nearest-focus frame per bank run, detected with `ModelPSF` forced on, at the SHIPPED defaults
(`BuildDefaultStarDetectorParams`) rather than a user's saved options — the question it answers is what a
default install produces.

```bash
# 1. pick the frames once and pin them, so a before/after pair scores the identical frames
TestApp psf-bank --runs "D:\Autofocus Bank" --out "C:\temp\psf" --select
# 2. measure
TestApp psf-bank --runs "D:\Autofocus Bank" --out "C:\temp\psf" --manifest "C:\temp\psf\psf_bank_manifest.json" --label after
```

- **`--select`** detects EVERY frame of every run with `ModelPSF=false` and writes `psf_bank_manifest.json`
  naming the lowest-median-HFR frame per run. Pin it: the change under test usually MOVES HFR, so re-selecting
  per arm would compare different frames.
- **`psf_bank_<label>.csv`** (one row per run): detected, PSF accepted, `PSFFitFailed`, saturated, HFR
  median/MAD, FWHM median/MAD/p05/p95/max (px and arcsec), eccentricity, R^2, `scatterSD`, and the timings.
- **`scatterSD`** is the residual SD of per-star FWHM about a least-squares quadratic surface in (x, y) — the
  measurement-noise term with the field's optical structure removed. It is the metric Part 3 of the saturated-star
  investigation is scored on.
- **Timing:** each frame is detected three times — a warm-up, then `ModelPSF=false`, then on — and `psfMs` is
  the difference. `PSFResolution` squares the sample count per star, so this is a real number to watch.

## Star Detection Optimizer harnesses (`optimize` / `review` / `diagnose-labels`)

The Star Detection Optimization Wizard ships with `TestApp` subcommands that drive the **same**
`StarDetectionOptimizer` the live wizard uses, so the optimizer can be exercised/tuned offline. They load the
user's real NINA profile and, mirroring the wizard, build two param bundles (each with the AF overrides
`ModelPSF=false`, `Region=Full`, `SaveIntermediateFilesPath=""`, `PixelScale` from the profile × binning=1 for raw
Mats): the optimizer **seed** = fully-default params (`BuildDefaultStarDetectorParams`, the wizard's
`LoadedRun.Seed`), and the **baseline** = the user's current settings (`BuildStarDetectorParams`, the wizard's
`LoadedRun.Baseline`). The search starts from the default seed; improvement (J, σ_focus, curated-param deltas, the
`optimized_settings.json` `BaselineJ`) is reported **vs the current-settings baseline** — exactly the wizard's
"vs current" display.

> **Perf:** detection is split into a cacheable EARLY context (`BuildDetectionContext`) + a cheap LATE
> `GateAndMeasure`; `RunEvaluationData` caches the early context per (frame, early-key) and reuses it across
> late-only candidate moves, plus bounded parallel per-frame detection. **Bit-identical, ~10–13× faster**, and
> it benefits BOTH the live wizard and replay (both converge on `RunEvaluationLoader` → `RunEvaluationData` →
> `HocusFocusSplitFrameDetector`). See `docs/star-detection-optimizer-performance-design.md`.

### `TestApp.exe optimize`

Headless driver of the optimizer. Args:
`--runs <folder>` (required), `--per-run` (flag), `--profile-id <guid>` (default active), `--out <dir>`
(default `%LOCALAPPDATA%\NINA\Logs\hf-diag\optimize\<timestamp>`), `--max-evals <int>` (override the
optimizer budget; wizard default 250), `--annotate extremes|all` (default `extremes` = min/max-focuser frames
only), `--labels <dir>` (label JSON dir; activates the recall/precision objective term), `--verbose` (restore
TRACE logging; default INFO), `--cv-threads <n>` (cap OpenCV's parallel-for pool; `0` restores the default —
the effective `Cv2.GetNumThreads()` is printed either way, so the knob cannot be silently disconnected).
`optimize` has **no** `--defocus-*` switches — the combined `DefocusAwareGates`
flag is in the optimizer's curated search set (`OptimizerVariable.CreateCuratedSet`), so the optimizer explores
the relaxation itself (guarded by the objective's `SDefocusPrecision` near-focus penalty); to force the gates
on for diagnosis, use `diagnose-labels`/`contamination`. It writes
`optimized_settings.json` into **each focus run's source folder** (the review handoff) **and** the `--out`
dir (or each per-run subfolder).

> ### PINNING AN ARM: `--settings` **AND** `--profile-id`. BOTH. EVERY TIME.
>
> **`--settings <fixed path>` pins the DETECTOR knobs** (F42). Every build directory otherwise bootstraps its own
> `harness_settings.json` from whatever the live profile holds at that moment, so two arms built minutes apart can
> run different detectors.
>
> **`--profile-id <guid>` pins the FIT, and for six waves nobody passed it** (F57/F58). `AutoFocusOptions` was read
> from whichever NINA profile happened to be ACTIVE, and four of its values reach the AF fit. On the machine that
> produced waves 5–11, nine profiles partition **2 / 7** on `MaxOutlierRejections` alone — enough to move
> `BaselineJ` (one evaluation of a fixed seed on fixed frames, no search) by **0.0144**, larger than any Δ`J` the
> project has argued about. Wave 9's gate ran under `Default`, wave 10's under `astrodet`, and the resulting
> discrepancy was attributed to a wavelet change for half a day.
>
> **The default is LRU-BY-LAST-LOAD, so an unpinned arm is seeded by whatever the previous arm pinned.**
> `Profile.Load` stamps `LastUsed = Now` and saves; `TryLoad("")` takes the newest. The act of measuring rewrites
> the default for the next measurement.
>
> **And concurrent unpinned processes each get a DIFFERENT profile.** NINA holds the `.profile` open
> (`FileShare.Read`) while it is loaded, `SelectProfile` returns false for a locked one, and `TryLoad`'s
> `SkipWhile` silently takes the next by `LastUsed`. That is F55's "nondeterminism": *N* concurrent `optimize`
> processes ran under *N* different fits. **With `--profile-id` the same situation fails LOUDLY** ("No active NINA
> profile could be loaded", non-zero exit) instead of returning a wrong number — which is the right trade, and is
> why fan-out needs one profile copy per worker.
>
> Since wave 11 the harness reads the fit inputs from the **pinned settings file**, and every landing records
> `ProfileId` **and** `FitInputs` (`MaxOutlierRejections=…;OutlierRejectionConfidence=…;…` — values, not a hash,
> so a reader sees *which* one moved). **`FitInputs` matching is NOT proof of comparability**: it is four values,
> and two genuinely different settings files can agree on all four. Check `ProfileId` and the settings path too
> — see "Pin `--settings` on EVERY arm of a comparison" above. Since wave 12 **every** harness runner does — `bank-verify`,
> `synth-validate`, `inspect-align` and `tilt` build their fit through
> `HarnessSettingsStore.BuildFitOptions`, print `FitInputs`, and a unit test fails the build if any `TestApp`
> source constructs `AutoFocusOptions` from the profile again.
>
> **`ConcurrencyCheck` MUST BE READ ACROSS A WHOLE ARM, NOT OFF ONE LANDING.** `WaitOne(0)` is won by exactly one
> of *N* contenders, so in any fan-out precisely one landing truthfully reports `exclusive`. One `concurrent`
> anywhere condemns the arm; one `exclusive` proves nothing.
>
> ### FAN-OUT: AUTHORISED AT DEGREE 4, UNPINNED ONLY — AND IT BUYS 25 %, NOT 4× (wave 12)
>
> Wave 12 ran the eight gate runs at **fan-out 4** with four workers on **four different profiles** split 4/4 on
> `MaxOutlierRejections`, and all eight landings reproduced the sequential values **bit-identically** (F55's
> RULE A12). So:
>
> - **Fan-out is authorised AT DEGREE 4.** Nothing was measured at 8 or 48.
> - **UNPINNED only.** `--profile-id` + fan-out still fails loudly, so a fanned-out arm relies on `--settings`
>   carrying the fit inputs. **An arm that needs a specific profile still runs sequentially.**
> - **It buys 1.33×, not 4×** (F60). Every run takes 1.45–2.55× longer under contention because `optimize`
>   already saturates the machine. **Sequential remains the default;** fan-out is for a pass long enough that
>   25 % of the wall clock is worth losing `--profile-id`.
> - **Re-snapshot the profile set before any fan-out arm.** The fall-through set is the top *N* by `LastUsed`,
>   and every pinned run reorders it — including the gate you just ran.

- **Run discovery is attempt-anchored** (pure logic in `OptimizationRunDiscovery`): it recursively finds
  `attempt<NN>` folders (1–4 levels under `--runs`) that contain ≥3 distinct focuser positions, mirroring
  `RunEvaluationData.MinPositionsForFit = 3`. Single-frame `final`/`initial` validation folders and frameless
  `attempt` folders are skipped (recorded with a reason, not silently dropped). Back-compat: pointing `--runs`
  directly at an `attempt01` folder works. Fallback: if no `attempt*` folders exist anywhere, each immediate
  subfolder with ≥3 positions is a run. Frame filenames match `0_Frame1_BitDepth16_Bayered0_Focuser5000.fits`.
- **Default = joint** optimization: all discovered runs are optimized together (N=1 reduces to a single run;
  N>1 is the balanced blend). **Only group runs from the SAME optical setup** — a joint objective across
  different cameras/scopes is meaningless. **`--per-run`** optimizes each discovered run independently, writing
  one subfolder per run plus a top-level `aggregate_summary.txt` (one scannable row per run: load OK/failed,
  hard-floor PASS/FAIL with min star count, seed→best J, σ_focus, recommended step, changed params). Use
  `--per-run` to verify across a bank of many different setups in one command.
- Detection deliberately scores the **full accepted-star set** (NumberOfAFStars=0 — no brightest-N trim),
  matching the wizard's `RunEvaluationLoader` (HFR aggregation at the `HocusFocusDetectionParams` defaults,
  high=4.0 / low=3.0; this is the loader path, NOT the live AF path). The whole-frame detection also keeps the
  harness useful for sensor-modeling work.
- Outputs (in `--out`, or per-run subfolders): `optimize_summary.txt` (seed→optimized `J`, per-run σ_focus /
  R² / reducedχ², recommended step size, curated params old→new with `*` markers, hard-floor check, per-frame
  star counts), `optimize_result.csv`, `optimize_trajectory.csv` (bestJ vs eval# — one row per accepted move; the
  convergence curve for eval-budget analysis), and stretched annotated PNG(s) (accepted = green circle + HFR; rejected
  color-coded by reason from `StarDetectorMetrics.*Bounds`; **a real star with no marker = missed entirely**);
  `--per-run` also writes `aggregate_summary.txt`. Verbose TRACE in `%LOCALAPPDATA%\NINA\Logs`.

```bash
./Joko.NINA.Plugins/TestApp/bin/Debug/net8.0-windows7.0/TestApp.exe \
  optimize --runs "C:\Users\me\AppData\Local\NINA\AutoFocus" --out "C:\temp\hf-opt" --per-run
```

### `TestApp.exe review`

Interactive box-based labeling dev tool (WPF; produces labels only;
**read-only on the profile**). Args: `--runs <folder>` (required), `--labels <dir>` (default `<runs>\labels`;
read+written, feeds `optimize --labels`), `--params current|optimized` (default `current`), `--opt-results
<dir>` (folder holding `optimized_settings.json`), `--review low|uncertain|all` (default `low` = frames with
`< N_review` accepted stars; `uncertain` adds the defocused extremes; `all`), `--profile-id <guid>`.

- **`--params optimized` load priority:** (1) explicit `--opt-results <dir>/optimized_settings.json`, then (2)
  auto-discover `<runFolder>/optimized_settings.json` (where `optimize` wrote it), then (3) the profile
  snapshot `options.GetOptimizedSettings()`, else (4) fall back to `current`.
- Detects every queued frame once, shows MTF-stretched frames with accepted/rejected-by-reason overlays (colors
  match `optimize`'s PNG legend), zoom/pan, and **thick, zoom-invariant** markers. **Three box-based label
  categories:** **missed** (false negative → recall) = drag a box on a real star with no marker;
  **should-reject** (false positive → precision) = click an **accepted** box; **wrongly-rejected** (recall) =
  click a **rejected** box. Labels reload incrementally (re-running merges).
- The label JSON is consumed by `optimize --labels <dir>` to activate the objective's recall/precision term
  (recall/precision are scored by **box containment**). One file per run (`<runId>.json`, or any `*.json` whose
  embedded `runId` matches a discovered run); each labeled box carries a bounding box (legacy point-only files
  auto-load with a default box).

### `TestApp.exe diagnose-labels`

Classifies each labeled box against a fresh detection of the same frame to
find **which gate rejects** flagged stars. Per box: **ACCEPTED** (overlaps an accepted star) /
**REJECTED:<reason>** (overlaps a rejected candidate — reports the gate: TooDistorted, NotCentered, TooFlat,
LowSensitivity, Saturated, Degenerate, Contaminated) / **NO CANDIDATE** (no candidate formed there = a true
structure-detection gap, only fixable by detector-algorithm work). Args: `--runs` (required), `--labels`
(default `<runs>\labels`), `--params current|optimized`, `--opt-results <dir>` (same load priority as
`review`), `--profile-id`, `--out <dir>` (writes `diagnose_labels.txt`), plus opt-in defocus switches
(`--defocus-distortion` / `--defocus-centering` / `--defocus-size-ref` / `--defocus-min-factor`
/ `--defocus-center-factor`) that force the gates ON on the built params (`contamination` has the same set).
Read-only on the profile.

### The labeling loop

`optimize` (baseline) → `review --labels L` (drag-box misses / click false positives / click
wrongly-rejected) → `optimize --labels L` (re-optimize with the box-containment recall/precision term
active). Use `diagnose-labels` to attribute each labeled miss to a specific gate vs. a structure gap.

## Defocus-aware gates (Advanced options)

A single `StarDetectionOptions.DefocusAwareGates` option
(**opt-in, default OFF**, Advanced-only CheckBox + tooltip in `OptionsDataTemplates.xaml`) drives **both** the
distortion and centering relaxations together (it maps to the two `StarDetectorParams` fields
`DefocusAwareDistortion` + `DefocusAwareCentering` in `BuildStarDetectorParams`; TestApp keeps the two as
separate `--defocus-distortion`/`--defocus-centering` CLI flags). They relax the distortion / centering gates
for large candidates (large size = defocus proxy) to recover bloated/donut defocused stars. Default-OFF returns
the gates verbatim, keeping detection **bit-identical**. The three numeric knobs are now Advanced options too
(UnitTextBox + DoubleRangeRule + tooltip): `DefocusDistortionSizeReference` (30 px), `DefocusDistortionMinFactor`
(0.25), `DefocusCenteringToleranceFactor` (2.0). The combined gate is also a curated optimizer variable
(`DefocusAwareGates`), guarded by the objective's `SDefocusPrecision` near-focus precision penalty (multiplicative,
= 1.0 when no star is relaxation-admitted ⇒ objective bit-identical when off). See
`docs/star-detection-optimization-wizard-results.md` (F2/F3 + cache-health notes).

## Synthetic-camera render benchmark (`bench-simrender`)

Times the simulator's render pipeline on a real ASTAP star field, and reports the PSF kernel-cache
cardinality — the quantity that actually grows when the cache key gains axes.

```
TestApp bench-simrender [--catalog "C:\Program Files\astap"] [--field dense-wide,dense,sparse|all]
                        [--defocus-steps 0,150,350] [--aberr A0,A1,A2] [--arms off,on-zero,on,on-strong]
                        [--corner-astig 15] [--corner-astig-strong 40] [--limit-mag 17] [--exposure 5]
                        [--iters 5] [--warmup 1] [--census] [--kernel-ladder] [--with-detection]
                        [--csv <path>]
```

**Read `kernelGen` and `kernels`, not just `total`.** Development is 50–90 % of a 61 MP render, so the
wall clock is an insensitive instrument for anything the PSF does.

Sub-modes, cheapest first:

- `--kernel-ladder` — times `PsfKernelGenerator` alone across R = 8…240 px and fits the log-log scaling
  exponent, circular vs elliptical. Seconds, no catalog needed. **Run it first** after any change to kernel
  generation: a separable convolution holds ~2, a direct 2-D one shows ~4.
- `--census` — star and kernel counts with no timing. Use it to check a pointing is as dense as intended
  before spending eight minutes on the matrix.
- (default) the full timing matrix, ~8 min at `--iters 5 --field all`.
- `--with-detection` — runs a real `StarDetector.Detect` loop alongside every timed render, emulating the
  contention a render actually meets in NINA (the camera prefetches the next frame while the previous
  autofocus point is still being detected, both through the shared CPU governor).

Fields are named pointings + optics on the QHY600/IMX455: `dense-wide` (γ Cygni at 530 mm f/5, ~35k on-frame
stars — the headline), `dense` (same sky at 1000 mm), `sparse` (North Galactic Pole, identical optics to
`dense` so only the star count differs), `dense-oversampled` (2000 mm f/8, the kernel-radius stress). At
1000 mm a 61 MP frame covers only 2.8 sq deg and the G18 catalog stops at mag 18, which is why the dense
field is the widefield one.

Arms: `off` (isotropic), `on-zero` (enabled at ratio 0 — a **verification** arm that must measure identical
to `off`), `on` (shipped ratio), `on-strong` (stress). Aberration configs `A0` clean / `A1` backfocus only /
`A2` tilt + backfocus.

**Run in Release**; the banner warns otherwise and Debug numbers are not comparable. Timing lives here rather
than in the unit suite because it needs the ASTAP database and is flaky by construction; what the suite
guards instead is kernel-cache cardinality and byte bounds
(`StarFieldCompositorTests.Render_KernelCacheCardinality_StaysBounded`), which is what actually regresses.

Results and the gate analysis: `docs/camera-simulator-astigmatism-results.md`.
