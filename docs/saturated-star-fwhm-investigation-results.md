# Why a saturated star reports a much larger FWHM (and HFR) than its neighbours

**Frame:** `LIGHT_2026-09-15_21-16-16_O_0.00_300.00s_0441.xisf` — Sh2-129, O-III, 300 s, QHY600M mono
(9576x6388, UInt16), TEC 140 FRC @ 882 mm, 0.879 arcsec/px.
**Settings:** the user's exported per-filter set for filter O (`O_settings.json`, Advanced mode, plugin 4.0.0.17;
`PSFFitType=Moffat_40`, `PSFResolution=10`, `PSFFitThreshold=0.9`, `SaturationThreshold=0.99`,
`HotpixelFiltering=on`/thresholding off, `StarMeasurementNoiseReduction=off`, `DetectionBinning=1`).
**Question:** near frame centre a bright star reports a much larger HFR than its dimmer neighbours (expected),
but *also* a much larger FWHM (not expected — a fitted model width should be brightness-independent).

All numbers are from this one frame.

## Summary

- **The saturated star's FWHM (6.06 px vs 3.8-4.1 px for its neighbours).** Its PSF is identical to its
  neighbours'. The PSF fit masks its clipped core, leaving a wing-only fit whose width the data cannot determine;
  the fitted amplitude runs into the solver's `A <= 2.0` bound and the width absorbs the rest. With the production
  fit, removing the bound does not recover the true width. With `PSFResolution = 20` and
  `UsePSFAbsoluteDeviation = true`, lightly clipped stars already measure within 3% of their neighbours, and
  removing the bound brings the heavily clipped one from +49% to +12% — but that rests on a single star, so
  rejecting saturated fits remains the safe default.
- **Its HFR (3.84 vs 2.54 px).** ~95% of the excess is the flat clipped core under-weighting the centre (already
  documented as accuracy analysis F14). The larger aperture a bright star gets contributes almost nothing.
- **The frame's FWHM spread.** 39% of the variance is a real top-to-bottom focus gradient; the rest is mostly
  photon noise on faint stars. A fixed-direction elongation (~70 deg) raises the median, not the spread.
- **The hot-pixel filter.** The unconditional 3x3 median applied to the measurement image biases every FWHM ~6%
  high and hides about a third of the focus gradient.
- **Settings.** `PSFResolution = 20` with `UsePSFAbsoluteDeviation = true` tightens the FWHM spread with no
  effect on detection or HFR. No other setting tested helps.

## Recommendations

In order of impact on this rig.

1. **Chase the focus gradient across the sensor** (Aberration Inspector / tilt adapter). It is the largest term in
   the frame's FWHM spread (39% of the variance), and today's measurement understates it: the top-to-bottom
   gradient reads +0.30 px, but +0.44 px once the hot-pixel median is taken out of the PSF measurement. One frame
   cannot separate tilt from field curvature with authority; a focus sweep can.
2. **Chase the fixed ~70 deg elongation** (mount, guiding, flexure). It adds ~1.9" in quadrature to the major axis
   everywhere. It raises the median FWHM, not the spread.
3. **Set `PSFResolution = 20` and `UsePSFAbsoluteDeviation = true`.** FWHM MAD -14.0%, scatter about the field
   surface -6.5%. Detected stars, star positions and every HFR value are bit-identical.
4. **Code change: repair hot pixels on the PSF measurement image with an isolation test instead of the
   unconditional 3x3 median**, leaving the structure (detection) path unchanged. Removes a ~6% upward bias on
   every FWHM and shows the focus gradient ~43% larger. Per-star precision drops ~20%, which does not reach the
   frame-level median.
5. **Code change: skip the PSF fit for saturated stars** (`Background + PeakBrightness >= SaturationThreshold`).
   Fixes the per-star numbers; does not visibly change `FWHMMAD`. With recommendation 3 in place only heavily
   clipped stars stay badly wrong, and raising the amplitude bound as well cut the one such star here from +49% to
   +12% — too little evidence to replace rejection.

**Measured and no help:** gating the fit on reduced chi-squared, any other `PSFFitType`, `PSFPixelIntegration`
(lowers the median 0.054 px but tightens nothing), a larger `StarBackgroundBoxExpansion`, raising
`HotpixelThreshold`, and turning `HotpixelFiltering` off. **Raising or removing the amplitude bound** changes no
frame-level statistic under any settings tested; it only moves heavily clipped stars (Part 1).

