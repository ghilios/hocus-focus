# Star Detection Internals

Read this when working on the star detector itself: the gradient-robust contamination test, the local background plane, or any `StarDetectorMetrics` field.

**Invariant:** every new field added to `StarDetectorMetrics` (rejection counts, flags, etc.) **must** also be displayed in the star detection metrics panel in `AutoFocus/DataTemplates.xaml` (see "Star Detection Metrics UI Requirement" below).

## Gradient-Robust Contamination Test + Local Background Plane

The star detector's contamination test is **gradient-robust** (`StarDetector.ComputeGradientContamination`):
for each star it fits a robust plane `b0 + b1·dx + b2·dy` to the background-annulus pixels via IRLS (Huber)
to model a smooth one-sided background (galaxy/nebula gradient), subtracts it, then flags a star **only** when
a single octant shows a one-sided **positive** residual excess above `ContaminationSensitivity` sigma — a
contaminant adds light, so this ignores both smooth gradients (removed by the fit) and edge-clip deficits
(negative). The fitted plane (`LocalBackgroundPlane`, carried on `Star.BackgroundPlane`) doubles as the
**local background** used per-pixel for centroid, flux, HFR, and PSF, so a gradient no longer biases any
measurement; for flat fields the plane equals the annulus median (no change). The PSF fit has the gradient
*tilt* removed before fitting (zero at the star center, so the amplitude and fitted background `B` are
unaffected) for cleaner sigma/FWHM/eccentricity.

- **Quality gate**: `StarDetectorParams.RejectContaminatedStars` (option `StarDetectionOptions.
  RejectContaminatedStars`, **default ON**) rejects contaminated stars (metric `ContaminationRejected`);
  when off they are kept and only flagged (`Star.StarContaminationSuspected`, metric `ContaminationSuspected`).
- The legacy opposite-sector-median test was removed (it tripped on smooth gradients). Validated on M31 +
  Pleiades: the gradient-robust test drops smooth-gradient/edge false positives and recovers real faint
  companions that an opposing gradient had masked.

For the headless TestApp diagnostic that exercises this test, see `testapp-cli.md`.

## Measurement image vs structure source (two hotpixel filters)

