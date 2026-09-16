# Detector fixes from the saturated-star FWHM investigation: before / after

What changed, and what it measurably does. The design and the evidence behind it are in
`docs/saturated-star-fwhm-investigation-results.md`; this records the implementation's measured effect.

> **What actually ships, after review.** `UsePSFAbsoluteDeviation` stays **off**: the decomposition below
> showed it costs 5.6x the PSF fitting time for about a fifth of the benefit. `PSFResolution = 20` is the new
> default with **no upgrade logic**, so it reaches an existing profile only through Restore Defaults or a
> detection optimization. And the measurement-path hot-pixel repair is behind a new advanced option,
> **Measurement Hotpixel Repair**, which is **off** for every existing configuration and for any settings file
> that predates it; Restore Defaults and applying an optimization turn it on, because both re-derive the
> acceptance gates it shifts. With the option off, detection is bit-identical to the pipeline before this
> change. Every measurement below was taken with the repair ON, which is what it does once enabled.

## What shipped

1. **`PSFResolution = 20`** becomes the default (recommendation 3). `UsePSFAbsoluteDeviation` was measured
   alongside it and is NOT adopted, on cost grounds.
2. **Saturated stars get no PSF fit** (recommendation 5). `Background + PeakBrightness >= SaturationThreshold`
   leaves `Star.PSF` null. It is not counted as a fit failure, because no fit was attempted.
3. **The measurement image is repaired with an isolation test, not a median** (recommendation 4), behind the new
   **Measurement Hotpixel Repair** option. The structure (candidate-formation) image keeps the 3x3 median it has
   always had, so both images are derived from the same raw pixels rather than one feeding the other.

`StarDetector.StarDetectorVersion` goes 3 -> 4, so every cached detection result from a previous build misses.

## The isolation test