**Not measured, but avoid:** `StarMeasurementNoiseReductionEnabled` Gaussian-blurs the measurement image (kernel
`2 x NoiseReductionRadius + 1`), which can only widen every star.

---

## Part 1 — The saturated star

### What the detector reports

`TestApp star-probe --image <frame> --settings <O settings> --near 4707,3262 --radius 260 --psf-sweep`

| star (x, y) | box | peak | HFR | FWHM px | fitted A | fitted B | R^2 | reduced chi^2 | sat. samples |
|---|---|---|---|---|---|---|---|---|---|
| **4706.9, 3260.6** | 25x24 | 0.989 | **3.84** | **6.06** | **2.000** | **0.000** | 0.972 | 62962 | 5 / 100 |
| 4821.3, 3192.3 | 20x20 | 0.504 | 2.54 | 3.99 | 0.494 | 0.00716 | 0.990 | 2730 | 0 |
| 4658.1, 3308.8 | 17x17 | 0.108 | 2.45 | 3.91 | 0.110 | — | 0.988 | 208 | 0 |
| 4850.3, 3378.4 | 16x16 | 0.045 | 2.48 | 3.94 | 0.045 | — | 0.988 | 41 | 0 |
| 4634.8, 3406.9 | 15x15 | 0.024 | 2.49 | 3.90 | 0.026 | — | 0.987 | 15 | 0 |
| 4834.2, 3226.7 | 13x13 | 0.019 | 2.39 | 3.81 | 0.020 | — | 0.978 | 19 | 0 |

Every unsaturated star lands at FWHM 3.8-4.1 px across a 25x brightness spread. Only the saturated one differs,
and its fitted amplitude sits **exactly** on `2.0` and its fitted background **exactly** on `0.0`: both parameters
are pinned at their bounds, which is what a runaway fit looks like.

### Its PSF is the same as its neighbours'

Azimuthally-averaged, background-subtracted radial profiles (ADU) on the detector's own measurement image
(raw / 65535, then the 3x3 hot-pixel median). The bright star is clipped out to r ~ 3 px; beyond that its ratio to
the neighbour is flat:

| r (px) | bright | neighbour (p=0.504) | ratio |
|---|---|---|---|
| 4.5 | 36504 | 1660 | 22.0 |
| 5.5 | 14801 | 646 | 22.9 |
| 6.5 | 6304 | 282 | 22.3 |
| 7.5 | 3103 | 146 | 21.2 |
| 8.5 | 1715 | 84 | 20.3 |
| 9.5 | 1052 | 51 | 20.8 |
| 10.5 | 679 | 34 | 19.8 |

A constant ratio across the unclipped range means one profile is a scaled copy of the other: same PSF, about
**21x** the flux. Unclipped, its peak would be 10-12x full well.

### Brightening an unsaturated star reproduces the artifact

Take the neighbour at (4821, 3192), multiply its signal by `k`, clip at full well, apply the 3x3 hot-pixel median
— in that order, as the sensor and then the detector do — and fit it exactly as the detector does with production
settings (Moffat beta=4, plain least squares, `PSFResolution = 10`, saturated samples masked) over the bright
star's 25x24 box. Its true FWHM, 3.989 px, never changes:

| k | peak / full well | masked samples | FWHM, `A <= 2` (production) | error | FWHM, amplitude uncapped | uncapped A | error |
|---|---|---|---|---|---|---|---|
| 1 | 0.5 | 0/110 | 3.978 | -0% | 3.978 | 0.48 | -0% |
| 2 | 0.9 | 0/110 | 3.998 | +0% | 3.998 | 0.96 | +0% |
| 4 | 1.9 | 1/110 | 3.862 | -3% | 3.475 | 2.56 | -13% |
| 6 | 2.8 | 1/110 | 4.420 | +11% | 3.892 | 2.61 | -2% |
| 8 | 3.7 | 3/110 | 4.647 | +16% | 4.469 | 2.20 | +12% |
| 12 | 5.6 | 5/110 | 5.291 | +33% | 4.184 | 3.83 | +5% |
| 16 | 7.4 | 5/110 | 5.740 | +44% | 4.432 | 3.99 | +11% |
| **21** | **9.8** | 5/110 | **6.070** | **+52%** | 5.060 | 3.15 | +27% |
| 30 | 14.0 | 9/110 | 6.489 | +63% | 4.088 | 10.57 | +2% |
| 50 | 23.3 | 9/110 | 7.533 | +89% | 4.746 | 8.88 | +19% |