**Gated by `StarDetectorParams.MeasurementHotpixelRepair` / `StarDetectionOptions.MeasurementHotpixelRepair`,
which is OFF unless a configuration was deliberately re-derived.** With it off the pipeline takes a legacy
branch that is BIT-IDENTICAL to the pipeline before the option existed (pinned by
`StarDetectorEquivalenceTests`, whose golden signature is once again develop's). The option turns itself on in
exactly two places, both of which re-derive the acceptance gates in the same breath: `ResetDefaults` and
`ApplyOptimizedSettings`. A profile load reads FALSE, and a settings file with no such field deserializes to
FALSE, so neither can switch it on. Two tests assert the deliberate reset-vs-construction difference rather
than skipping it (`DeliberatelyDiffersFromFreshConstruction`, and the second named exception in
`BuildDefaultStarDetectorParams_MatchesConstructedOptionsBuild`).

**Why it is gated at all:** it changes what two shipped gates MEAN. The measurement image's K-sigma becomes the
frame's honest noise (the median was suppressing it), so `Sensitivity` is a stricter bar; and every star
measures a smaller HFR, so `MinHFR` is a stricter floor. Which one bites depends on the rig — the investigated
frame lost 17% of its stars to Sensitivity, a synthetic frame lost 24% to MinHFR, and the real bank at defaults
was a wash. See `docs/saturated-star-fwhm-fixes-results.md`.

When ON, `BuildDetectionContextInternal` derives TWO images from the same raw pixels and filters each
differently (`StarDetector.PrepareMeasurementAndStructureSources`):

| | image | hotpixel filter | who reads it |
|---|---|---|---|
| structure | `noiseReducedImage` -> `structureMap` | `ApplyHotpixelFilter` — the 3x3 median, thresholded per `HotpixelThresholdingEnabled` | candidate formation (wavelet, binarize, flood fill) |
| measurement | `srcImage` (`ctx.MeasurementImage`) | `HotpixelFiltering.RepairIsolatedHotpixels` — the isolation test | `MeasureStar` (HFR, background, peak), `ModelPSF` |

- **The isolation test:** amplitude above a coarse local background (block median, 128 px, bilinear) must
  exceed 5 local sigmas AND the brightest of the eight neighbours must sit below 1/3 of that amplitude.
  Qualifying pixels get their 3x3 median. A hot pixel passes; a star core fails the second test, because its
  neighbours carry most of its amplitude.
- **Why not the median on the measurement image:** it drops a bright star's peak ~20%, biases every fitted
  FWHM ~6% high and flattens ~a third of the real focus gradient. The thresholded variant is no better — a
  star core deviates from its own 3x3 median exactly as a hot pixel does, so at the 0.001 default it rewrites
  star cores too. Evidence: `docs/saturated-star-fwhm-investigation-results.md` Part 4 and
  `docs/saturated-star-fwhm-fixes-results.md`.
- **Why the structure path keeps the median:** candidate formation needs the smoothing. Running detection on
  an unsmoothed frame loses ~26% of detections.
- **σ consistency:** the two images now differ in configurations where they used to be identical, so
  `measurementDiffersFromStructure` (not the old `NoiseReductionRadius > 0 && !noiseReductionApplied`) decides
  whether the measurement image gets its own K-σ estimate. The measurement σ is now the frame's HONEST noise
  — the median was suppressing it — which makes the `Sensitivity` gate bite harder at the same setting.
- **GPU parity is exact, and checkable:** `bench-gpu --compare --image <frame>` prints
  `measurement hotpixel repairs: cpu=N gpu=N` and the measurement-image diff. On the 61 MP investigated frame
  both report 223982 repairs and the image is bit-identical (0 pixels differing). A zero on BOTH sides means the
  repair never ran and the image diff proves nothing — the compare says so.
- **Mirrors that must move together:** `Gpu/GpuEarlyChain.cs` (+ `IsolatedHotpixelRepairKernel`),
  `TestApp/Gpu/CpuEarlyChain.cs` (the `bench-gpu --compare` oracle), and `TestApp/StarProbeRunner.cs`'s
  measurement reconstruction. With `DetectionBinning > 1` the split happens at NATIVE resolution and both
  images are binned; the binned structure source is handed to the early span as `structureSource`.

## Saturated stars get no PSF fit

`ModelPSF` skips any star with `Background + PeakBrightness >= SaturationThreshold`, leaving `Star.PSF` null
(every consumer already handles that, including the frame's FWHM/Sigma/Eccentricity medians). It is NOT
counted as a `PSFFitFailed` — the fit was never attempted — and `metrics.Saturated` already counts these
stars. A clipped core is masked out of the sample set, so the fit sees wings only and the width is not
identifiable from them: the amplitude runs into its solver bound and the width absorbs the rest. Measured
errors against unsaturated neighbours ran -15% to +54% with the sign set by how much of the core survived,
and neither the R^2 gate nor a reduced-chi^2 gate separates them.

## Software Detection Binning

`StarDetectorParams.DetectionBinning` (int, 1 = off) resamples the frame at the top of
`BuildDetectionContextInternal`, so **the whole pipeline runs in binned pixels** and every pixel-unit knob
stays in its calibrated range (in-focus HFR ~2-4 px) regardless of the rig's pixel scale. Non-obvious rules:

- **It is an EARLY param** (`EarlyCacheKeyProperties`) — it changes candidate formation, so an early context
  can never be reused across factors.
- **Hotpixel filtering is hoisted above the resample** (native resolution). A hot pixel averaged into its
  block is no longer the isolated outlier the filter looks for.
- **Everything pixel-valued is scaled back to SOURCE pixels** at the end of `GateAndMeasureInternal`, BEFORE
  the ROI offset (the ROI is cropped at native resolution, so its offset is already in source pixels):
  `Star.ScaleToSourcePixels`, `StarDetectorMetrics.ScaleBounds`, `PSFModel.ScaledToSourcePixels`. Centers
  pick up a `(b-1)/2` half-block shift; intensities are unchanged (mean binning preserves level);
  `PSF.FWHMArcsecs` is NOT rescaled because `StarDetectorParams.PixelScale` already carries the factor.
  `HocusFocusStarDetection.SourcePixelScale` divides it back out for anything user-facing.
- **The factor is always explicit — there is no Auto, deliberately.** A self-resolving factor would change
  detection behavior on upgrade and invalidate already-tuned settings. `Utility/DetectionBinningResolver`
  RECOMMENDS one (options page + optimization wizard) and never writes the setting. `ToFactor` clamps, so an
  out-of-range persisted value can never bin someone's frames.
- **The recommendation comes from a MEASUREMENT, never from pixel scale.** `InFocusHfrRecord` holds the last
  auto-focus run's FINAL HFR (a real exposure at the settled position), written from
  `AutoFocusEngine.OnCompleted` and by a live wizard sweep. Do NOT reintroduce an assumed-seeing estimate:
  plausible seeing spans ~1.5-4", wider than the whole 1x-vs-2x margin, and the old estimate told a user with
  3.6 px stars to bin 2x2.
- **Never read the in-focus HFR off the fitted curve vertex without checking the fit.** Past ~22 px of defocus
  the detector loses the donuts and reports ~2 px noise blobs; those points drag the vertex to 2.16 px against
  a 4.87 px truth, with R2 negative. The wizard gates on R2 >= 0.9
  (`OptimizationSummary.MinRSquaredForBinningRecommendation`). Evidence:
  `Tests/CameraSimulator/InFocusHfrDiagnosticTests`.
- The optimization wizard changes the factor ONLY via "Optimize again at NxN": it re-runs the search on the
  saved frames at the new factor (stamped onto the reloaded seed/baseline in `LoadRunStampedAsync`, nothing
  persisted) and Accept writes the factor together with the settings measured at it. Don't add an
  apply-the-factor-alone path — that pair is only valid together.
- **Reported HFR drifts with the factor** (+5% at 2x, +16% at 3x on the capstone frames): higher per-pixel
  SNR admits more outer flux into the measurement. Harmless for best-focus, but HFR is not comparable across
  factors — don't "fix" a test that observes this.

## Star Detection Metrics UI Requirement

Every new field added to `StarDetectorMetrics` (rejection counts, flags, etc.) **must** also be displayed in the star detection metrics panel in `AutoFocus/DataTemplates.xaml`. The metrics panel uses a `UniformGrid Columns="2"` with `StackPanel` pairs. Add new entries using the `HF_ZeroToDoubleDashConverter` pattern:

```xaml
<StackPanel Orientation="Horizontal">
    <TextBlock Width="120" VerticalAlignment="Center" Text="My Metric" />
    <TextBlock Width="70" HorizontalAlignment="Center" VerticalAlignment="Center"
        Text="{Binding Metrics.MyMetricField, Converter={StaticResource HF_ZeroToDoubleDashConverter}}" />
</StackPanel>
```