A pixel on the measurement image is rewritten to its 3x3 median when BOTH hold, against a coarse local
background grid (block median and 1.4826·MAD sigma, 128 px blocks, bilinearly interpolated between block
centres — the reduction the golden reference detector's `coarse_bg` uses):

- its amplitude above the local background exceeds **5** local sigmas, and
- the brightest of its eight neighbours sits below **1/3** of that amplitude.

A hot pixel passes both: its neighbours are at background. A star core fails the second: its neighbours carry
most of its amplitude. The thresholded median cannot make that distinction at all — a star core deviates from
its own 3x3 median exactly as a hot pixel does, which is why at the shipped `HotpixelThreshold` of 0.001 it
rewrites star cores.

---

## Part 1 — The investigated frame

**Frame:** `LIGHT_2026-09-15_21-16-16_O_0.00_300.00s_0441.xisf` (Sh2-129, O-III, 300 s, QHY600M mono, 9576x6388,
0.8793 arcsec/px).
**Settings:** the user's exported per-filter set for filter O, converted to a harness option bag. The after arm
differs by exactly two keys, `PSFResolution` 10 -> 20 and `UsePSFAbsoluteDeviation` False -> True.

> **A trap worth recording.** `--settings` takes a harness option bag. Handing the runners the plugin's own
> `O_settings.json` export instead produces an *empty* bag and a run at stock defaults, with no error and no
> warning: 1615 detections rather than 1737. `TestApp convert-settings` now performs the conversion through the
> plugin's own import path. The before arm below reproduces the investigation's Part 1 table exactly, which is
> the check that the two arms are comparable.

### Frame totals

| | before | after | |
|---|---|---|---|
| detected stars | 1737 | 1439 | -17.2% |
| PSF fits accepted | 1511 | 1099 | |
| saturated stars | 5 | 6 | |
| saturated stars carrying a PSF | 5 | **0** | |
| measurement hot pixels repaired | n/a | 223 982 | 0.37% of the frame |
| HFR median | 2.4001 | 2.3010 | -4.1% |
| FWHM median (px) | 4.1014 | 3.8101 | -7.1% |
| FWHM MAD (px) | 0.2297 | 0.2029 | -11.7% |
| FWHM p95 (px) | 4.5011 | 4.2439 | |
| FWHM max (px) | 6.0594 | 4.6982 | |
| `scatterSD` (px) | 0.1954 | 0.1224 | -37.4% |
| top-to-bottom FWHM gradient (px) | +0.3030 | +0.4108 | +35.6% |

### The same stars, measured twice

1058 unsaturated stars have an accepted fit in both arms. Restricting to those removes composition from the
comparison:

| | before | after | |
|---|---|---|---|
| FWHM median (px) | 4.0892 | 3.8101 | -6.8% |
| FWHM MAD (px) | 0.2059 | 0.2029 | -1.5% |
| `scatterSD` (px) | 0.1465 | 0.1216 | -17.0% |
| HFR median (px) | 2.4226 | 2.2918 | -5.4% |
| top-to-bottom gradient (px) | +0.2964 | +0.4121 | +39.0% |
| R^2 median | 0.9762 | 0.9592 | |

Per star, the median FWHM moves -0.2489 px and the median HFR -0.1200 px.

These land where the investigation predicted: it expected the repair to take ~5.8% off the median FWHM and to
show the focus gradient ~43% larger, and both hold. What it did **not** predict is the per-star precision: it
expected MAD to worsen ~20%, because the median was also suppressing noise. It does not, because recommendation
3 pulls in the other direction by about the same amount. The two recommendations were measured separately and
happen to cancel.

### Saturated stars

Before, all five were biased, with the sign set by how much of the core survived the clip. "Local median" is the
median FWHM of unsaturated stars with an accepted fit within 900 px.

| star (x, y) | saturated px in box | fitted FWHM | fitted amplitude | R^2 | vs local median |
|---|---|---|---|---|---|
| 3840, 2542 | 3 | 3.40 | 1.576 | 0.988 | 4.01 (-15%) |
| 5066, 812 | 7 | 3.43 | 2.000 (at the bound) | 0.986 | 3.90 (-12%) |
| 4262, 3751 | 8 | 3.54 | 1.795 | 0.995 | 3.99 (-11%) |
| 6864, 522 | 12 | 3.64 | 2.000 (at the bound) | 0.994 | 3.98 (-9%) |
| **4707, 3261** | **46** | **6.06** | 2.000 (at the bound) | 0.972 | 3.94 (**+54%**) |

After, none of the six saturated stars carries a PSF, and the frame's FWHM maximum drops from 6.06 px to
4.70 px. Every R^2 above is comfortably over the 0.9 gate, which is the point: goodness of fit does not see this.

### The 17% detection drop

A real consequence the investigation did not anticipate. It is **specific to this frame's settings**, not to the
change: across the bank at the shipped defaults the star count is a wash (Part 2). The whole of it is the
sensitivity gate:

| rejection reason | before | after |
|---|---|---|
| low sensitivity | 2419 | 2769 |
| contaminated | 52 | 2 |
| too low HFR | 2 | 0 |
| every other gate | unchanged | unchanged |

Candidate formation is untouched (5319 candidates in both arms), as designed. What moved is the **measurement
image's noise estimate**. The `Sensitivity` gate compares a star's normalised brightness against that sigma, and
the median filter was suppressing it. With the isolation repair the sigma is the frame's honest noise, so the
same numeric setting is a stricter bar.

The stars it drops are the faint tail, and they are the ones the investigation's Part 2 identified as the frame's
dominant noise term:

| | dropped (349) | kept (1388) |
|---|---|---|
| peak brightness, median | 0.00231 | 0.00629 |
| peak brightness, p95 | 0.00305 | |
| measured sensitivity, median | 15.97 | 44.39 |
| FWHM MAD (px) | 0.3380 | 0.2059 |

Their fitted widths scatter 64% more than the stars that survive. 51 stars are newly detected, almost all of them
recovered from the contamination gate.

**What a user should do about it.** Nothing, unless they want the faint stars back, in which case lower
Brightness Sensitivity or re-run the optimization wizard. This frame's 13.67 came from an optimization run
against the old, suppressed sigma; the shipped default of 10.0 costs nothing bank-wide.

---

## Part 2 — PSF modelling across the AF bank

One frame per run, the nearest-focus frame of each of the 19 runs in `D:\Autofocus Bank`, detected with
`ModelPSF` on at the **shipped defaults** rather than any saved settings. The frames are pinned in a manifest
chosen once, because the change moves HFR and re-selecting per arm would compare different frames. Rigs span
0.28 to 5.97 arcsec/px, mono and bayered, 9.8 MP to 102 MP, and 23 to 5581 detected stars per frame.

### Fit quality

Medians are per-run medians across the 19 runs; the change column is the median of the per-run ratios, which is
what a single user would see.

| | before | after | median per-run change |
|---|---|---|---|
| detected stars | 400 | 431 | 0.0% |
| PSF fits accepted | 292 | 291 | 0.0% |
| PSF fits failed | 96 | 135 | +17.0% |
| HFR median (px) | 1.8559 | 1.7631 | -6.6% |
| FWHM median (px) | 3.7029 | 3.4676 | -10.2% |
| FWHM MAD (px) | 0.3275 | 0.2968 | -9.9% |
| FWHM max (px) | 5.5955 | 4.6512 | -14.5% |
| `scatterSD` (px) | 0.2702 | 0.1892 | **-21.9%** |
| eccentricity median | 0.4211 | 0.4434 | +4.3% |
| R^2 median | 0.9662 | 0.9658 | -0.2% |

The direction is consistent rather than an average of wins and losses:

- `scatterSD` improves on **16 of 19** runs (median ratio 0.78).
- FWHM MAD improves on **17 of 19** (median ratio 0.90).
- FWHM maximum falls on **18 of 19** (median ratio 0.86) — mostly the saturated stars leaving the distribution.
- Saturated stars go 68 -> 154 across the bank, and **none of them carries a PSF**. The count rises because the
  measurement image is no longer median-suppressed, so a clipped core now reads its true clipped value.

### Detection is a wash at the defaults

Total detected across the bank moves 23 281 -> 22 821, **-2.0%**: 9 runs gain stars, 5 lose, 5 are unchanged,
with per-run ratios from 0.67 to 1.70 and a median of exactly 1.00.

This is the important correction to Part 1. The investigated frame lost 17% of its stars, but it was detected
with a Brightness Sensitivity of 13.67 that an optimization run had landed against the OLD, suppressed noise
estimate. At the shipped default of 10.0 the honest sigma costs nothing bank-wide. The exposure is to settings
tuned against the old sigma, not to the default configuration.

PSF acceptance rate moves 0.847 -> 0.823 (per-run medians), and fit failures rise 17%: with the measurement
image's noise no longer suppressed, more fits land under the fixed R^2 gate of 0.9. The median R^2 of the fits
that ARE accepted is unchanged (-0.2%), so this is the gate biting, not the fits degrading. Lowering
**PSF Fit Threshold** is the lever if a rig wants those stars back.

### Runtime

This is the real cost of the change, and it is not small.

| across all 19 frames | before | after | |
|---|---|---|---|
| detection, PSF off | 54.6 s | 58.9 s | **1.08x** |
| PSF stage | 10.3 s | 114.1 s | **11.1x** |
| stars fitted | 18 274 | 17 013 | |
| ms per fitted star | 0.56 | 6.71 | 11.95x |

**Detection itself costs 8% more.** That is the isolation repair: a full-frame clone, a coarse local-background
grid, and one parallel pass, against the median filter it replaces. This is the number that matters for
autofocus, which runs with `ModelPSF` off.

**PSF modelling costs about 11x more**, entirely from the two new defaults. Splitting them apart over six of
the bank's frames (all four arms on the after build, so the measurement repair is common to all of them, and
`ms/star` normalises out the star count):