At `k = 21`, the measured brightness ratio, the simulated star reports **FWHM 6.070 px** and **HFR 3.893 px**
against the real star's **6.060** and **3.838**. Brightness and clipping alone reproduce it.

With this production fit, the uncapped columns show the amplitude bound is not the fix. From `k = 4` on, the
unconstrained fit wants an amplitude above 2 (2.2 to 10.6), so the bound binds throughout — but removing it leaves
an error that wanders between **-13% and +27%** with no trend to correct for. This ladder has not been run through
the robust fit (`UsePSFAbsoluteDeviation`); see *Raising the bound with the recommended settings* for what the real
star does.

### The width is not identifiable from the wings

Real bright star, core masked, production least-squares fit, amplitude **fixed** at a series of values, everything
else free:

| A fixed | sigX | sigY | FWHM px | R^2 |
|---|---|---|---|---|
| **2.00** (the bound) | 7.22 | 6.72 | **6.06** | 0.976 |
| 3.00 | 6.09 | 5.75 | 5.15 | 0.995 |
| 4.00 | 5.15 | 5.43 | 4.60 | 0.991 |
| 6.00 | 4.71 | 4.45 | 3.98 | 0.973 |
| 10.40 (the physical value) | 4.11 | 3.64 | 3.36 | 0.944 |
| 20.00 | 3.70 | 2.92 | 2.86 | 0.920 |

FWHM slides from 6.06 to 2.86 px while R^2 stays inside 0.92-0.995: the wing-only data cannot pick a width, and
the bound pins `A` at the largest-FWHM end of that valley. Letting the fit go further does not help:

| fit on the real star | samples kept | A | FWHM px | error vs 3.989 | R^2 |
|---|---|---|---|---|---|
| production (`A <= 2`) | 95/100 | 2.000 | 6.060 | +52% | 0.976 |
| amplitude uncapped | 95/100 | 3.094 | 5.085 | +27% | 0.995 |
| uncapped, beta free (pins at its own 10.0 bound) | 95/100 | 2.475 | 5.751 | +44% | 0.997 |
| uncapped, saturation mask dilated 5 px | 78/100 | 1.308 | 5.694 | +43% | 0.993 |
| uncapped, saturation mask dilated 9 px | 55/100 | 0.500 | 7.013 | +76% | 0.970 |

(These are refits outside the detector. The detector's own solver, rebuilt with the bound removed, gives 5.07 px
with A = 3.116 for the uncapped row.)

Dilating the mask, to drop the shoulder samples that the hot-pixel median smeared the clipped plateau into, only
throws away the last informative samples. Changing the sampling does not matter either (`--psf-sweep` refits of the real
star, production fit otherwise):

| variation | FWHM px |
|---|---|
| production (`PSFResolution=10`) | 6.06 |
| `PSFResolution=30` / `60` | 6.14 / 6.15 |
| saturation mask disabled (clipped core kept) | 7.09 |
| bounding box halved | 6.29 |

**Where the bound is.** `upperBounds[0] = 2.0d`, in normalised [0,1] image units, in all four solver entry
points:

| file | line | method | used by |
|---|---|---|---|
| `StarDetection/PSFModeler.cs` | 353 | `PSFModelTypeAlglibBase.Solve` | Gaussian and fixed-beta Moffat (`Moffat_40`, `Moffat_25`, `Moffat_15`), `UsePSFAbsoluteDeviation` off |
| `StarDetection/PSFModeler.cs` | 235 | `PSFModelTypeAlglibBase.SolveIRLS` | the same types, `UsePSFAbsoluteDeviation` on |
| `StarDetection/MoffatPSFType.cs` | 308 | `FittableMoffatPSFAlglibType.Solve` | `MoffatFittable`, `UsePSFAbsoluteDeviation` off |
| `StarDetection/MoffatPSFType.cs` | 376 | `FittableMoffatPSFAlglibType.SolveIRLS` | `MoffatFittable`, `UsePSFAbsoluteDeviation` on |

