# Detection-Binning Recommendation Oscillation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for
> tracking.

**Goal:** Stop the detection-binning recommendation flipping between runs, by making the wizard publish the
same *kind* of in-focus HFR the auto-focus engine publishes (a real measurement, not a fitted vertex) and by
giving the factor comparator a no-change zone around each band boundary.

**Architecture:** Two independent changes to two files, each with its own failing test first.
(1) `DetectionBinningResolver` gains a `HysteresisPixels` constant and a two-argument
`RecommendFromHfr(hfr, currentFactor)` overload that holds the current factor while the measurement is still
within that margin of the current factor's own band; the existing single-argument form stays pure and
unchanged so the harness's factor-derivation path and all 18 existing resolver tests keep working.
(2) `StarDetectionOptimizerWizardVM` changes `OptimizationSummary.MeasuredInFocusHfr` from
`BestFit.Minimum.Y` to the **pooled HFR of the sweep position nearest the fitted minimum**, and moves the
fitted vertex to a new `FitVertexInFocusHfr` field so `MinHfrSeed` keeps its documented input.

**Tech Stack:** C# / .NET 8-windows, NUnit 4.4.0 + NUnit3TestAdapter, OxyPlot `ScatterErrorPoint`/`DataPoint`.

**Spec:** `docs/binning-recommendation-oscillation-design.md` — read §2, §3 and §6 before starting.

---

## Why BOTH halves are required (do not drop either)

Measured on the reported run (`D:\Autofocus Bank\TinkerBolts\AutoFocus_20260929_040614`), at his
`DetectionBinning = 2x2`:

| quantity | value | `round(h/3)` |
|---|---|---|
| auto-focus engine publishes (`FinalHFR`, 4 real exposures at the settled position) | **4.662 px** | **2** |
| wizard publishes today (`BestFit.Minimum.Y`) | **4.314 px** | **1** |
| wizard publishes AFTER change (b) (pooled HFR at position 19900, nearest the fitted minimum X=19975.06) | **4.4363 px** | **1** |

**Change (b) alone does not fix this rig.** It narrows the writer disagreement from 0.348 px to 0.226 px but
leaves the value on the far side of the 4.5 px boundary, so the flip survives. The hysteresis is what closes
it: with a 0.6 px margin, a user on 2x2 holds 2x2 for anything in `[3.9, 8.1)`, which contains **both**
4.4363 and 4.662.

Conversely hysteresis alone would mask the problem rather than fix it: the two surfaces would still *display*
different measurements (4.3 px vs 4.7 px) for one rig on one night, and the published number would still be a
biased statistic. Ship both.

### Why the margin is 0.6 px

It must swallow three measured sources of run-to-run variation in the in-focus HFR, all from the spec:

| source | measured | reference |
|---|---|---|
| writer disagreement remaining after (b) | 0.226 px | table above |
| detection-factor dependence (same frames, 2x2 vs 1x1) | 0.444 px | design doc §3 |
| frame-to-frame spread at the vertex position (4 subs) | ~0.1 px | mean 4.4363 vs median 4.4136 |

0.6 px covers all three with headroom and is 20 % of the 3.0 px `TargetHfrPixels`. It is a **fixed pixel**
margin, not a fraction of the boundary, because every band is exactly 3.0 px wide at every factor — a
fractional margin would be needlessly wider at 10.5 px than at 4.5 px. Cost of the margin: a user sitting on
a genuinely wrong factor is told to move 0.6 px later than today (at 1x1, they now hold until 5.1 px rather
than 4.5 px). Task 1 pins those hold boundaries in a test so the constant stays visible and revisable.

---

## File structure

| File | Responsibility | Change |
|---|---|---|
| `Joko.NINA.Plugins/Joko.NINA.Plugins.HocusFocus/Utility/DetectionBinningResolver.cs` | the single source of truth for the factor rule | add `HysteresisPixels` + 2-arg `RecommendFromHfr`; route the three `currentFactor`-aware helpers through it |
| `.../StarDetection/Optimization/StarDetectionOptimizerWizardVM.cs` | wizard summary + Accept | add `MeasuredHfrNearestFocus` helper; add `FitVertexInFocusHfr` / `BaselineFitVertexInFocusHfr`; repoint `MeasuredInFocusHfr`; `HasUndersampledStars` → vertex field |
| `Joko.NINA.Plugins/TestApp/OptimizationDiagnosticRunner.cs` | headless repro | print vertex **and** measured point, and the hysteresis-aware recommendation |
| `.../Tests/Utility/DetectionBinningResolverTests.cs` | resolver tests | add hysteresis tests |
| `.../Tests/StarDetection/Optimization/StarDetectionOptimizerWizardVMTests.cs` | wizard tests | add nearest-point + regression tests |
| `documentation/docs/settings/detection-binning.md` | user-facing rule | update the table + the "where it comes from" section |
| `documentation/docs/optimization/index.md` | wizard summary docs | correct "reads the in-focus HFR off your fitted focus curve" |

No files are created. Nothing is split.

---

### Task 0: Branch, and land the investigation artifacts

**Do this first.** Every later task commits, and the repo is currently sitting on `develop` with the
investigation's output uncommitted. `develop` is only ever updated by PR merge.

**Files:**
- Commit (already written, do not re-create): `Joko.NINA.Plugins/TestApp/OptimizationDiagnosticRunner.cs`
  (modified — the headless binning diagnostic), `docs/binning-recommendation-oscillation-design.md` (new),
  `plans/binning-recommendation-oscillation-plan.md` (new — this file)

- [ ] **Step 1: Confirm the starting state**

```bash
git branch --show-current && git status --short
```

Expected: `develop`, with exactly those three paths dirty/untracked. If anything else is dirty, stop and ask.

- [ ] **Step 2: Create the feature branch**

```bash
git checkout -b ghilios/binning-recommendation-oscillation
```

- [ ] **Step 3: Commit the three artifacts**