| arm | PSF stage | vs old | ms per fitted star | FWHM MAD | vs old | `scatterSD` | vs old | fits accepted |
|---|---|---|---|---|---|---|---|---|
| resolution 10, least squares (the old default) | 7.2 s | 1.00x | 0.51 | 0.2106 | | 0.1908 | | 75.0% |
| resolution 20, least squares | 13.1 s | **1.83x** | 0.93 | 0.1983 | -4.5% | 0.1781 | **-6.4%** | 74.9% |
| resolution 10, Huber | 40.1 s | **5.60x** | 2.84 | 0.2044 | -2.3% | 0.1887 | -1.1% | 75.1% |
| resolution 20, Huber (the new default) | 77.3 s | **10.80x** | 5.49 | 0.1954 | -5.3% | 0.1761 | **-7.7%** | 75.0% |

The two knobs are not equal value for money:

- **`PSFResolution = 20` is cheap and does most of the work.** 1.8x the time for 6.4% off the scatter.
- **`UsePSFAbsoluteDeviation` is where the cost is.** On its own it is 5.6x the time for 1.1% off the scatter.
  Adding it on top of resolution 20 takes 1.8x to 10.8x and moves the scatter from -6.4% to -7.7%.

So roughly **80% of the added cost buys the last 20% of the improvement.** Both are shipped as defaults here,
but a rig that cares about analysis latency more than about the last bit of FWHM consistency should turn
**PSF MAD Fitting** off and keep **PSF Resolution** at 20: that lands at 1.8x for most of the benefit.

