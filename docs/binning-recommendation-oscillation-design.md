# Detection-Binning Recommendation Oscillates Between Runs — Diagnosis

**Reported:** a user (TinkerBolts rig) sees the Star Detection Optimization Wizard "oscillate between
recommendations, such as binning, each time it is run."

**Status:** REPRODUCED, root cause identified and **isolated** — four headless arms rule out the optimizer
itself (it is deterministic and converges to a fixed point) and leave exactly one cause: two writers publish
two different quantities into the one field the recommendation reads, and this rig's two values straddle the
decision boundary. No fix implemented yet — the fix is a user-visible behaviour change to a recommendation
(see *Fix options*), and this document is the spec it should be chosen from.

**Evidence run:** `D:\Autofocus Bank\TinkerBolts\AutoFocus_20260929_040614` (plugin 4.0.0.20, detector v3).

---

## 1. The rig, and why it sits on a knife edge

| | |
|---|---|
| Camera | ZWO ASI2600MC Pro, 3.76 µm, 6248×4176, Bayer RGGB, **XBINNING = 1** |
| Scope | Celestron EDGE-HD8, FOCALLEN 1422 mm, f/7 |
| Pixel scale | **0.5454 "/px** unbinned |
| His settings | Simple mode, `Simple_PixelScale = LongFocalLength`, `Simple_FocusRange = WideRange`, **`DetectionBinning = 2x2`** |
| Sweep | 9 focuser positions × 4 frames, step 343, 8 s subs |
| Fit | R² = 0.977 |

The frames are **camera-unbinned**; `DetectionBinning` is a *software* resample applied inside detection, so
both binning arms below run on the same FITS files. No binned copy of the run was needed.

His measured in-focus HFR is **≈4.3–4.7 captured px**. `DetectionBinningResolver.RecommendFromHfr` is a bare

```csharp
Clamp((int)Math.Round(measuredHfrPixels / TargetHfrPixels /* 3.0 */, MidpointRounding.AwayFromZero))
```

so the 1x1 / 2x2 decision boundary is **exactly 4.5 px**, with **no hysteresis and no dead band**. His rig
lands within ±4 % of it. Anything that perturbs the measurement by 0.2 px flips the advice.

## 2. Root cause — two writers of one field, measuring two different things

`InFocusHfrRecord` stores one profile-scoped value (`LastInFocusHfrPixels`) that every binning
recommendation reads. **Two code paths write it, and they do not write the same quantity:**

| Writer | Site | Quantity | This run |
|---|---|---|---|
| Auto-focus run | `AutoFocusEngine.cs:2933` | `FinalHFR` — a **real exposure** at the settled position | **4.662 px** |
| Wizard live-sweep Accept | `StarDetectionOptimizerWizardVM.cs:4468` | `summary.MeasuredInFocusHfr` = `BestFit.Minimum.Y` — the **fitted curve minimum** | **4.314 px** |

Run them through the comparator:

- `RecommendFromHfr(4.662)` = round(1.554) = **2** → "Measured in-focus HFR 4.7 px - 2x2 is right", and
  `ShouldShowRecommendation` is **false**, so the Options-page row is *hidden* (it agrees with his 2x2).
- `RecommendFromHfr(4.314)` = round(1.438) = **1** → the row **appears** saying
  "Measured in-focus HFR 4.3 px - 1x1 recommended".

**The boundary (4.5) falls between the two writers' values.** So the advice alternates with whichever ran
last: wizard → "switch to 1x1"; next auto-focus → row vanishes, "2x2 is right"; next wizard → "switch to
1x1". That is the reported oscillation, and it is fully deterministic.

**Preconditions, stated exactly**, because they decide which of two shapes the user sees:

- The wizard writes only on **Accept** and only after a **Live sweep** (`lastRunWasLive`,
  `StarDetectionOptimizerWizardVM.cs:4467`) — replaying a saved run deliberately does not overwrite the
  rig's measurement.
- The auto-focus writer has no *explicit* live/replay guard, but it is protected **incidentally**: it
  publishes `regionState.FinalHFR`, which is assigned only in `FinalHFRMeasurementAction`
  (`AutoFocusEngine.cs:1218`), wired only on the live `RunImpl` path (`:1974`). `RerunImpl` — the
  replay/loaded-run path — reaches the same `OnCompleted` (`:2711`) with `FinalHFR` still **null**, so
  `if (measuredInFocusHfr.HasValue)` skips the write. Replaying a saved run therefore does not publish
  either. Worth knowing because it is a guard by absence, not by intent: anything that later gives the
  replay path a final measurement would silently start overwriting the rig's value.
- So **both writers fire only on live work**, and the oscillation needs a user who alternates live
  auto-focus runs with live wizard sweeps — which is exactly the tune-then-verify loop this wizard is for.
  The stored value then alternates 4.662 ↔ 4.314 and the Options-page row appears and disappears run to run.
