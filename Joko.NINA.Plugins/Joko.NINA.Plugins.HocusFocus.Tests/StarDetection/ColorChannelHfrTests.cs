using NINA.Core.Enum;
using NINA.Image.ImageAnalysis;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.Joko.Plugins.HocusFocus.Interfaces;
using NINA.Joko.Plugins.HocusFocus.StarDetection;
using NINA.Joko.Plugins.HocusFocus.Utility;
using NINA.Profile.Interfaces;
using NSubstitute;
using NUnit.Framework;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NINA.Joko.Plugins.HocusFocus.Tests.StarDetection;

[TestFixture]
public class ColorChannelHfrTests {
    private const int Width = 256;
    private const int Height = 192;
    private const double Background = 0.05;
    private const double Amplitude = 0.4;

    // A 4×3 grid of stars, well inside the frame and well apart.
    private static readonly (double X, double Y)[] StarCenters = Enumerable.Range(0, 12)
        .Select(i => (X: 40.0 + 56.0 * (i % 4), Y: 40.0 + 56.0 * (i / 4)))
        .ToArray();

    private const int BoxHalfSize = 12;

    private static StarDetectorParams Params() => new StarDetectorParams() {
        HotpixelFiltering = false,
        ModelPSF = false
    };

    private static double Gaussian(double x, double y, double cx, double cy, double sigma) {
        var dx = x - cx;
        var dy = y - cy;
        return Amplitude * Math.Exp(-(dx * dx + dy * dy) / (2.0 * sigma * sigma));
    }

    private static double[,] BuildField(double sigma, double noise, int seed) {
        var rng = new Random(seed);
        var field = new double[Height, Width];
        for (var y = 0; y < Height; ++y) {
            for (var x = 0; x < Width; ++x) {
                var v = Background;
                foreach (var (cx, cy) in StarCenters) {
                    v += Gaussian(x, y, cx, cy, sigma);
                }
                if (noise > 0) {
                    // Box-Muller
                    var u1 = 1.0 - rng.NextDouble();
                    var u2 = rng.NextDouble();
                    v += noise * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                }
                field[y, x] = Math.Clamp(v, 0.0, 1.0);
            }
        }
        return field;
    }

    private static Mat ToMat(double[,] field) {
        var mat = new Mat(new OpenCvSharp.Size(Width, Height), MatType.CV_32F);
        for (var y = 0; y < Height; ++y) {
            for (var x = 0; x < Width; ++x) {
                mat.Set(y, x, (float)field[y, x]);
            }
        }
        return mat;
    }

    private static List<Star> BuildStars() => StarCenters.Select(c => new Star() {
        Center = new Point2d(c.X, c.Y),
        StarBoundingBox = new Rect((int)c.X - BoxHalfSize, (int)c.Y - BoxHalfSize, 2 * BoxHalfSize + 1, 2 * BoxHalfSize + 1),
        Background = Background,
        PeakBrightness = Amplitude
    }).ToList();

    [Test]
    public void MeasureStar_OnAFlatNoiselessBackground_MatchesTheLuminanceMeasureStarExactly() {
        // Refactor parity: with a flat background the annulus median IS the background, so the channel path and the
        // detector's own MeasureStar (fed a flat plane at that value) must run the identical aperture sum.
        using var mat = ToMat(BuildField(sigma: 2.0, noise: 0.0, seed: 1));
        var p = Params();
        const double noiseSigma = 0.002;
        var detector = new StarDetector(new AlglibAPI());

        foreach (var star in BuildStars()) {
            star.BackgroundPlane = LocalBackgroundPlane.Flat(star.Center.X, star.Center.Y, ColorChannelHfr.AnnulusMedian(mat, star.StarBoundingBox, p.BackgroundBoxExpansion));
            Assert.That(detector.MeasureStar(mat, star, p, noiseSigma), Is.True);
            Assert.That(ColorChannelHfr.MeasureStar(mat, star, p, noiseSigma), Is.EqualTo(star.HFR), $"star at {star.Center}");
        }
    }

    [Test]
    public void AnnulusMedian_OnAFlatField_IsTheBackground() {
        using var mat = ToMat(BuildField(sigma: 2.0, noise: 0.0, seed: 1));
        var star = BuildStars()[0];
        Assert.That(ColorChannelHfr.AnnulusMedian(mat, star.StarBoundingBox, 3), Is.EqualTo(Background).Within(1e-6));
    }