Note the fit acceptance rate is **identical (75.0%) in all four arms**. The rise in failed fits reported above
is therefore entirely the measurement image's honest noise against a fixed R^2 gate, not something either of the
two settings does.

### What this does not cover

Autofocus runs with `ModelPSF` off, so the 11x does not touch it; the 8% is what autofocus pays. PSF modelling
is what the Star Detection Results panel, the Aberration Inspector and Review Frames use, and on a 100 MP frame
with 5000 stars the PSF stage now runs about 21 s rather than 2 s.

---

## Part 3 — Autofocus and sensor-model quality across the AF bank

`bank-verify` over all 19 runs of `D:\Autofocus Bank`, C0 as-default at `NoiseClippingMultiplier` 4, before vs
after, **with `--settings` pinned to one file for both arms and both reports recording the same profile**.

> An earlier version of this section was measured on arms that had been launched WITHOUT `--settings`. They read
> different option bags (68 keys from profile `astrodet` versus 44 from `Default`) and loaded different NINA
> profiles. Detection was unaffected, because C0 builds its params from
> `BuildDefaultStarDetectorParams` rather than the file, so the recall and precision numbers were right by luck.
> The autofocus and sensor columns were not: they read the inspector and AF options from the file, and the
> visible symptom was a "uniform 4x shrink" in the sensor model's residual and recovered tilt that does not
> exist. See the guard note at the end of this section.

### Detection

| | mean before | mean after | better / worse / tied |
|---|---|---|---|
| recall @ SNR >= 12 | 0.6056 | 0.6502 | **13 / 1 / 3** |
| recall @ all tiers | 0.4653 | 0.4944 | **13 / 1 / 3** |
| precision | 0.9540 | 0.9504 | 4 / 8 / 5 |
| detections | 6169 | 6360 | 13 / 1 / 5 |

Recall against the high-confidence golden tier improves on 13 of 17 scorable runs and drops on one (`vsn07`,
0.726 -> 0.668). Precision's median change is exactly zero. The stars the change gains are real golden stars.

### Sensor model

| | mean before | mean after | better / worse / tied |
|---|---|---|---|
| R^2 | 0.7376 | 0.7588 | **11 / 4 / 3** |
| RMS residual (microns) | 50.22 | 37.65 | 8 / 7 / 3 |
| stars in model | 470.7 | 480.7 | 10 / 2 / 7 |

The fit is better or neutral. **Recovered tilt is essentially unchanged run by run** — 0.03/0.03, 0.31/0.31,
0.52/0.51, 0.80/0.84, 5.99/6.33 degrees and so on — with one exception, `lumos` (2.74 -> 0.63), whose fit also
improved (R^2 0.975 -> 0.987) and which is one of the two runs with no scorable recall at all.

### Autofocus

This is the one place with a mild negative signal.

| | median ratio after/before | p10 | p90 | better / worse / tied |
|---|---|---|---|---|
| sigma_focus (lower better) | 1.000 | 0.847 | 1.858 | 6 / 9 / 4 |
| fit R^2 (higher better) | 1.000 | 0.989 | 1.000 | 5 / 10 / 4 |
| reduced chi^2 (lower better) | 1.018 | 0.685 | 3.343 | 4 / 11 / 4 |

The median run is unchanged on all three, but the tail is asymmetric: more runs get slightly worse than get
better, and a few get materially worse. Sorted by the ratio, the worst are `caboose` (1.309 -> 3.557 steps,
2.7x), `fmeschia_Focus` (9.785 -> 18.182, 1.9x), `vsn07` (2.474 -> 4.004, 1.6x) and `muggsie`
(15.143 -> 18.978, 1.3x); the best by far is `FlyData` (4.474 -> 1.653, 0.37x).

