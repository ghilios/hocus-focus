# Why a saturated star reports a much larger FWHM (and HFR) than its neighbours

**Frame:** `LIGHT_2026-09-15_21-16-16_O_0.00_300.00s_0441.xisf` — Sh2-129, O-III, 300 s, QHY600M mono
(9576x6388, UInt16), TEC 140 FRC @ 882 mm, 0.879 arcsec/px.
**Settings:** the user's exported per-filter set for filter O (`O_settings.json`, Advanced mode, plugin 4.0.0.17;
`PSFFitType=Moffat_40`, `PSFResolution=10`, `PSFFitThreshold=0.9`, `SaturationThreshold=0.99`,
`HotpixelFiltering=on`/thresholding off, `StarMeasurementNoiseReduction=off`, `DetectionBinning=1`).
**Question:** near frame centre a bright star reports a much larger HFR than its dimmer neighbours (expected),
but *also* a much larger FWHM (not expected — a fitted model width should be brightness-independent).

## Answer

The bright star's optical PSF is **identical** to its neighbours'. The whole difference is a measurement
artifact of saturation, in two separate places:

- **FWHM 3.99 -> 6.06 px:** the PSF fit masks saturated samples, which removes the core. What is left is a
  wing-only fit, in which amplitude `A` and width `sigma` are degenerate, so the width is not identifiable from
  the surviving data. `A` runs up to its **hard upper bound `2.0`** (`MoffatPSFType.Solve`) and `sigma` absorbs
  the rest. Removing that bound does NOT recover the truth — see the follow-up section; the right response is to
  reject the fit.
- **HFR 2.54 -> 3.84 px:** the flat clipped core under-weights the centre of the flux-weighted mean radius.
  This one is already known and documented in `StarDetector.MeasureStar`'s own remarks (accuracy analysis F14,
  decided document-only). The larger structure box / HFR aperture contributes essentially **nothing** here.

This document grew in three follow-ups, each of which corrected something before it. **Read the final
recommendations below first**; where an earlier section disagrees with them, the later section wins, and the
superseded claims are marked in place.

## Final recommendations

In order of how much they matter for this rig. Numbers are from this one frame.

1. **Chase the focus gradient across the sensor** (Aberration Inspector / tilt adapter). It is the biggest single
   term in the frame's FWHM spread (39% of the variance), and the production measurement *understates* it: the
   top-to-bottom gradient is +0.30 px as reported but +0.44 px once the hot-pixel median is taken out of the PSF
   measurement (Follow-up 3). A focus sweep is needed to separate tilt from field curvature with authority.
2. **Chase the fixed ~70 deg elongation** (mount, guiding, flexure). It adds ~1.9" in quadrature to the major
   axis everywhere; it moves the median FWHM, not the spread (Follow-up 2).
3. **Set `PSFResolution = 20` and `UsePSFAbsoluteDeviation = true`.** FWHM MAD -13.8%, scatter about the field
   surface -7.3%; detected stars, positions and every HFR value are bit-identical (Follow-up 3).
4. **Code change — repair hot pixels on the PSF measurement image by an isolation test instead of the
   unconditional 3x3 median**, leaving the structure path unchanged. Removes a ~6% upward bias on every FWHM
   and shows the tilt gradient ~43% larger; per-star precision drops ~20%, which does not reach the frame-level
   median (Follow-up 3).
5. **Code change — skip the PSF fit for saturated stars** (`Background + PeakBrightness >= SaturationThreshold`).
   Per-star correctness; it does not visibly change `FWHMMAD` (Follow-ups 1 and 2).

Do **not**: raise or remove the amplitude bound, gate on reduced chi-squared, switch PSF type, raise
`HotpixelThreshold`, turn `HotpixelFiltering` off, or enable `StarMeasurementNoiseReductionEnabled`. Each was
measured and each is neutral or harmful; the sections below say why.

## What the detector reports

`TestApp star-probe --image <frame> --settings <O settings> --near 4707,3262 --radius 260 --psf-sweep`