- Independently of the stored value, the wizard's **start page and summary page disagree within one run**:
  the start page (`SweepDetectionBinningRecommendation`) reads the stored value — "4.7 px - 2x2 is right" —
  while the summary says "2x2 → 1x1 (measured in-focus HFR 4.3 px)" and offers "Optimize again at 1x1".

This is not an unknown hazard — `InFocusHfrRecord`'s own class comment says *"Two writers, and they do NOT
agree on provenance"*, and `AutoFocusEngine` says the fitted minimum is *"deliberately NOT"* what it
publishes. The mitigation chosen at the time was an **R² ≥ 0.9 gate** on the wizard path. **That gate does
not bite here:** his R² is 0.977. The gate was calibrated against *catastrophic* vertex collapse (the
simulator case in the comment: 2.16 px vertex against 4.87 px truth, R² negative). It cannot catch a
well-fitting curve whose vertex merely sits a few percent below a real exposure — which is the ordinary,
expected case, because:

1. a hyperbola's vertex is an **extrapolated minimum**, always at or below every measured point. Here the
   two numbers describe the *same focuser position* — the run settled at 19975 and the fit's optimum is
   19975.06 — so the whole 0.35 px is **fit bias at the vertex**, not a position offset. And it is bias of
   exactly the kind `AutoFocusEngine`'s comment names: his wing frames carry only **3–4 accepted stars
   each**, so the far-from-focus end of the curve is built from a handful of fragments and drags the vertex
   down. R² = 0.977 does not notice, because the fit is a perfectly good fit *to those points*; and
2. the wizard's number comes from the **optimized** params, not the user's — and on a star-starved run the
   optimizer loosens gates to gain stars, which admits smaller stars and pulls the median HFR down further
   (measured below: 4.314 → **3.859 px** for the optimized variant).

Both effects push the wizard's value **below** the auto-focus value, always in the same direction.

### It is systematic, not a one-off

Every AF run in the bank that carries a `metadata.json` shows the vertex below the final HFR:

| run | `estimatedFinalHFR` (vertex) | `finalHFR` (exposure) | gap | flips? |
|---|---|---|---|---|
| **TinkerBolts/AutoFocus_20260929_040614** | **4.314** | **4.662** | −7.5 % | **YES (1x ↔ 2x)** |
| vsn07/AutoFocus_20260714_213204 | 1.468 | 1.625 | −9.7 % | no (both far below the band) |
| bobp/AutoFocus_20260626_215910 | 1.316 | 1.945 | −32 % | no |
| bobp_m101/AutoFocus_20260626_225408 | 0.959 | 1.854 | −48 % | no |