**The worst AF runs are the runs whose recall improved most.** `caboose` goes 0.808 -> 0.954 recall and
`fmeschia_Focus` 0.702 -> 0.947. With the measurement image no longer median-suppressed, peak brightness rises
and more faint stars clear the gate at the default Sensitivity of 10.0. Those extra stars are real, and they are
also the noisiest, so the frame's HFR median gets a wider spread and the curve fit's self-reported uncertainty
widens with it. It is a detection/precision trade, not a broken fit: fit R^2 barely moves (p10 0.989).

Whether a wider sigma_focus means autofocus actually lands further from true focus is a question this bank
cannot answer, because it has no ground truth. Part 4 measures it where truth exists.

### The guard this section needed

`HarnessSettingsStore`'s default path resolves per machine **and per binary directory**, which is what let two
arms diverge in silence while each printed a plausible `Settings:` banner. `bank-verify` now records
`settingsPath` and `settingsPinned` beside `profileId` in the JSON, prints both under the report header with an
explicit comparability line, and writes a loud stderr warning when `--settings` was omitted. `fitInputs` existed
to make two reports comparable and did not catch this: its four AF-fit values happened to agree across the two
different files.

---

## Part 4 — The synthetic bank: absolute autofocus accuracy, and a tilt null test

All 22 datasets of `D:\SyntheticAutofocusBank`, same harness, same pinned settings, same profile. The synthetic
bank carries rendered ground truth, which makes two things measurable that the real bank cannot answer:

- **Absolute autofocus accuracy.** `bank-verify` now reads `OptimalFocuserPosition` from `synthetic_meta.json`
  and scores the signed error between it and the fitted best focus. That is accuracy, not the fit's own
  self-reported precision.
- **A sensor-model null test.** Every dataset renders `TiltAngleDegrees = 0` and `TiltAmountMicrons = 0`, so any
  recovered tilt is invented. This measures spurious tilt; it cannot measure tilt accuracy.

### Autofocus lands in the same place

| | mean before | mean after | median before | median after |
|---|---|---|---|---|
| \|focus error\| (focuser steps) | 0.2334 | 0.2125 | 0.0392 | 0.0519 |
| sigma_focus | 0.8434 | 0.9014 | 0.6954 | 0.6627 |
| fit R^2 | 0.9566 | 0.9492 | 0.9991 | 0.9990 |

Nine datasets get closer to truth and 13 get further, but **read the units**: these are fractions of a single
focuser step, on rigs whose steps are 2 to 5 microns. The median error moves 0.039 -> 0.052 steps, which is
about a tenth of a micron. The worst dataset either way is `D13_apo200_1800mm` at 1.5 steps, roughly 3 microns.
Both arms land on true focus to far inside anything that matters.

**This is the answer Part 3 could not give.** The real bank showed sigma_focus widening on a few runs; the
synthetic bank shows that the fitted position does not actually move away from truth. The widened uncertainty is
the curve honestly reporting a noisier input, not autofocus getting worse at its job.

### The sensor model invents no tilt

| | mean before | mean after |
|---|---|---|
| \|recovered tilt\| (degrees, truth 0) | 0.0026 | 0.0026 |
| RMS residual (microns) | 0.7304 | 0.6299 |
| R^2 | 0.0937 | 0.1058 |

Recovered tilt is identical to four decimal places, at 0.003 degrees against a truth of zero. The residual
improves on 18 of 21 datasets. (R^2 is near zero in both arms because there is no tilt for the paraboloid to
explain, which is the correct answer on a flat field.)

### Detection: precision perfect, recall slightly down, and a different mechanism

Precision is **1.000 in both arms on all 22 datasets**: against exact rendered truth, neither arm produces a
single false positive. Recall against the high-confidence tier goes the other way from the real bank, though:
mean 0.7823 -> 0.7602, worse on 7 datasets, better on 3, tied on 12. The losses concentrate on
`D04_esprit_550mm` (0.814 -> 0.665), `D16_esprit550_ha3` (0.870 -> 0.679), `D18_m24_deep_shed` (0.795 -> 0.658)
and `D20_m24_bright_control` (0.953 -> 0.858).

The reason the two banks disagree is that **the synthetic frames have almost no hot pixels**. A census of one
D18 frame finds 134 isolated hot pixels; the real investigated frame has 223 982. So the synthetic bank
exercises only half of this change, the half where the measurement image loses its blur, with none of the
compensating benefit of actually removing hot pixels. It is a worst case for the change, deliberately so.