| star (x, y) | box | peak | HFR | FWHM px | fitted A | fitted B | R^2 | reduced chi^2 | sat. samples |
|---|---|---|---|---|---|---|---|---|---|
| **4706.9, 3260.6** | 25x24 | 0.989 | **3.84** | **6.06** | **2.000** | **0.000** | 0.972 | 62962 | 5 / 100 |
| 4821.3, 3192.3 | 20x20 | 0.504 | 2.54 | 3.99 | 0.494 | 0.00716 | 0.990 | 2730 | 0 |
| 4658.1, 3308.8 | 17x17 | 0.108 | 2.45 | 3.91 | 0.110 | — | 0.988 | 208 | 0 |
| 4850.3, 3378.4 | 16x16 | 0.045 | 2.48 | 3.94 | 0.045 | — | 0.988 | 41 | 0 |
| 4634.8, 3406.9 | 15x15 | 0.024 | 2.49 | 3.90 | 0.026 | — | 0.987 | 15 | 0 |
| 4834.2, 3226.7 | 13x13 | 0.019 | 2.39 | 3.81 | 0.020 | — | 0.978 | 19 | 0 |

Every unsaturated star lands at FWHM 3.8-4.1 px regardless of a 25x brightness spread. Only the saturated one
is different — and its fitted amplitude sits **exactly** on `2.0` and its fitted background **exactly** on
`0.0`: both parameters are pinned at their bounds, which is what a runaway fit looks like.

## Evidence 1 — the PSF really is the same

Azimuthally-averaged, background-subtracted radial profiles (ADU) on the detector's own measurement image
(raw / 65535 then the 3x3 hot-pixel median). The bright star is clipped out to r ~ 3 px; beyond that the ratio
to the neighbour is flat:

| r (px) | bright | neighbour (p=0.504) | ratio |
|---|---|---|---|
| 4.5 | 36504 | 1660 | 22.0 |
| 5.5 | 14801 | 646 | 22.9 |
| 6.5 | 6304 | 282 | 22.3 |
| 7.5 | 3103 | 146 | 21.2 |
| 8.5 | 1715 | 84 | 20.3 |
| 9.5 | 1052 | 51 | 20.8 |
| 10.5 | 679 | 34 | 19.8 |

A constant ratio over the whole unclipped range means one profile is a scaled copy of the other — same PSF,
about **21x** the flux. The bright star's true peak is therefore ~640 000 ADU, roughly **10x full well**.

## Evidence 2 — brighten an unsaturated star and the artifact appears

Take the neighbour at (4821, 3192), multiply its signal by `k`, clip at full well **before** the 3x3 hot-pixel
median (as the sensor does), and run the *identical* fit (Moffat beta=4, saturated samples masked, `A <= 2.0`)
over the real bright star's 25x24 box. Its true FWHM never changes:

| k | peak / full well | masked samples | fitted A | FWHM px | error |
|---|---|---|---|---|---|
| 1 | 0.5 | 0/110 | 0.48 | 3.978 | -0% |
| 2 | 0.9 | 0/110 | 0.96 | 3.998 | +0% |
| 4 | 1.9 | 1/110 | **2.000** | 3.862 | -3% |
| 6 | 2.8 | 1/110 | **2.000** | 4.420 | +11% |
| 8 | 3.7 | 3/110 | **2.000** | 4.647 | +16% |
| 12 | 5.6 | 5/110 | **2.000** | 5.291 | +33% |
| **21** | **9.8** | 5/110 | **2.000** | **6.070** | **+52%** |
| 30 | 14.0 | 9/110 | **2.000** | 6.489 | +63% |
| 50 | 23.3 | 9/110 | **2.000** | 7.533 | +89% |

At `k = 21` — the measured brightness ratio — the simulated star reports **FWHM 6.070 px** and (see below)
**HFR 3.90 px**, against the real bright star's **6.060** and **3.838**. Brightness plus clipping reproduces the
real star to within 0.4%, with nothing about its shape changed.

> Clipping has to be applied **before** the hot-pixel median. An earlier version of this ladder scaled the
> already-median-filtered image and then clipped, and it mis-attributed the whole effect to the amplitude bound
> (see the follow-up section): the 3x3 median smears a clipped plateau outward and corrupts exactly the shoulder
> samples that carry what little width information survives.

## Evidence 3 — the width is unconstrained; the clamp picks the answer

Real bright star, core masked, amplitude **fixed** at a series of values, everything else free:

| A fixed | sigX | sigY | FWHM px | R^2 |
|---|---|---|---|---|
| **2.00** (the bound) | 7.22 | 6.72 | **6.06** | 0.976 |
| 3.00 | 6.09 | 5.75 | 5.15 | 0.995 |
| 4.00 | 5.15 | 5.43 | 4.60 | 0.991 |
| 6.00 | 4.71 | 4.45 | 3.98 | 0.973 |
| 10.40 (the physical value) | 4.11 | 3.64 | 3.36 | 0.944 |
| 20.00 | 3.70 | 2.92 | 2.86 | 0.920 |

