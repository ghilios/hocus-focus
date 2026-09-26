# Plan — detector fixes from the saturated-star FWHM investigation

Source spec: `docs/saturated-star-fwhm-investigation-results.md` (recommendations 3, 4, 5).
Recommendations 1 and 2 are rig/hardware actions and are out of scope for code.

## Goal

1. **Recommendation 3 (settings)** — `PSFResolution = 20` and `UsePSFAbsoluteDeviation = true` become the shipped
   defaults.
2. **Recommendation 4 (code)** — repair hot pixels on the **measurement** image with an *isolation* test instead of
   the unconditional (or thresholded) 3x3 median; the **structure/detection** path keeps exactly the filter it has
   today.
3. **Recommendation 5 (code)** — skip the PSF fit for saturated stars (`Background + PeakBrightness >=
   SaturationThreshold`), leaving `star.PSF == null`.

## Task 1 — defaults

`PSFResolution` 10 -> 20 and `UsePSFAbsoluteDeviation` false -> true in every place a default is spelled out:

- `StarDetection/StarDetectionOptions.cs`: `DerivePresetSettings` (Simple-mode derivation), `InitializeOptions`
  (accessor fallbacks), `ResetDefaultsImpl`.
- `StarDetection/HocusFocusStarDetection.cs`: `BuildDefaultStarDetectorParams` (optimizer seed; kept in lockstep
  with `ResetDefaults` by a drift-guard test).
- Update `Joko.NINA.Plugins.HocusFocus.Tests/AutoFocus/HocusFocusReportTests.cs` default assertions and any
  option-toggle test that used the old default as its "changed" value.

`UsePSFAbsoluteDeviation` is not derived by `DerivePresetSettings`, so Simple mode inherits the new default
automatically; `PSFResolution` is derived there and must be changed.

## Task 2 — skip the PSF fit for saturated stars

- `StarDetector.ModelPSF`: before building the modeler, `continue` when
  `detectedStar.Background + detectedStar.PeakBrightness >= p.SaturationThreshold`. Do **not** increment
  `PSFFitFailed` — the fit was never attempted, and `metrics.Saturated` already counts these stars.
- Update the `SDR_Saturated_Tooltip` in `AutoFocus/DataTemplates.xaml`, which currently says saturated pixels are
  masked during PSF fitting.

## Task 3 — isolation-based hot-pixel repair on the measurement image

### The test

A pixel is an isolated hot pixel when, against a coarse local background grid (block median + 1.4826·MAD sigma,
128 px blocks, bilinearly interpolated — the same reduction `ComputeLocalBackgroundGrid` and the golden reference
detector's `coarse_bg` use):

- `amplitude = v - bg > 5 * sigma`, and
- `(maxOf8Neighbours - bg) * 3 < amplitude` (the brightest neighbour is under a third of the pixel's amplitude).

Repair value: the 3x3 median (BORDER_REPLICATE), i.e. what the current filter would have written.

### Pipeline restructure (`StarDetector.BuildDetectionContextInternal`)

Today `ApplyHotpixelFilter` mutates `srcImage` (the measurement image) and the structure source is then *copied
from it*. Invert that so both paths start from the raw pixels:

- structure source = a copy of the **raw** `srcImage`, then today's `ApplyHotpixelFilter` (unconditional or
  thresholded median) — bit-identical to today, and it is the same Mat allocation the code already made, so no
  extra memory on the unbinned path.
- measurement image = `srcImage` with the isolation repair applied in place.
- `NoiseReductionRadius` Gaussian: applied to the structure source whenever the radius is set (it used to inherit
  it via the copy when measurement noise reduction was on), and to the measurement image only when
  `StarMeasurementNoiseReductionEnabled`.
- Binning hoist: the split happens at native resolution, then both images are binned.
- The two images can now differ in cases where they used to be identical, so the "measurement image differs"
  predicate that decides whether to run a second K-sigma estimate has to account for the repair.
- Paths where `hotpixelFilterAlreadyApplied` is set by the CFA raw filter keep today's behaviour exactly.

### Parity surfaces that must mirror the new branch structure

- `Gpu/GpuEarlyChain.cs` (+ a repair kernel in `Gpu/GpuEarlyKernels.cs`) — the GPU early span is documented as an
  exact mirror of these steps and is consumed by the optimizer.
- `TestApp/Gpu/CpuEarlyChain.cs` — the CPU oracle `bench-gpu --compare` scores the GPU chain against.
- `TestApp/StarProbeRunner.cs` — reconstructs the measurement image to refit probed stars; its `base` column must
  keep reproducing the detector's `fwhmPx`.

### Metrics / versioning

- New `StarDetectorMetrics.MeasurementHotpixelCount` (repaired pixel count), merged in `Merge`, and surfaced in the
  metrics panel in `AutoFocus/DataTemplates.xaml` (project invariant).
- Bump `StarDetector.StarDetectorVersion` 3 -> 4: detection output changes, so every cached detection result and
  every early-cache key must miss.

## Task 4 — tests

- Unit tests for the isolation filter: an isolated hot pixel is repaired; a star core of realistic width is not;
  a hot pixel below the significance threshold is left alone; counts are right.
- Drift-guard updates for the new defaults.
- Full suite: `dotnet test Joko.NINA.Plugins/Joko.NINA.Plugins.sln -c Debug --nologo`.

## Task 5 — validation

1. **Single frame** — `TestApp star-probe` on
   `\\192.168.88.20\apdata\LIGHT_2026-09-15_21-16-16_O_0.00_300.00s_0441.xisf` with
   `\\192.168.88.20\apdata\O_settings.json`, before vs after (after = the same settings with the two new
   defaults). Report detection counts, PSF acceptance, FWHM median/MAD/percentiles, HFR, and the saturated stars.
   Cross-check against the spec's Part 4 table (median FWHM ~3.859 px, MAD ~0.233, y-gradient +0.435) — that is
   the reference the implementation has to reproduce.
2. **Bank** — run the detector with `ModelPSF` on over the best (nearest-focus) frame of every focus run in
   `D:\Autofocus Bank`, before vs after, and report the impact on PSF modelling: fit acceptance, FWHM
   median/MAD/scatter, and PSF fit wall time (`PSFResolution = 20` quadruples the sample count per star and the
   Huber IRLS path iterates, so runtime is a genuine part of "performance impact").