It can only ever bind on a clipped star: with the production fit and with `PSFResolution = 20` + Huber alike,
exactly 5 of this frame's accepted fits have a fitted amplitude above 1.0, and they are precisely the 5 stars
flagged saturated.

### Raising the bound with the recommended settings

Measured with the detector's own solver: a temporary build (not committed; isolated worktree with the NINA deploy
step removed) read the bound from a file. At bound 2 it reproduced the unmodified `PSFResolution = 20` + Huber run
on all 1737 stars, with no FWHM or fit-acceptance difference.

Which stars change when the bound is raised:

| settings | bound | stars whose FWHM changes | fits newly accepted / rejected |
|---|---|---|---|
| production | 2 -> removed | 3, all saturated: (4707, 3261) 6.059 -> 5.070; (5066, 812) 3.435 -> 3.130; (6864, 522) 3.639 -> 3.492 | 0 / 0 |
| `PSFResolution = 20` + Huber | 2 -> 10 | 1, saturated: (4707, 3261) 5.806 -> 4.380 (A 2.000 -> 5.245, R^2 0.914 -> 0.955) | 0 / 0 |
| `PSFResolution = 20` + Huber | 10 -> removed | none | 0 / 0 |

Each saturated star against the median FWHM of the unsaturated, above-median-peak stars within 900 px, measured in
the same run:

| star (x, y) | saturated px | production, bound 2 | production, no bound | res20 + Huber, bound 2 | res20 + Huber, no bound |
|---|---|---|---|---|---|
| 3840, 2542 | 3 | 3.40 vs 3.99 (-15%) | 3.40 vs 3.99 (-15%) | 3.88 vs 3.92 (-1%) | 3.88 vs 3.92 (-1%) |
| 5066, 812 | 7 | 3.43 vs 3.89 (-12%) | 3.13 vs 3.89 (-19%) | 4.03 vs 3.90 (+3%) | 4.03 vs 3.90 (+3%) |
| 4262, 3751 | 8 | 3.54 vs 3.98 (-11%) | 3.54 vs 3.98 (-11%) | 3.88 vs 3.94 (-2%) | 3.88 vs 3.94 (-2%) |
| 6864, 522 | 12 | 3.64 vs 4.01 (-9%) | 3.49 vs 4.01 (-13%) | 4.07 vs 3.99 (+2%) | 4.07 vs 3.99 (+2%) |
| **4707, 3261** | **46** | 6.06 vs 3.94 (+54%) | 5.07 vs 3.94 (+29%) | 5.81 vs 3.91 (+49%) | **4.38 vs 3.91 (+12%)** |
| **mean absolute error** | | **20.0%** | **17.3%** | **11.3%** | **4.0%** |

Frame aggregate as the app computes it (all accepted fits, saturated included):

| settings | bound | n | median FWHM | MAD | max |
|---|---|---|---|---|---|
| production | 2 | 1511 | 4.1014 | 0.2297 | 6.06 |
| production | removed | 1511 | 4.1014 | 0.2297 | 5.51 |
| `PSFResolution = 20` + Huber | 2 | 1510 | 4.0654 | 0.2103 | 5.81 |
| `PSFResolution = 20` + Huber | removed | 1510 | 4.0654 | 0.2103 | 5.55 |

- `PSFResolution = 20` + Huber on its own brings the four lightly clipped stars from 9-15% too small to within 3%
  of their neighbours. The bound never binds on them there (fitted A 1.15-1.51).
- Raising the bound then changes only the heavily clipped star: +49% -> +12%. Bound 10 and no bound are identical.
- With the production fit, raising the bound is a wash: it roughly halves the heavy star's error but makes two
  lightly clipped stars worse.
- No frame median or MAD moves in any arm.

This rests on one heavily clipped star, which is still 12% wide, and the least-squares ladder above shows uncapped
error swinging with brightness. Rejecting saturated fits stays the recommendation until the equivalent ladder has
been run through the robust fit.

### Why nothing catches it

- `PSFGoodnessOfFitThreshold = 0.9` is checked against **R^2**, which is dominated by the dynamic range inside the
  box; the bad fit scores 0.972 and is accepted.
