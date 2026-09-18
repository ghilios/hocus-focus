# Optimizer harness/app parity — remaining divergences

## Context

`TestApp optimize` (the offline harness) and the in-app **Star Detection Optimization Wizard** are supposed
to be the same optimizer. Where they are not, harness numbers stop predicting what a user sees, and the
bank's `optimized_settings.json` — which feeds `bank-verify`'s A/B columns — stops describing the product.

This document records what is still different, with file:line on both sides, so the work can be picked up
without re-deriving it. It is **evidence, not a plan**; the plan comes when the work is scheduled.

## Two recorded premises that are false

Both were believed going in and both were checked rather than argued:

- **The `bobp` evidence is an already-fixed bug.** The recorded symptom (wizard landed Sensitivity `10.0`,
  headless landed `0.0` from the same baseline `15.667`) is recorded verbatim in
  `Tests/StarDetection/HeadlessDetectionParityTests.cs:22-28`. Cause: the harness detected the raw Bayer
  mosaic where the app detected CFA-filtered luminance. Fixed 2026-07-29 (`653915b`, `d76d116`, `49804b0`).
  The memory describing it as open predates its own fix by hours.
- **The auto-stretch hypothesis is wrong.** `NINA.Image.dll` was decompiled: `DebayeredImage` declares its own
  `Stretch` override whose state machine ends in `newobj DebayeredImage::.ctor`, so
  `imagingMediator.PrepareImage(autoStretch: true)` returns an object that is *still* `IDebayeredImage`,
  carrying the same `RawImageData`, `SaveLumChannel == false` and the same `BayerPattern`.
  `StarDetector.PrepareSrcImageFromRenderedImage:324` therefore takes the identical branch either way. The
  stretch only ever touched the display `BitmapSource`.

The related asymmetry — `ImageControlVM.PrepareImage` passes `saveColorChannels: UnlinkedStretch` where
`RenderedImageLoading.ForDetection:67` hard-codes `false` — is inert for the same reason: it only selects
`StretchUnlinked` vs `Stretch` for the display source, and the CFA path rebuilds from
`RawImageData.Data.FlatArray` regardless (`StarDetector.cs:348-360`).

## What is NOT in question

The objective and the measurement are single copies, shared by both sides:

- `OptimizationObjective.JRun` / `JTotal` — one copy, in the plugin. TestApp has none.
- `RunEvaluationData` — the same type, constructed through the same split constructor on both sides.
- `RunEvaluationLoader.HocusFocusSplitFrameDetector` — the wizard's own detector, which the harness drives.
  Enforced by `Tests/StarDetection/HeadlessDetectionParityGuardTests.cs:112-121`.

**Every remaining divergence is upstream of J**: what pixels, what params bundle, what search budget.

## Fixed (2026-09-17, commit `b836fba`)

The wizard's Optimize step loaded frames through `imagingMediator.PrepareImage` while its own Review step used
`RenderedImageLoading.ForDetection`. Both now use the shared seam, so the frames a user inspects in Review are
by construction the frames the optimizer scored. Behaviour-preserving (see above). Dropping `IImagingMediator`
also makes `RunEvaluationLoader` constructible outside NINA, which is the precondition for everything below.
Guarded by `HeadlessDetectionParityGuardTests.TheOptimizersRunLoaderLoadsThroughTheSharedSeam`.

## Remaining divergences, ranked by how far they can move a landing

### 1. `DetectionBinning` is never taken from the pinned settings in joint mode

| | |
|---|---|
| App | `HocusFocusStarDetection.cs:576-577,583` — `DetectionBinning = ToFactor(options.DetectionBinning)`, and `PixelScale` carries the factor |
| Harness | `OptimizationDiagnosticRunner.cs:361-372` never assigns it; `BuildStarDetectorParams` doesn't either, so it keeps the field default `1` (`Interfaces/IStarDetector.cs:495`). The only `ApplyRunDetectionBinningIfRequested` call is at `:725`, inside `RunPerRun` — `RunJoint` (`:666-686`) never calls it. In per-run mode the factor comes from `synthetic_meta.json` / a per-run `harness_settings.json` (`HarnessSettingsStore.cs:439-457`), **not** from the `--settings` file the arm pinned |

