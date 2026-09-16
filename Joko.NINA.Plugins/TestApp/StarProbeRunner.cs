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
using NINA.Joko.Plugins.HocusFocus.Interfaces;
using NINA.Joko.Plugins.HocusFocus.StarDetection;
using NINA.Joko.Plugins.HocusFocus.Utility;
using NINA.Profile;
using OpenCvSharp;
using Rect = OpenCvSharp.Rect;
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
    /// Per-star measurement probe: runs one detection over a single frame and dumps EVERY measured quantity for
    /// each accepted star — the structure bounding box, the HFR aperture derived from it, the PSF fit's sampling
    /// grid, how many of those samples the saturation mask threw away, and the fit's own goodness statistics —
    /// alongside HFR and FWHM.
    ///
    /// <para><b>Why it is separate from <c>contamination</c>.</b> That runner's CSV carries HFR and FWHM but not
    /// the bounding box, the peak, or the PSF sampling grid — which are exactly the quantities needed to explain
    /// WHY a star's HFR and FWHM disagree, since both the HFR aperture and the PSF fit domain are derived from the
    /// box and the box grows with brightness.</para>
    ///
    /// <para><b><c>--psf-sweep</c> refits</b> the PSF for the probed stars at other <c>PSFResolution</c> values,
    /// with the saturation mask disabled, and over shrunken boxes, to attribute an anomalous FWHM to a specific
    /// input. Those refits run against a measurement image reconstructed here (display Mat + the same hotpixel
    /// filter the detector applies in place); the <c>fwhmPx</c>/<c>refit base</c> agreement printed for each star
    /// is the check that the reconstruction matches what detection actually fitted. The refits use the detector's
    /// own measurement noise sigma, which the Huber IRLS path (<c>UsePSFAbsoluteDeviation</c>) needs for its
    /// threshold. UNBINNED MONO ONLY — a bayered frame is CFA-filtered before its debayer and a binned one is fitted
    /// in binned pixels, and this reconstruction does neither, so for those the sample-census columns are left empty
    /// and the sweep is skipped.</para>
    /// </summary>
    internal static class StarProbeRunner {

        public static async Task Run(string[] args) {
            try {
                await RunImpl(args);
            } catch (Exception ex) {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                Console.Error.WriteLine(ex.ToString());
                Logger.Error(ex, "star-probe run failed");
                Environment.ExitCode = 1;
            }
        }

        private static readonly StarDetector Detector = new StarDetector(new AlglibAPI());

        private static async Task RunImpl(string[] args) {
            var imagePath = DiagnosticUtil.GetArg(args, "--image");
            if (string.IsNullOrWhiteSpace(imagePath)) {
                Console.Error.WriteLine(
                    "Usage: TestApp star-probe --image <path> [--settings <path>] [--profile-id <guid>] [--out <dir>]\n" +
                    "                          [--near <x,y>] [--radius <px>] [--top <n>] [--psf-sweep] [--hotpixel-census]");
                Environment.ExitCode = 2;
                return;
            }
            if (!File.Exists(imagePath)) {
                throw new FileNotFoundException($"Image not found: {imagePath}", imagePath);
            }

            var profileId = DiagnosticUtil.GetArg(args, "--profile-id");
            var outDir = DiagnosticUtil.GetArg(args, "--out");
            if (string.IsNullOrWhiteSpace(outDir)) {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                outDir = Path.Combine(localAppData, "NINA", "Logs", "hf-diag", "star-probe",
                    DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            }
            Directory.CreateDirectory(outDir);

            double? nearX = null, nearY = null;
            var nearArg = DiagnosticUtil.GetArg(args, "--near");
            if (!string.IsNullOrWhiteSpace(nearArg)) {
                var parts = nearArg.Split(',');
                if (parts.Length != 2) {
                    throw new ArgumentException("--near expects <x,y>");
                }
                nearX = double.Parse(parts[0], CultureInfo.InvariantCulture);
                nearY = double.Parse(parts[1], CultureInfo.InvariantCulture);
            }
            var radius = ParseDouble(DiagnosticUtil.GetArg(args, "--radius"), 250.0);
            var top = (int)ParseDouble(DiagnosticUtil.GetArg(args, "--top"), 25);
            var psfSweep = DiagnosticUtil.HasFlag(args, "--psf-sweep");
            var hotpixelCensus = DiagnosticUtil.HasFlag(args, "--hotpixel-census");

            Logger.SetLogLevel(LogLevelEnum.INFO);
            if (Application.Current == null) {
                new Application();
            }

            Console.WriteLine($"Image: {imagePath}");
            Console.WriteLine($"Output: {outDir}");

            var profileService = new ProfileService();
            profileService.TryLoad(profileId ?? string.Empty);
            var activeProfile = profileService.ActiveProfile;
            if (activeProfile == null) {
                throw new InvalidOperationException("No active NINA profile could be loaded. Pass --profile-id.");
            }
            Console.WriteLine($"Profile: {activeProfile.Name} ({activeProfile.Id})");

            var harnessSettings = HarnessSettingsStore.Resolve(args, profileService, activeProfile);
            var options = new StarDetectionOptions(profileService, harnessSettings.Accessor);
            var p = HocusFocusStarDetection.BuildStarDetectorParams(options);
            if (!p.ModelPSF) {
                Console.WriteLine("Forcing ModelPSF on (the probe exists to report the PSF fit)");
                p.ModelPSF = true;
            }

            using var source = await DetectionSource.LoadAsync(imagePath, profileService);
            Console.WriteLine($"Image dimensions: {source.Width} x {source.Height}");

            // Pixel scale from the FRAME's own header when it has one, so FWHMArcsecs is physical for this rig
            // rather than for whichever profile happened to load. It scales every star alike, so it can never
            // change the bright-vs-faint comparison this probe exists for.
            var frameMeta = source.MetaData;
            p.PixelScale = HarnessSettingsStore.PixelScaleForFrame(frameMeta, harnessSettings, out var pixelScaleSource);
            Console.WriteLine($"PixelScale: {p.PixelScale.ToString("0.####", CultureInfo.InvariantCulture)} arcsec/px ({pixelScaleSource})");
            // BuildStarDetectorParams leaves DetectionBinning at 1; live layers the option on (with the matching
            // binned pixel scale) in HocusFocusStarDetection.ApplyDetectionImageContext, so do the same here or a
            // binned profile would be probed unbinned.
            DetectionBinningResolver.ApplyFactor(p, DetectionBinningResolver.ToFactor(options.DetectionBinning));
            Console.WriteLine($"PSFFitType={p.PSFFitType}, PSFResolution={p.PSFResolution}, PSFGoodnessOfFitThreshold={p.PSFGoodnessOfFitThreshold}, " +
                $"SaturationThreshold={p.SaturationThreshold.ToString(CultureInfo.InvariantCulture)}, HotpixelFiltering={p.HotpixelFiltering}, " +
                $"HotpixelThresholdingEnabled={p.HotpixelThresholdingEnabled}, StarMeasurementNoiseReduction={p.StarMeasurementNoiseReductionEnabled}, " +
                $"DetectionBinning={p.DetectionBinning}");

            var result = await source.DetectAsync(Detector, p, CancellationToken.None);
            Console.WriteLine($"Detected {result.DetectedStars.Count} stars (PsfFitFailed={result.Metrics.PSFFitFailed}, Saturated={result.Metrics.Saturated})");
            PrintRejectionCensus(result.Metrics);

            // The measurement image the detector fitted against, reconstructed: display Mat + the same in-place
            // hotpixel filter. Only used for the saturation/sample census and the --psf-sweep refits, and only
            // faithful for an unbinned mono frame (see the class remarks).
            using var measurement = source.CreateDisplayMat();
            if (hotpixelCensus) {
                PrintHotpixelCensus(measurement);
            }
            var bayered = DiagnosticUtil.IsBayeredFrameFileName(imagePath);
            var reconstructionFaithful = !bayered && p.DetectionBinning <= 1;
            if (p.HotpixelFiltering || (p.NoiseReductionRadius > 0 && p.StarMeasurementNoiseReductionEnabled)) {
                if (!p.MeasurementHotpixelRepair) {
                    // Legacy: the measurement image takes the same median the structure path does.
                    if (p.HotpixelThresholdingEnabled) {
                        HotpixelFiltering.HotpixelFilterWithThresholding(measurement, p.HotpixelThreshold);
                    } else {
                        HotpixelFiltering.HotpixelFilter(measurement);
                    }
                    Console.WriteLine("  MeasurementHotpixelRepair is OFF: reconstruction uses the legacy median");
                } else {
                // The MEASUREMENT path's filter, which is the isolation repair — NOT the median the structure
                // path still takes (StarDetector.PrepareMeasurementAndStructureSources).
                var reconstructionRepaired = HotpixelFiltering.RepairIsolatedHotpixels(measurement);
                // Reconstruction-fidelity check, the same role the sweep's `base` column plays: this must equal
                // the detector's own MeasurementHotpixelCount, or the image being refit is not the one detection
                // measured.
                Console.WriteLine($"  reconstruction repaired {reconstructionRepaired} isolated hot pixel(s) (must equal measurementRepaired above)");
                }
            }
            if (p.NoiseReductionRadius > 0 && p.StarMeasurementNoiseReductionEnabled) {
                CvImageUtility.ConvolveGaussian(measurement, measurement, p.NoiseReductionRadius * 2 + 1);
            }

            var rows = result.DetectedStars
                .Select(s => Row.Build(s, measurement, p, reconstructionFaithful))
                .ToList();

            WriteCsv(Path.Combine(outDir, "star_probe.csv"), rows);
            Console.WriteLine($"Wrote star_probe.csv ({rows.Count} rows) to {outDir}");

            var probed = rows;
            if (nearX.HasValue) {
                probed = rows
                    .Where(r => Math.Sqrt((r.X - nearX.Value) * (r.X - nearX.Value) + (r.Y - nearY.Value) * (r.Y - nearY.Value)) <= radius)
                    .ToList();
                Console.WriteLine($"\n{probed.Count} star(s) within {radius.ToString("0", CultureInfo.InvariantCulture)} px of " +
                    $"({nearX.Value.ToString("0", CultureInfo.InvariantCulture)}, {nearY.Value.ToString("0", CultureInfo.InvariantCulture)}):");
            } else {
                Console.WriteLine($"\nBrightest {Math.Min(top, probed.Count)} of {probed.Count} star(s):");
            }
            probed = probed.OrderByDescending(r => r.Peak).Take(top).ToList();
            PrintTable(probed, p);

            if (psfSweep) {
                if (bayered) {
                    Console.WriteLine("\n--psf-sweep SKIPPED: bayered frame (the reconstruction would not match the detector's CFA-filtered source).");
                } else if (!reconstructionFaithful) {
                    Console.WriteLine($"\n--psf-sweep SKIPPED: DetectionBinning={p.DetectionBinning} (detection fitted binned pixels; the reconstruction is full resolution).");
                } else {
                    PrintPsfSweep(probed, measurement, p, result.MeasurementNoiseSigma);
                }
            }
        }

        private static double ParseDouble(string s, double fallback) =>
            string.IsNullOrWhiteSpace(s) ? fallback : double.Parse(s, CultureInfo.InvariantCulture);

        /// <summary>One accepted star, with the derived quantities that explain its HFR and FWHM.</summary>
        private sealed class Row {
            public Star Star;
            public double X, Y;
            public int BX, BY, BW, BH;
            public double Background, Peak, MeanBrightness, Hfr, HfrAperture, Sensitivity;
            public double PsfSampling;
            public int? Samples, SamplesSaturated, SaturatedPixelsInBox;

            /// <summary><paramref name="censusFaithful"/> false leaves the census columns null: the reconstructed
            /// measurement image would not be the one detection sampled.</summary>
            public static Row Build(Star s, Mat measurement, StarDetectorParams p, bool censusFaithful) {
                var box = s.StarBoundingBox;
                var nominal = Math.Sqrt((double)box.Width * box.Height);
                var sampling = nominal / p.PSFResolution;
                int? samples = null, saturated = null, saturatedInBox = null;
                if (censusFaithful) {
                    CensusPsfSamples(s, measurement, p, sampling, out var n, out var nSat);
                    samples = n;
                    saturated = nSat;
                    saturatedInBox = CountSaturatedInBox(measurement, box, p.SaturationThreshold);
                }
                return new Row {
                    Star = s,
                    X = s.Center.X,
                    Y = s.Center.Y,
                    BX = box.X,
                    BY = box.Y,
                    BW = box.Width,
                    BH = box.Height,
                    Background = s.Background,
                    Peak = s.PeakBrightness,
                    MeanBrightness = s.MeanBrightness,
                    Hfr = s.HFR,
                    HfrAperture = Math.Min(box.Width, box.Height) / 2.0,
                    Sensitivity = s.MeasuredSensitivity,
                    PsfSampling = sampling,
                    Samples = samples,
                    SamplesSaturated = saturated,
                    SaturatedPixelsInBox = saturatedInBox
                };
            }
        }

        /// <summary>
        /// Counts the PSF fit's sample grid the way <c>PSFModeler.Create</c> lays it out (same start/step/extent),
        /// and how many of those samples its saturation mask drops. Mirrors that method deliberately — the point is
        /// to report the grid the fit actually saw.
        /// </summary>
        private static void CensusPsfSamples(Star s, Mat measurement, StarDetectorParams p, double sampling, out int samples, out int saturated) {
            samples = 0;
            saturated = 0;
            if (sampling <= 0) {
                return;
            }
            var box = s.StarBoundingBox;
            var startX = s.Center.X - sampling * Math.Floor((s.Center.X - box.Left) / sampling);
            var startY = s.Center.Y - sampling * Math.Floor((s.Center.Y - box.Top) / sampling);
            for (var y = startY; y < box.Bottom; y += sampling) {
                for (var x = startX; x < box.Right; x += sampling) {
                    var v = CvImageUtility.BilinearSamplePixelValue(measurement, y: y, x: x);
                    ++samples;
                    if (v >= p.SaturationThreshold) {
                        ++saturated;
                    }
                }
            }
        }

        private static int CountSaturatedInBox(Mat measurement, Rect box, double saturationThreshold) {
            int count = 0;
            for (var y = Math.Max(0, box.Top); y < Math.Min(measurement.Height, box.Bottom); ++y) {
                for (var x = Math.Max(0, box.Left); x < Math.Min(measurement.Width, box.Right); ++x) {
                    if (measurement.Get<float>(y, x) >= saturationThreshold) {
                        ++count;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Census of the RAW measurement image's single-pixel outliers, before any filtering: how many pixels sit
        /// above the local background by 3/5/8 local sigmas, and how many of those are ISOLATED (their brightest
        /// neighbour under 1/ratio of their own amplitude) at a few ratios. This is what decides how much the
        /// measurement-path hotpixel repair has to do on a given sensor — and, when it does nothing, says whether
        /// the frame simply has no single-pixel outliers or the thresholds are mis-scaled for it.
        /// </summary>
        private static void PrintHotpixelCensus(Mat raw) {
            const int Block = 128;
            var grid = CvImageUtility.ComputeLocalBackgroundGrid(raw, Block, 1e-6f);
            var sigmas = grid.Sigma.OrderBy(v => v).ToArray();
            var meds = grid.Median.OrderBy(v => v).ToArray();
            Console.WriteLine($"  measurement census: local bg median={meds[meds.Length / 2]:E4}, local sigma median={sigmas[sigmas.Length / 2]:E4} " +
                              $"(p05={sigmas[(int)(0.05 * sigmas.Length)]:E4}, p95={sigmas[(int)(0.95 * sigmas.Length)]:E4})");

            var thresholds = new[] { 3.0f, 5.0f, 8.0f };
            var ratios = new[] { 3.0f, 2.0f, 1.0f };
            var above = new long[thresholds.Length];
            var isolated = new long[thresholds.Length * ratios.Length];
            int width = raw.Cols, height = raw.Rows;

            for (int y = 0; y < height; ++y) {
                int gy = Math.Min(grid.GridRows - 1, y / Block);
                int yUp = y > 0 ? y - 1 : 0;
                int yDown = y < height - 1 ? y + 1 : height - 1;
                for (int x = 0; x < width; ++x) {
                    int gx = Math.Min(grid.GridCols - 1, x / Block);
                    float bg = grid.MedianAt(gy, gx);
                    float sig = grid.SigmaAt(gy, gx);
                    float amp = raw.At<float>(y, x) - bg;
                    if (amp <= 0) {
                        continue;
                    }
                    int xLeft = x > 0 ? x - 1 : 0;
                    int xRight = x < width - 1 ? x + 1 : width - 1;
                    float maxN = float.NegativeInfinity;
                    for (int ny = yUp; ny <= yDown; ++ny) {
                        for (int nx = xLeft; nx <= xRight; ++nx) {
                            if (nx == x && ny == y) {
                                continue;
                            }
                            var v = raw.At<float>(ny, nx);
                            if (v > maxN) {
                                maxN = v;
                            }
                        }
                    }
                    for (int t = 0; t < thresholds.Length; ++t) {
                        if (amp <= thresholds[t] * sig) {
                            continue;
                        }
                        above[t]++;
                        for (int r = 0; r < ratios.Length; ++r) {
                            if ((maxN - bg) * ratios[r] < amp) {
                                isolated[t * ratios.Length + r]++;
                            }
                        }
                    }
                }
            }
            long total = (long)width * height;
            for (int t = 0; t < thresholds.Length; ++t) {
                var parts = string.Join(", ", ratios.Select((r, i) => $"ratio{r:0.#}={isolated[t * ratios.Length + i]}"));
                Console.WriteLine($"    >{thresholds[t]:0}sigma: {above[t]} ({100.0 * above[t] / total:F4}%) | isolated: {parts}");
            }
        }

        /// <summary>
        /// Why candidates did NOT become stars. A settings or code change that moves the detected count is only
        /// interpretable next to this: the same drop reads very differently as "rejected below the sensitivity
        /// bar" than as "rejected for an HFR under the floor".
        /// </summary>
        private static void PrintRejectionCensus(StarDetectorMetrics m) {
            Console.WriteLine(
                $"  candidates={m.StructureCandidates} totalDetected={m.TotalDetected} | rejected: tooSmall={m.TooSmall} onBorder={m.OnBorder} " +
                $"tooDistorted={m.TooDistorted} degenerate={m.Degenerate} lowSensitivity={m.LowSensitivity} notCentered={m.NotCentered} " +
                $"tooFlat={m.TooFlat} tooLowHFR={m.TooLowHFR} hfrFailed={m.HFRAnalysisFailed} contaminated={m.ContaminationSuspected} " +
                $"tooElongated={m.TooElongated} bloom={m.BloomSuppressed} outsideROI={m.OutsideROI}");
            Console.WriteLine($"  hotpixels: structure={m.HotpixelCount}{MeasurementHotpixelSuffix(m)} saturatedPixels={m.SaturatedPixelCount}");
        }

        /// <summary>
        /// Reads MeasurementHotpixelCount reflectively so this runner still compiles against a plugin build that
        /// predates the measurement-path hotpixel repair (used to produce before/after arms of one comparison).
        /// </summary>
        private static string MeasurementHotpixelSuffix(StarDetectorMetrics m) {
            var prop = typeof(StarDetectorMetrics).GetProperty("MeasurementHotpixelCount");
            return prop == null ? string.Empty : $" measurementRepaired={prop.GetValue(m)}";
        }

        private static void PrintTable(List<Row> rows, StarDetectorParams p) {
            Console.WriteLine(
                "      x       y   box(WxH)  peak    bg     HFR  aper  | PSF: fwhmPx fwhmX fwhmY  sigX  sigY   R2  redChi2   amp   step  samp sat");
            foreach (var r in rows) {
                var psf = r.Star.PSF;
                // A saturated star is not a fit FAILURE: the detector never attempts one, because a clipped core
                // leaves only wings and the width is not identifiable from them.
                var saturated = (r.Star.Background + r.Star.PeakBrightness) >= p.SaturationThreshold;
                var psfPart = psf == null
                    ? (saturated
                        ? "   (no fit - saturated, not attempted)                               "
                        : "   (no fit - R2 below threshold)                                    ")
                    : $"{psf.FWHMPixels,7:F2}{psf.FWHMx,6:F2}{psf.FWHMy,6:F2}{psf.SigmaX,6:F2}{psf.SigmaY,6:F2}{psf.RSquared,6:F3}{psf.ReducedChiSquared,9:F1}{psf.Peak,7:F3}";
                Console.WriteLine(
                    $"{r.X,8:F1}{r.Y,8:F1}  {r.BW,3}x{r.BH,-3} {r.Peak,7:F3}{r.Background,7:F4}{r.Hfr,7:F2}{r.HfrAperture,6:F1}  | {psfPart}{r.PsfSampling,6:F2}{r.Samples,6}{r.SamplesSaturated,4}");
            }
        }

        /// <summary>
        /// Refits each probed star's PSF under one-at-a-time variations of the fit's inputs, so an anomalous FWHM
        /// can be attributed: finer sampling (<c>PSFResolution</c>), no saturation mask, and a box shrunk toward
        /// the core. "base" repeats the production fit — it must reproduce the detector's <c>fwhmPx</c>, otherwise
        /// the reconstructed measurement image is wrong and nothing below it means anything.
        /// <paramref name="noiseSigma"/> is the detector's measurement noise sigma — the value it passed to the fit.
        /// </summary>
        private static void PrintPsfSweep(List<Row> rows, Mat measurement, StarDetectorParams p, double noiseSigma) {
            Console.WriteLine("\nPSF refit sweep (fwhmPx / R2). 'base' must match the detected fwhmPx above.");
            Console.WriteLine("      x       y  | " + string.Join("  ", Variants(p).Select(v => v.Name.PadLeft(13))));
            foreach (var r in rows) {
                var cells = new List<string>();
                foreach (var v in Variants(p)) {
                    var psf = Refit(r.Star, measurement, p, v, noiseSigma);
                    cells.Add((psf == null ? "  fail" : $"{psf.FWHMPixels,6:F2}/{psf.RSquared,5:F2}").PadLeft(13));
                }
                Console.WriteLine($"{r.X,8:F1}{r.Y,8:F1}  | " + string.Join("  ", cells));
            }
        }

        private sealed class Variant {
            public string Name;
            public int Resolution;
            public bool MaskSaturated;
            public double BoxScale;
        }

        private static IEnumerable<Variant> Variants(StarDetectorParams p) {
            yield return new Variant { Name = "base", Resolution = p.PSFResolution, MaskSaturated = true, BoxScale = 1.0 };
            yield return new Variant { Name = "res=30", Resolution = 30, MaskSaturated = true, BoxScale = 1.0 };
            yield return new Variant { Name = "res=60", Resolution = 60, MaskSaturated = true, BoxScale = 1.0 };
            yield return new Variant { Name = "nomask", Resolution = p.PSFResolution, MaskSaturated = false, BoxScale = 1.0 };
            yield return new Variant { Name = "box*0.5", Resolution = p.PSFResolution, MaskSaturated = true, BoxScale = 0.5 };
            yield return new Variant { Name = "box*.5,res60", Resolution = 60, MaskSaturated = true, BoxScale = 0.5 };
        }

        private static PSFModel Refit(Star star, Mat measurement, StarDetectorParams p, Variant v, double noiseSigma) {
            var scaled = star;
            if (Math.Abs(v.BoxScale - 1.0) > 1e-9) {
                var box = star.StarBoundingBox;
                var w = Math.Max(3, (int)Math.Round(box.Width * v.BoxScale));
                var h = Math.Max(3, (int)Math.Round(box.Height * v.BoxScale));
                scaled = new Star {
                    Center = star.Center,
                    Background = star.Background,
                    BackgroundPlane = star.BackgroundPlane,
                    MeanBrightness = star.MeanBrightness,
                    PeakBrightness = star.PeakBrightness,
                    HFR = star.HFR,
                    StarBoundingBox = new Rect(
                        (int)Math.Round(star.Center.X) - w / 2,
                        (int)Math.Round(star.Center.Y) - h / 2,
                        w, h)
                };
            }
            try {
                var modeler = PSFModeler.Create(
                    alglibAPI: new AlglibAPI(),
                    fitType: p.PSFFitType,
                    psfResolution: v.Resolution,
                    detectedStar: scaled,
                    srcImage: measurement,
                    pixelScale: p.PixelScale,
                    saturationThreshold: v.MaskSaturated ? p.SaturationThreshold : double.MaxValue,
                    pixelIntegration: p.PSFPixelIntegration);
                return modeler == null ? null : PSFModeler.Solve(modeler, useAbsoluteResiduals: p.UsePSFAbsoluteDeviation, noiseSigma: noiseSigma);
            } catch (Exception) {
                return null;
            }
        }

        private static void WriteCsv(string path, List<Row> rows) {
            var sb = new StringBuilder();
            sb.AppendLine("x,y,boxX,boxY,boxW,boxH,background,peak,meanBrightness,measuredSensitivity,hfr,hfrAperture," +
                "psfOk,fwhmPixels,fwhmArcsec,fwhmX,fwhmY,sigmaX,sigmaY,thetaDeg,eccentricity,rSquared,reducedChiSquared,psfPeak,psfBackground,beta," +
                "psfSampling,psfSamples,psfSamplesSaturated,saturatedPixelsInBox");
            foreach (var r in rows) {
                var psf = r.Star.PSF;
                sb.Append(F(r.X)).Append(',').Append(F(r.Y)).Append(',')
                  .Append(r.BX).Append(',').Append(r.BY).Append(',').Append(r.BW).Append(',').Append(r.BH).Append(',')
                  .Append(F(r.Background)).Append(',').Append(F(r.Peak)).Append(',').Append(F(r.MeanBrightness)).Append(',')
                  .Append(F(r.Sensitivity)).Append(',').Append(F(r.Hfr)).Append(',').Append(F(r.HfrAperture)).Append(',')
                  .Append(psf != null ? 1 : 0).Append(',')
                  .Append(F(psf?.FWHMPixels)).Append(',').Append(F(psf?.FWHMArcsecs)).Append(',')
                  .Append(F(psf?.FWHMx)).Append(',').Append(F(psf?.FWHMy)).Append(',')
                  .Append(F(psf?.SigmaX)).Append(',').Append(F(psf?.SigmaY)).Append(',')
                  .Append(F(psf != null ? psf.ThetaRadians * 180.0 / Math.PI : (double?)null)).Append(',')
                  .Append(F(psf?.Eccentricity)).Append(',')
                  .Append(F(psf?.RSquared)).Append(',').Append(F(psf?.ReducedChiSquared)).Append(',')
                  .Append(F(psf?.Peak)).Append(',').Append(F(psf?.Background)).Append(',').Append(F(psf?.Beta)).Append(',')
                  .Append(F(r.PsfSampling)).Append(',').Append(r.Samples).Append(',').Append(r.SamplesSaturated).Append(',')
                  .Append(r.SaturatedPixelsInBox).AppendLine();
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static string F(double? v) =>
            !v.HasValue || double.IsNaN(v.Value) ? "" : v.Value.ToString("G9", CultureInfo.InvariantCulture);
    }
}
