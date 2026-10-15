# Issue #217: UI freezes for minutes during AF (degenerate fit → giant dashed WPF path)

## Context

Reporter (HF 3.0.0.25, NINA 3.2) sees NINA's UI thread pinned at 100% for 40 s to 14.5 min during and after AF.
AFs then hit the timeout. Live stacks show WPF in `PathGeometry.GetPathBoundsAsRB` (stroked bounds of a dashed
pen) during the render pass. The reporter linked the freezes to intermediate **Uneven Blend** fits with `b` on its
0.001 lower bound: 7/7 stalled runs vs 8/140 clean runs, and 0/246 with the symmetric fit. Goal: AF can never
freeze the UI, whatever curve the optimizer returns.

## Root cause (verified against current `develop` code + decompiled OxyPlot 2.2.0 / Contrib.Wpf 2.1.2)

Four links, each confirmed:

1. **A legitimate but extreme fit.** All four hyperbolic models bound `b ≥ 0.001` and have far-field slope
   `a/b` HFR/step (`HyperbolicUnevenFittingAlglib.cs:166`, `HyperbolicFittingAlglib.cs:116`,
   `TiltedHyperbolicFittingAlglib.cs:174`, `SmoothBlendHyperbolicFittingAlglib.cs:210`).
   - x0 is also bounded to `[lowestInputX, highestInputX]`.
   - Mid-sweep, while HFR is still falling toward the outermost sampled point, x0 pins to that edge. With no data
     on the far side, the Uneven Blend's left branch (`t = clamp((x0−x)/StepSize)`) leaves `b` unconstrained, so LM
     drives it to 0.001.
   - Reporter's example: `a=2.781, b=0.001` gives ≈1e8 HFR one axis-padding past the edge.
   - The fit is still a correct least-squares answer on the sampled points; it just can't be plotted naively.
2. **OxyPlot doesn't clip annotation geometry.** Decompiled `PathAnnotation.Render` (Core 2.2.0):
   - It calls `rc.DrawReducedLine(screenPoints, …, dashArray, …)` on the raw points and only clips through the
     render context's clip rect. Pre-2.1 OxyPlot used `DrawClippedLine`, which clipped geometrically.
   - `PathAnnotation` defaults to `LineStyle.Dash`.
   - `FunctionAnnotation.GetScreenPoints` samples X over the visible axis (Resolution 400) but leaves Y unbounded.
     So a 1e8 HFR value becomes a ~1e9–1e10 px dashed polyline.
3. **WPF computes stroked bounds of the dashed path on the UI thread.** `UIElement.GetHitTestBounds →
   DrawGeometry(…, Pen)` runs during `MediaContext.Render`, and its cost scales with path length / dash period
   (about 1e9 dashes). That is the reporter's stack, and it accounts for the minutes-long CPU-bound stall.
4. **The AF engine waits on the UI thread.**
   - `FocusPointMeasurementAction` raises `MeasurementPointCompleted` while holding `SubMeasurementsLock`
     (`AutoFocusEngine.cs:1059-1081`).
   - `HocusFocusVM.AutoFocusEngine_MeasurementPointCompleted` (`HocusFocusVM.cs:1260`) and
     `InspectorVM.cs:1288` mutate `AsyncObservableCollection`s, which `Send` synchronously to the frozen dispatcher.
   - The measurement task, and the walk awaiting it ("Waiting on next left movement analysis"), stall with it.
   - Post-AF freezes come from the same render path: `AutoFocusEngine_CompletedNoReport` re-assigns the final fit.

**Still reachable on v4:**
- Users on `HyperbolicFitModel.UnevenBlend` are affected exactly as reported.
- The default Hybrid mode renders Tilted live, which shares the same `b` lower bound, so it is plausibly affected
  but unconfirmed.
- A Hybrid run that finalizes to Uneven Blend is exposed after the AF.
- Exposed charts:
  - The main AF chart: `AutoFocus/DataTemplates.xaml:296/309/322`.
  - The Inspector region charts: `AutoFocus/DataTemplates.xaml:2499-2655` and `Resources/OptionsDataTemplates.xaml:208-283`.
  - The optimizer / frame-review charts: `StarDetection/Optimization/DataTemplates.xaml:178/244`.

## Fix (recommended): bound the curve in screen space at the rendering layer

The defect is "a valid steep curve produces an unbounded WPF path", so fix it where the path is built, for every
chart and model. Fit results stay unchanged, so AF results are bit-identical.