At a user setting of Bin2 the app detects a half-resolution image at `PixelScale × 2`; the harness detects at
full resolution at `PixelScale × 1`. Every pixel-unit gate (`MinHFR`, `MinimumStarBoundingBoxSize`,
`NoiseReductionRadius`, `StructureLayers`) then sits in a different regime and star counts move by hundreds.
This is the largest lever, and the repo already knows it: `OptimizationDiagnosticRunner.cs:455-464` records a
prior wave reaching a **wrong verdict** by excluding exactly this field.

### 2. Donut master not stamped on the harness seed — different search space *and* budget

| | |
|---|---|
| App | `StarDetectionOptimizerWizardVM.cs:3952-3953` stamps `seed.DefocusAwareDonutDetection` from the effective options, `:3957` sets `MaxEvaluations = donutMaster ? 400 : 250`, `:3996` builds the curated set from that seed, so the defocus axes are searched (`OptimizerVariable.cs:118-119`) |
| Harness | Seed is `BuildDefaultStarDetectorParams()` with the flag `false` (`HocusFocusStarDetection.cs:376`), flipped only by `--donut` (`:381-385`); `ctx.Variables` frozen at `OptimizationDiagnosticRunner.cs:524`; budget stays 250 (`StarDetectionOptimizer.cs:33`) unless `--max-evals` |

With donut detection on in the profile the wizard explores ~9 extra axes over 400 evaluations and the harness
explores none over 250 — different dimensionality *and* 1.6× the budget.

### 3. PixelScale comes from a different rig

| | |
|---|---|
| App | profile `CameraSettings.PixelSize` / `TelescopeSettings.FocalLength` × frame `BinX` (`HocusFocusStarDetection.cs:563-564`) |
| Harness | harness settings file with `binning` hard-coded to 1 (`OptimizationDiagnosticRunner.cs:351-352`); the frame header's `XPIXSZ`/`FOCALLEN` in `--per-run` (`:817`, `HarnessSettingsStore.cs:323-340`) |

On foreign bank data the two can differ by >5×. **This one is arguably a deliberate deviation** — the frame
header is more correct than the local profile for someone else's rig — so the fix is to make it a *named,
logged* override on the shared loader, not to delete it. Note also that the harness comment at `:449-453`
calls PixelScale "inert by proof… consumed only inside the ModelPSF block"; that claim needs re-auditing,
since `StarDetectorParams.PixelScale` is also the carrier of the binning factor from #1.

### 4. MinHFR seeding (F35) triggers off a different fit

| | |
|---|---|
| App | `StarDetectionOptimizerWizardVM.cs:3975-3976` uses `seedFitVertexHfr`, set at `:3803` from the most recent analyze pass — for Replay, the pass at `:3146-3147` over the **fully-default seed** |
| Harness | `OptimizationDiagnosticRunner.cs:945-950` uses `perRunBaseline[0].BestFit.Minimum.Y` — the **baseline (current settings)** fit |

`MinHfrSeed.Resolve` lowers the seed's `MinHFR` below the fitted vertex, so one side can start with a lowered
gate and the other not. On an undersampled rig that is the difference between a scorable start and F20's
`J ≡ 0` plateau.

### 5. GPU decision uses a different policy overload

| | |
|---|---|
| App | `RunEvaluationLoader.cs:227-231` — the dimension-aware `ShouldUseForOptimization(enabled, width, height, out reason)`, which declines below `MinPixels` and on insufficient VRAM, **per run** |
| Harness | `OptimizationDiagnosticRunner.cs:408` — the 2-arg overload (`GpuAccelerationPolicy.cs:44-56`), no size gate, no VRAM gate, stamped **once per batch** before any frame is loaded |

On a sub-`MinPixels` frame the app runs CPU and the harness runs CUDA. The harness's own comment concedes
"GPU results differ from CPU at float-contraction level" (`:400-402`), and a pattern search accepts on strict
`>` comparisons, so a small delta is enough to fork a trajectory.

### 6. Per-filter settings resolution