FWHM slides from 6.06 to 2.86 while R^2 never leaves 0.92-0.995. The wing-only data cannot pick a width, and
`A` is pinned at the **largest-FWHM end** of that valley because `2.0` is its hard ceiling
(`MoffatPSFType.Solve`: `upperBounds = { 2.0d, 1.0d, dxLimit, dyLimit, sigmaUpperBound, sigmaUpperBound,
PI/2, 10.0d }`). Note the bound is in normalised [0,1] units, so it is exceeded by any star brighter than
2x full well.

Things that are **not** the cause, ruled out by `--psf-sweep` refits of the real star:

| variation | FWHM px |
|---|---|
| production (`PSFResolution=10`) | 6.06 |
| `PSFResolution=30` / `60` (finer sampling grid) | 6.14 / 6.15 |
| saturation mask disabled (clipped core kept) | 7.09 (worse) |
| bounding box halved | 6.29 |

## HFR decomposition

Same simulated brightening of the neighbour, HFR measured the way `StarDetector.MeasureStar` does:

| case | HFR px |
|---|---|
| as observed (k=1, aperture 10.0) | 2.539 (detector reports 2.535) |
| aperture widened to 12.0 alone | 2.539 |
| k=21 **unclipped**, aperture 12.0 | 2.606 |
| k=21 **clipped**, aperture 10.0 | 3.800 |
| k=21 clipped, aperture 12.0 | 3.901 (detector reports 3.838 for the real star) |

So the HFR excess is ~95% the flat clipped core. The "bigger star gets a bigger aperture" intuition contributes
~0.07 px here, because the per-pixel noise gate (`flux > clip * sigma`) already excludes the far wing pixels the
wider aperture adds.

## Why nothing catches it

- `PSFGoodnessOfFitThreshold = 0.9` is checked against **R^2**, which is dominated by the huge dynamic range
  inside the box; the bad fit scores 0.972 and is accepted. Reduced chi-squared is far more discriminating here
  (62 962 vs 2 730 for the neighbour and 15-208 for the faint ones) but is **not** usable as a fixed gate — it
  scales with the star's peak, and the bright *unsaturated* neighbour outscores three of the five saturated
  stars. See the follow-up section.
- `ExcludeSaturatedStarsFromHFR` (on) keeps this star's HFR out of the frame's `AverageHFR`
  (`HocusFocusStarDetection.StarsForHfrAggregation`), so **auto-focus is not affected**. There is **no
  equivalent filter for the PSF aggregate** — `result.FWHM` / `Sigma` / `Eccentricity` are medians over every
  star with a fit, saturated ones included (`HocusFocusStarDetection.cs:741-760`).

## Blast radius on this frame

Of 1737 accepted stars, **5** are saturated; all five have a saturated PSF sample and **3** have the amplitude
pinned at the bound. Only the one with 5 masked samples runs away visibly (6.06 px, the frame maximum); the four
with a single masked sample report 3.40-3.64 px, which looks normal but sits at or below the frame's p05 of
3.72 — the bias changes sign with how much of the core survives (see the follow-up table). With 1511 accepted
fits the frame-level median FWHM is unaffected; what the user sees is the **per-star** number in the star list /
annotator / aberration inspector.

## Follow-up 1: is uncapping the amplitude a fix? No - reject the star instead

### Where the cap is

`upperBounds[0] = 2.0d`, in normalised [0,1] image units, appears in **all four** solver entry points:

| file | line | used by |
|---|---|---|
| `StarDetection/PSFModeler.cs` | 235 (`Solve`), 353 (`SolveIRLS`) | `PSFModelTypeAlglibBase` -> Gaussian |
| `StarDetection/MoffatPSFType.cs` | 308 (`Solve`), 376 (`SolveIRLS`) | Moffat (fixed and fittable beta) |

**The cap can only ever bind on a clipped star.** On this frame, exactly 5 of 1511 accepted fits have a fitted
amplitude above 1.0, and they are precisely the 5 stars flagged saturated. An unsaturated star's peak is below
`SaturationThreshold` by definition, so its amplitude never approaches 2.0. Raising the bound therefore changes
nothing for any star worth keeping.