- **Reduced chi-squared is not a usable gate either.** It scales with the star's peak, because the beta=4 model
  error is proportional to amplitude:

  | peak decile | n | mean reduced chi^2 | max |
  |---|---|---|---|
  | 0.0020-0.0023 | 151 | 0.8 | 1.7 |
  | 0.0157-0.0403 | 151 | 28.8 | 165 |
  | 0.0405-0.9915 | 152 | 1467 | 62962 |

  The bright **unsaturated** neighbour sits at 2730 — higher than three of the five saturated stars (6413, 10069,
  10375). No fixed threshold separates them.
- `ExcludeSaturatedStarsFromHFR` (on) keeps the star's HFR out of the frame's `AverageHFR`
  (`HocusFocusStarDetection.StarsForHfrAggregation`), so **auto-focus is not affected**. There is no equivalent
  for the PSF aggregate: `result.FWHM` / `Sigma` / `Eccentricity` are medians over every star with a fit,
  saturated ones included (`HocusFocusStarDetection.cs:741-760`).

### The HFR side

Same brightening of the neighbour, clipped before the hot-pixel median, with HFR measured the way
`StarDetector.MeasureStar` does (gate at `StarClippingMultiplier x` the measurement noise sigma, GateOnly). This
reimplementation reproduces the detector's HFR on 300 random stars (median difference 0.0014 px) and exactly on
both stars here:

| case | HFR px |
|---|---|
| as observed (k = 1, aperture 10.0) | 2.535 (detector: 2.535) |
| aperture widened to 12.0 (k = 1) | 2.560 |
| k = 21 unclipped, aperture 12.0 | 2.603 |
| k = 21 clipped, aperture 10.0 | 3.787 |
| k = 21 clipped, aperture 12.0 | 3.893 (real star: 3.838) |

Clipping accounts for 1.29 of the 1.36 px excess (~95%). The wider aperture adds 0.025 px on its own, because the
per-pixel noise gate already drops the far-wing pixels it would add.

### Saturated stars: reject the fit

Of 1737 accepted stars, 5 are saturated. With production settings all five are biased, not just the obvious one:

| x, y | box | saturated px in box | masked PSF samples | fitted A | FWHM px |
|---|---|---|---|---|---|
| 3840, 2542 | 21x21 | 3 | 1 | 1.576 | 3.40 |
| 5066, 812 | 21x21 | 7 | 1 | 2.000 | 3.43 |
| 4262, 3751 | 20x21 | 8 | 1 | 1.795 | 3.54 |
| 6864, 522 | 21x21 | 12 | 1 | 2.000 | 3.64 |
| **4707, 3261** | 25x24 | **46** | **5** | 2.000 | **6.06** |

The frame's accepted fits run p05 = 3.72, median = 4.10, p95 = 4.50. The four lightly-clipped stars sit at or below
p05 and the heavily-clipped one is the frame maximum: the bias changes sign with how much of the core survives.
With `PSFResolution = 20` + Huber the four lightly clipped stars come within 3% of their neighbours and only the
heavily clipped one stays wrong (*Raising the bound with the recommended settings*).

Use the flag the detector already computes — `star.Background + star.PeakBrightness >= p.SaturationThreshold`, the
test behind `metrics.SaturatedBounds` and `StarsForHfrAggregation` — and skip `ModelPSF` for those stars, leaving
`star.PSF == null`, which every consumer already handles (including the frame aggregate).

It is a per-star correctness fix, not a dispersion fix. `result.FWHMMAD` is a *median* absolute deviation
(`HocusFocusStarDetection.cs:752`), so removing 5 of 1511 barely registers:

| | n | median | MAD | SD | p05 | p95 | max |
|---|---|---|---|---|---|---|---|
| current behaviour | 1511 | 4.1014 | 0.2297 | 0.2448 | 3.72 | 4.50 | 6.06 |
| saturated stars rejected | 1506 | 4.1021 | 0.2283 (-0.6%) | 0.2379 (-2.8%) | 3.73 | 4.50 | 5.51 |

---

## Part 2 — Where the frame's FWHM spread comes from

Fitting a quadratic surface in (x, y) to the per-star FWHM over the 1506 unsaturated fits:

```
total variance 0.0566 px^2  =  field structure 0.0218 (39%)  +  scatter about it 0.0348 (61%)
```

### A focus gradient across the sensor (39%)

