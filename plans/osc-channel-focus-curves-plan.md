# Per-Channel (R/G/B) Focus Curves for OSC Autofocus — Plan

## Context
With a one-shot-color (Bayer) camera, the R, G and B channels often come to focus at different focuser positions because of longitudinal chromatic aberration. Today HocusFocus measures only luminance, so users can't see that spread. This feature adds an opt-in option, **off by default**. When it's on, each AF frame also measures star HFR in the R, G and B planes. Each channel gets its own fitted curve, drawn on the same AF chart, and the properties table shows one row: `R/G/B Δ focus: R +12 / G −3 / B +27`, each relative to the luminance optimum. **The AF still focuses on luminance only:** per-channel data never feeds `regionState.Fittings`, rejection, windowing or the final position.

On execution, copy this plan to `plans/osc-channel-focus-curves-plan.md` (repo convention).

## Key design decision: measure the luminance stars in each channel; don't re-detect
- **Reuse the stars the luminance pass accepted.** For each one, compute HFR again on each colour plane with the same centroid, bounding box and aperture. This:
  - gives every channel the same star set, so the deltas compare like with like;
  - stays robust when the blue channel is noisy or faint;
  - is cheap: an aperture sum per star per channel, with no detection pipeline.
- **Colour planes** come from NINA `ImageUtility.Debayer(bitmapSource, ..., saveColorChannels: true, saveLumChannel: true, bayerPattern)`, which returns `.Data.Red/Green/Blue` at full resolution. Those planes share the luminance coordinate system, so star geometry carries over without rescaling, and the HFR units match luminance, so all the curves share the y-axis.
  - Unverified: the `Red/Green/Blue` getters were confirmed from DLL symbols only. Step 1 checks this.
  - Fallback if the planes aren't available: extract half-resolution CFA planes, modelled on the CFA site indexing in `Utility/HotpixelFiltering.cs:74`. That needs coordinate scaling ×0.5 and HFR ×2.
- **Background and noise per channel:** the luminance `BackgroundPlane` is not valid on a colour plane. For each star, take the median of an annulus in that channel and fit a flat plane with `LocalBackgroundPlane.Flat`. Use a global noise sigma per channel plane for the clip threshold.
- **Shared HFR code:** refactor the body of `StarDetector.MeasureStar` (`StarDetection/StarDetector.cs:1277`) into an `internal static` helper that takes `(Mat, center, bbox, LocalBackgroundPlane, noiseThreshold, AnalysisSamplingSize, HfrTauPolicy)`. The luminance call path must stay bit-identical.
- **Per-frame aggregation:** apply `StarsForHfrAggregation` (saturated-exclusion subset, `HocusFocusStarDetection.cs:688`) and the same average/σ convention as luminance (`HocusFocusStarDetection.cs:792–809`). Each channel produces a `MeasureAndError`.
- **Scope:**
  - Only region 0 / the full-frame AF path, which is the series the chart shows. Inspector and per-region runs ignore the option.
  - Silently a no-op when the image isn't Bayered, or when the Bayer pattern can't be resolved. That case logs once at Info level.

## Implementation steps

1. **Verify the debayer API.** Confirm that `ImageUtility.Debayer(... saveColorChannels:true)` fills the Red/Green/Blue arrays and that they are full-size `ushort[]`. Use a quick TestApp or unit-test probe on a Bayered FITS from the AF bank. If it fails, switch to the CFA-plane fallback.

2. **Option** (`AutoFocus/AutoFocusOptions.cs`, `Interfaces/IAutoFocusOptions.cs`):
   - Add `bool MeasureColorChannels` (persisted with `GetValueBoolean(nameof(...), false)`, plus a `ResetDefaults` entry and the setter pattern at lines 233–244).
   - Add the member to the fakes: `Tests/Inspection/FakeSensorModelOptions.cs`, `Tests/TestDoubles/MediatorBundle.cs`, `TestApp/HarnessFitInputs.cs`.
   - Plumb it through `AutoFocusEngineOptions` (`Interfaces/IAutoFocusEngine.cs:104`) and `AutoFocusEngine.GetOptions` (~3041).
   - Add it to the replay snapshot (`AutoFocusReplayOptionsMapper.cs`, `AutoFocusOptionsSnapshot`).
   - UI, which is required: in `Resources/OptionsDataTemplates.xaml`, template `HocusFocus_AutoFocus_Options`, add a new `RowDefinition`, a label and CheckBox at `Grid.Row="19"`, and a `MeasureColorChannels_Tooltip` resource. Tooltip text: "OSC only. Measures R/G/B focus alongside luminance for display; does not change where AF focuses."

3. **Channel measurement utility.** Add `StarDetection/ColorChannelHfr.cs`, a static class:
   - `ExtractChannelMats(IDebayeredImage/IRenderedImage, SensorType pattern)` returns three `Mat`s. Bayer pattern resolution reuses `RenderedImageLoading.ResolveBayerPattern` (`Utility/RenderedImageLoading.cs:82`).
   - `Measure(Mat channel, IEnumerable<Star> stars, StarDetectorParams p)` returns a `MeasureAndError`: per-star annulus background, then the shared HFR helper from the design section, then aggregation.
   - Dispose the Mats deterministically.