### Uncapping does not restore accuracy

Real bright star, truth 3.989 px:

| fit | A | FWHM px | error | R^2 |
|---|---|---|---|---|
| production (`A <= 2`) | 2.000 | 6.060 | +52% | 0.9756 |
| uncapped | 3.094 | 5.085 | **+27%** | 0.9951 |
| uncapped + beta free | 2.475 | 5.751 | +44% | 0.9967 (beta pinned at its own 10.0 bound) |

And across the simulated brightness ladder (neighbour x k, clipped **in the sensor**, i.e. before the 3x3
hot-pixel median, box 25x24 — this reproduces the real star to 0.4%: 6.070 vs 6.060 capped, 5.060 vs 5.085
uncapped):

| k | peak / full well | masked | capped FWHM | err | uncapped FWHM | err |
|---|---|---|---|---|---|---|
| 1 | 0.5 | 0/110 | 3.978 | -0% | 3.978 | -0% |
| 4 | 1.9 | 1/110 | 3.862 | -3% | 3.475 | -13% |
| 6 | 2.8 | 1/110 | 4.420 | +11% | 3.892 | -2% |
| 8 | 3.7 | 3/110 | 4.647 | +16% | 4.469 | +12% |
| 12 | 5.6 | 5/110 | 5.291 | +33% | 4.184 | +5% |
| 16 | 7.4 | 5/110 | 5.740 | +44% | 4.432 | +11% |
| 21 | 9.8 | 5/110 | 6.070 | +52% | 5.060 | +27% |
| 30 | 14.0 | 9/110 | 6.489 | +63% | 4.088 | +2% |
| 50 | 23.3 | 9/110 | 7.533 | +89% | 4.746 | +19% |

Uncapping removes the monotone runaway but leaves an error that wanders between **-13% and +27%** with no trend
you could correct for. The cap is not the disease; it is where the runaway happens to stop.

> **Simulation caveat that mattered.** An earlier version of this ladder scaled the *already median-filtered*
> image and then clipped, which made uncapping look like a clean fix (error flat at +-3%). Clipping must happen
> **before** the hot-pixel median, as it does in the sensor: the 3x3 median then smears the clipped plateau
> outward and corrupts exactly the shoulder samples that carry what little width information survives.

### Why the data cannot support a width

With the core gone, `A` and `sigma` trade off along a valley the objective barely distinguishes (from the
A-fixed table above: FWHM 6.06 -> 2.86 while R^2 stays inside 0.92-0.995). Trying to remove the median-smeared
shoulder as well only removes the last informative samples:

| real star, uncapped | samples kept | A | FWHM px | error |
|---|---|---|---|---|
| mask `v >= 0.99` (production) | 95/100 | 3.094 | 5.085 | +27% |
| mask dilated 5 px | 78/100 | 1.308 | 5.694 | +43% |
| mask dilated 9 px | 55/100 | 0.500 | 7.013 | +76% |

### Reduced chi-squared is not a usable gate

It scales with the star's peak, because the beta=4 model error is proportional to amplitude:

| peak decile | n | mean reduced chi^2 | max |
|---|---|---|---|
| 0.0020-0.0023 | 151 | 0.8 | 1.7 |
| 0.0157-0.0403 | 151 | 28.8 | 165 |
| 0.0405-0.9915 | 152 | 1467 | 62962 |

The bright **unsaturated** neighbour sits at 2730 — higher than three of the five saturated stars (6413, 10069,
10375). No fixed threshold separates them.

### Recommendation: reject saturated stars from the PSF fit

Use the flag the detector already computes — `star.Background + star.PeakBrightness >= p.SaturationThreshold`,
the same test behind `metrics.SaturatedBounds` and `HocusFocusStarDetection.StarsForHfrAggregation` — and skip
`ModelPSF` for those stars (leaving `star.PSF == null`, which every consumer already handles).

Cost on this frame: **5 of 1737** stars. And all five are biased, not just the obvious one:

| x, y | box | saturated px in box | masked PSF samples | fitted A | FWHM px |
|---|---|---|---|---|---|
| 3840, 2542 | 21x21 | 3 | 1 | 1.576 | 3.40 |
| 5066, 812 | 21x21 | 7 | 1 | 2.000 | 3.43 |
| 4262, 3751 | 20x21 | 8 | 1 | 1.795 | 3.54 |
| 6864, 522 | 21x21 | 12 | 1 | 2.000 | 3.64 |
| **4707, 3261** | 25x24 | **46** | **5** | 2.000 | **6.06** |