1. **New `Controls/BoundedFunctionAnnotation.cs`** (namespace `NINA.Joko.Plugins.HocusFocus.Controls`, added in
   XAML as `xmlns:hfcontrols`). It contains:
   - `internal static IList<ScreenPoint> ClampToBand(IList<ScreenPoint> pts, OxyRect plotArea)`, pure:
     - Clamp each Y to `[Top − Height, Bottom + Height]` and each X to `[Left − Width, Right + Width]`.
     - Leave NaN untouched (current behavior).
     - Map ±∞ to the band edge.
     - Visual error is ≤ one sample step (~plot width/400 px) on the segment that leaves the plot, so it can't be seen.
   - Core subclass `BoundedFunctionAnnotationModel : OxyPlot.Annotations.FunctionAnnotation`. It overrides
     `GetScreenPoints()` to return `ClampToBand(base.GetScreenPoints(), PlotModel.PlotArea)`.
   - WPF wrapper `BoundedFunctionAnnotation : OxyPlot.Wpf.FunctionAnnotation`. Its ctor sets
     `InternalAnnotation = new BoundedFunctionAnnotationModel()`. The setter is `protected`, and the base
     `SynchronizeProperties` casts to `OxyPlot.Annotations.FunctionAnnotation`, which the subclass satisfies, so
     every existing DP (Equation, Color, LineStyle, StrokeThickness, Resolution) keeps working.
2. **Swap all 16 `oxy:FunctionAnnotation` usages** to the new element in the three XAML files above. This is a
   mechanical element rename (`oxy:FunctionAnnotation` → `hfcontrols:BoundedFunctionAnnotation`, including the
   `.Color`/`.StrokeThickness` property-element tags), plus the `hfcontrols` xmlns in each file. Quadratic and Gaussian annotations switch too:
   harmless, and it future-proofs them.
   - *As implemented:* `AutoFocus/DataTemplates.xaml` and `Resources/OptionsDataTemplates.xaml` already map
     `xmlns:controls` to this CLR namespace, so the element is `controls:BoundedFunctionAnnotation` and
     `StarDetection/Optimization/DataTemplates.xaml` gained the same `xmlns:controls` line; no `hfcontrols` alias.
3. **Tests** in `Joko.NINA.Plugins.HocusFocus.Tests/Controls/BoundedFunctionAnnotationTests.cs` (NUnit, existing style):
   - `ClampToBand` unit cases: in-band points untouched, far points clamped, NaN preserved, ±∞ → edge.
   - Headless end-to-end regression for #217, no WPF needed:
     - Build a `PlotModel` with linear X 240000–320000 and Y 2–6.
     - Plot the issue's degenerate Uneven Blend curve (x0=279518.987, a=2.781, b=0.001, c from the issue, step
       16000) through `BoundedFunctionAnnotationModel`.
     - Render with `OxyPlot.SvgExporter.ExportToString` and assert every path coordinate stays inside the band.
     - A control assertion with the plain `FunctionAnnotation` shows coordinates > 1e7, which documents the bug.
   - A well-behaved curve renders identically through both annotation types.

## Explicitly not in this fix (proposed follow-ups, each its own issue/PR)

- **Engine/UI decoupling (defense in depth).** Moving the `OnMeasurementPointCompleted` call outside the lock
  alone does not unblock the engine: `AsyncObservableCollection` still `Send`s synchronously from the measurement
  task. The real fix has three parts:
  - Snapshot the event args under the lock and raise them after it is released.
  - Have the VM handlers marshal asynchronously via `applicationDispatcher` (FIFO keeps ordering).
  - Flush that queue before `Completed` builds the report.

  That's a riskier ordering and report-completeness change, not needed once the render can't stall.
- **Fit-side sanity.** Options are a step-size-relative lower bound on `b`/`c`, or skipping the intermediate
  hyperbolic display until both sides of x0 have data. These change AF results, so they need an AF-bank
  regression (`running-af-bank-validation` skill). They'd improve intermediate Grubbs flags, but they aren't
  needed for the freeze.

## Execution steps

0. Branch `ghilios/issue-217-bounded-fit-curve` from `develop`. Copy this plan to
   `plans/issue-217-bounded-fit-curve-plan.md`.
1. Write the tests (TDD), watch them fail, then implement `BoundedFunctionAnnotation.cs`.
2. Swap the XAML usages (`grep -rn "oxy:FunctionAnnotation" --include=*.xaml` should come back empty afterwards).
3. Commit with the privacy email. Open a PR referencing #217 (Fixes #217) after the user approves.

## Verification

- `rtk dotnet test Joko.NINA.Plugins/Joko.NINA.Plugins.sln -c Debug --nologo` must be all green. That includes the
  new SVG regression test, which proves the path is bounded with the exact parameters from the issue.
- `rtk dotnet build` with 0 errors, which confirms the XAML compiles with the new element.
- Manual in NINA:
  - Run or replay an AF, then check that the fitted curve and its dashed style look unchanged on the main AF
    chart, the Inspector region charts, and the optimizer charts.
  - Optionally, temporarily force a degenerate `Fitting` (or replay a reporter AF from the gist with
    `HyperbolicFitModel=UnevenBlend`) and confirm the UI stays responsive.
- Offer the reporter a test build. They volunteered and have the exact repro.
