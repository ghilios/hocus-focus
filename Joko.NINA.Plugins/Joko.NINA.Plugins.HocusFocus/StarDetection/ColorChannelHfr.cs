#region "copyright"

/*
    Copyright © 2021 - 2026 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Enum;
using NINA.Image.ImageAnalysis;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.Joko.Plugins.HocusFocus.Interfaces;
using NINA.Joko.Plugins.HocusFocus.Utility;
using NINA.WPF.Base.ViewModel.AutoFocus;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace NINA.Joko.Plugins.HocusFocus.StarDetection {

    /// <summary>
    /// The R, G and B planes of one debayered OSC frame as CV_32F [0,1] Mats at FULL sensor resolution — the same
    /// coordinate system and normalisation as the luminance detection input, so a luminance star's centroid and
    /// bounding box address the same light on every plane.
    /// </summary>
    internal sealed class ColorChannelMats : IDisposable {
        public ColorChannelMats(Mat red, Mat green, Mat blue) {
            Red = red;
            Green = green;
            Blue = blue;
        }

        public Mat Red { get; }
        public Mat Green { get; }
        public Mat Blue { get; }

        /// <summary>R, G, B — the order every per-channel array in the AF engine uses.</summary>
        public IReadOnlyList<Mat> All => new[] { Red, Green, Blue };

        public void Dispose() {
            Red?.Dispose();
            Green?.Dispose();
            Blue?.Dispose();
        }
    }

    /// <summary>
    /// Per-colour-channel HFR for the OSC focus curves. Display only: nothing here feeds the luminance AF.
    ///
    /// <para>Stars are NOT re-detected per channel. Each star the luminance pass accepted is re-measured on each
    /// plane with the same centroid, bounding box and aperture (<see cref="StarDetector.ComputeApertureHfr"/>), so
    /// every channel compares the same stars and a faint, noisy blue plane cannot change which stars count.</para>
    ///
    /// <para>The luminance <see cref="Star.BackgroundPlane"/> is not valid on a colour plane (the channels have
    /// different sky levels), so each star's background is the median of the same annulus the detector uses
    /// (<see cref="StarDetectorParams.BackgroundBoxExpansion"/> around the bounding box), taken on that plane, and
    /// the clip threshold uses that plane's own global noise σ.</para>
    /// </summary>
    internal static class ColorChannelHfr {

        /// <summary>
        /// Debayers <paramref name="imageData"/> into its R/G/B planes. The CFA hotpixel filter runs first on a copy
        /// of the mosaic when <paramref name="p"/> enables it, exactly as the detector's own OSC input preparation
        /// does, so a hot pixel inside an aperture cannot skew one channel.
        /// </summary>
        public static ColorChannelMats ExtractChannelMats(IImageData imageData, SensorType bayerPattern, StarDetectorParams p) {
            var props = imageData.Properties;
            var source = imageData.Data.FlatArray;
            var mosaic = new ushort[source.Length];
            Buffer.BlockCopy(source, 0, mosaic, 0, source.Length * sizeof(ushort));
            if (p.HotpixelFiltering) {
                var threshold = (ushort)(p.HotpixelThreshold * (1 << props.BitDepth));
                HotpixelFiltering.CFAHotpixelFilter(new RawImageData(mosaic, width: props.Width, height: props.Height), bayerPattern, threshold);
            }

            var bitmapSource = ImageUtility.CreateSourceFromArray(new ImageArray(mosaic), props, PixelFormats.Gray16);
            var debayered = ImageUtility.Debayer(bitmapSource, pf: System.Drawing.Imaging.PixelFormat.Format16bppGrayScale,
                saveColorChannels: true, saveLumChannel: true, bayerPattern: bayerPattern);
            var data = debayered.Data;
            Mat red = null, green = null;
            try {
                red = CvImageUtility.ToOpenCVMat(data.Red, bpp: props.BitDepth, width: props.Width, height: props.Height);
                green = CvImageUtility.ToOpenCVMat(data.Green, bpp: props.BitDepth, width: props.Width, height: props.Height);
                var blue = CvImageUtility.ToOpenCVMat(data.Blue, bpp: props.BitDepth, width: props.Width, height: props.Height);
                return new ColorChannelMats(red, green, blue);
            } catch {
                red?.Dispose();
                green?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Measures <paramref name="stars"/> on each of <paramref name="channels"/> (R, G, B).
        /// </summary>
        public static MeasureAndError[] MeasureAll(ColorChannelMats channels, IReadOnlyList<Star> stars, StarDetectorParams p) {
            return channels.All.Select(channel => Measure(channel, stars, p)).ToArray();
        }

        /// <summary>
        /// One channel's per-frame HFR. <paramref name="stars"/> is the luminance pass's final star list; the
        /// saturated-star exclusion (<see cref="HocusFocusStarDetection.StarsForHfrAggregation"/>, decided on the
        /// luminance values so every channel keeps the same subset) and the average/dispersion convention
        /// (<see cref="StarDetectorParams.MeasurementAverage"/>) match the luminance curve point. Fewer than two
        /// measurable stars yields a zero measure, which the AF treats as a rejected point, like luminance.
        /// </summary>
        public static MeasureAndError Measure(Mat channel, IReadOnlyList<Star> stars, StarDetectorParams p) {
            var hfrStars = HocusFocusStarDetection.StarsForHfrAggregation(stars, p.ExcludeSaturatedStarsFromHFR, p.SaturationThreshold);
            if (hfrStars == null || hfrStars.Count < 2) {
                return new MeasureAndError() { Measure = 0.0, Stdev = 0.0 };
            }

            var noiseSigma = CvImageUtility.KappaSigmaNoiseEstimate(channel, clippingMultipler: p.NoiseClippingMultiplier).Sigma;
            var hfrs = new List<double>(hfrStars.Count);
            foreach (var star in hfrStars) {
                var hfr = MeasureStar(channel, star, p, noiseSigma);
                if (!double.IsNaN(hfr)) {
                    hfrs.Add(hfr);
                }
            }
            return Aggregate(hfrs, p.MeasurementAverage);
        }

        /// <summary>One star's HFR on <paramref name="channel"/>, or NaN when nothing survives the clip.</summary>
        internal static double MeasureStar(Mat channel, Star star, StarDetectorParams p, double noiseSigma) {
            var background = AnnulusMedian(channel, star.StarBoundingBox, p.BackgroundBoxExpansion);
            if (double.IsNaN(background)) {
                return double.NaN;
            }
            var backgroundPlane = LocalBackgroundPlane.Flat(star.Center.X, star.Center.Y, background);
            var boxSize = Math.Max(star.StarBoundingBox.Width, star.StarBoundingBox.Height);
            var noiseThreshold = StarDetector.EffectiveClipMultiplier(p, boxSize) * noiseSigma;
            return StarDetector.ComputeApertureHfr(channel, star.Center, star.StarBoundingBox, backgroundPlane, noiseThreshold, p.AnalysisSamplingSize, p.HfrTauPolicy);
        }

        /// <summary>
        /// Median of the annulus between <paramref name="box"/> and <paramref name="box"/> grown by
        /// <paramref name="expansion"/> on every side, clipped to the image — the detector's background annulus.
        /// NaN when the annulus is empty.
        /// </summary>
        internal static double AnnulusMedian(Mat image, Rect box, int expansion) {
            var startX = Math.Max(0, box.X - expansion);
            var startY = Math.Max(0, box.Y - expansion);
            var endX = Math.Min(image.Width, box.Right + expansion);
            var endY = Math.Min(image.Height, box.Bottom + expansion);
            var values = new List<float>(Math.Max(0, (endX - startX) * (endY - startY) - box.Width * box.Height));
            unsafe {
                var data = (float*)image.DataPointer;
                var width = image.Width;
                for (var y = startY; y < endY; ++y) {
                    var insideRows = y >= box.Y && y < box.Bottom;
                    for (var x = startX; x < endX; ++x) {
                        if (insideRows && x >= box.X && x < box.Right) {
                            continue;
                        }
                        values.Add(data[y * width + x]);
                    }
                }
            }
            if (values.Count == 0) {
                return double.NaN;
            }
            values.Sort();
            var mid = values.Count / 2;
            return (values.Count % 2) == 1 ? values[mid] : (values[mid - 1] + (double)values[mid]) / 2.0;
        }

        /// <summary>The luminance curve point's convention: mean ± sample σ for MeanOutliers, otherwise median ± MAD.</summary>
        internal static MeasureAndError Aggregate(IReadOnlyList<double> hfrs, MeasurementAverageEnum measurementAverage) {
            if (hfrs.Count < 2) {
                return new MeasureAndError() { Measure = 0.0, Stdev = 0.0 };
            }
            if (measurementAverage == MeasurementAverageEnum.MeanOutliers) {
                var mean = hfrs.Average();
                var variance = hfrs.Sum(h => (h - mean) * (h - mean)) / (hfrs.Count - 1);
                return new MeasureAndError() { Measure = mean, Stdev = Math.Sqrt(variance) };
            }
            var (median, mad) = hfrs.MedianMAD();
            return new MeasureAndError() { Measure = median, Stdev = mad };
        }
    }
}
