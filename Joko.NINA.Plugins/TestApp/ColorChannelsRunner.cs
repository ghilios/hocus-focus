#region "copyright"

/*
    Copyright © 2021 - 2026 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Image.Interfaces;
using NINA.Joko.Plugins.HocusFocus.Interfaces;
using NINA.Joko.Plugins.HocusFocus.StarDetection;
using NINA.Joko.Plugins.HocusFocus.Utility;
using NINA.Profile;
using NINA.WPF.Base.ViewModel.AutoFocus;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Logger = NINA.Core.Utility.Logger;

namespace TestApp {

    /// <summary>
    /// Headless check of the per-colour-channel (OSC) focus curves on a saved AF run. Each frame is detected exactly
    /// as the live AF does (the rendered image, the run's own saved detector params), the luminance point is
    /// aggregated the way the engine aggregates it, and the same stars are re-measured on the debayered R/G/B planes
    /// through the production <see cref="ColorChannelHfr"/>. Luminance is fitted with the production Hybrid model
    /// selection; each channel is fitted with the model luminance selected, as the engine's finalization does, and
    /// its vertex is reported relative to the luminance vertex.
    /// </summary>
    internal static class ColorChannelsRunner {
        private static readonly string[] ChannelNames = { "R", "G", "B" };

        public static async Task Run(string[] args) {
            try {
                await RunImpl(args);
            } catch (Exception ex) {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                Console.Error.WriteLine(ex.ToString());
                Environment.ExitCode = 1;
            }
        }

        private static async Task RunImpl(string[] args) {
            var afRun = DiagnosticUtil.GetArg(args, "--af-run");
            if (string.IsNullOrWhiteSpace(afRun)) {
                Console.Error.WriteLine("Usage: TestApp color-channels --af-run <dir> [--profile-id <guid>] [--out <dir>]");
                Console.Error.WriteLine("                              [--max-rejections <n>=1] [--confidence <c>=0.9] [--step-size <n>]");
                Environment.ExitCode = 2;
                return;
            }
            var outDir = DiagnosticUtil.GetArg(args, "--out");
            if (string.IsNullOrWhiteSpace(outDir)) {
                outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NINA", "Logs", "hf-diag", "color-channels");
            }
            Directory.CreateDirectory(outDir);
            Logger.SetLogLevel(LogLevelEnum.INFO);

            var frames = AfFitDiagnosticRunner.ParseAfRun(afRun);
            if (frames.Count == 0) {
                throw new InvalidOperationException($"No AF frames matched the filename pattern in {afRun}");
            }
            if (Application.Current == null) {
                new Application();
            }
            var profileService = new ProfileService();
            profileService.TryLoad(DiagnosticUtil.GetArg(args, "--profile-id") ?? string.Empty);
            if (profileService.ActiveProfile == null) {
                throw new InvalidOperationException("No active NINA profile. Pass --profile-id or run NINA once.");
            }

            var (p, paramsSource) = AfFitDiagnosticRunner.LoadOriginalDetectorParams(afRun, null);
            if (p == null) {
                throw new InvalidOperationException("The run has no saved detection JSON to take detector params from.");
            }
            p.ModelPSF = false;
            var maxRejections = (int)Parse(DiagnosticUtil.GetArg(args, "--max-rejections"), 1);
            var confidence = Parse(DiagnosticUtil.GetArg(args, "--confidence"), 0.9);
            var stepSize = (int)Parse(DiagnosticUtil.GetArg(args, "--step-size"), AfFitDiagnosticRunner.EstimateStepSize(frames, profileService));
            Console.WriteLine($"Detector params from: {paramsSource}; region {p.Region}; MeasurementAverage={p.MeasurementAverage}");
            Console.WriteLine($"Frames: {frames.Count}; step size {stepSize}; max rejections {maxRejections}; confidence {confidence}");

            var detector = new StarDetector(new AlglibAPI());
            var luminance = new List<ScatterErrorPoint>();
            var channels = new[] { new List<ScatterErrorPoint>(), new List<ScatterErrorPoint>(), new List<ScatterErrorPoint>() };
            var csv = new StringBuilder("FocuserPosition,Stars,LumHFR,LumSigma,RHFR,RSigma,GHFR,GSigma,BHFR,BSigma\n");
            foreach (var frame in frames.OrderByDescending(f => f.FocuserPosition)) {
                using var source = await DetectionSource.LoadAsync(frame.Path, profileService);
                if (!(source.Rendered is IDebayeredImage debayered)) {
                    throw new InvalidOperationException($"{Path.GetFileName(frame.Path)} is not a debayered OSC frame (profile DebayerImage off, or a mono run)");
                }
                var result = await source.DetectAsync(detector, p, CancellationToken.None);
                var stars = result.DetectedStars;
                var hfrStars = HocusFocusStarDetection.StarsForHfrAggregation(stars, p.ExcludeSaturatedStarsFromHFR, p.SaturationThreshold);
                var lum = ColorChannelHfr.Aggregate(hfrStars.Select(s => s.HFR).ToList(), p.MeasurementAverage);
                MeasureAndError[] measures;
                using (var planes = ColorChannelHfr.ExtractChannelMats(debayered.RawImageData, debayered.BayerPattern, p)) {
                    measures = ColorChannelHfr.MeasureAll(planes, stars, p);
                }

                luminance.Add(new ScatterErrorPoint(frame.FocuserPosition, lum.Measure, 0, lum.Stdev));
                for (var c = 0; c < 3; c++) {
                    channels[c].Add(new ScatterErrorPoint(frame.FocuserPosition, measures[c].Measure, 0, measures[c].Stdev));
                }
                Console.WriteLine($"  pos {frame.FocuserPosition}: {stars.Count} stars ({debayered.BayerPattern})  " +
                    $"L={lum.Measure:F3}  R={measures[0].Measure:F3}  G={measures[1].Measure:F3}  B={measures[2].Measure:F3}");
                csv.AppendLine(string.Join(",", new[] { frame.FocuserPosition.ToString(CultureInfo.InvariantCulture), stars.Count.ToString(CultureInfo.InvariantCulture),
                    F(lum.Measure), F(lum.Stdev), F(measures[0].Measure), F(measures[0].Stdev), F(measures[1].Measure), F(measures[1].Stdev),
                    F(measures[2].Measure), F(measures[2].Stdev) }));
            }
            File.WriteAllText(Path.Combine(outDir, "color_channel_points.csv"), csv.ToString());

            var alglib = new AlglibAPI();
            var lumPoints = WeightRegularization.Regularize(luminance.Where(pt => pt.Y > 0).ToList());
            var model = AlglibHyperbolicFitting.SelectBestModel(alglib, lumPoints, stepSize, true, maxRejections, confidence, out var lumFit, out _);
            if (lumFit == null) {
                throw new InvalidOperationException("Luminance fit failed.");
            }
            var summary = new StringBuilder();
            void Emit(string line) { Console.WriteLine(line); summary.AppendLine(line); }
            Emit("");
            Emit($"AF run: {afRun}");
            Emit($"Luminance: model {model}, vertex {lumFit.Minimum.X:F1} (HFR {lumFit.Minimum.Y:F3}), R^2 {lumFit.RSquared:F4}");
            for (var c = 0; c < 3; c++) {
                var pts = WeightRegularization.Regularize(channels[c].Where(pt => pt.Y > 0).ToList());
                if (pts.Count < 3) {
                    Emit($"  {ChannelNames[c]}: fewer than 3 measurable points - no fit (delta -)");
                    continue;
                }
                var fit = AlglibHyperbolicFitting.Create(alglib, model, pts, stepSize, true);
                if (!fit.Solve()) {
                    Emit($"  {ChannelNames[c]}: fit failed (delta -)");
                    continue;
                }
                var inRange = fit.Minimum.X >= pts.Min(pt => pt.X) && fit.Minimum.X <= pts.Max(pt => pt.X);
                var delta = (int)Math.Round(fit.Minimum.X - lumFit.Minimum.X);
                Emit($"  {ChannelNames[c]}: vertex {fit.Minimum.X:F1} (HFR {fit.Minimum.Y:F3}), R^2 {fit.RSquared:F4}, delta {delta:+0;-0;0} steps{(inRange ? "" : "  [vertex outside measured range - engine shows -]")}");
            }
            File.WriteAllText(Path.Combine(outDir, "color_channel_summary.txt"), summary.ToString());
            Console.WriteLine($"Wrote {outDir}");
        }

        private static double Parse(string s, double fallback) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        private static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    }
}