The frame's accepted fits run p05 = 3.72, median = 4.10, p95 = 4.50. Three of the lightly-clipped stars fall at
or below p05 and the heavily-clipped one is the frame maximum — the bias just changes sign with how much of the
core survives.

Secondary, optional: also **exclude saturated stars from the PSF aggregate** (`result.FWHM` / `Sigma` /
`Eccentricity` medians, `HocusFocusStarDetection.cs:741-760`), mirroring `StarsForHfrAggregation`. Rejecting the
fit outright makes this redundant, since a star with no PSF is already skipped there.

## Reproducing

```bash
# the probe used above (new subcommand, see TestApp/StarProbeRunner.cs)
dotnet Joko.NINA.Plugins/TestApp/bin/Debug/net8.0-windows7.0/TestApp.dll star-probe \
  --image "<frame>.xisf" --settings "<harness settings>.json" \
  --out "<outdir>" --near 4707,3262 --radius 260 --psf-sweep
```

`star_probe.csv` carries, per accepted star: centre, structure box, background, peak, HFR and the HFR aperture,
the full PSF fit (sigmas, FWHM in px and arcsec, theta, eccentricity, R^2, reduced chi^2, fitted amplitude and
background, beta), the PSF sampling step, and how many of its samples the saturation mask dropped.
`--psf-sweep` refits each probed star at other `PSFResolution` values, without the saturation mask, and over a
halved box; its `base` column reproduces the detector's own `fwhmPx` exactly, which is the check that the
reconstructed measurement image matches what detection fitted. The refits use the detector's measurement noise
sigma (the Huber IRLS threshold depends on it). The sample-census columns and the sweep are only produced for an
unbinned mono frame; for a bayered or `DetectionBinning > 1` frame the reconstruction would not be the image
detection sampled, so the columns are left empty and the sweep is skipped. See `.claude/docs/testapp-cli.md`.

## Follow-up 2: what the saturated-star fix does to the frame's FWHM dispersion, and what actually drives it

### The fix barely moves the dispersion

| | n | median | MAD | SD | p05 | p95 | max |
|---|---|---|---|---|---|---|---|
| current behaviour | 1511 | 4.1014 | 0.2297 | 0.2448 | 3.72 | 4.50 | 6.06 |
| saturated stars rejected | 1506 | 4.1021 | **0.2283** | **0.2379** | 3.73 | 4.50 | 5.51 |

MAD **-0.6%**, SD **-2.8%**, max 6.06 -> 5.51. `result.FWHMMAD` is a *median* absolute deviation
(`HocusFocusStarDetection.cs:752` always uses `MedianMAD`, regardless of `MeasurementAverage`), so removing 5
outliers out of 1511 is almost invisible to it. **The fix is for per-star correctness, not for dispersion.**

### Where the dispersion actually comes from

Fitting a quadratic surface in (x, y) to the per-star FWHM, over the unsaturated set:

```
total variance 0.0566 px^2  =  field structure 0.0218 (39%)  +  scatter about it 0.0348 (61%)
```

**(a) A focus gradient across the sensor — 39%, and it is real.** Median FWHM by y-band, top to bottom:

| y band | all unsaturated | brightest 50% | brightest 20% |
|---|---|---|---|
| 0-1597 | 3.987 | 3.983 | 3.985 |
| 1597-3194 | 4.030 | 4.030 | 4.057 |
| 3194-4791 | 4.132 | 4.115 | 4.106 |
| 4791-6388 | 4.289 | 4.294 | 4.285 |
| **delta** | **+0.302** | **+0.310** | **+0.300** |

Identical at every brightness, so it is not a faint-star artifact. Nearly flat in x (4.23, 4.04, 4.02, 4.12
left to right), and **both** axes grow together (FWHMx 4.23 -> 4.63, FWHMy 3.71 -> 4.01) with eccentricity
essentially constant (0.471 -> 0.482). Both axes growing with no eccentricity change is *defocus*, not
astigmatism — i.e. a **tilt about a roughly horizontal hinge**. The fitted surface spans 3.91 -> 4.38 px
(5th-95th percentile of the frame), a real ~12% gradient. This belongs to the Aberration Inspector / tilt-adapter
workflow, not to any detector setting.