4. **Engine** (`AutoFocus/AutoFocusEngine.cs`):
   - **Where it runs:** in `EvaluateExposure` (798), on the region-null path, when the option is on and the image is Bayered.
     - The current `starDetection.Detect(image, pixelFormat, ...)` result has to expose the accepted `Star` list, which `BuildStarDetectionResult` already has.
     - Then call `ColorChannelHfr` for R, G and B.
     - Extend the return value: add an optional `ChannelMeasures` (`MeasureAndError[3]`) to `MeasureAndError`, or return a small wrapper. The main `Measure` and `Stdev` are unchanged.
   - **Per-point state:** in `AutoFocusRegionState` (188), add `ChannelMeasurementsByFocuserPoint[3]`. Pool frames per position exactly like luminance in `TryCompleteFocuserPoint` (1094).
   - **Live fits:** in `FocusPointMeasurementAction` (1055–1083), build per-channel `ScatterErrorPoint` lists. Fit each with the static `CurveFittingResult.Calculate(state, method, fitting, points)` (111) into a separate `ChannelFittings[3]` (`AutoFocusFitting`). Never touch `regionState.Fittings`.
   - **Finalization (live 1929–1932, replay 2720–2725):**
     - Apply the same symmetric window to the channel points as luminance.
     - Use the **same hyperbolic model** that luminance selected; don't run a separate model selection.
     - Compute each channel's optimum with `DetermineFinalFocusPoint` (416).
     - `Δ_c = channelOptimum.X − luminanceFinal.X`, in focuser steps, rounded.
     - A channel whose fit fails (fewer than 3 points, or the fit isn't acceptable) gets Δ = null and shows "—".
   - **DTOs** (`Interfaces/IAutoFocusEngine.cs`): add nullable `ChannelFittings`/`ChannelPoints` to `AutoFocusMeasurementPointCompletedEventArgs` (320), and `ChannelFocusDeltas` plus `ChannelFittings` to `AutoFocusRegionResult`/`AutoFocusFinishedEventArgsBase` (289/350).

5. **VM** (`AutoFocus/HocusFocusVM.cs`):
   - **New properties:**
     - `PlotRedFocusPoints`, `PlotGreenFocusPoints`, `PlotBlueFocusPoints` (AsyncObservableCollection<ScatterErrorPoint>);
     - `RedFitting`, `GreenFitting`, `BlueFitting` (the active curve `Func<double,double>` for the chosen method);
     - `ChannelFocusDeltaText` (string);
     - `ShowChannelCurves` (bool).
   - **Where they're set:**
     - Fill them in `AutoFocusEngine_MeasurementPointCompleted` (1260) and `AutoFocusEngine_CompletedNoReport` (1180).
     - Clear them in reset (533–547) and in `IterationFailed` (1168).
   - **Format:** `ChannelFocusDeltaText` reads `R +12  G −3  B +27` (signed steps). Use a pure static formatter so it can be unit-tested.

6. **Chart and table** (`AutoFocus/DataTemplates.xaml`, `HocusFocusVM_Core`):
   - **Chart:** add three `ScatterSeries` (small markers, fixed red/green/blue, semi-transparent) and three `controls:BoundedFunctionAnnotation`s (patterned after line 309, thin lines in the channel colours). Bind visibility to `ShowChannelCurves`. Luminance series keep their current styling and z-order on top.
   - **Table:** add one row in Grid.Row 2, a `UniformGrid Columns="2"` with the label "R/G/B Δ focus" and the value `ChannelFocusDeltaText`, collapsed through `BooleanToVisibilityCollapsedConverter` when `ShowChannelCurves` is false.
   - Update `Tests/AutoFocus/AutoFocusFooterRowMarkupTests.cs` if it enumerates rows.

7. **Report persistence** (`AutoFocus/HocusFocusReport.cs`):
   - Add `[JsonProperty]` nullable `ChannelFocusDeltas` (R/G/B ints) and per-channel measured points, through new optional parameters on `GenerateReport` (79–162).
   - Extend `ApplyInfoRowsFromLoadedReport` (810–851) so the Δ row and the channel points come back when a report is reloaded. The curves are refit from the points with `CurveFittingResult.Calculate`.
   - Old reports deserialize with the fields null, so the row stays hidden.

8. **Docs.** Add a short section to the AF page of the MkDocs manual (`documentation/docs/`), following `.claude/docs/documentation-style.md`.

## Tests (NUnit, `Joko.NINA.Plugins.HocusFocus.Tests/`)
- **`AutoFocusOptionsTests`:** default false, the setter persists, `ResetDefaults` restores it.
- **`ColorChannelHfrTests`:**
  - Synthetic Gaussian stars at known σ, different per channel, on a flat background plus noise. Measured HFR increases with σ, and the shared helper matches the old `MeasureStar` exactly on the luminance path (refactor parity).
  - A non-Bayered image yields no channel data.
- **Engine, via `ScriptedStarDetection` / `AutoFocusEngineSweepBatteryTests` harness:** script per-channel HFR curves with known minima offsets (for example R +20, B −30). Assert:
  - the deltas are recovered to within tolerance;
  - the luminance final position is **identical** with the option on and off (the key invariant);
  - option off produces no channel data.
- **Formatter test** for `ChannelFocusDeltaText`, including a null channel shown as "—".
- **Report round-trip:** the new fields serialize and reload; an old report loads with the row hidden (`HocusFocusReportTests`, `HocusFocusVMChartReloadTests`).

## Verification
1. `rtk dotnet test Joko.NINA.Plugins/Joko.NINA.Plugins.sln -c Debug --nologo` passes, with nothing skipped.
2. Use the replay path (`AutoFocusEngine` replay, or TestApp against a saved OSC AF run in `D:\Autofocus Bank`) with the option on. Confirm:
   - three channel curves render;
   - the Δ row appears;
   - the deltas are plausible: G ≈ 0, R and B of opposite sign on a refractor;
   - the luminance final position matches a replay with the option off.
3. In NINA with an OSC camera or simulator Bayer images: toggle the option, run AF, and check the chart, the table row, and that a reloaded report restores both.
4. Mono camera with the option on: no channel series, no row, no errors in the log.
