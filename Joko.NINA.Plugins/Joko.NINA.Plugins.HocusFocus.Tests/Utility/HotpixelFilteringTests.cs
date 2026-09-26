using NINA.Core.Enum;
using NINA.Joko.Plugins.HocusFocus.Utility;
using NUnit.Framework;
using OpenCvSharp;
using System.Linq;
using System.Runtime.InteropServices;
using Size = OpenCvSharp.Size;

namespace NINA.Joko.Plugins.HocusFocus.Tests.Utility {

    [TestFixture]
    public class HotpixelFilteringTests {

        [Test]
        public void RawImageData_LengthMismatch_Throws() {
            Assert.Throws<System.ArgumentException>(() =>
                new RawImageData(new ushort[10], width: 4, height: 4));
        }

        [Test]
        public void RawImageData_GetSetPixel_RoundTrip() {
            var data = new RawImageData(new ushort[16], 4, 4);
            data.SetPixel(2, 1, 1234);
            Assert.That(data.GetPixel(2, 1), Is.EqualTo(1234));
        }

        [Test]
        public void HotpixelFilter_ReplacesIsolatedSpike_WithMedianBlur() {
            using var mat = new Mat(new Size(5, 5), MatType.CV_16UC1, new Scalar(100));
            mat.Set(2, 2, (ushort)10000);

            HotpixelFiltering.HotpixelFilter(mat);

            // Median of a 3x3 region of mostly 100s with one spike → 100
            Assert.That((ushort)mat.At<ushort>(2, 2), Is.EqualTo(100));
        }

        [Test]
        public void HotpixelFilterWithThresholding_CountsAndFixesSpike() {
            using var mat = new Mat(new Size(5, 5), MatType.CV_16UC1, new Scalar(100));
            mat.Set(2, 2, (ushort)10000);

            var count = HotpixelFiltering.HotpixelFilterWithThresholding(mat, threshold: 1000);

            Assert.Multiple(() => {
                Assert.That(count, Is.EqualTo(1));
                Assert.That((ushort)mat.At<ushort>(2, 2), Is.EqualTo(100));
            });
        }

        [Test]
        public void HotpixelFilterWithThresholding_BelowThreshold_DoesNotReplace() {
            using var mat = new Mat(new Size(5, 5), MatType.CV_16UC1, new Scalar(100));
            mat.Set(2, 2, (ushort)200);

            var count = HotpixelFiltering.HotpixelFilterWithThresholding(mat, threshold: 1000);

            Assert.Multiple(() => {
                Assert.That(count, Is.EqualTo(0));
                Assert.That((ushort)mat.At<ushort>(2, 2), Is.EqualTo(200));
            });
        }

        [Test]
        public void CFAHotpixelFilter_DetectsSpike_RGGB() {
            // 8x8 RGGB with a single hot pixel (R) at (4,4)
            const int W = 8, H = 8;
            var arr = new ushort[W * H];
            for (var i = 0; i < arr.Length; ++i) arr[i] = 100;
            var raw = new RawImageData(arr, W, H);
            raw.SetPixel(4, 4, 5000);

            var count = HotpixelFiltering.CFAHotpixelFilter(raw, SensorType.RGGB, threshold: 1000);

            Assert.Multiple(() => {
                Assert.That(count, Is.GreaterThanOrEqualTo(1));
                Assert.That(raw.GetPixel(4, 4), Is.LessThan((ushort)5000));
            });
        }

        [Test]
        public void CFAHotpixelFilter_FlatImage_FindsNoHotpixels() {
            const int W = 16, H = 16;
            var arr = new ushort[W * H];
            for (var i = 0; i < arr.Length; ++i) arr[i] = 200;
            var raw = new RawImageData(arr, W, H);

            var count = HotpixelFiltering.CFAHotpixelFilter(raw, SensorType.RGGB, threshold: 50);

            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void CFAHotpixelFilter_UnsupportedPattern_Throws() {
            const int W = 8, H = 8;
            var arr = new ushort[W * H];
            var raw = new RawImageData(arr, W, H);

            // SensorType.Monochrome is not handled by the filter
            Assert.Throws<Accord.Imaging.InvalidImagePropertiesException>(() =>
                HotpixelFiltering.CFAHotpixelFilter(raw, SensorType.Monochrome, threshold: 100));
        }
    
        // ---- isolated-hotpixel repair (measurement path) --------------------------------------------------

        /// <summary>
        /// A noisy flat field, so the local-sigma grid has a realistic value to threshold against. Deterministic
        /// (fixed seed) so the tests below are not flaky.
        /// </summary>
        private static Mat NoisyBackground(int width, int height, float level, float sigma, int seed = 1234) {
            var rng = new System.Random(seed);
            var mat = new Mat(new Size(width, height), MatType.CV_32F);
            var data = new float[width * height];
            for (int i = 0; i < data.Length; ++i) {
                // Box-Muller, so the MAD-derived sigma of the grid matches `sigma`.
                double u1 = 1.0 - rng.NextDouble();
                double u2 = rng.NextDouble();
                data[i] = level + sigma * (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2));
            }
            Marshal.Copy(data.Select(v => v).ToArray(), 0, mat.Data, data.Length);
            return mat;
        }