**The gradient is in the pixels, not in the PSF model.** A model-free measurement on the RAW frame (no hot-pixel
median), fixed 8 px aperture, 452 bright unsaturated stars:

| y band | fit FWHM | second-moment FWHM | half-flux radius |
|---|---|---|---|
| 0-1597 | 3.986 | 4.783 | 2.111 |
| 1597-3194 | 4.057 | 4.803 | 2.146 |
| 3194-4791 | 4.103 | 4.876 | 2.187 |
| 4791-6388 | 4.289 | 4.988 | 2.269 |
| **delta** | **+0.303** | **+0.205** | **+0.158** |

(Second moments of a heavy-winged profile read larger than a Moffat FWHM, so only the trend is comparable.)
*Superseded in part by Follow-up 3:* with the hot-pixel median removed from the PSF measurement the fit's
gradient is **+0.44 px**, not +0.30 — the median compresses sharp stars more than broad ones and flattens it.

*Caveat:* one sub plus a quadratic fit cannot separate tilt from field curvature with authority. The monotonic
in-y / flat-in-x pattern is far more tilt-like than curvature (which would be radial), but the sensor-tilt
measurement across a focus sweep is the instrument for this.

**(b) A fixed-direction elongation — inflates the median, not the spread.** Eccentricity is ~0.48 almost
everywhere and the major axis clusters at a **fixed frame angle of ~+70 deg**:

| set | n | mean axis | axial concentration R | median ecc |
|---|---|---|---|---|
| all unsaturated | 1506 | +71.2 deg | 0.528 | 0.48 |
| brightest 20% | 301 | +69.7 deg | 0.588 | 0.47 |

and the per-decile mean axis stays inside +65..+77 deg at every brightness. The angle between the major axis and
the radius vector has a median of 51 deg with no radial ordering — an *optical* aberration (astigmatism, coma)
orients radially or tangentially relative to the field centre, so a single fixed frame direction points at
**mount/guiding drift or flexure**, not optics. It costs ~0.6 px on the major axis uniformly (FWHMx - FWHMy).

The orientation is also model-free: second moments on the RAW frame give an axial mean of -67.8 deg
(R = 0.462) in array coordinates. The fit reports theta with the opposite sign — on the 200 brightest stars with
eccentricity > 0.5, the per-star disagreement between the fit's theta and the *negated* moment angle has a
median of 3.4 deg (42.6 deg without the negation) — so that is +67.8 deg in the fit's convention, against the
fit's own +69.7 deg.

**(c) Photon noise on faint stars — 61%, and mostly irreducible by the fit.** (Follow-up 3 finds settings that
cut it ~7%, and a measurement-image change that trades some of it for accuracy.) Scatter about the fitted field
surface:

| peak decile | gate SNR | residual MAD (px) |
|---|---|---|
| 0.0020-0.0023 | 15 | 0.356 |
| 0.0033-0.0040 | 26 | 0.208 |
| 0.0067-0.0094 | 55 | 0.121 |
| 0.0156-0.0379 | 157 | 0.092 |
| 0.0379-0.8263 | 566 | 0.069 |

Nothing is wrong with those fits; the stars simply do not have the photons. Restricting to the brightest half
drops the scatter SD from 0.184 to 0.110 px.

### Detector settings tested — first pass

> **Superseded by Follow-up 3.** This pass tested each knob alone and concluded none helped. Follow-up 3 ran
> seventeen arms and found that `PSFResolution = 20` **combined with** Huber IRLS cuts MAD 13.8% and the scatter
> about the field surface 7.3% — better than either alone — and that the hot-pixel median biases every FWHM ~6%
> high. The single-knob numbers below still stand.

Matched set of 1466 unsaturated stars present in every run:

| variant | median | MAD | SD | scatter SD after field model |
|---|---|---|---|---|
| baseline (production) | 4.0978 | 0.2275 | 0.2363 | 0.1842 |
| `PSFPixelIntegration = true` | 4.0432 | 0.2299 | 0.2384 | 0.1857 |
| `UsePSFAbsoluteDeviation = true` (Huber IRLS) | 4.0645 | 0.2134 (-6%) | 0.2297 | 0.1872 |
| `PSFResolution = 20` | 4.0873 | 0.2192 (-4%) | 0.2316 | 0.1784 (-3%) |

