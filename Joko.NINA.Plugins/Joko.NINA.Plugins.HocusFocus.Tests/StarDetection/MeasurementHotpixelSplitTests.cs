using NINA.Joko.Plugins.HocusFocus.Interfaces;
using NINA.Joko.Plugins.HocusFocus.StarDetection;
using NINA.Joko.Plugins.HocusFocus.Tests.Synthetic;
using NINA.Joko.Plugins.HocusFocus.Utility;
using NUnit.Framework;
using OpenCvSharp;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Joko.Plugins.HocusFocus.Tests.StarDetection {

    /// <summary>
    /// End-to-end pins for the measurement/structure split: the structure source keeps the 3x3 median that
    /// candidate formation needs, while the image HFR and the PSF are measured from gets the isolation repair
    /// instead (StarDetector.PrepareMeasurementAndStructureSources).
    ///
    /// <para>The point of the split is what a median does to a star it is not meant to touch: it drops the peak
    /// and widens the profile, which biases every measurement taken from that image. These tests pin the two
    /// halves of the contract — the hot pixel dies, the star's peak survives — through the real
    /// <c>Detect</c> path rather than against the filter in isolation.</para>
    /// </summary>
    [TestFixture]
    public class MeasurementHotpixelSplitTests {
        private const int Size = 384;
        private const float Background = 0.05f;
        private const double NoiseSigma = 0.002;
        private const double StarSigmaPx = 2.0;
        private const double StarPeak = 0.40;
        private const int Seed = 90210;

        // Well separated, away from the border, and off the block boundaries of the local-background grid.
        private static readonly (double x, double y)[] StarPositions = {
            (70, 70), (190, 70), (310, 70),
            (70, 190), (190, 190), (310, 190),
            (70, 310), (190, 310), (310, 310)
        };

        private static Mat BuildField(params (int x, int y, float value)[] hotPixels) {
            var mat = SyntheticStarField.CreateFlat(Size, Size, Background);
            foreach (var (x, y) in StarPositions) {
                SyntheticStarField.AddStar(mat, x, y, StarSigmaPx, StarPeak);
            }
            SyntheticDefocusedStarImage.AddGaussianNoise(mat, NoiseSigma, Seed);
            foreach (var (x, y, value) in hotPixels) {
                mat.Set(y, x, value);
            }
            return mat;
        }

        private static StarDetectorParams Params(int binning = 1) => new StarDetectorParams {
            // The option under test. It is OFF by default so an existing configuration keeps the legacy
            // single-image behaviour; StarDetectorEquivalenceTests pins that off path as bit-identical.
            MeasurementHotpixelRepair = true,
            HotpixelFiltering = true,
            HotpixelThresholdingEnabled = false,
            StarMeasurementNoiseReductionEnabled = false,
            NoiseReductionRadius = 0,
            ModelPSF = false,
            DetectionBinning = binning,
            MinimumStarBoundingBoxSize = 3
        };

        private static async Task<HocusFocusStarDetectorResult> DetectAsync(Mat field, StarDetectorParams p) {
            var detector = new StarDetector(new AlglibAPI());
            using var clone = field.Clone();
            return await detector.Detect(clone, p, null, CancellationToken.None);
        }

        [Test]
        public async Task Detect_RepairsAHotPixelAndReportsIt() {
            // Two hot pixels in empty field, well away from any star.
            using var field = BuildField((130, 130, 0.9f), (250, 250, 0.9f));

            var result = await DetectAsync(field, Params());

            Assert.That(result.Metrics.MeasurementHotpixelCount, Is.EqualTo(2),
                "both planted hot pixels must be repaired on the measurement image, and nothing else");
        }

        [Test]
        public async Task Detect_LeavesStarPeaksIntactOnTheMeasurementImage() {
            // No hot pixels at all: whatever the measurement path does must not touch the stars.
            using var field = BuildField();

            var result = await DetectAsync(field, Params());

            var stars = result.DetectedStars;
            Assert.That(stars.Count, Is.EqualTo(StarPositions.Length), "every planted star should be detected");

            var peaks = stars.Select(s => s.Background + s.PeakBrightness).ToList();
            var expected = Background + StarPeak;
            Assert.Multiple(() => {
                Assert.That(result.Metrics.MeasurementHotpixelCount, Is.EqualTo(0), "no hot pixels were planted");
                foreach (var peak in peaks) {
                    // A 3x3 median over this profile reads ~0.88 of the true peak; the isolation repair leaves it
                    // alone, so the measured peak must land within noise of what was planted.
                    Assert.That(peak, Is.EqualTo(expected).Within(0.02),
                        "the measurement image must not have been median-smoothed");
                }
            });
        }

        [Test]
        public async Task Detect_RepairsAHotPixelInsideAStarBoxWithoutEatingTheStar() {
            // 6 px from the star centre: inside its bounding box, out on the profile's skirt.
            const int starIndex = 4;
            var (sx, sy) = StarPositions[starIndex];
            using var field = BuildField(((int)sx + 6, (int)sy, 0.9f));

            var result = await DetectAsync(field, Params());

            var star = result.DetectedStars
                .OrderBy(s => (s.Center.X - sx) * (s.Center.X - sx) + (s.Center.Y - sy) * (s.Center.Y - sy))
                .First();
            Assert.Multiple(() => {
                Assert.That(result.Metrics.MeasurementHotpixelCount, Is.EqualTo(1));
                Assert.That(star.Background + star.PeakBrightness, Is.EqualTo(Background + StarPeak).Within(0.02),
                    "repairing a hot pixel in the box must not disturb the star's own peak");
            });
        }

        [Test]
        public async Task Detect_Binned_SplitsAtNativeResolution() {
            // The binning hoist has to do the split BEFORE the resample — a hot pixel averaged into its block is
            // no longer the isolated outlier either filter looks for — and then bin both images.
            using var field = BuildField((130, 130, 0.9f), (250, 250, 0.9f));

            var result = await DetectAsync(field, Params(binning: 2));

            Assert.Multiple(() => {
                Assert.That(result.Metrics.MeasurementHotpixelCount, Is.EqualTo(2),
                    "the repair must run at native resolution, before the bin");
                Assert.That(result.DetectedStars.Count, Is.GreaterThan(0), "binned detection must still find stars");
            });
        }

        [Test]
        public async Task Detect_HotpixelFilteringOff_RepairsNothing() {
            using var field = BuildField((130, 130, 0.9f));
            var p = Params();
            p.HotpixelFiltering = false;

            var result = await DetectAsync(field, p);

            Assert.That(result.Metrics.MeasurementHotpixelCount, Is.EqualTo(0),
                "with hotpixel filtering off neither path filters, exactly as before");
        }

        [Test]
        public async Task Detect_SaturatedStar_GetsNoPsfAndIsNotCountedAsAFitFailure() {
            var mat = SyntheticStarField.CreateFlat(Size, Size, Background);
            foreach (var (x, y) in StarPositions) {
                SyntheticStarField.AddStar(mat, x, y, StarSigmaPx, StarPeak);
            }
            // One star driven well past full well, then clipped the way a sensor does.
            SyntheticStarField.AddStar(mat, 190, 190, StarSigmaPx, 3.0);
            SyntheticDefocusedStarImage.AddGaussianNoise(mat, NoiseSigma, Seed);
            for (int y = 0; y < Size; ++y) {
                for (int x = 0; x < Size; ++x) {
                    if (mat.At<float>(y, x) > 1.0f) {
                        mat.Set(y, x, 1.0f);
                    }
                }
            }
            using var field = mat;

            var p = Params();
            p.ModelPSF = true;
            p.PixelScale = 1.0;
            var result = await DetectAsync(field, p);

            var saturated = result.DetectedStars
                .Where(s => (s.Background + s.PeakBrightness) >= p.SaturationThreshold)
                .ToList();
            Assert.That(saturated, Is.Not.Empty, "the clipped star must still be detected");
            Assert.Multiple(() => {
                foreach (var s in saturated) {
                    Assert.That(s.PSF, Is.Null, "a saturated star must carry no PSF model");
                }
                Assert.That(result.Metrics.Saturated, Is.EqualTo(saturated.Count));
                // Every unsaturated star here is a clean Gaussian, so nothing should fail the R^2 gate. If the
                // skipped saturated stars were being tallied as failures, this would be non-zero.
                Assert.That(result.Metrics.PSFFitFailed, Is.Zero,
                    "skipping a saturated star is not a fit failure — no fit was attempted");
            });
        }
    }
}