        private static void PlantGaussianStar(Mat mat, int cx, int cy, float amplitude, double fwhm) {
            double sigma = fwhm / (2.0 * System.Math.Sqrt(2.0 * System.Math.Log(2.0)));
            int radius = (int)System.Math.Ceiling(3.0 * sigma) + 1;
            for (int dy = -radius; dy <= radius; ++dy) {
                for (int dx = -radius; dx <= radius; ++dx) {
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= mat.Cols || y >= mat.Rows) {
                        continue;
                    }
                    var add = amplitude * (float)System.Math.Exp(-(dx * dx + dy * dy) / (2.0 * sigma * sigma));
                    mat.Set(y, x, mat.At<float>(y, x) + add);
                }
            }
        }

        [Test]
        public void RepairIsolatedHotpixels_RepairsAnIsolatedSpike() {
            using var mat = NoisyBackground(256, 256, level: 0.10f, sigma: 0.001f);
            var neighborhoodBefore = mat.At<float>(128, 129);
            mat.Set(128, 128, 0.9f);

            var repaired = HotpixelFiltering.RepairIsolatedHotpixels(mat);

            Assert.Multiple(() => {
                Assert.That(repaired, Is.EqualTo(1L), "exactly the planted spike should be repaired");
                Assert.That(mat.At<float>(128, 128), Is.EqualTo(0.10f).Within(0.01f), "the spike should be replaced by its local median");
                Assert.That(mat.At<float>(128, 129), Is.EqualTo(neighborhoodBefore), "neighbours must be left untouched");
            });
        }

        [Test]
        public void RepairIsolatedHotpixels_LeavesAWellSampledStarCoreAlone() {
            using var mat = NoisyBackground(256, 256, level: 0.10f, sigma: 0.001f);
            PlantGaussianStar(mat, 128, 128, amplitude: 0.5f, fwhm: 4.0);
            var peakBefore = mat.At<float>(128, 128);

            var repaired = HotpixelFiltering.RepairIsolatedHotpixels(mat);

            Assert.Multiple(() => {
                Assert.That(repaired, Is.EqualTo(0L), "a real star has bright neighbours and must fail the isolation test");
                Assert.That(mat.At<float>(128, 128), Is.EqualTo(peakBefore), "the star's peak must survive intact");
            });
        }

        [Test]
        public void RepairIsolatedHotpixels_LeavesASubThresholdSpikeAlone() {
            using var mat = NoisyBackground(256, 256, level: 0.10f, sigma: 0.001f);
            // 3 sigma above background: isolated, but not significant enough to be called a hot pixel.
            mat.Set(128, 128, 0.103f);
            var before = mat.At<float>(128, 128);

            var repaired = HotpixelFiltering.RepairIsolatedHotpixels(mat);

            Assert.Multiple(() => {
                Assert.That(repaired, Is.EqualTo(0L));
                Assert.That(mat.At<float>(128, 128), Is.EqualTo(before));
            });
        }

        [Test]
        public void RepairIsolatedHotpixels_RepairsEveryPlantedSpikeIncludingBorders() {
            using var mat = NoisyBackground(256, 256, level: 0.10f, sigma: 0.001f);
            // Corners, edges and interior: BORDER_REPLICATE duplicates a corner pixel four times in its own 3x3
            // window, which still leaves the median on a background value, so a corner spike is repairable too.
            var spikes = new[] { (0, 0), (255, 0), (0, 255), (255, 255), (128, 0), (0, 128), (40, 200), (200, 40) };
            foreach (var (x, y) in spikes) {
                mat.Set(y, x, 0.9f);
            }

            var repaired = HotpixelFiltering.RepairIsolatedHotpixels(mat);

            Assert.Multiple(() => {
                Assert.That(repaired, Is.EqualTo((long)spikes.Length));
                foreach (var (x, y) in spikes) {
                    Assert.That(mat.At<float>(y, x), Is.EqualTo(0.10f).Within(0.01f), $"spike at {x},{y}");
                }
            });
        }

        [Test]
        public void RepairIsolatedHotpixels_IsUnaffectedByAStrongBackgroundGradient() {
            using var mat = NoisyBackground(512, 512, level: 0.0f, sigma: 0.001f);
            // A gradient far larger than the noise: a fixed global background would swamp the test, the
            // interpolated local grid must not.
            for (int y = 0; y < mat.Rows; ++y) {
                for (int x = 0; x < mat.Cols; ++x) {
                    mat.Set(y, x, mat.At<float>(y, x) + 0.05f + 0.4f * x / mat.Cols);
                }
            }
            mat.Set(300, 400, 0.95f);

            var repaired = HotpixelFiltering.RepairIsolatedHotpixels(mat);

            Assert.That(repaired, Is.EqualTo(1L));
        }

        [Test]
        public void RepairIsolatedHotpixels_RejectsANonFloatMat() {
            using var mat = new Mat(new Size(16, 16), MatType.CV_16UC1, new Scalar(100));
            Assert.Throws<System.ArgumentException>(() => HotpixelFiltering.RepairIsolatedHotpixels(mat));
        }

        [Test]
        public void RepairIsolatedHotpixels_IsDeterministic() {
            using var first = NoisyBackground(256, 256, level: 0.10f, sigma: 0.002f);
            using var second = first.Clone();

            var a = HotpixelFiltering.RepairIsolatedHotpixels(first);
            var b = HotpixelFiltering.RepairIsolatedHotpixels(second);

            using var diff = new Mat();
            Cv2.Absdiff(first, second, diff);
            Cv2.MinMaxLoc(diff, out _, out double maxDiff);
            Assert.Multiple(() => {
                Assert.That(b, Is.EqualTo(a));
                Assert.That(maxDiff, Is.EqualTo(0.0));
            });
        }
}
}