    [Test]
    public void Measure_HfrIncreasesWithStarSigma() {
        var p = Params();
        var stars = BuildStars();
        var measures = new[] { 1.5, 2.0, 3.0 }.Select((sigma, i) => {
            using var mat = ToMat(BuildField(sigma, noise: 0.003, seed: 10 + i));
            return ColorChannelHfr.Measure(mat, stars, p);
        }).ToArray();

        Assert.Multiple(() => {
            Assert.That(measures.All(m => m.Measure > 0), Is.True, "every channel must yield a curve point");
            Assert.That(measures[0].Measure, Is.LessThan(measures[1].Measure));
            Assert.That(measures[1].Measure, Is.LessThan(measures[2].Measure));
        });
    }

    [Test]
    public void Measure_FewerThanTwoStars_IsARejectedPoint() {
        using var mat = ToMat(BuildField(sigma: 2.0, noise: 0.0, seed: 1));
        var result = ColorChannelHfr.Measure(mat, BuildStars().Take(1).ToList(), Params());
        Assert.Multiple(() => {
            Assert.That(result.Measure, Is.EqualTo(0.0));
            Assert.That(result.Stdev, Is.EqualTo(0.0));
        });
    }

    [Test]
    public void Aggregate_FollowsTheLuminanceConvention() {
        var hfrs = new[] { 2.0, 3.0, 4.0, 10.0 };
        var median = ColorChannelHfr.Aggregate(hfrs, MeasurementAverageEnum.Median);
        var (expectedMedian, expectedMad) = hfrs.MedianMAD();
        var mean = ColorChannelHfr.Aggregate(hfrs, MeasurementAverageEnum.MeanOutliers);
        Assert.Multiple(() => {
            Assert.That(median.Measure, Is.EqualTo(expectedMedian));
            Assert.That(median.Stdev, Is.EqualTo(expectedMad));
            Assert.That(mean.Measure, Is.EqualTo(4.75));
            Assert.That(mean.Stdev, Is.EqualTo(Math.Sqrt(hfrs.Sum(h => (h - 4.75) * (h - 4.75)) / 3.0)).Within(1e-12));
        });
    }

    [Test]
    public void ExtractChannelMats_ThenMeasure_RecoversPerChannelSharpnessFromAnRggbMosaic() {
        // Each CFA site samples its own channel's field: R sharpest, B softest. After the debayer, the channel
        // planes must line up with the luminance star geometry (full resolution, same coordinates) so the
        // per-channel HFRs come back in that order.
        var red = BuildField(sigma: 1.6, noise: 0.0, seed: 1);
        var green = BuildField(sigma: 2.2, noise: 0.0, seed: 2);
        var blue = BuildField(sigma: 3.2, noise: 0.0, seed: 3);
        var mosaic = new ushort[Width * Height];
        for (var y = 0; y < Height; ++y) {
            for (var x = 0; x < Width; ++x) {
                var isEvenRow = (y % 2) == 0;
                var isEvenCol = (x % 2) == 0;
                var v = isEvenRow ? (isEvenCol ? red[y, x] : green[y, x]) : (isEvenCol ? green[y, x] : blue[y, x]);
                mosaic[y * Width + x] = (ushort)Math.Round(v * 65535.0);
            }
        }
        var starDetection = Substitute.For<IStarDetection>();
        starDetection.CreateAnalysis().Returns(_ => new StarDetectionAnalysis());
        var imageData = new BaseImageData(mosaic, Width, Height, 16, isBayered: true, new ImageMetaData(),
            Substitute.For<IProfileService>(), starDetection, Substitute.For<IStarAnnotator>());

        var p = Params();
        using var channels = ColorChannelHfr.ExtractChannelMats(imageData, SensorType.RGGB, p);
        var measures = ColorChannelHfr.MeasureAll(channels, BuildStars(), p);

        Assert.Multiple(() => {
            Assert.That(channels.Red.Width, Is.EqualTo(Width));
            Assert.That(channels.Red.Height, Is.EqualTo(Height));
            Assert.That(measures, Has.Length.EqualTo(3));
            Assert.That(measures[0].Measure, Is.GreaterThan(0));
            Assert.That(measures[0].Measure, Is.LessThan(measures[1].Measure), "R sharper than G");
            Assert.That(measures[1].Measure, Is.LessThan(measures[2].Measure), "G sharper than B");
        });
    }
}