- **Huber** shaves 6% off MAD but the scatter about the field surface goes slightly *up* — it is trimming tails,
  not reducing noise.
- **`PSFResolution = 20`** is a real but small 3-4% gain, at 4x the samples per fit.
- **`PSFPixelIntegration`** lowers the reported median by 0.055 px (it models the pixel area, so it is the more
  accurate absolute number) but tightens nothing.
- **Do not** turn on `StarMeasurementNoiseReductionEnabled` to chase this: it Gaussian-blurs the measurement
  image and would bias every FWHM upward.

### Priority order (superseded — see Final recommendations at the top)

1. **Chase the focus gradient** (Aberration Inspector / tilt adapter) — the single biggest lever, 39% of the
   variance and a real 12% FWHM gradient across the frame.
2. **Chase the ~70 deg elongation** (guiding / flexure) — worth ~0.6 px on the major axis everywhere; it moves
   the median, not the spread.
3. **Judge seeing/focus on the brighter half of the star list** — scatter SD 0.184 -> 0.110 px, for free.
4. **Implement the saturated-star PSF rejection** — for per-star correctness; expect no visible change to
   `FWHMMAD`.
5. Optional: `PSFResolution = 20` (-3 to -4%), and `PSFPixelIntegration` for absolute accuracy of the median.

## Follow-up 3: exhaustive settings sweep, and the hot-pixel filter's cost

Seventeen detector arms were run over the same frame with `TestApp star-probe`, each differing from the
production set by one knob (or a stated combination). Statistics are on the star set common to every arm, so
composition cannot explain a difference. `scatterSD` is the residual SD about a quadratic field surface in
(x, y) — the measurement-noise term with the real optical structure taken out.

| arm | detected | median FWHM px | MAD | scatterSD | verdict |
|---|---|---|---|---|---|
| **baseline (production)** | 1737 | 4.0719 | 0.1950 | 0.1216 | — |
| `UsePSFAbsoluteDeviation` (Huber IRLS) | 1737 | 4.0267 | **-13.8%** | +2.5% | keep |
| `PSFResolution = 20` | 1737 | 4.0716 | **-8.6%** | **-5.5%** | keep |
| `PSFResolution = 40` | 1737 | 4.0703 | -9.2% | -5.1% | no better than 20 |
| **`PSFResolution 20` + Huber** | 1737 | 4.0254 | **-13.8%** | **-7.3%** | **recommended** |
| `PSFPixelIntegration` | 1737 | 4.0182 | +1.1% | +0.4% | accuracy only |
| `StarBackgroundBoxExpansion = 6` | 1731 | 4.0714 | +0.9% | +0.3% | no effect |
| `PSFFitType = Gaussian` | 1737 | 4.2803 | -1.5% | +2.4% | biases the number up 5% |
| `PSFFitType = Moffat 2.5` | 1737 | 3.9959 | -0.9% | +6.2% | no |
| `PSFFitType = Moffat 1.5` | 1737 | 3.9108 | +1.8% | +27.7% | no |
| `PSFFitType = Moffat (beta fittable)` | 1737 | 4.1644 | +9.1% | +21.7% | no — beta pins at its bound |
| `HotpixelThresholdingEnabled` (thr 0.001) | 1206 | 4.1429 | -1.5% | +10.7% | loses 30% of the stars |
| `HotpixelThresholding` thr 0.01 | 1836 | 3.9400 | +44.8% | +40.0% | 758 failed fits |
| `HotpixelThresholding` thr 0.05 | 4278 | 3.8525 | +25.1% | +10.6% | **2886 failed fits — hot-pixel junk** |
| `HotpixelFiltering = false` | 1576 | 3.8282 | +21.1% | -2.4% | 645 failed fits |
| res20 + Huber + hpThr 0.05 | 4278 | 3.8404 | +7.6% | -12.8% | same junk problem |
| res20 + Huber + hpThr 0.05 + pixInt | 4278 | 3.7867 | +8.7% | -11.6% | same junk problem |

`PSFResolution 20` + `UsePSFAbsoluteDeviation` is free: detected stars, star positions and **every HFR value are
bit-identical** to baseline (verified: `max |dHFR| = 0.0`), accepted PSF fits 1511 -> 1510. Only the PSF fit
changes.

### The hot-pixel filter inflates FWHM by ~6% and hides a third of the tilt