App resolves the capture-time filter's snapshot (`HocusFocusStarDetection.cs:505-523`) and the wizard threads
the target filter's set as `baselineOptionsOverride` (`StarDetectionOptimizerWizardVM.cs:3223-3238`). The
harness stubs the store off entirely — `StubBehaviorSelectors.cs:171-174`, already self-documented at `:149`
as "a real divergence". Entirely different detector knobs when the feature is on.

### 7. Fit config comes from different places

Step size — app: `savedAttempt.StepSize`, or **the live profile's `FocuserSettings.AutoFocusStepSize`** when
that is 0 (`AutoFocusEngine.cs:3021`). Harness: always `InferStepSize`
(`OptimizationDiagnosticRunner.cs:827,1561-1564`). `StepSize` scales `SigmaFocus`
(`RunEvaluationData.cs:827-845`), which is `S_fit` in `JRun`.

### 8. Frame set: two regexes, two extension policies, two orderings

App: `IAutoFocusEngine.cs:55-85`, **any** extension, `GetFiles()` order. Harness:
`OptimizationRunDiscovery.cs:44-52`, a byte-identical copy of the regex text plus a `.fits/.fit/.xisf`
whitelist, ordinal-by-name. A name-matching non-frame artifact is loaded by one and skipped by the other, and
order picks `firstImage`, which sets `BinX`, `ExposureTime` and the whole image context.

### 9-11. Lower impact

`RecoveryStepsPerSide` never set by the harness (`RunEvaluationData.cs:295`) — inert for Replay, divergent for
Live. The app's seed-guard / `TryRescueSeedAsync` path (`StarDetectionOptimizerWizardVM.cs:3484-3499`,
`:3600-3634`) has no harness equivalent and can refuse outright where the harness happily optimizes.
`HighSigmaOutlierRejection` / `LowSigmaOutlierRejection` (`OptimizationDiagnosticRunner.cs:468-472`) and the
`MeanOutliers` re-filter (`:1918-1923`) feed only annotated PNGs, never J.

## Suggested shape of the fix

Make `RunEvaluationLoader` the single owner; the harness calls it instead of rebuilding it.

1. **Done** — drop the mediator so the loader is constructible headlessly.
2. Extract `AutoFocusEngine.LoadSavedAttemptImpl:3107-3135` to a static, and have the loader take a narrow
   `IAfRunSource { SavedAutoFocusAttempt Load(string); AutoFocusEngineOptions OptionsFor(SavedAutoFocusAttempt); }`
   instead of the whole `IAutoFocusEngine`. The wizard passes an adapter over the real engine; TestApp passes
   one built from `HarnessFitInputs` so `--settings` keeps pinning the fit (F58). Closes #7 and #8 by deleting
   the harness's second regex.
3. Add `pixelScaleOverride` / `detectionBinningOverride` to `LoadSavedRunAsync`, applied through
   `DetectionBinningResolver.ApplyFactor`. Every harness deviation then survives as a *named, logged* override
   rather than a silent re-implementation. Closes #1 and #3; `ApplyAfContext:361-372`,
   `PrepareRunAsync:793-857` and `InferStepSize` get deleted.
4. Move the donut stamp + budget (`:3952-3957`) and the MinHFR-seed rule (`:3975-3976`) out of the VM into a
   shared `OptimizationRunPlan` both paths call. Closes #2 and #4.
5. Stamp GPU per-run from frame dimensions on both sides. Closes #5.

### Tests to add

- `Tests/StarDetection/Optimization/HarnessAppOptimizeParityTests.cs` — write a synthetic 3-position attempt
  folder, load it through both front-ends' wiring, assert the two `LoadedRun`s match field-for-field on
  `Seed`/`Baseline` (reuse `ParamsDump.Lines(...)`, the reflective formatter, so fields added later are
  covered for free) plus `RunFitConfig`, frame count and frame order. Catches #1, #3, #7, #8.
- Extend `HeadlessDetectionParityGuardTests` with `TheHarnessDoesNotBuildRunEvaluationDataItself` —
  `OffendingFiles("TestApp", @"new\s+RunEvaluationData\s*\(")`, no allowances.
- Extend `GpuAccelerationScopeGuardTests` to require the dimension-aware overload at every call site.
- A pure unit test for `OptimizationRunPlan`: donut master → `MaxEvaluations ∈ {250,400}` → curated-set
  dimensionality, and the MinHFR-seed source.
