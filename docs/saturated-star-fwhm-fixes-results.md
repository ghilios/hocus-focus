# Detector fixes from the saturated-star FWHM investigation: before / after

What changed, and what it measurably does. The design and the evidence behind it are in
`docs/saturated-star-fwhm-investigation-results.md`; this records the implementation's measured effect.

## What shipped

1. **`PSFResolution = 20` and `UsePSFAbsoluteDeviation = true` are the defaults** (recommendation 3).
2. **Saturated stars get no PSF fit** (recommendation 5). `Background + PeakBrightness >= SaturationThreshold`
   leaves `Star.PSF` null. It is not counted as a fit failure, because no fit was attempted.
3. **The measurement image is repaired with an isolation test, not a median** (recommendation 4). The structure
   (candidate-formation) image keeps the 3x3 median it has always had, so both images are now derived from the
   same raw pixels rather than one feeding the other.

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

## Part 3 — Does losing faint stars cost anything? (AF-bank verification)

Part 1 raises the question the PSF numbers cannot answer: the change moves which stars are detected, so are the
ones it drops real? `bank-verify` answers it against the bank's detector-independent golden star sets, in the
C0 as-default config at the shipped `NoiseClippingMultiplier` of 4, before and after, over all 19 runs.

Paired per run, so every number is the same run measured twice:

| | mean before | mean after | better / worse / tied |
|---|---|---|---|
| recall @ SNR >= 12 | 0.6056 | 0.6502 | **13 / 1 / 3** |
| recall @ all tiers | 0.4653 | 0.4944 | **13 / 1 / 3** |
| precision | 0.9540 | 0.9504 | 4 / 8 / 5 |
| detections | 6169 | 6360 | 13 / 1 / 5 |
| AF sigma_focus (lower better) | | | 6 / 9 / 4 |
| AF fit R^2 | 0.9962 | 0.9938 | 5 / 10 / 4 |
| sensor model R^2 | 0.7376 | 0.7545 | 9 / 9 / 0 |

**Recall is up and precision is not paid for it.** Recall against the high-confidence golden tier improves on
13 of 17 scorable runs and drops on one (`vsn07`, 0.726 -> 0.668). Precision's median change is exactly zero and
its mean moves 0.004. So the stars the change gains are real golden stars, and the ones it drops on some frames
are not costing precision.

**Autofocus is neutral.** sigma_focus and the AF fit R^2 are a coin flip run to run, with median changes of
zero. Nothing here says the focus curve got better or worse, which is the answer this check existed to get:
HFR shifted about 6% but autofocus fits a curve shape, not an absolute HFR.

### One unexplained number

`sensor RMS` (the paraboloid fit's residual, in microns) falls by a near-constant factor on **every single run**:
the after/before ratio runs 0.09 to 0.28 with a median of 0.24, across rigs from 10 to 2765 stars in the model.
The sensor model's R^2 is essentially unchanged run by run over the same range, which means the fitted surface's
own scale shrank by about the same factor. A uniform factor across every rig is a scale change, not a quality
change, and the cause is not established here. It is recorded rather than claimed as an improvement, and it is
worth a look before anyone reads sensor RMS as a regression signal.

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
$EXE bank-verify --runs "D:\Autofocus Bank" --out <dir> --nc-sweep 4 --match-radius 12 --commit <hash>
$EXE bench-gpu --compare --image "<frame>.xisf"
```

The before arm is the same commands against a build of the parent commit, in a worktree with the NINA deploy
step disabled. See `.claude/docs/testapp-cli.md`.

The raw outputs every table above is computed from are committed under `docs/data/`:
`psf-bank-before.csv` / `psf-bank-after.csv` (per run), `psf-bank-sub_res*.csv` (the four timing-decomposition
arms), and `bank-verify-before.md` / `bank-verify-after.md`.