4 of 4 disagree; only TinkerBolts happens to straddle a boundary. Whether a user *sees* the bug is purely
where their rig's HFR falls relative to 4.5 / 7.5 / 10.5 px. With this run's −7.5 % gap the vulnerable
window above each boundary is ~8 % wide (an AF-measured 4.50–4.86 px flips); against the **optimized**
variant's 3.859 px — a −17 % gap — it is ~21 % wide (4.50–5.44 px). **0.55 "/px at ~2.4"
seeing puts a very common rig (8" SCT + APS-C CMOS) right inside it.**

## 3. The measurement is NOT factor-independent — the invariant the comparator rests on is false

`DetectionBinningResolver.RecommendFromHfr`'s doc comment states the contract it depends on:

> The HFR is in CAPTURED pixels — detection reports it that way at every factor — **so this answer does not
> depend on the factor the measurement was taken at.**

Measured on this run, it does:

| detected at | current-settings fitted vertex | recommends |
|---|---|---|
| 2x2 | **4.31406 px** | 1x |
| 1x1 | **3.86951 px** | 1x |

**−10.3 % for the same stars on the same frames.** The back-scaling (`ScaleToSourcePixels` multiplies HFR by
the factor, `StarDetector.cs:907`) is exact arithmetic, but the *measurement being scaled* is not: at 2x2 the
pixel-unit gates (`MinHFR` 1.2, `MinStarBoundingBoxSize` 6, `StructureLayers` 6, `NoiseReductionRadius` 4)
all cover twice the sky, and the 2×2 average raises per-pixel SNR, so a **different population of stars** is
accepted and measured.

0.44 px is **more than twice** the distance from his 4.314 to the 4.5 boundary. For this rig both factors
happen to land on the same side, so this does not flip *him* — but it is a live oscillation source for any
rig placed a little higher, and it means the comparator cannot be reasoned about as factor-free.

## 4. A second loop that is structurally open (but does not fire for this rig)

Inside the wizard, `OptimizationSummary.RecommendedDetectionBinning` is recomputed every run from
`RecommendFromHfr(MeasuredInFocusHfr)`. When it disagrees with the factor the run was detected at,
`ShowOptimizeAgainAtRecommendedBinning` surfaces an **"Optimize again at NxN"** button
(`StarDetectionOptimizerWizardVM.cs:2370`, `:5075`). Taking it re-runs the whole search at the new factor
and **builds a fresh summary, which recomputes the recommendation from the new factor's measurement**.

Nothing damps that: there is no hysteresis in `RecommendFromHfr`, no dead band in
`DetectionBinningDiffers`, and no memory of factors already visited. If the new factor's measurement points
back at the old one, the button simply reappears pointing the other way.

**Measured: it does not close for this rig.** Arm B re-ran the whole search at 1x1 and the recommendation
there is **1x1** (vertex 3.870 px), so taking the button once settles rather than ping-ponging. Reported as a
negative result, not a cleared risk — §3 shows the two factors' measurements differ by 2× the boundary
margin, so the loop is reachable, just not from where he is standing.

## 5. Reproduction

Profile mirrored from his `metadata.json` into a harness settings file —
`D:\hf-repro\settings\tinkerbolts_bin{1,2}.json`. `UseAdvanced = False`, so
`Simple_NoiseLevel=Typical` / `Simple_PixelScale=LongFocalLength` / `Simple_FocusRange=WideRange` re-derive
exactly the effective values his metadata recorded (StructureLayers 6, MinStarBoundingBoxSize 6,
BrightnessSensitivity 14, NoiseReductionRadius 4). Run copied to `D:\hf-repro` so the bank is untouched.

```bash
TestApp.exe optimize --per-run --runs "D:\hf-repro" \
  --settings "D:\hf-repro\settings\tinkerbolts_bin2.json" \
  --profile-id ce3f3e63-8fd3-4b72-a0ca-d90db9441382 --out "D:\hf-repro\out\armA_bin2"
# then swap attempt01\harness_settings.json to Bin1 and repeat with tinkerbolts_bin1.json
```

**The mirror is exact.** Arm A's baseline fit vertex is **4.31406 px** against his metadata's
`estimatedFinalHFR = 4.314063080205218`, and the per-position accepted-star counts reproduce his saved
`*_star_detection_result.json` frame for frame (4,4,4,4 / 4,4,4,4 / 8,9,9,9 / 21,21,23,23 / 24,20,18,27 /
…; one frame of 36 differs).

### Arm A — detected at 2x2 (his setting)

```
in-focus HFR current=4.31406 px -> recommends 1x
             optimized=3.85891 px -> recommends 1x   (fit R^2 0.977239 -> 0.965496)
Current J : 0.782263
Best J    : 0  (no improvement over current)
FAIL hard floor (min observed = 0 stars)
```

So at his own setting the wizard **cannot improve on his settings** (BestJ = 0 — every candidate starves a
wing frame below `NHard` = 3), falls back to the **Current** variant, and that variant's summary
nonetheless recommends **1x1** — because it reads the fitted vertex 4.314, not the 4.662 his auto-focus
published. The "Optimize again at 1x1" button is shown.

### Arm B — detected at 1x1 (what the wizard just told him to do)

```
in-focus HFR current=3.86951 px -> recommends 1x
             optimized=2.15273 px -> recommends 1x   (fit R^2 0.97302 -> 0.392667)
Current J : 0          <-- his settings are INFEASIBLE at 1x1
Best J    : 0
FAIL hard floor (min observed = 0 stars)
```

**Following the wizard's 1x1 advice is actively harmful on this rig.** His settings score J = 0.782 at 2x2
and **J = 0 at 1x1** — the hard floor (≥ `NHard` = 3 accepted stars per frame) fails, because binning 2×2
raises per-pixel SNR and `BrightnessSensitivity` = 14 is a σ threshold: at 1x1 the wing frames lose the stars
they had. The optimized fit at 1x1 collapses to R² = 0.393, i.e. no usable curve at all.

So the advice points away from the factor that is demonstrably correct for this rig, and the quantity it is
computed from (the fitted vertex) is lower at 1x1 than at 2x2, which keeps it pointing that way.

### Arms C1 / C2 — "Start from my current settings", then Accept, then run again

Arms A and B seed from the fully-default params (the wizard's default), where this run is infeasible and the
wizard just says "could not improve on your current settings". With **`--start-from-current`** — the wizard's
"Start from my current settings" toggle — the search has a feasible seed and does real work, which is the
path on which a *landing* (and therefore the published measurement) can move between runs.

**C1** (seed = baseline = his settings, 2x2):

```
Current settings J: 0.782263  ->  Best J: 0.926952   (improved over current), evals=218
PASS hard floor (min observed = 4)
in-focus HFR current=4.31406 px -> recommends 1x
             optimized=4.14873 px -> recommends 1x   (fit R^2 0.977239 -> 0.999421)
Sensitivity 14 -> 0 *   StarClippingMultiplier 2 -> 0.25 *   MaxDistortion 0.5 -> 0.4 *
MinHFR 1.2 -> 1.325 *   StarCenterTolerance 0.3 -> 0.35 *
```

Accepting this gives him **Sensitivity pinned at the search floor (0)** and a near-perfect fit (R² 0.9994).
Its published measurement would be **4.149 px → 1x1**, so against the auto-focus writer's 4.662 the straddle
is *wider* than before (−11 %, a ~12 %-wide vulnerable window).

**C2** emulates Accept and re-runs: the harness settings file gets `UseOptimizedSettings=True` plus C1's
`optimized_settings.json` verbatim in `OptimizedSettingsJson`, which is exactly what
`ApplyOptimizedSettings` stores. The harness confirms the snapshot winning over his Simple-mode presets
(`BrightnessSensitivity 14->0, MaxDistortion 0.5->0.4, MinHFR 1.2->1.325, StarCenterTolerance 0.3->0.35,
StarClippingMultiplier 2->0.25`), so C2's "current" really is C1's landing.

**C2** result — an exact fixed point:

```
Current settings J: 0.926952  ->  Best J: 0.926952   (no improvement over current), evals=123
PASS hard floor (min observed = 4)
in-focus HFR current=4.14873 px -> recommends 1x
             optimized=4.14873 px -> recommends 1x   (fit R^2 0.999421 -> 0.999421)
```

C2's baseline J is **bit-identical** to C1's `bestJ`, and the second run converges straight back to C1's
landing with no change to any curated param. **So "Accept, then run it again" does not move the
recommendation either.** Reported as a negative result.

### What the four arms establish

| arm | detected at | seed | current vertex → rec | optimized vertex → rec | J |
|---|---|---|---|---|---|
| A | 2x2 | default | 4.31406 → **1x** | 3.85891 → **1x** | 0.782 → 0 |
| B | 1x1 | default | 3.86951 → **1x** | 2.15273 → **1x** | 0 → 0 |
| C1 | 2x2 | current | 4.31406 → **1x** | 4.14873 → **1x** | 0.782 → **0.927** |
| C2 | 2x2 | current (= C1 accepted) | 4.14873 → **1x** | 4.14873 → **1x** | 0.927 → 0.927 |

**The optimizer is deterministic and converges to a fixed point. Every wizard path on this run recommends
1x1, from a vertex of 4.15–4.31 px. The auto-focus engine publishes 4.662 px, which recommends 2x2.** Two of
the three candidate loops are ruled out by measurement, which isolates the oscillation entirely to the
two-writer disagreement in §2 — and that is the thing to fix.

## 6. Fix options — not yet chosen

1. **One quantity, one writer (preferred).** Make the wizard publish the same kind of number the AF engine
   does, or stop it publishing at all and let auto-focus own the measurement. The AF engine's comment
   already argues its own value is the trustworthy one; the wizard's fitted vertex is strictly the weaker
   statistic and is additionally contaminated by the optimizer's own gate loosening.
2. **Hysteresis in the comparator.** Only recommend a change when the measurement clears the boundary by a
   margin (e.g. recommend *away* from the current factor only outside `[B·(1−h), B·(1+h)]` with h ≈ 15 %).
   This is cheap, fixes both loops, and is the only thing that protects a rig whose true HFR genuinely sits
   at 4.5 px — where *no* amount of writer-unification helps, because the right answer is "either is fine".
3. **Report the provenance.** Store which writer produced the value and say so in the recommendation, so a
   user can see why the advice moved.

(1) and (2) are complementary: (1) removes the 0.35 px step, (2) removes the hair trigger. Both are
user-visible behaviour changes to a recommendation, so neither should land without a decision.

Separately worth noting, though not the reported bug: this run is **star-starved at both factors** (3–4
accepted stars on the wing frames at 2x2, zero at 1x1 under the default seed), which is why `J` is so
sensitive here and why the default-seeded search cannot beat his settings at all. That is its own issue and
does not belong in this fix.

## 7. Harness gap closed along the way

`TestApp optimize` reported no in-focus HFR and no binning recommendation, so the wizard's Detection
Binning block had **no offline reproduction at all**. `OptimizationDiagnosticRunner` now prints, per run,
the factor it detected at plus both variants' fitted vertex and the factor each recommends (console and
`optimize_summary.txt`). That line is what made this diagnosis a measurement rather than an argument.

## 8. Verification

`dotnet test Joko.NINA.Plugins/Joko.NINA.Plugins.sln -c Debug` — **Passed: 4438, Failed: 0, Skipped: 0**
(5 m 16 s). The only production-code change in this investigation is the diagnostic print in §7; nothing in
the plugin was modified, so this is a clean-tree gate on the diagnosis, not on a fix.