Median FWHM by y-band, top to bottom, as the production pipeline measures it:

| y band | all unsaturated | brightest 50% | brightest 20% |
|---|---|---|---|
| 0-1597 | 3.987 | 3.983 | 3.985 |
| 1597-3194 | 4.030 | 4.030 | 4.057 |
| 3194-4791 | 4.132 | 4.115 | 4.106 |
| 4791-6388 | 4.289 | 4.294 | 4.285 |
| **delta** | **+0.302** | **+0.310** | **+0.300** |

Identical at every brightness, so it is not a faint-star artifact. Nearly flat in x (4.23, 4.04, 4.02, 4.12 left
to right), and **both** axes grow together (FWHMx 4.23 -> 4.63, FWHMy 3.71 -> 4.01) with eccentricity essentially
constant (0.471 -> 0.482). Both axes growing with no eccentricity change is *defocus*, not astigmatism — a tilt
about a roughly horizontal hinge. The production measurement understates it: with the hot-pixel median taken out
of the PSF measurement the same gradient is **+0.44 px** (Part 4).

The gradient is in the pixels, not in the PSF model. A model-free measurement on the RAW frame (no hot-pixel
median), fixed 8 px aperture, 452 bright unsaturated stars:

| y band | fit FWHM | second-moment FWHM | half-flux radius |
|---|---|---|---|
| 0-1597 | 3.986 | 4.783 | 2.111 |
| 1597-3194 | 4.057 | 4.803 | 2.146 |
| 3194-4791 | 4.103 | 4.876 | 2.187 |
| 4791-6388 | 4.289 | 4.988 | 2.269 |
| **delta** | **+0.303** | **+0.205** | **+0.158** |

Second moments of a heavy-winged profile read larger than a Moffat FWHM, so only the trend is comparable.

*Caveat:* one frame plus a quadratic fit cannot separate tilt from field curvature with authority. The
monotonic-in-y, flat-in-x pattern is far more tilt-like than curvature (which would be radial), but a sensor-tilt
measurement across a focus sweep is the instrument for this.

### A fixed-direction elongation (raises the median, not the spread)

Eccentricity is ~0.48 almost everywhere, and the major axis clusters at a fixed frame angle:

| set | n | mean axis | axial concentration R | median ecc |
|---|---|---|---|---|
| all unsaturated | 1506 | +71.2 deg | 0.528 | 0.48 |
| brightest 20% | 301 | +69.7 deg | 0.588 | 0.47 |

The per-decile mean axis stays inside +65..+77 deg at every brightness, and the angle between the major axis and
the radius vector has a median of 51 deg with no radial ordering. An optical aberration (astigmatism, coma)
orients radially or tangentially about the field centre, so one fixed frame direction points at **mount, guiding
or flexure**. It costs ~0.6 px on the major axis uniformly.

The orientation is model-free too: second moments on the RAW frame give an axial mean of -67.8 deg (R = 0.462) in
array coordinates. The fit reports theta with the opposite sign — on the 200 brightest stars with eccentricity
> 0.5, the median per-star difference between the fit's theta and the *negated* moment angle is 3.4 deg (42.6 deg
without the negation) — so that is +67.8 deg in the fit's convention, against the fit's +69.7 deg.

### Photon noise on faint stars (61%)

Scatter about the fitted field surface, by brightness:

| peak decile | gate SNR | residual MAD (px) |
|---|---|---|
| 0.0020-0.0023 | 15 | 0.356 |
| 0.0033-0.0040 | 26 | 0.208 |
| 0.0067-0.0094 | 55 | 0.121 |
| 0.0156-0.0379 | 157 | 0.092 |
| 0.0379-0.8263 | 566 | 0.069 |

Those fits are not broken; the stars do not have the photons. Restricting to the brightest half drops the scatter
SD from 0.184 to 0.110 px. Part 3's settings change trims this term; nothing removes it.

---

## Part 3 — Detector settings

Seventeen detector arms over the same frame with `TestApp star-probe`, each differing from production by one knob
or a stated combination. Median, MAD and `scatterSD` are over the **486 unsaturated stars with an accepted fit in
every arm**, so star composition cannot explain a difference. `scatterSD` is the residual SD about a quadratic
field surface in (x, y): the measurement-noise term with the optical structure taken out. Percentages are relative
to baseline.

