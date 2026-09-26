#region "copyright"

/*
    Copyright © 2021 - 2026 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Enum;
using NINA.Joko.Plugins.HocusFocus.Interfaces;
using NINA.Joko.Plugins.HocusFocus.StarDetection;
using NINA.Joko.Plugins.HocusFocus.Utility;
using NINA.Profile;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Runs the detector with PSF modelling ON over ONE frame per AF-bank run — the frame nearest best focus —
    /// and reports what PSF modelling produced there: fit acceptance, the FWHM distribution, the scatter about a
    /// fitted quadratic field surface, and the wall time the PSF stage itself costs.
    ///
    /// <para><b>Two passes.</b> <c>--select</c> detects EVERY frame of every run with <c>ModelPSF=false</c> and
    /// writes a manifest naming the lowest-median-HFR frame per run. The measurement pass then reads that
    /// manifest, so a before/after comparison scores the IDENTICAL frames even though the change moves HFR.</para>
    ///
    /// <para><b>Timing.</b> Each selected frame is detected twice, once with <c>ModelPSF=false</c> and once with
    /// it on; the difference is the PSF stage's cost. Both are preceded by a warm-up detect that runs the PSF
    /// path, so image decode, JIT and the solver's first-call cost land there rather than in either number.</para>
    ///
    /// <para>Settings are the SHIPPED DEFAULTS (<c>BuildDefaultStarDetectorParams</c>), which is the point: the
    /// comparison is of what a user gets out of the box before and after a change.</para>
    /// </summary>
    internal static class PsfBankRunner {

        public static async Task Run(string[] args) {
            try {
                await RunImpl(args);
            } catch (Exception ex) {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                Console.Error.WriteLine(ex.ToString());
                Logger.Error(ex, "psf-bank run failed");
                Environment.ExitCode = 1;
            }
        }

        private static int? psfResolutionOverride;
        private static bool? psfAbsDevOverride;

        private sealed class ManifestEntry {
            public string runId { get; set; }
            public string framePath { get; set; }
            public int focuserPosition { get; set; }
            public double medianHfr { get; set; }
            public int starCount { get; set; }
        }

        private sealed class Manifest {
            public string runsRoot { get; set; }
            public string createdUtc { get; set; }
            public List<ManifestEntry> entries { get; set; } = new List<ManifestEntry>();
        }

        private static async Task RunImpl(string[] args) {
            var runsDir = DiagnosticUtil.GetArg(args, "--runs");
            var outDir = DiagnosticUtil.GetArg(args, "--out");
            var manifestPath = DiagnosticUtil.GetArg(args, "--manifest");
            var label = DiagnosticUtil.GetArg(args, "--label") ?? "run";
            var select = DiagnosticUtil.HasFlag(args, "--select");
            // Overrides on top of the shipped defaults, so one arm can isolate a single knob's cost.
            var psfResolutionArg = DiagnosticUtil.GetArg(args, "--psf-resolution");
            var psfAbsDevArg = DiagnosticUtil.GetArg(args, "--psf-abs-dev");
            psfResolutionOverride = string.IsNullOrWhiteSpace(psfResolutionArg) ? (int?)null : int.Parse(psfResolutionArg, CultureInfo.InvariantCulture);
            psfAbsDevOverride = string.IsNullOrWhiteSpace(psfAbsDevArg) ? (bool?)null : bool.Parse(psfAbsDevArg);
            var profileId = DiagnosticUtil.GetArg(args, "--profile-id");

            if (string.IsNullOrWhiteSpace(runsDir) || string.IsNullOrWhiteSpace(outDir)) {
                Console.Error.WriteLine(
                    "Usage: TestApp psf-bank --runs <bankDir> --out <dir> [--select] [--manifest <path>] [--label <name>] [--profile-id <guid>]\n" +
                    "  --select            pick the nearest-focus frame per run and write <out>/psf_bank_manifest.json\n" +
                    "  --manifest <path>   measure PSF modelling on the frames that manifest names\n" +
                    "  --psf-resolution N  override PSFResolution (to isolate one knob's cost)\n" +
                    "  --psf-abs-dev T|F   override UsePSFAbsoluteDeviation");
                Environment.ExitCode = 2;
                return;
            }
            Directory.CreateDirectory(outDir);

            Logger.SetLogLevel(LogLevelEnum.ERROR);
            if (Application.Current == null) {
                new Application();
            }

            var profileService = new ProfileService();
            profileService.TryLoad(profileId ?? string.Empty);
            var activeProfile = profileService.ActiveProfile;
            if (activeProfile == null) {
                throw new InvalidOperationException("No active NINA profile could be loaded. Pass --profile-id.");
            }
            Console.WriteLine($"Profile: {activeProfile.Name} ({activeProfile.Id})");

            var harnessSettings = HarnessSettingsStore.Resolve(args, profileService, activeProfile);
            var detector = new StarDetector(new AlglibAPI());

            if (select) {
                await SelectAsync(runsDir, outDir, profileService, harnessSettings, detector);
                return;
            }

            if (string.IsNullOrWhiteSpace(manifestPath)) {
                manifestPath = Path.Combine(outDir, "psf_bank_manifest.json");
            }
            if (!File.Exists(manifestPath)) {
                throw new FileNotFoundException($"Manifest not found: {manifestPath}. Run with --select first.", manifestPath);
            }
            await MeasureAsync(manifestPath, outDir, label, profileService, harnessSettings, detector);
        }

        // ── selection ───────────────────────────────────────────────────────────────────────────────────

        private static async Task SelectAsync(string runsDir, string outDir, ProfileService profileService, HarnessSettingsStore.Resolved harnessSettings, StarDetector detector) {
            var discovery = OptimizationRunDiscovery.Discover(runsDir);
            Console.WriteLine($"Discovered {discovery.Runs.Count} run(s) under {runsDir}");

            var manifest = new Manifest {
                runsRoot = runsDir,
                createdUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };

            foreach (var run in discovery.Runs) {
                ManifestEntry best = null;
                foreach (var frame in run.Frames.OrderBy(f => f.FocuserPosition)) {
                    try {
                        using var source = await DetectionSource.LoadAsync(frame.Path, profileService);
                        var p = DefaultParams(source, harnessSettings, modelPsf: false);
                        var result = await source.DetectAsync(detector, p, CancellationToken.None);
                        var stars = result.DetectedStars;
                        if (stars.Count < 5) {
                            continue;
                        }
                        var medianHfr = Median(stars.Select(s => s.HFR).ToList());
                        if (best == null || medianHfr < best.medianHfr) {
                            best = new ManifestEntry {
                                runId = run.RunId,
                                framePath = frame.Path,
                                focuserPosition = frame.FocuserPosition,
                                medianHfr = medianHfr,
                                starCount = stars.Count
                            };
                        }
                    } catch (Exception ex) {
                        Console.Error.WriteLine($"  {run.RunId}: {Path.GetFileName(frame.Path)} failed: {ex.Message}");
                    }
                }
                if (best == null) {
                    Console.WriteLine($"  {run.RunId}: NO usable frame");
                    continue;
                }
                manifest.entries.Add(best);
                Console.WriteLine($"  {run.RunId}: {Path.GetFileName(best.framePath)} focuser={best.focuserPosition} medianHFR={best.medianHfr:F3} stars={best.starCount}");
            }

            var path = Path.Combine(outDir, "psf_bank_manifest.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(manifest, Formatting.Indented));
            Console.WriteLine($"Wrote {path} ({manifest.entries.Count} runs)");
        }

        // ── measurement ─────────────────────────────────────────────────────────────────────────────────

        private static async Task MeasureAsync(string manifestPath, string outDir, string label, ProfileService profileService, HarnessSettingsStore.Resolved harnessSettings, StarDetector detector) {
            var manifest = JsonConvert.DeserializeObject<Manifest>(File.ReadAllText(manifestPath));
            Console.WriteLine($"Measuring {manifest.entries.Count} frame(s) from {manifestPath}");
            Console.WriteLine($"  PSFResolution override: {(psfResolutionOverride.HasValue ? psfResolutionOverride.Value.ToString(CultureInfo.InvariantCulture) : "none")}, " +
                              $"UsePSFAbsoluteDeviation override: {(psfAbsDevOverride.HasValue ? psfAbsDevOverride.Value.ToString() : "none")}");

            var runCsv = new StringBuilder();
            runCsv.AppendLine("runId,frame,focuser,width,height,pixelScale,detected,psfAccepted,psfFitFailed,saturated," +
                              "hotpixelCount,hfrMedian,hfrMad,fwhmPxMedian,fwhmPxMad,fwhmPxP05,fwhmPxP95,fwhmPxMax," +
                              "fwhmArcsecMedian,eccMedian,r2Median,scatterSD,detectMsNoPsf,detectMsWithPsf,psfMs");
            var starCsv = new StringBuilder();
            starCsv.AppendLine("runId,x,y,hfr,background,peak,saturated,fwhmPx,fwhmArcsec,ecc,r2,psfPeak");

            foreach (var entry in manifest.entries) {
                if (!File.Exists(entry.framePath)) {
                    Console.Error.WriteLine($"  {entry.runId}: missing {entry.framePath}");
                    continue;
                }
                try {
                    using var source = await DetectionSource.LoadAsync(entry.framePath, profileService);

                    var noPsf = DefaultParams(source, harnessSettings, modelPsf: false);
                    var withPsf = DefaultParams(source, harnessSettings, modelPsf: true);

                    // Warm-up on the PSF path, so decode caches, JIT and the alglib solver's first-call cost
                    // land here rather than in either timed run.
                    await source.DetectAsync(detector, withPsf, CancellationToken.None);

                    var sw = Stopwatch.StartNew();
                    var baseline = await source.DetectAsync(detector, noPsf, CancellationToken.None);
                    sw.Stop();
                    var msNoPsf = sw.Elapsed.TotalMilliseconds;

                    sw.Restart();
                    var result = await source.DetectAsync(detector, withPsf, CancellationToken.None);
                    sw.Stop();
                    var msWithPsf = sw.Elapsed.TotalMilliseconds;

                    var stars = result.DetectedStars;
                    var fitted = stars.Where(s => s.PSF != null).ToList();
                    var fwhmPx = fitted.Select(s => s.PSF.FWHMPixels).ToList();
                    var saturationThreshold = withPsf.SaturationThreshold;

                    var (hfrMedian, hfrMad) = MedianMad(stars.Select(s => s.HFR).ToList());
                    var (fwhmMedian, fwhmMad) = MedianMad(fwhmPx);
                    var scatter = ScatterAboutQuadraticSurface(fitted);

                    runCsv.AppendLine(string.Join(",",
                        Csv(entry.runId),
                        Csv(Path.GetFileName(entry.framePath)),
                        entry.focuserPosition.ToString(CultureInfo.InvariantCulture),
                        source.Width.ToString(CultureInfo.InvariantCulture),
                        source.Height.ToString(CultureInfo.InvariantCulture),
                        F(withPsf.PixelScale, 4),
                        stars.Count.ToString(CultureInfo.InvariantCulture),
                        fitted.Count.ToString(CultureInfo.InvariantCulture),
                        result.Metrics.PSFFitFailed.ToString(CultureInfo.InvariantCulture),
                        result.Metrics.Saturated.ToString(CultureInfo.InvariantCulture),
                        result.Metrics.HotpixelCount.ToString(CultureInfo.InvariantCulture),
                        F(hfrMedian, 6), F(hfrMad, 6),
                        F(fwhmMedian, 6), F(fwhmMad, 6),
                        F(Percentile(fwhmPx, 0.05), 6), F(Percentile(fwhmPx, 0.95), 6),
                        F(fwhmPx.Count > 0 ? fwhmPx.Max() : double.NaN, 6),
                        F(Median(fitted.Select(s => s.PSF.FWHMArcsecs).ToList()), 6),
                        F(Median(fitted.Select(s => s.PSF.Eccentricity).ToList()), 6),
                        F(Median(fitted.Select(s => s.PSF.RSquared).ToList()), 6),
                        F(scatter, 6),
                        F(msNoPsf, 1), F(msWithPsf, 1), F(msWithPsf - msNoPsf, 1)));

                    foreach (var s in stars) {
                        var sat = (s.Background + s.PeakBrightness) >= saturationThreshold;
                        starCsv.AppendLine(string.Join(",",
                            Csv(entry.runId),
                            F(s.Center.X, 3), F(s.Center.Y, 3),
                            F(s.HFR, 6), F(s.Background, 6), F(s.PeakBrightness, 6),
                            sat ? "1" : "0",
                            s.PSF != null ? F(s.PSF.FWHMPixels, 6) : "",
                            s.PSF != null ? F(s.PSF.FWHMArcsecs, 6) : "",
                            s.PSF != null ? F(s.PSF.Eccentricity, 6) : "",
                            s.PSF != null ? F(s.PSF.RSquared, 6) : "",
                            s.PSF != null ? F(s.PSF.Peak, 6) : ""));
                    }

                    Console.WriteLine($"  {entry.runId}: detected={stars.Count} psf={fitted.Count} failed={result.Metrics.PSFFitFailed} " +
                                      $"sat={result.Metrics.Saturated} fwhm={fwhmMedian:F4}+/-{fwhmMad:F4} scatter={scatter:F4} " +
                                      $"psfMs={msWithPsf - msNoPsf:F0} (baseline {msNoPsf:F0}, detected-baseline {baseline.DetectedStars.Count})");
                } catch (Exception ex) {
                    Console.Error.WriteLine($"  {entry.runId}: FAILED {ex.Message}");
                }
            }

            var runPath = Path.Combine(outDir, $"psf_bank_{label}.csv");
            var starPath = Path.Combine(outDir, $"psf_bank_{label}_stars.csv");
            File.WriteAllText(runPath, runCsv.ToString());
            File.WriteAllText(starPath, starCsv.ToString());
            Console.WriteLine($"Wrote {runPath}");
            Console.WriteLine($"Wrote {starPath}");
        }

        // ── params ──────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The SHIPPED defaults, plus the frame's own pixel scale (so FWHM in arcsec is physical for that rig)
        /// and the full-frame region. Deliberately NOT the user's saved options: the point is what a default
        /// install produces.
        /// </summary>
        private static StarDetectorParams DefaultParams(DetectionSource source, HarnessSettingsStore.Resolved harnessSettings, bool modelPsf) {
            var p = HocusFocusStarDetection.BuildDefaultStarDetectorParams();
            p.Region = StarDetectionRegion.Full;
            p.SaveIntermediateFilesPath = string.Empty;
            p.SuppressInfoLogging = true;
            p.ModelPSF = modelPsf;
            p.PixelScale = HarnessSettingsStore.PixelScaleForFrame(source.MetaData, harnessSettings, out _);
            if (psfResolutionOverride.HasValue) {
                p.PSFResolution = psfResolutionOverride.Value;
            }
            if (psfAbsDevOverride.HasValue) {
                p.UsePSFAbsoluteDeviation = psfAbsDevOverride.Value;
            }
            return p;
        }

        // ── statistics ──────────────────────────────────────────────────────────────────────────────────

        private static double Median(List<double> values) {
            if (values == null || values.Count == 0) {
                return double.NaN;
            }
            var sorted = values.OrderBy(v => v).ToList();
            return sorted[sorted.Count / 2];
        }

        private static (double median, double mad) MedianMad(List<double> values) {
            if (values == null || values.Count == 0) {
                return (double.NaN, double.NaN);
            }
            var median = Median(values);
            var mad = Median(values.Select(v => Math.Abs(v - median)).ToList()) * 1.4826;
            return (median, mad);
        }

        private static double Percentile(List<double> values, double q) {
            if (values == null || values.Count == 0) {
                return double.NaN;
            }
            var sorted = values.OrderBy(v => v).ToList();
            var idx = (int)Math.Round(q * (sorted.Count - 1), MidpointRounding.AwayFromZero);
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1, idx))];
        }

        /// <summary>
        /// Residual standard deviation of per-star FWHM about a least-squares quadratic surface in (x, y) — the
        /// measurement-noise term with the field's optical structure (defocus gradient, curvature) taken out.
        /// This is the <c>scatterSD</c> of docs/saturated-star-fwhm-investigation-results.md Part 3.
        /// </summary>
        private static double ScatterAboutQuadraticSurface(List<Star> fitted) {
            const int Terms = 6;
            if (fitted == null || fitted.Count <= Terms) {
                return double.NaN;
            }
            // Normalize coordinates so the normal equations stay conditioned.
            var maxX = fitted.Max(s => s.Center.X);
            var maxY = fitted.Max(s => s.Center.Y);
            if (maxX <= 0 || maxY <= 0) {
                return double.NaN;
            }

            var ata = new double[Terms, Terms];
            var atb = new double[Terms];
            foreach (var s in fitted) {
                var x = s.Center.X / maxX;
                var y = s.Center.Y / maxY;
                var basis = new[] { 1.0, x, y, x * x, x * y, y * y };
                var v = s.PSF.FWHMPixels;
                for (int i = 0; i < Terms; ++i) {
                    atb[i] += basis[i] * v;
                    for (int j = 0; j < Terms; ++j) {
                        ata[i, j] += basis[i] * basis[j];
                    }
                }
            }

            var coeff = SolveSymmetric(ata, atb, Terms);
            if (coeff == null) {
                return double.NaN;
            }

            double sum = 0;
            foreach (var s in fitted) {
                var x = s.Center.X / maxX;
                var y = s.Center.Y / maxY;
                var model = coeff[0] + coeff[1] * x + coeff[2] * y + coeff[3] * x * x + coeff[4] * x * y + coeff[5] * y * y;
                var r = s.PSF.FWHMPixels - model;
                sum += r * r;
            }
            return Math.Sqrt(sum / (fitted.Count - Terms));
        }

        /// <summary>Gaussian elimination with partial pivoting; null when the system is singular.</summary>
        private static double[] SolveSymmetric(double[,] a, double[] b, int n) {
            var m = new double[n, n + 1];
            for (int i = 0; i < n; ++i) {
                for (int j = 0; j < n; ++j) {
                    m[i, j] = a[i, j];
                }
                m[i, n] = b[i];
            }
            for (int col = 0; col < n; ++col) {
                int pivot = col;
                for (int r = col + 1; r < n; ++r) {
                    if (Math.Abs(m[r, col]) > Math.Abs(m[pivot, col])) {
                        pivot = r;
                    }
                }
                if (Math.Abs(m[pivot, col]) < 1e-14) {
                    return null;
                }
                if (pivot != col) {
                    for (int j = col; j <= n; ++j) {
                        (m[col, j], m[pivot, j]) = (m[pivot, j], m[col, j]);
                    }
                }
                for (int r = 0; r < n; ++r) {
                    if (r == col) {
                        continue;
                    }
                    var factor = m[r, col] / m[col, col];
                    for (int j = col; j <= n; ++j) {
                        m[r, j] -= factor * m[col, j];
                    }
                }
            }
            var x = new double[n];
            for (int i = 0; i < n; ++i) {
                x[i] = m[i, n] / m[i, i];
            }
            return x;
        }

        private static string F(double v, int decimals) =>
            double.IsNaN(v) || double.IsInfinity(v) ? "" : v.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        private static string Csv(string s) => s != null && (s.Contains(',') || s.Contains('"')) ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