```bash
git add Joko.NINA.Plugins/TestApp/OptimizationDiagnosticRunner.cs \
        docs/binning-recommendation-oscillation-design.md \
        plans/binning-recommendation-oscillation-plan.md
GIT_COMMITTER_NAME="George Hilios" GIT_COMMITTER_EMAIL="322725+ghilios@users.noreply.github.com" \
  git commit --author="George Hilios <322725+ghilios@users.noreply.github.com>" \
  -m "Diagnose the detection-binning recommendation oscillation

Reproduces the reported oscillation on TinkerBolts/AutoFocus_20260929_040614 and isolates it to two writers
publishing different quantities into LastInFocusHfrPixels. Adds the headless optimizer diagnostic that made
the wizard's Detection Binning block reproducible offline.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 4: Verify the tree is clean**

```bash
git status --short && git log --oneline -1
```

Expected: no output from `status`; the new commit shown.

---

### Task 1: Hysteresis in `DetectionBinningResolver`

**Files:**
- Modify: `Joko.NINA.Plugins/Joko.NINA.Plugins.HocusFocus/Utility/DetectionBinningResolver.cs`
- Test: `Joko.NINA.Plugins/Joko.NINA.Plugins.HocusFocus.Tests/Utility/DetectionBinningResolverTests.cs`

- [ ] **Step 1: Write the failing tests**

Append inside the existing `DetectionBinningResolverTests` class:

```csharp
        // The reported oscillation, as a test. Both writers' values for the TinkerBolts rig
        // (AutoFocus_20260929_040614) must agree on the factor the user is already running.
        [Test]
        public void RecommendFromHfr_WithCurrentFactor_HoldsAcrossTheReportedWriterDisagreement() {
            Assert.Multiple(() => {
                // auto-focus engine's FinalHFR, and the wizard's post-fix measured point
                Assert.That(DetectionBinningResolver.RecommendFromHfr(4.662, 2), Is.EqualTo(2));
                Assert.That(DetectionBinningResolver.RecommendFromHfr(4.4363, 2), Is.EqualTo(2));
                // and a user on 1x1 is held there by the same pair, rather than bounced
                Assert.That(DetectionBinningResolver.RecommendFromHfr(4.662, 1), Is.EqualTo(1));
                Assert.That(DetectionBinningResolver.RecommendFromHfr(4.4363, 1), Is.EqualTo(1));
            });
        }

        // The hold boundaries are the whole cost of the margin, so they are pinned explicitly rather than
        // left implied by the constant. Band for factor c is [3c-1.5, 3c+1.5); the hold zone widens it by
        // HysteresisPixels on each side.
        [TestCase(1, 5.09, 1)]   // just inside 1x1's hold zone (upper edge 5.1)
        [TestCase(1, 5.11, 2)]   // just outside it -> move
        [TestCase(2, 3.91, 2)]   // just inside 2x2's hold zone (lower edge 3.9)
        [TestCase(2, 3.89, 1)]   // just outside it -> move
        [TestCase(2, 8.09, 2)]   // upper edge 8.1
        [TestCase(2, 8.11, 3)]
        [TestCase(3, 6.91, 3)]   // lower edge 6.9
        [TestCase(4, 9.91, 4)]   // lower edge 9.9
        [TestCase(4, 9.89, 3)]
        public void RecommendFromHfr_WithCurrentFactor_HoldsInsideTheZoneAndMovesOutsideIt(
            int currentFactor, double hfr, int expected) {
            Assert.That(DetectionBinningResolver.RecommendFromHfr(hfr, currentFactor), Is.EqualTo(expected));
        }

        // Once it does move, it moves to the RIGHT factor, not one step toward it.
        [Test]
        public void RecommendFromHfr_WithCurrentFactor_JumpsStraightToTheIndicatedFactor() {
            Assert.That(DetectionBinningResolver.RecommendFromHfr(11.0, 1), Is.EqualTo(4));
        }

        [Test]
        public void RecommendFromHfr_WithCurrentFactor_ClampsTheCurrentFactorAndHandlesDegenerateInput() {
            Assert.Multiple(() => {
                Assert.That(DetectionBinningResolver.RecommendFromHfr(4.662, 0), Is.EqualTo(1),
                    "current 0 clamps to 1, whose hold zone reaches 5.1");
                Assert.That(DetectionBinningResolver.RecommendFromHfr(4.662, 99), Is.EqualTo(2),
                    "current 99 clamps to 4, whose hold zone starts at 9.9, so the raw answer stands");
                Assert.That(DetectionBinningResolver.RecommendFromHfr(double.NaN, 2), Is.EqualTo(1));
                Assert.That(DetectionBinningResolver.RecommendFromHfr(0.0, 2), Is.EqualTo(1));
                Assert.That(DetectionBinningResolver.RecommendFromHfr(-3.0, 2), Is.EqualTo(1));
            });
        }

        // The pure form is what HarnessSettingsStore derives a dataset's factor from, where there is no
        // "current factor" to latch against. It must stay hysteresis-free.
        [Test]
        public void RecommendFromHfr_SingleArgument_IsUnaffectedByTheHysteresis() {
            Assert.Multiple(() => {
                Assert.That(DetectionBinningResolver.RecommendFromHfr(4.4), Is.EqualTo(1));
                Assert.That(DetectionBinningResolver.RecommendFromHfr(4.6), Is.EqualTo(2));
                Assert.That(DetectionBinningResolver.RecommendFromHfr(4.662), Is.EqualTo(2));
            });
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" test "$(wslpath -w "$PWD/Joko.NINA.Plugins/Joko.NINA.Plugins.sln")" \
  -c Debug --nologo --filter "FullyQualifiedName~DetectionBinningResolverTests"
```

Expected: FAIL — `RecommendFromHfr` has no two-argument overload (CS1501 at compile time).

- [ ] **Step 3: Add the constant and the overload**

In `DetectionBinningResolver.cs`, after the `MaxBinningFactor` constant:

```csharp
        /// <summary>
        /// Half-width, in captured pixels, of the NO-CHANGE zone straddling each band boundary. Inside it the
        /// recommendation holds whatever factor is already in service instead of naming the other one.
        ///
        /// <para>The comparator had no margin at all, and the quantity it reads moves by more than the
        /// distance to a boundary for perfectly ordinary reasons: two different code paths publish the
        /// measurement and disagree by 0.23 px even after they were unified on statistic type, the same
        /// frames measure 0.44 px apart at 1x1 versus 2x2, and four subs at one position scatter ~0.1 px.
        /// A rig whose in-focus HFR lands near 4.5 / 7.5 / 10.5 px therefore got a different answer every
        /// run. 0.6 px covers all three with headroom. See
        /// docs/binning-recommendation-oscillation-design.md.</para>
        ///
        /// <para>FIXED pixels, not a fraction of the boundary: every band is exactly
        /// <see cref="TargetHfrPixels"/> wide at every factor, so a fractional margin would be wider at 10.5
        /// px than at 4.5 px for no reason.</para>
        ///
        /// <para>The cost is stated plainly: a user on a genuinely wrong factor is told to move 0.6 px later
        /// than before. That is the right trade here, because within this zone BOTH factors land inside the
        /// detector's calibrated 2-4 px band, so there is no correct answer to converge on — which is
        /// precisely when a latching comparator is the right instrument.</para>
        /// </summary>
        public const double HysteresisPixels = 0.6;
```

Then, immediately after the existing single-argument `RecommendFromHfr`:

```csharp
        /// <summary>
        /// The factor to recommend to someone ALREADY RUNNING <paramref name="currentFactor"/>: the same answer
        /// as <see cref="RecommendFromHfr(double)"/>, except that it holds the current factor while the
        /// measurement is still within <see cref="HysteresisPixels"/> of that factor's own band.
        ///
        /// <para>This makes the recommendation PATH-DEPENDENT on purpose — two rigs with identical
        /// measurements and different current factors are told different things. Deliberate: see
        /// <see cref="HysteresisPixels"/>. Callers that have no factor in service (deriving one from scratch,
        /// e.g. the headless harness) must use the single-argument form, which stays hysteresis-free.</para>
        ///
        /// <para>When the measurement DOES clear the zone, this returns the factor the measurement actually
        /// indicates rather than stepping one toward it: a 1x1 rig measuring 11 px needs 4x4, not 2x2.</para>
        /// </summary>
        public static int RecommendFromHfr(double measuredHfrPixels, int currentFactor) {
            if (!IsUsableHfr(measuredHfrPixels)) {
                return 1;
            }
            var current = Clamp(currentFactor);
            var raw = RecommendFromHfr(measuredHfrPixels);
            if (raw == current) {
                return current;
            }
            var halfBand = 0.5 * TargetHfrPixels;
            var lower = TargetHfrPixels * current - halfBand - HysteresisPixels;
            var upper = TargetHfrPixels * current + halfBand + HysteresisPixels;
            return measuredHfrPixels >= lower && measuredHfrPixels < upper ? current : raw;
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Same command as Step 2. Expected: PASS, and the 18 pre-existing resolver tests still pass.

- [ ] **Step 5: Commit**

```bash
git add Joko.NINA.Plugins/Joko.NINA.Plugins.HocusFocus/Utility/DetectionBinningResolver.cs \
        Joko.NINA.Plugins/Joko.NINA.Plugins.HocusFocus.Tests/Utility/DetectionBinningResolverTests.cs
GIT_COMMITTER_NAME="George Hilios" GIT_COMMITTER_EMAIL="322725+ghilios@users.noreply.github.com" \
  git commit --author="George Hilios <322725+ghilios@users.noreply.github.com>" \
  -m "Add a no-change zone to the detection-binning recommendation

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Route the two user-facing surfaces through the hysteresis

**Files:**
- Modify: `Joko.NINA.Plugins/Joko.NINA.Plugins.HocusFocus/Utility/DetectionBinningResolver.cs`
- Test: `Joko.NINA.Plugins/Joko.NINA.Plugins.HocusFocus.Tests/Utility/DetectionBinningResolverTests.cs`

`DescribeRecommendation`, `DescribeRecommendationDetail` and `DiffersFromRecommendation` already take
`currentFactor`; they just call the pure form internally. Changing those three calls routes the options page
(`StarDetectionOptions.cs:535-546`) and the wizard start page
(`StarDetectionOptimizerWizardVM.cs:1348-1358`) at once, with no edits in either file.

- [ ] **Step 1: Write the failing test**

```csharp
        [Test]
        public void DescribeRecommendation_HoldsTheCurrentFactorInsideTheNoChangeZone() {
            Assert.Multiple(() => {
                // 4.662 px on a 2x2 rig used to read "1x1 recommended"
                Assert.That(DetectionBinningResolver.DescribeRecommendation(2, 4.662),
                    Is.EqualTo("Measured in-focus HFR 4.7 px - 2x2 is right"));
                Assert.That(DetectionBinningResolver.DiffersFromRecommendation(2, 4.662), Is.False);
                Assert.That(DetectionBinningResolver.ShouldShowRecommendation(2, 4.662), Is.False,
                    "a row that confirms the current setting is noise");
                // and outside the zone it still asks for the change
                Assert.That(DetectionBinningResolver.DescribeRecommendation(1, 6.1),
                    Is.EqualTo("Measured in-focus HFR 6.1 px - 2x2 recommended"));
                Assert.That(DetectionBinningResolver.ShouldShowRecommendation(1, 6.1), Is.True);
            });
        }

        [Test]
        public void DescribeRecommendationDetail_InTheNoChangeZone_UsesTheInRangeWording() {
            var detail = DetectionBinningResolver.DescribeRecommendationDetail(2, 4.662, null);
            Assert.That(detail, Does.Contain("already in that range"));
        }
```

- [ ] **Step 2: Run to verify it fails**

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" test "$(wslpath -w "$PWD/Joko.NINA.Plugins/Joko.NINA.Plugins.sln")" \
  -c Debug --nologo --filter "FullyQualifiedName~DetectionBinningResolverTests"
```

Expected: FAIL — `DescribeRecommendation` returns `"Measured in-focus HFR 4.7 px - 1x1 recommended"`.

- [ ] **Step 3: Route the three helpers through the overload**

Three single-line edits in `DetectionBinningResolver.cs`.

In `DescribeRecommendation`, replace:

```csharp
            var recommended = RecommendFromHfr(measuredHfrPixels);
```

with:

```csharp
            // The two-argument form: this line is read by a user who already HAS a factor in service, so the
            // no-change zone applies. HarnessSettingsStore's from-scratch derivation uses the pure form.
            var recommended = RecommendFromHfr(measuredHfrPixels, current);
```

(`current` is already declared immediately above it.)

In `DescribeRecommendationDetail`, replace:

```csharp
            var recommended = RecommendFromHfr(measuredHfrPixels);
```

with:

```csharp
            var recommended = RecommendFromHfr(measuredHfrPixels, Clamp(currentFactor));
```

In `DiffersFromRecommendation`, replace:

```csharp
            => IsUsableHfr(measuredHfrPixels) && Clamp(currentFactor) != RecommendFromHfr(measuredHfrPixels);
```

with:

```csharp
            => IsUsableHfr(measuredHfrPixels)
                && Clamp(currentFactor) != RecommendFromHfr(measuredHfrPixels, currentFactor);
```

`ShouldShowRecommendation` needs no edit — it delegates to `DiffersFromRecommendation` and
`IsBelowCalibratedBand`, and the below-band path must stay independent of the factor (it changes what the
user is *told*, never what is recommended).

- [ ] **Step 4: Run to verify it passes**

Same command. Expected: PASS, **including all 18 pre-existing resolver tests with no edits.** That was checked
against their actual assertions, not assumed — every existing case is either single-argument
(`RecommendFromHfr_IsUnchangedByTheBandWording`, 17 pinned values) or uses an HFR that falls outside the
current factor's hold zone and so is unaffected:

| existing assertion | after hysteresis | why |
|---|---|---|
| `ShouldShowRecommendation(1, 6.1)` → true | true | 6.1 is outside 1x1's zone `[0.9, 5.1)` |
| `ShouldShowRecommendation(2, 6.1)` → false | false | raw already equals current |
| `ShouldShowRecommendation(1, 3.6)` → false | false | raw already equals current |
| `DiffersFromRecommendation(1, 6.1)` → true | true | outside the zone |
| `DescribeRecommendation(1, 6.1)` → "2x2 recommended" | unchanged | outside the zone |
| `DescribeRecommendation(2, 6.1)` / `(1, 3.6)` → "is right" | unchanged | raw equals current |
| the below-band cases (1.4 px, 0.7 px at factor 1) | unchanged | raw clamps to 1 = current, so the below-band branch still decides |

**If any of them does fail, the hold zone was computed wrongly — fix the code, not the test.**

- [ ] **Step 5: Commit**

```bash
git add Joko.NINA.Plugins/Joko.NINA.Plugins.HocusFocus/Utility/DetectionBinningResolver.cs \
        Joko.NINA.Plugins/Joko.NINA.Plugins.HocusFocus.Tests/Utility/DetectionBinningResolverTests.cs
GIT_COMMITTER_NAME="George Hilios" GIT_COMMITTER_EMAIL="322725+ghilios@users.noreply.github.com" \
  git commit --author="George Hilios <322725+ghilios@users.noreply.github.com>" \
  -m "Apply the binning no-change zone on the options and wizard start pages

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: The wizard publishes a MEASUREMENT, not a fitted vertex

**Files:**
- Modify: `Joko.NINA.Plugins/Joko.NINA.Plugins.HocusFocus/StarDetection/Optimization/StarDetectionOptimizerWizardVM.cs`
- Test: `Joko.NINA.Plugins/Joko.NINA.Plugins.HocusFocus.Tests/StarDetection/Optimization/StarDetectionOptimizerWizardVMTests.cs`

Two constraints the implementation must respect, both documented in the existing code — read them before
editing:

1. **`MeasuredInFocusHfr` must keep following the VARIANT** (optimized on the Optimized view, current on the
   Current view). Its doc comment records why: reading it off the current-settings curve while displaying the
   optimized one produced a recommendation that contradicted the chart. The new value is the *variant's own*
   measured point, so this is preserved — and it is now a value the user can literally see plotted, which the
   extrapolated vertex never was.
2. **`MinHfrSeed` must keep receiving the fitted vertex.** `MinHfrSeed.Resolve`/`IsBelowGate` document
   `BestFit.Minimum.Y` as their input, the whole F35/F38 analysis rests on it, and the headless harness seeds
   the optimizer from it. Changing that input would move optimizer seeding across the whole bank. So the
   vertex gets its own field; it is not deleted.

Also note: the R² ≥ 0.9 gate (`MinRSquaredForBinningRecommendation`) **stays**. Its doc comment records that
in the catastrophic case "the point nearest best focus [was] just as wrong" as the vertex, so this change does
not replace the gate — the gate now guards *which point gets selected* rather than the value itself.

- [ ] **Step 1: Write the failing tests**

Append inside `StarDetectionOptimizerWizardVMTests`:

```csharp
        [Test]
        public void MeasuredHfrNearestFocus_TakesThePooledPointNearestTheFittedMinimum() {
            // The reported run's pooled curve (TinkerBolts AutoFocus_20260929_040614, 2x2). The fitted
            // minimum sits at 19975.06, which is 75 steps from 19900 and 268 from 20243.
            var points = new List<ScatterErrorPoint> {
                new ScatterErrorPoint(21272, 17.5207, 0, 0),
                new ScatterErrorPoint(20929, 16.7038, 0, 0),
                new ScatterErrorPoint(20586, 11.8217, 0, 0),
                new ScatterErrorPoint(20243, 6.8162, 0, 0),
                new ScatterErrorPoint(19900, 4.4363, 0, 0),
                new ScatterErrorPoint(19557, 6.6012, 0, 0),
                new ScatterErrorPoint(19214, 10.2686, 0, 0),
                new ScatterErrorPoint(18871, 18.1985, 0, 0),
                new ScatterErrorPoint(18528, 22.2525, 0, 0),
            };
            Assert.That(StarDetectionOptimizerWizardVM.MeasuredHfrNearestFocus(points, 19975.057614367677),
                Is.EqualTo(4.4363).Within(1e-9));
        }

        [Test]
        public void MeasuredHfrNearestFocus_IsNaNWithoutPointsOrAFit() {
            var points = new List<ScatterErrorPoint> { new ScatterErrorPoint(100, 3.0, 0, 0) };
            Assert.Multiple(() => {
                Assert.That(StarDetectionOptimizerWizardVM.MeasuredHfrNearestFocus(null, 100.0), Is.NaN);
                Assert.That(StarDetectionOptimizerWizardVM.MeasuredHfrNearestFocus(
                    new List<ScatterErrorPoint>(), 100.0), Is.NaN);
                Assert.That(StarDetectionOptimizerWizardVM.MeasuredHfrNearestFocus(points, double.NaN), Is.NaN);
            });
        }

        [Test]
        public void MeasuredHfrNearestFocus_BreaksATieOnTheLowerFocuserPosition() {
            // Deterministic tie-break, so the published number cannot depend on enumeration order.
            var points = new List<ScatterErrorPoint> {
                new ScatterErrorPoint(1100, 5.0, 0, 0),
                new ScatterErrorPoint(900, 4.0, 0, 0),
            };
            Assert.That(StarDetectionOptimizerWizardVM.MeasuredHfrNearestFocus(points, 1000.0),
                Is.EqualTo(4.0).Within(1e-9));
        }

        // The end-to-end regression: the reported run's two writers must now agree on the factor.
        [Test]
        public void ReportedRun_BothWritersNowAgreeOnTheFactor() {
            const int runFactor = 2;
            var afEngineValue = 4.661769018918253;   // regionState.FinalHFR.Measure
            var wizardValue = 4.4363;                // pooled HFR at 19900, nearest the fitted minimum
            Assert.Multiple(() => {
                Assert.That(DetectionBinningResolver.RecommendFromHfr(afEngineValue, runFactor),
                    Is.EqualTo(runFactor));
                Assert.That(DetectionBinningResolver.RecommendFromHfr(wizardValue, runFactor),
                    Is.EqualTo(runFactor));
                Assert.That(new OptimizationSummary {
                    RunDetectionBinning = runFactor,
                    PersistedDetectionBinning = runFactor,
                    MeasuredInFocusHfr = wizardValue,
                    FitRSquared = 0.977238532067901,
                    RecommendedDetectionBinning =
                        DetectionBinningResolver.RecommendFromHfr(wizardValue, runFactor)
                }.DetectionBinningDiffers, Is.False, "no 'Optimize again at 1x1' button");
            });
        }

        [Test]
        public void HasUndersampledStars_ReadsTheFittedVertexNotTheMeasuredPoint() {
            // MinHfrSeed's documented input is BestFit.Minimum.Y. A gate of 2.4 binned px at 2x2 is 4.8
            // captured px: the vertex (4.6) is below it, the measured point (5.0) is not.
            var summary = new OptimizationSummary {
                RunDetectionBinning = 2,
                MeasuredInFocusHfr = 5.0,
                FitVertexInFocusHfr = 4.6,
                VariantMinHfr = 2.4,
                FitRSquared = 0.99
            };
            Assert.That(summary.HasUndersampledStars, Is.True);
        }
```

Add to the test file's usings if absent: `using OxyPlot.Series;`, `using NINA.Joko.Plugins.HocusFocus.Utility;`.

- [ ] **Step 2: Run to verify they fail**

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" test "$(wslpath -w "$PWD/Joko.NINA.Plugins/Joko.NINA.Plugins.sln")" \
  -c Debug --nologo --filter "FullyQualifiedName~StarDetectionOptimizerWizardVMTests"
```

Expected: FAIL to compile — `MeasuredHfrNearestFocus` and `FitVertexInFocusHfr` do not exist.

- [ ] **Step 3: Add the two summary fields**

In `OptimizationSummary`, directly after the `BaselineFitRSquared` property:

```csharp
        /// <summary>
        /// THIS VARIANT's fitted curve MINIMUM (the hyperbola vertex), in captured pixels. Kept separate from
        /// <see cref="MeasuredInFocusHfr"/>, which is now a real measured point, because
        /// <see cref="MinHfrSeed"/> documents the vertex as its input and the optimizer's MinHFR seeding (F35
        /// / F38) plus every bank result rest on that. Display and publication use the measurement; the
        /// undersampled-stars test uses this.
        /// </summary>
        public double FitVertexInFocusHfr { get; set; } = double.NaN;

        /// <summary>The CURRENT-settings equivalent, carried for <c>BuildCurrentSummary</c>.</summary>
        public double BaselineFitVertexInFocusHfr { get; set; } = double.NaN;
```

Change `MeasuredInFocusHfr`'s doc comment first paragraph to:

```csharp
        /// <summary>
        /// The in-focus HFR THIS VARIANT's settings MEASURED, in captured pixels: the pooled HFR of the sweep
        /// position nearest the fitted minimum. A real measurement at a real focuser position, which is the
        /// same KIND of quantity the auto-focus engine publishes (<c>AutoFocusEngine.OnCompleted</c> ->
        /// <c>regionState.FinalHFR</c>) — the two used to disagree by 0.35 px because this was the fitted
        /// vertex, and on a rig sitting near a band boundary that flipped the binning recommendation every
        /// run. See docs/binning-recommendation-oscillation-design.md. The vertex itself is
        /// <see cref="FitVertexInFocusHfr"/>.
```

Then repoint `HasUndersampledStars`:

```csharp
        public bool HasUndersampledStars =>
            MinHfrSeed.IsBelowGate(FitVertexInFocusHfr, VariantMinHfr, RunDetectionBinning);
```

- [ ] **Step 4: Add the helper**

In `StarDetectionOptimizerWizardVM`, immediately after `PartitionRecoveryPoints`:

```csharp
        /// <summary>
        /// The in-focus HFR a sweep MEASURED: the pooled Y of the point whose focuser position is nearest
        /// <paramref name="fittedMinimumX"/>. Takes the points and the abscissa rather than a
        /// <see cref="RunEvaluationResult"/> so it stays pure and directly testable.
        ///
        /// <para>Ties break on the LOWER focuser position, so the published number cannot depend on
        /// enumeration order.</para>
        ///
        /// <para>The fit is still required — it is what says WHICH point is nearest focus — which is why the
        /// R² gate (<see cref="OptimizationSummary.MinRSquaredForBinningRecommendation"/>) stays: on a sweep
        /// that lost the defocused donuts the vertex lands in the wrong place, and this would then select the
        /// wrong point. The gate guards the selection, not the arithmetic.</para>
        /// </summary>
        internal static double MeasuredHfrNearestFocus(
            IReadOnlyList<ScatterErrorPoint> points, double fittedMinimumX) {
            if (points == null || points.Count == 0 || !double.IsFinite(fittedMinimumX)) {
                return double.NaN;
            }
            var result = double.NaN;
            var bestDistance = double.MaxValue;
            var bestX = double.MaxValue;
            foreach (var p in points) {
                var distance = Math.Abs(p.X - fittedMinimumX);
                if (distance < bestDistance || (distance == bestDistance && p.X < bestX)) {
                    bestDistance = distance;
                    bestX = p.X;
                    result = p.Y;
                }
            }
            return result;
        }
```

- [ ] **Step 5: Wire it into `BuildSummaryAsync`**

Declare `fitVertexInFocusHfr` / `baselineFitVertexInFocusHfr` locals alongside the existing
`measuredInFocusHfr` / `baselineInFocusHfr` declarations (initialised to `double.NaN`).

Inside the `if (i == 0)` block, **hoist** the two existing `PartitionRecoveryPoints` calls (currently just
above `currentCurveLocal`) to the top of the block so they can be reused, then replace:

```csharp
                    measuredInFocusHfr = bestEval.BestFit?.Minimum.Y ?? double.NaN;
                    measuredFitRSquared = bestEval.Metrics?.RSquared ?? double.NaN;
                    baselineInFocusHfr = baselineEval.BestFit?.Minimum.Y ?? double.NaN;
                    baselineFitRSquared = baselineEval.Metrics?.RSquared ?? double.NaN;
```

with:

```csharp
                    var (baselineCore, baselineRecovery) = PartitionRecoveryPoints(baselineEval);
                    var (bestCore, bestRecovery) = PartitionRecoveryPoints(bestEval);
                    // Recovery positions are far-from-focus and down-weighted, so they must not be eligible as
                    // "the point nearest focus"; fall back to all points when the partition yields none.
                    var bestPoints = bestCore != null && bestCore.Count > 0 ? bestCore : bestEval.Points;
                    var baselinePoints = baselineCore != null && baselineCore.Count > 0
                        ? baselineCore : baselineEval.Points;
                    measuredInFocusHfr = MeasuredHfrNearestFocus(
                        bestPoints, bestEval.BestFit?.Minimum.X ?? double.NaN);
                    fitVertexInFocusHfr = bestEval.BestFit?.Minimum.Y ?? double.NaN;
                    measuredFitRSquared = bestEval.Metrics?.RSquared ?? double.NaN;
                    baselineInFocusHfr = MeasuredHfrNearestFocus(
                        baselinePoints, baselineEval.BestFit?.Minimum.X ?? double.NaN);
                    baselineFitVertexInFocusHfr = baselineEval.BestFit?.Minimum.Y ?? double.NaN;
                    baselineFitRSquared = baselineEval.Metrics?.RSquared ?? double.NaN;
```

and delete the now-duplicated `var (baselineCore, baselineRecovery) = ...` / `var (bestCore, bestRecovery) = ...`
pair further down (the `currentCurveLocal` / `optimizedCurveLocal` initialisers keep using those locals).

- [ ] **Step 6: Populate the new fields and apply the hysteresis in the summary**

In the `var summary = new OptimizationSummary { ... }` initialiser, hoist the run factor into a local *above*
the initialiser:

```csharp
            // Hoisted: the initialiser cannot reference its own RunDetectionBinning, and the recommendation
            // now needs it to apply the no-change zone.
            var runDetectionBinning = Math.Max(1, baseline.DetectionBinning);
```

then replace these three lines in the initialiser:

```csharp
                RunDetectionBinning = Math.Max(1, baseline.DetectionBinning),
                ...
                RecommendedDetectionBinning = DetectionBinningResolver.RecommendFromHfr(measuredInFocusHfr),
```

with:

```csharp
                RunDetectionBinning = runDetectionBinning,
                ...
                RecommendedDetectionBinning =
                    DetectionBinningResolver.RecommendFromHfr(measuredInFocusHfr, runDetectionBinning),
                FitVertexInFocusHfr = fitVertexInFocusHfr,
                BaselineFitVertexInFocusHfr = baselineFitVertexInFocusHfr,
```

In `BuildCurrentSummary`, replace:

```csharp
                RecommendedDetectionBinning = DetectionBinningResolver.RecommendFromHfr(optimized.BaselineMeasuredInFocusHfr),
```

with:

```csharp
                RecommendedDetectionBinning = DetectionBinningResolver.RecommendFromHfr(
                    optimized.BaselineMeasuredInFocusHfr, optimized.RunDetectionBinning),
                // The Current view's vertex is the baseline's, exactly as its measured HFR is.
                FitVertexInFocusHfr = optimized.BaselineFitVertexInFocusHfr,
                BaselineFitVertexInFocusHfr = optimized.BaselineFitVertexInFocusHfr,
```

`Apply`'s `recordMeasuredInFocusHfr?.Invoke(summary.MeasuredInFocusHfr)` needs **no change** — it now
publishes the measurement by construction.

- [ ] **Step 7: Run the tests to verify they pass**

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" test "$(wslpath -w "$PWD/Joko.NINA.Plugins/Joko.NINA.Plugins.sln")" \
  -c Debug --nologo --filter "FullyQualifiedName~StarDetectionOptimizerWizardVMTests"
```

Expected: PASS. Any pre-existing test asserting `MeasuredInFocusHfr == <a vertex>` must be updated to the
nearest-point value — that is the intended behaviour change, not a regression. A test that asserts
`HasUndersampledStars` must set `FitVertexInFocusHfr` now.

- [ ] **Step 8: Commit**

```bash
git add Joko.NINA.Plugins/Joko.NINA.Plugins.HocusFocus/StarDetection/Optimization/StarDetectionOptimizerWizardVM.cs \
        Joko.NINA.Plugins/Joko.NINA.Plugins.HocusFocus.Tests/StarDetection/Optimization/StarDetectionOptimizerWizardVMTests.cs
GIT_COMMITTER_NAME="George Hilios" GIT_COMMITTER_EMAIL="322725+ghilios@users.noreply.github.com" \
  git commit --author="George Hilios <322725+ghilios@users.noreply.github.com>" \
  -m "Publish the sweep's measured in-focus HFR, not its fitted vertex

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: Keep the headless repro honest

**Files:**
- Modify: `Joko.NINA.Plugins/TestApp/OptimizationDiagnosticRunner.cs` (the `detection binning (wizard block)`
  console loop and the `detection bin` lines in `WriteSummary`)

The harness diagnostic added during the investigation prints the vertex and the pure recommendation. It must
now print both quantities and the hysteresis-aware answer, or it stops predicting the wizard.

- [ ] **Step 1: Update the console loop**

Replace the body of the `for (var i = 0; i < loadedRuns.Count; i++)` diagnostic loop with:

```csharp
                var ranAt = Math.Max(1, ctx.Baseline.DetectionBinning);
                var baseVertex = perRunBaseline[i].BestFit?.Minimum.Y ?? double.NaN;
                var bestVertex = perRunBest[i].BestFit?.Minimum.Y ?? double.NaN;
                var baseMeasured = StarDetectionOptimizerWizardVM.MeasuredHfrNearestFocus(
                    perRunBaseline[i].Points, perRunBaseline[i].BestFit?.Minimum.X ?? double.NaN);
                var bestMeasured = StarDetectionOptimizerWizardVM.MeasuredHfrNearestFocus(
                    perRunBest[i].Points, perRunBest[i].BestFit?.Minimum.X ?? double.NaN);
                Console.WriteLine($"  detection binning (wizard block) {loadedRuns[i].Discovered.RunId}: ran at {ranAt}x{ranAt}; "
                    + $"current measured={F(baseMeasured)} px (vertex {F(baseVertex)}) -> recommends "
                    + $"{DetectionBinningResolver.RecommendFromHfr(baseMeasured, ranAt)}x, "
                    + $"optimized measured={F(bestMeasured)} px (vertex {F(bestVertex)}) -> recommends "
                    + $"{DetectionBinningResolver.RecommendFromHfr(bestMeasured, ranAt)}x "
                    + $"(fit R^2 {F(perRunBaseline[i].Metrics.RSquared)} -> {F(perRunBest[i].Metrics.RSquared)})");
```

- [ ] **Step 2: Update `WriteSummary`'s two lines**

Replace the `in-focus HFR` / `detection bin` pair with:

```csharp
                var ranAt = Math.Max(1, baseline.DetectionBinning);
                var baseMeasured = StarDetectionOptimizerWizardVM.MeasuredHfrNearestFocus(
                    perRunBaseline[i].Points, perRunBaseline[i].BestFit?.Minimum.X ?? double.NaN);
                var bestMeasured = StarDetectionOptimizerWizardVM.MeasuredHfrNearestFocus(
                    perRunBest[i].Points, perRunBest[i].BestFit?.Minimum.X ?? double.NaN);
                sb.AppendLine($"    in-focus HFR  : {F(baseMeasured)} -> {F(bestMeasured)} px (measured at the position nearest focus)");
                sb.AppendLine($"    fit vertex    : {F(perRunBaseline[i].BestFit?.Minimum.Y ?? double.NaN)} -> "
                    + $"{F(perRunBest[i].BestFit?.Minimum.Y ?? double.NaN)} px (MinHFR-seed input only)");
                sb.AppendLine($"    detection bin : ran at {ranAt}x{ranAt}; recommended "
                    + $"{DetectionBinningResolver.RecommendFromHfr(baseMeasured, ranAt)}x (current) / "
                    + $"{DetectionBinningResolver.RecommendFromHfr(bestMeasured, ranAt)}x (optimized)");
```

- [ ] **Step 3: Build**

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build \
  "$(wslpath -w "$PWD/Joko.NINA.Plugins/TestApp/TestApp.csproj")" -c Debug --nologo
```

Expected: `Build succeeded`, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Joko.NINA.Plugins/TestApp/OptimizationDiagnosticRunner.cs
GIT_COMMITTER_NAME="George Hilios" GIT_COMMITTER_EMAIL="322725+ghilios@users.noreply.github.com" \
  git commit --author="George Hilios <322725+ghilios@users.noreply.github.com>" \
  -m "Report both in-focus HFR statistics in the headless optimizer summary

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: Update the manual

**Files:**
- Modify: `documentation/docs/settings/detection-binning.md` (the `## Where the recommendation comes from`
  section, lines ~51-62)
- Modify: `documentation/docs/optimization/index.md` (line ~61)

Read `.claude/docs/documentation-style.md` first. House voice, no confessional headings.

- [ ] **Step 1: Rewrite the "Where the recommendation comes from" opening and table**

Replace:

```markdown
It is **measured, not estimated**. The number is the final HFR of your last auto-focus run: a real exposure
taken at the focuser position the run settled on. Accepting a live sweep in the
[Optimization Wizard](../optimization/index.md) updates it too, from the sweep's fitted curve. The factor is
then whichever brings that measurement closest to 3 px:

| Measured in-focus HFR | Recommended |
|---|---|
| below 4.5 px | 1x1 (off) |
| 4.5 to 7.5 px | 2x2 |
| 7.5 to 10.5 px | 3x3 |
| 10.5 px and above | 4x4 |
```

with:

```markdown
It is **measured, not estimated**. The number is the final HFR of your last auto-focus run: a real exposure
taken at the focuser position the run settled on. Accepting a live sweep in the
[Optimization Wizard](../optimization/index.md) updates it too, from the sweep frames measured at the
position nearest best focus — the same kind of number, so the two sources agree. The factor is then whichever
brings that measurement closest to 3 px:

| Measured in-focus HFR | Recommended |
|---|---|
| below 4.5 px | 1x1 (off) |
| 4.5 to 7.5 px | 2x2 |
| 7.5 to 10.5 px | 3x3 |
| 10.5 px and above | 4x4 |

Those boundaries decide what a *fresh* rig is told. Once you are running a factor, the recommendation holds
it until the measurement is more than **0.6 px** clear of your factor's row, so a rig sitting near 4.5, 7.5
or 10.5 px is not sent back and forth by ordinary night-to-night variation. On 2x2, for instance, anything
from 3.9 px to 8.1 px reads as "2x2 is right". Inside that margin both factors put your stars within the
calibrated range, so either one works.
```

- [ ] **Step 2: Correct the wizard page**

In `documentation/docs/optimization/index.md`, replace:

```markdown
[Detection Binning](../settings/detection-binning.md) fixed, but it reads the in-focus HFR off your fitted
focus curve and reports the factor that implies.
```

with:

```markdown
[Detection Binning](../settings/detection-binning.md) fixed, but it reads the in-focus HFR off the sweep
point nearest best focus and reports the factor that implies.
```

The paragraph below it about the R² ≥ 0.9 gate stays as-is: the fit is still what identifies which point is
nearest focus, so a sweep that lost its donuts still has nothing trustworthy to say.

- [ ] **Step 3: Build the docs**

```bash
cd documentation && mkdocs build --strict 2>&1 | tail -5; cd ..
```

Expected: no warnings or errors. If `mkdocs` is unavailable, verify the two files render by eye and move on.

- [ ] **Step 4: Commit**

```bash
git add documentation/docs/settings/detection-binning.md documentation/docs/optimization/index.md
GIT_COMMITTER_NAME="George Hilios" GIT_COMMITTER_EMAIL="322725+ghilios@users.noreply.github.com" \
  git commit --author="George Hilios <322725+ghilios@users.noreply.github.com>" \
  -m "Document the binning recommendation's no-change zone

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: Verify against the reported run, then gate

**Files:** none modified.

- [ ] **Step 1: Full suite**

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" test \
  "$(wslpath -w "$PWD/Joko.NINA.Plugins/Joko.NINA.Plugins.sln")" -c Debug --nologo
```

Expected: `Failed: 0`. Baseline before this plan was **4438 passed**; expect 4438 + the new tests.
`SendAsync_WritesOnABackgroundThread` is a known flake — re-run it alone if it fails; do not chase it. Do not
pipe this command to `tail`, which masks the exit code.

- [ ] **Step 2: Headless re-verification on the real run**

The working copy and the mirrored profile from the investigation:

```bash
cp /mnt/d/hf-repro/settings/tinkerbolts_bin2.json \
   "/mnt/d/hf-repro/TinkerBolts/AutoFocus_20260929_040614/attempt01/harness_settings.json"
./Joko.NINA.Plugins/TestApp/bin/Debug/net8.0-windows7.0/TestApp.exe optimize --per-run \
  --runs "D:\hf-repro" --settings "D:\hf-repro\settings\tinkerbolts_bin2.json" \
  --profile-id ce3f3e63-8fd3-4b72-a0ca-d90db9441382 --out "D:\hf-repro\out\verify_bin2" \
  > /mnt/d/hf-repro/out_verify_bin2.log 2>&1
grep "detection binning (wizard block)" /mnt/d/hf-repro/out_verify_bin2.log
```

Expected: `ran at 2x2; current measured=4.4363 px (vertex 4.31406) -> recommends 2x, ...`. The **`2x`** is the
whole point — before this plan it read `1x`. If the measured value is not 4.4363, the nearest-point selection
or the recovery-point exclusion is wrong.

- [ ] **Step 3: Update the design doc's status**

Replace the **Status** paragraph of `docs/binning-recommendation-oscillation-design.md` with:

```markdown
**Status:** FIXED. Two changes, both required (see *Why BOTH halves are required* in
`plans/binning-recommendation-oscillation-plan.md`): the wizard now publishes the pooled HFR of the sweep
position nearest the fitted minimum instead of the fitted vertex, and `DetectionBinningResolver` holds the
factor in service while the measurement is within `HysteresisPixels` (0.6 px) of that factor's band. Verified
headless on the reported run: it now reads `ran at 2x2; current measured=4.4363 px -> recommends 2x`, where
before it read `1x`. The fitted vertex is retained as `OptimizationSummary.FitVertexInFocusHfr` for
`MinHfrSeed`, whose documented input it is. NOT yet verified live in NINA.
```

- [ ] **Step 4: Branch, commit, PR**

The branch already exists and the design doc / plan were committed in Task 0, so this commits only Step 3's
status edit, then pushes. `develop` is never pushed to directly.

```bash
git add docs/binning-recommendation-oscillation-design.md
GIT_COMMITTER_NAME="George Hilios" GIT_COMMITTER_EMAIL="322725+ghilios@users.noreply.github.com" \
  git commit --author="George Hilios <322725+ghilios@users.noreply.github.com>" \
  -m "Mark the binning-oscillation diagnosis as fixed

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
git push -u origin ghilios/binning-recommendation-oscillation
gh pr create --base develop \
  --title "Stop the detection-binning recommendation oscillating between runs" \
  --body "$(cat <<'BODY'
A user reported the Star Detection Optimization Wizard oscillating between binning recommendations each run.
Reproduced and isolated on his saved run (`TinkerBolts/AutoFocus_20260929_040614`, ASI2600MC + EdgeHD8 at
1422 mm, 0.545"/px, Detection Binning 2x2): the optimizer is deterministic and converges to a fixed point,
and the cause is that two code paths publish two different quantities into the one field the recommendation
reads.

- `AutoFocusEngine.OnCompleted` published `FinalHFR` — four real exposures at the settled position: 4.662 px.
- The wizard published `BestFit.Minimum.Y` — the fitted hyperbola vertex: 4.314 px.

`RecommendFromHfr` is `round(hfr/3)`, so the 1x1/2x2 boundary is exactly 4.5 px with no margin, and his two
values straddled it. The existing R^2 >= 0.9 mitigation never fired: his fit is R^2 = 0.977.

Two changes, both needed:

1. The wizard now publishes the pooled HFR of the sweep position nearest the fitted minimum (4.4363 px here) —
   the same kind of statistic auto-focus publishes. This narrows the disagreement to 0.226 px but, on its own,
   leaves this rig on the wrong side of 4.5.
2. `DetectionBinningResolver` gains a 0.6 px no-change zone around each band boundary, keyed on the factor
   already in service. On 2x2 that holds 2x2 for anything in [3.9, 8.1), which contains both values.

The fitted vertex is kept as `OptimizationSummary.FitVertexInFocusHfr`, because `MinHfrSeed` documents it as
its input and the optimizer's MinHFR seeding plus every bank result rest on it.

Verified headless on the reported run: `ran at 2x2; current measured=4.4363 px -> recommends 2x` (was `1x`).
Full diagnosis and the four-arm evidence table: `docs/binning-recommendation-oscillation-design.md`.

Not verified live in NINA — the binning block and the "Optimize again at NxN" button are UI.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
BODY
)"
```

---

## Out of scope

- **Fix option 3 from the spec — recording the measurement's provenance** ("stored by auto-focus" vs "stored
  by the wizard") and showing it in the recommendation. Not chosen. Worth revisiting only if users still
  report confusion after this lands: with (b) the two sources now produce the same *kind* of number, so the
  provenance is much less interesting than it was.
- **Live in-app verification.** Nothing here was exercised in NINA. The binning block, the "Optimize again at
  NxN" button and the options-page row are all UI, and this plan only proves them by unit test and headless
  run. Verify in the app before release.
- **The star-starved run itself.** The reported sweep keeps 3-4 stars on its wing frames at 2x2 and the
  default-seeded optimizer scores `J = 0` on it. Real, separate, not this fix.
- **The factor-dependence of the measurement** (design doc §3). The hysteresis absorbs it at 0.44 px against
  a 0.6 px margin; actually making the measurement factor-invariant is a detector change, not this one.
- **`MinHfrSeed`'s input.** Deliberately unchanged — see Task 3's constraint 2.

## Awaiting go-ahead

Per `CLAUDE.md` this plan is not to be executed until explicitly asked. When that happens, `/clear` first so
no planning context leaks into the implementation.