| arm | detected | failed PSF fits | median FWHM px | MAD | scatterSD |
|---|---|---|---|---|---|
| baseline (production) | 1737 | 226 | 4.0714 | 0.1994 | 0.1226 |
| `UsePSFAbsoluteDeviation` (Huber IRLS) | 1737 | 223 | 4.0223 | -12.4% | +2.8% |
| `PSFResolution = 20` | 1737 | 223 | 4.0714 | -10.4% | -5.3% |
| `PSFResolution = 40` | 1737 | 230 | 4.0702 | -9.5% | -4.9% |
| **`PSFResolution = 20` + Huber** | 1737 | 227 | 4.0215 | **-14.0%** | **-6.5%** |
| `PSFPixelIntegration` | 1737 | 242 | 4.0176 | +0.8% | +0.3% |
| `StarBackgroundBoxExpansion = 6` | 1731 | 226 | 4.0725 | +0.2% | +0.4% |
| `PSFFitType = Gaussian` | 1737 | 251 | 4.2829 | -2.9% | +2.3% |
| `PSFFitType = Moffat_25` | 1737 | 242 | 3.9911 | +0.7% | +6.8% |
| `PSFFitType = Moffat_15` | 1737 | 249 | 3.8995 | -1.6% | +29.9% |
| `PSFFitType = MoffatFittable` | 1737 | 248 | 4.1637 | +8.7% | +21.7% |
| `HotpixelThresholdingEnabled` (thr 0.001) | 1206 | 163 | 4.1387 | -2.0% | +11.0% |
| `HotpixelThresholdingEnabled`, thr 0.01 | 1836 | 758 | 3.9427 | +42.5% | +39.5% |
| `HotpixelThresholdingEnabled`, thr 0.05 | 4278 | 2886 | 3.8554 | +24.4% | +11.0% |
| `HotpixelFiltering = false` | 1576 | 645 | 3.8285 | +19.8% | -2.4% |
| res20 + Huber + thr 0.05 | 4278 | 2961 | 3.8416 | +7.1% | -12.4% |
| res20 + Huber + thr 0.05 + pixel integration | 4278 | 2950 | 3.7888 | +8.6% | -11.1% |

- **`PSFResolution = 20` + Huber** is the best arm on both MAD and `scatterSD` among those that leave detection
  unchanged; `PSFResolution = 20` alone gets most of it (-10.4%, -5.3%). It is free: detected stars, star positions and **every HFR value are bit-identical** to baseline
  (`max |dHFR| = 0.0`); accepted PSF fits go 1511 -> 1510. Huber alone trims tails (MAD down, `scatterSD` up);
  `PSFResolution = 40` is no better than 20.
- **Gaussian** reads every star ~5% wider; the other Moffat variants add scatter, and beta-fittable pins beta at
  its bound.
- **Every hot-pixel arm** either loses stars (thr 0.001: 1206) or floods detection with hot-pixel junk (thr 0.05:
  4278 detections, 2886 failed fits). The lower medians there are real (Part 4) but these knobs are the wrong way
  to get them.

---

## Part 4 — The hot-pixel filter inflates FWHM ~6% and hides a third of the gradient

With `HotpixelThresholdingEnabled` off — the production setting — `ApplyHotpixelFilter` runs
`Cv2.MedianBlur(m, m, 3)`: an **unconditional 3x3 median over the whole measurement image**, the same image the PSF
is fitted to. On a bright isolated star it drops the peak 19.7% and widens the half-max width from 3.92 to
4.20 px.