With `HotpixelThresholdingEnabled` off — the production setting — `ApplyHotpixelFilter` runs
`Cv2.MedianBlur(m, m, 3)`: an **unconditional 3x3 median over the whole measurement image**, the same image the
PSF is fitted to. On a bright isolated star it drops the peak 19.7% and widens the half-max width from 3.92 to
4.20 px.

This sensor needs *a* filter: **0.36% of the frame is isolated hot pixels** (pixels >5 sigma above a 15x15 local
background whose brightest neighbour is under a third of their own amplitude), which is **~2.2 hot pixels inside
an average 25x24 star box**. Simply turning the filter off is not the answer — it gives 645 failed fits and a
+21% MAD.

But the existing threshold knob cannot fix it either. `HotpixelFilterWithThresholding` gates on
`|v - median3| > threshold`, and **a star core deviates from its own 3x3 median just as a hot pixel does** (7453
ADU for the star above). Raising the threshold therefore spares hot pixels before it spares star cores — hence
4278 "detections" with 2886 failed fits at thr 0.05.

An **isolation test** separates them cleanly, because a hot pixel's neighbours stay at background and a star
core's do not. Repairing only those pixels and leaving every other pixel untouched, then refitting the PSF with
**detection held fixed at the production boxes** (the pipeline already keeps the measurement image `srcImage`
separate from the structure image, so this needs no change on the detection side):

| measurement image | pixels rewritten | median FWHM | MAD | scatterSD | top-to-bottom gradient |
|---|---|---|---|---|---|
| production 3x3 median | all 61 171 488 | 4.0953 | 0.1941 | 0.1192 | +0.304 |
| isolation repair, ratio 3, 5 sigma | 221 966 | 3.8588 | 0.2337 | 0.1240 | +0.435 |
| isolation repair, ratio 2, 5 sigma | 232 706 | 3.8585 | 0.2338 | 0.1098 | +0.432 |
| isolation repair, ratio 2, 4 sigma | 278 032 | 3.8585 | 0.2338 | 0.1097 | +0.437 |
| isolation repair, ratio 1.6, 4 sigma | 285 965 | 3.8585 | 0.2332 | **0.1075** | **+0.439** |

Three results, all stable across every repair setting:

1. **The reported FWHM is ~5.8% too high** (4.095 -> 3.859 px; 3.60" -> 3.39").
2. **The field gradient is understated by ~43%** (+0.304 -> +0.439 px): the median compresses sharp stars more
   than broad ones, so it flattens exactly the signal a tilt search is looking for. Tilt detectability
   (gradient / scatterSD) improves from 2.55 to 4.08.
3. **Per-star precision gets ~20% worse** (MAD 0.194 -> 0.233) because the median was also suppressing noise.
   That cost does not reach the *frame* FWHM, which is a median over ~1500 stars: its standard error is
   0.0063 px today vs 0.0075 px repaired, both negligible. The 5.8% bias, by contrast, is systematic and does
   not average out.

An end-to-end run (repaired frame written to FITS, `HotpixelFiltering = false`) confirms the measurement side
(median 3.828 px, -6.5%, scatterSD -3.2%) but loses 26% of the detections, because the structure path lost its
smoothing too. That is why the repair belongs on the **measurement** image only, with the structure path keeping
the median + Gaussian it already applies. A FITS control of the unmodified frame reproduced the XISF run exactly
(identical median, MAD, SD, scatterSD), so the round-trip introduces nothing.

### Full budget for this frame

On the repaired measurement, detection fixed, 903 stars, 0.8793 arcsec/px:

| y band | FWHM major px | FWHM minor px | minor arcsec |
|---|---|---|---|
| 0-1597 | 4.027 | 3.383 | 2.98 |
| 1597-3194 | 4.102 | 3.438 | 3.02 |
| 3194-4791 | 4.252 | 3.557 | 3.13 |
| 4791-6388 | 4.562 | 3.773 | 3.32 |

- **Seeing + optics floor** (minor axis, best part of the field): **3.383 px = 2.98"**
- **+ mount / guiding elongation**, fixed ~70 deg axis: **2.18 px = 1.92"** in quadrature -> major axis 3.54"
- **+ focus gradient** across the sensor, bottom vs top: up to **1.67 px = 1.47"** in quadrature
- **+ the hot-pixel median filter**: +0.24 px, taking the reported frame number from 3.86 to 4.10 px
  (3.39" -> 3.61")