Detection on that same D18 frame, before and after, accounts for the loss exactly:

| | before | after |
|---|---|---|
| structure candidates | 9225 | 9225 |
| detected | 395 | 301 |
| rejected: too low HFR | 3047 | **3125** |
| rejected: not centered | 577 | **605** |
| rejected: too flat | 52 | 43 |
| rejected: low sensitivity | 5 | 2 |
| rejected: too small / too distorted | 4727 / 405 | 4727 / 405 |

Candidate formation is bit-identical, as designed. The 94 lost stars are +78 to the **MinHFR floor** and +28 to
the **centering tolerance**, partly offset elsewhere. With the measurement image no longer smoothed, stars
measure sharper, so more of them fall under the 1.2 px `MinHFR` floor, and their centroids shift enough for a
few more to exceed `StarCenterTolerance`.

Note this is a *different* mechanism from the investigated frame in Part 1, where the loss was almost entirely
the sensitivity gate. Which gate bites depends on the rig and the settings:

| | dominant mechanism | star count |
|---|---|---|
| the investigated frame (Sensitivity 13.67, hot-pixel-rich) | sensitivity gate | -17% |
| the real bank at defaults | none; hot-pixel removal offsets the losses | wash |
| the synthetic bank (no hot pixels, sharp stars) | MinHFR floor, then centering | -24% on D18 |

**`MinHFR` is the same class of exposure as Brightness Sensitivity.** Its 1.2 px default was itself calibrated
against noise-inflated faint HFRs; honest HFR is lower still, so the same number is a stricter floor than it was.
Nothing here forces a change, since precision stays perfect and autofocus accuracy is unmoved, but it is the
knob to reach for if a clean, sharp-star rig loses stars it wants.

---

## GPU parity

The GPU early span is documented as an exact mirror of the CPU pipeline, so the isolation repair was written as
a matching kernel rather than left to fall back. `bench-gpu --compare` on the investigated frame, against the
CPU oracle:

```
measurement hotpixel repairs: cpu=223982 gpu=223982 EXACT
measurement differs from structure: cpu=True gpu=True EXACT
measurement image: max-abs-diff=0.00E+000, pixels-differing=0
K-sigma measurement: sigma rel-delta=9.91E-014
```

The repaired measurement image is **bit-identical** on both paths, and both agree with the detector's own
`MeasurementHotpixelCount`. The structure map keeps its pre-existing 1.19E-7 float-ordering divergence, which
flips zero binarized pixels.

---

## Reproducing

```bash
EXE=Joko.NINA.Plugins/TestApp/bin/Debug/net8.0-windows7.0/TestApp.exe

# The settings conversion the frame comparison depends on
$EXE convert-settings --import O_settings.json --out harness_before.json
$EXE convert-settings --import O_settings.json --out harness_after.json \
     --set PSFResolution=20 --set UsePSFAbsoluteDeviation=True

# The frame
$EXE star-probe --image "<frame>.xisf" --settings harness_after.json --out <dir> \
     --near 4707,3262 --radius 260 --psf-sweep --hotpixel-census

# The bank
$EXE psf-bank --runs "D:\Autofocus Bank" --out <dir> --select
$EXE psf-bank --runs "D:\Autofocus Bank" --out <dir> --manifest <dir>\psf_bank_manifest.json --label after
```

The bank verification and GPU parity:

```bash
$EXE bank-verify --runs "D:\Autofocus Bank"        --out <dir> --settings <one file> --nc-sweep 4 --match-radius 12 --commit <hash>
$EXE bank-verify --runs "D:\SyntheticAutofocusBank" --out <dir> --settings <one file> --nc-sweep 4 --match-radius 12 --commit <hash>
$EXE bench-gpu --compare --image "<frame>.xisf"
```

The before arm is the same commands against a build of the parent commit, in a worktree with the NINA deploy
step disabled. See `.claude/docs/testapp-cli.md`.

The raw outputs every table above is computed from are committed under `docs/data/`:
`psf-bank-before.csv` / `psf-bank-after.csv` (per run), `psf-bank-sub_res*.csv` (the four timing-decomposition
arms), `bank-verify-before.md` / `bank-verify-after.md` (the real bank) and `synth-verify-before.md` /
`synth-verify-after.md` (the synthetic bank).