This sensor needs *a* filter: **0.36% of the frame is isolated hot pixels** (>5 sigma above a 15x15 local
background, with the brightest neighbour under a third of the pixel's own amplitude) — **~2.2 inside an average
25x24 star box**. Turning the filter off gives 645 failed fits and +20% MAD (Part 3).

The existing threshold knob cannot fix it. `HotpixelFilterWithThresholding` gates on `|v - median3| > threshold`,
and **a star core deviates from its own 3x3 median just as a hot pixel does** (7453 ADU for the neighbour star).
Raising the threshold spares hot pixels before it spares star cores.

An **isolation test** separates them, because a hot pixel's neighbours stay at background and a star core's do
not. Repairing only those pixels, then refitting the PSF with **detection held fixed at the production boxes**
(903 stars; the pipeline already keeps the measurement image `srcImage` separate from the structure image, so this
needs no change on the detection side):

| measurement image | pixels rewritten | median FWHM | MAD | scatterSD | top-to-bottom gradient |
|---|---|---|---|---|---|
| production 3x3 median | all 61 171 488 | 4.0953 | 0.1941 | 0.1192 | +0.304 |
| isolation repair, ratio 3, 5 sigma | 221 966 | 3.8588 | 0.2337 | 0.1240 | +0.435 |
| isolation repair, ratio 2, 5 sigma | 232 706 | 3.8585 | 0.2338 | 0.1098 | +0.432 |
| isolation repair, ratio 2, 4 sigma | 278 032 | 3.8585 | 0.2338 | 0.1097 | +0.437 |
| isolation repair, ratio 1.6, 4 sigma | 285 965 | 3.8585 | 0.2332 | 0.1075 | +0.439 |

Stable across every repair setting:

1. **The reported FWHM is ~5.8% too high** (4.095 -> 3.859 px; 3.60" -> 3.39").
2. **The focus gradient is understated by ~43%** (+0.304 -> +0.439 px). The median compresses sharp stars more
   than broad ones, so it flattens exactly the signal a tilt search looks for. Gradient over `scatterSD` improves
   from 2.55 to 4.08.
3. **Per-star precision is ~20% worse** (MAD 0.194 -> 0.233), because the median was also suppressing noise. That
   does not reach the *frame* FWHM, a median over ~1500 stars: its standard error is 0.0063 px today vs 0.0075 px
   repaired. The 5.8% bias is systematic and does not average out.

An end-to-end run (repaired frame written to FITS, `HotpixelFiltering = false`) confirms the measurement side
(median 3.828 px, -6.5%, `scatterSD` -3.2%) but loses 26% of the detections, because the structure path lost its
smoothing too. That is why the repair belongs on the measurement image only, with the structure path keeping the
median + Gaussian it already applies. A FITS control of the unmodified frame reproduced the XISF run exactly, so
the round-trip introduces nothing.

---

## Error budget for this frame

On the repaired measurement, detection fixed, 903 stars, 0.8793 arcsec/px:

| y band | FWHM major px | FWHM minor px | minor arcsec |
|---|---|---|---|
| 0-1597 | 4.027 | 3.383 | 2.98 |
| 1597-3194 | 4.102 | 3.438 | 3.02 |
| 3194-4791 | 4.252 | 3.557 | 3.13 |
| 4791-6388 | 4.562 | 3.773 | 3.32 |

- **Seeing + optics floor** (minor axis, best part of the field): 3.383 px = **2.98"**
- **+ mount / guiding elongation**, fixed ~70 deg axis: 2.18 px = **1.92"** in quadrature -> major axis 3.54"
- **+ focus gradient**, bottom vs top: up to 1.67 px = **1.47"** in quadrature
- **+ the hot-pixel median filter**: +0.24 px, taking the reported frame FWHM from 3.86 to 4.10 px
  (3.39" -> 3.61")

---

## Reproducing

```bash
dotnet Joko.NINA.Plugins/TestApp/bin/Debug/net8.0-windows7.0/TestApp.dll star-probe \
  --image "<frame>.xisf" --settings "<harness settings>.json" \
  --out "<outdir>" --near 4707,3262 --radius 260 --psf-sweep
```

`star_probe.csv` carries, per accepted star: centre, structure box, background, peak, HFR and the HFR aperture,
the full PSF fit (sigmas, FWHM in px and arcsec, theta, eccentricity, R^2, reduced chi^2, fitted amplitude and
background, beta), the PSF sampling step, and how many of its samples the saturation mask dropped. `--psf-sweep`
refits each probed star at other `PSFResolution` values, without the saturation mask, and over a halved box, using
the detector's own measurement noise sigma; its `base` column reproduces the detector's `fwhmPx`, which is the
check that the reconstructed measurement image is the one detection fitted. The census columns and the sweep are
only produced for an unbinned mono frame. Settings arms were produced by varying one option in the harness settings
file per run. The amplitude-bound arms need a rebuilt solver, since the bound is a literal; see *Raising the bound
with the recommended settings*. See `.claude/docs/testapp-cli.md`.
