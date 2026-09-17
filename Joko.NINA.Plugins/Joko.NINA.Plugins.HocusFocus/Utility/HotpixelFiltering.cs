#region "copyright"

/*
    Copyright © 2021 - 2026 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Accord.Imaging;
using NINA.Core.Enum;
using NINA.Core.Locale;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Joko.Plugins.HocusFocus.Utility {

    public class RawImageData {

        public RawImageData(ushort[] imageData, int width, int height) {
            if (imageData.Length != (width * height)) {
                throw new ArgumentException($"Image Data array length {imageData.Length} does not equal expected length for {width} x {height}");
            }

            this.Data = imageData;
            this.Width = width;
            this.Height = height;
        }

        public ushort[] Data { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public ushort GetPixel(int x, int y) {
            return Data[y * Width + x];
        }

        public void SetPixel(int x, int y, ushort value) {
            Data[y * Width + x] = value;
        }
    }

    public static class HotpixelFiltering {

        public static void HotpixelFilter(Mat m) {
            Cv2.MedianBlur(m, m, 3);
        }

        /// <summary>
        /// Block size, in pixels, of the coarse local-background grid the isolation test measures amplitudes
        /// against. Matches the grid the golden reference detector uses (tools/golden/snr_ref.py:coarse_bg) and
        /// the default adaptive-binarization block size.
        /// </summary>
        public const int IsolatedHotpixelBackgroundBlockSize = 128;

        /// <summary>How far above the local background, in local sigmas, a pixel must sit to be a candidate.</summary>
        public const float IsolatedHotpixelSigmaMultiplier = 5.0f;

        /// <summary>
        /// Isolation ratio: the brightest of the eight neighbours must be under 1/ratio of the candidate's own
        /// amplitude above the local background. A hot pixel's neighbours sit at background; a star core's do not.
        /// </summary>
        public const float IsolatedHotpixelNeighborRatio = 3.0f;

        /// <summary>
        /// Floor applied to the local-sigma grid, so a block with a zero MAD cannot divide the significance test
        /// by nothing. It is deliberately far below any real frame's noise (5 x this is a third of one 16-bit ADU),
        /// which means it does NOT act as a minimum amplitude: in a block with no measurable noise at all — an
        /// all-zero masked or overscan region — a single pixel one ADU above its neighbours does qualify. That is
        /// the right answer for such a pixel, and nothing is measured in those regions anyway.
        /// </summary>
        public const float IsolatedHotpixelSigmaFloor = 1e-6f;

        /// <summary>
        /// Repairs ISOLATED hot pixels in place, with the default thresholds, and returns how many were rewritten.
        /// </summary>
        public static long RepairIsolatedHotpixels(Mat m) {
            return RepairIsolatedHotpixels(m, IsolatedHotpixelBackgroundBlockSize, IsolatedHotpixelSigmaMultiplier, IsolatedHotpixelNeighborRatio);
        }

        /// <summary>
        /// Repairs ISOLATED hot pixels in place and returns how many pixels were rewritten. A pixel qualifies when
        /// BOTH hold, against a coarse local background grid (block median and 1.4826·MAD sigma, bilinearly
        /// interpolated between block centres):
        ///
        /// <list type="bullet">
        ///   <item>its amplitude above the local background exceeds <paramref name="sigmaMultiplier"/> local sigmas, and</item>
        ///   <item>the brightest of its eight neighbours sits below 1/<paramref name="neighborRatio"/> of that amplitude.</item>
        /// </list>
        ///
        /// <para>Qualifying pixels are replaced by their 3x3 median (BORDER_REPLICATE), the same value
        /// <see cref="HotpixelFilter"/> would have written. The point of the isolation test is what it does NOT
        /// rewrite: a star core deviates from its own 3x3 median exactly as a hot pixel does — which is why
        /// <see cref="HotpixelFilterWithThresholding"/> flattens star cores — but a star core's neighbours carry
        /// most of its amplitude, so the second test spares it. Only a genuinely single-pixel spike is repaired.</para>
        ///
        /// <para>This is the MEASUREMENT-path filter: applying the unconditional median there drops a bright
        /// star's peak ~20%, widens its half-max width, and biases every fitted FWHM ~6% high. The structure
        /// (detection) path keeps the median, which it needs for candidate formation.
        /// See docs/saturated-star-fwhm-investigation-results.md Part 4.</para>
        ///
        /// <para>Deterministic and order-independent: every test reads an unmodified copy of the input, so the
        /// parallel row partitioning cannot affect the result.</para>
        /// </summary>
        public static long RepairIsolatedHotpixels(Mat m, int blockSize, float sigmaMultiplier, float neighborRatio) {
            if (m == null) {
                throw new ArgumentNullException(nameof(m));
            }
            if (m.Type() != MatType.CV_32F) {
                throw new ArgumentException("Only CV_32F supported", nameof(m));
            }
            if (blockSize <= 0) {
                throw new ArgumentException("blockSize must be positive", nameof(blockSize));
            }

            int width = m.Cols;
            int height = m.Rows;
            if (width < 3 || height < 3) {
                return 0L;
            }

            var grid = CvImageUtility.ComputeLocalBackgroundGrid(m, blockSize, IsolatedHotpixelSigmaFloor);
            int gridCols = grid.GridCols;
            int gridRows = grid.GridRows;
            var gridMedian = grid.Median;
            var gridSigma = grid.Sigma;

            // Per-column interpolation weights, hoisted out of the row loop (they do not depend on y).
            var colLow = new int[width];
            var colHigh = new int[width];
            var colFrac = new float[width];
            for (int x = 0; x < width; ++x) {
                ComputeGridWeights(x, blockSize, gridCols, out colLow[x], out colHigh[x], out colFrac[x]);
            }

            long repaired = 0L;
            using (var source = m.Clone()) {
                IntPtr srcPtr = source.Data;
                IntPtr dstPtr = m.Data;
                long srcStep = source.Step();
                long dstStep = m.Step();

                Parallel.For(0, height, () => 0L, (y, _, localCount) => {
                    ComputeGridWeights(y, blockSize, gridRows, out var rowLow, out var rowHigh, out var rowFrac);
                    int rowLowBase = rowLow * gridCols;
                    int rowHighBase = rowHigh * gridCols;
                    int yUp = y > 0 ? y - 1 : 0;
                    int yDown = y < height - 1 ? y + 1 : height - 1;

                    unsafe {
                        var srcBase = (byte*)srcPtr;
                        var rowMid = (float*)(srcBase + (long)y * srcStep);
                        var rowAbove = (float*)(srcBase + (long)yUp * srcStep);
                        var rowBelow = (float*)(srcBase + (long)yDown * srcStep);
                        var dstRow = (float*)((byte*)dstPtr + (long)y * dstStep);

                        for (int x = 0; x < width; ++x) {
                            float v = rowMid[x];
                            int cLow = colLow[x];
                            int cHigh = colHigh[x];
                            float cFrac = colFrac[x];

                            float bg = Bilinear(gridMedian, rowLowBase, rowHighBase, cLow, cHigh, cFrac, rowFrac);
                            float amplitude = v - bg;
                            if (amplitude <= 0f) {
                                continue;
                            }
                            float sigma = Bilinear(gridSigma, rowLowBase, rowHighBase, cLow, cHigh, cFrac, rowFrac);
                            if (amplitude <= sigmaMultiplier * sigma) {
                                continue;
                            }

                            int xLeft = x > 0 ? x - 1 : 0;
                            int xRight = x < width - 1 ? x + 1 : width - 1;

                            float n0 = rowAbove[xLeft], n1 = rowAbove[x], n2 = rowAbove[xRight];
                            float n3 = rowMid[xLeft], n4 = rowMid[xRight];
                            float n5 = rowBelow[xLeft], n6 = rowBelow[x], n7 = rowBelow[xRight];

                            // Border pixels have fewer than eight neighbours, and BORDER_REPLICATE maps some of
                            // the missing ones back onto the pixel itself. Those must not count as "neighbours" —
                            // a border hot pixel would then be its own brightest neighbour and never qualify.
                            var maxNeighbor = MaxNeighbor(
                                n0, n1, n2, n3, n4, n5, n6, n7,
                                hasLeft: xLeft != x, hasRight: xRight != x, hasAbove: yUp != y, hasBelow: yDown != y);

                            // The brightest neighbour must be under amplitude/ratio above the background. Written
                            // as a multiply so there is no division and a negative excess trivially passes.
                            if ((maxNeighbor - bg) * neighborRatio >= amplitude) {
                                continue;
                            }

                            var repairValue = Median9(n0, n1, n2, n3, v, n4, n5, n6, n7);
                            if (repairValue >= v) {
                                // Nothing to pull down. Not reachable for a genuine isolated spike (its own value
                                // is the window maximum), but it keeps the count honest rather than tallying a
                                // no-op write.
                                continue;
                            }

                            dstRow[x] = repairValue;
                            ++localCount;
                        }
                    }
                    return localCount;
                }, localCount => System.Threading.Interlocked.Add(ref repaired, localCount));
            }

            return repaired;
        }

        /// <summary>
        /// Maps a pixel coordinate to the two neighbouring grid-block indices and the interpolation fraction
        /// between their block centres, clamped at the edges (block centre of block i is at (i + 0.5)·blockSize).
        /// </summary>
        private static void ComputeGridWeights(int pixel, int blockSize, int gridExtent, out int low, out int high, out float frac) {
            float g = (pixel + 0.5f) / blockSize - 0.5f;
            if (g <= 0f) {
                low = 0;
                high = 0;
                frac = 0f;
                return;
            }
            int floor = (int)g;
            if (floor >= gridExtent - 1) {
                low = gridExtent - 1;
                high = gridExtent - 1;
                frac = 0f;
                return;
            }
            low = floor;
            high = floor + 1;
            frac = g - floor;
        }

        private static float Bilinear(float[] gridValues, int rowLowBase, int rowHighBase, int colLow, int colHigh, float colFrac, float rowFrac) {
            float topLeft = gridValues[rowLowBase + colLow];
            float topRight = gridValues[rowLowBase + colHigh];
            float bottomLeft = gridValues[rowHighBase + colLow];
            float bottomRight = gridValues[rowHighBase + colHigh];
            float top = topLeft + (topRight - topLeft) * colFrac;
            float bottom = bottomLeft + (bottomRight - bottomLeft) * colFrac;
            return top + (bottom - top) * rowFrac;
        }

        /// <summary>
        /// Brightest of the eight neighbours, skipping the ones that do not exist because the pixel is on an
        /// image border (BORDER_REPLICATE would otherwise hand back the pixel's own value). Returns negative
        /// infinity only for an image with no neighbours at all, which the caller has already ruled out.
        /// </summary>
        private static float MaxNeighbor(
            float upLeft, float up, float upRight,
            float left, float right,
            float downLeft, float down, float downRight,
            bool hasLeft, bool hasRight, bool hasAbove, bool hasBelow) {
            var max = float.NegativeInfinity;
            if (hasAbove) {
                if (hasLeft && upLeft > max) max = upLeft;
                if (up > max) max = up;
                if (hasRight && upRight > max) max = upRight;
            }
            if (hasLeft && left > max) max = left;
            if (hasRight && right > max) max = right;
            if (hasBelow) {
                if (hasLeft && downLeft > max) max = downLeft;
                if (down > max) max = down;
                if (hasRight && downRight > max) max = downRight;
            }
            return max;
        }

        /// <summary>Median of nine floats via a sorting network (19 compare-exchanges).</summary>
        private static float Median9(float a0, float a1, float a2, float a3, float a4, float a5, float a6, float a7, float a8) {
            Swap(ref a1, ref a2); Swap(ref a4, ref a5); Swap(ref a7, ref a8);
            Swap(ref a0, ref a1); Swap(ref a3, ref a4); Swap(ref a6, ref a7);
            Swap(ref a1, ref a2); Swap(ref a4, ref a5); Swap(ref a7, ref a8);
            Swap(ref a0, ref a3); Swap(ref a5, ref a8); Swap(ref a4, ref a7);
            Swap(ref a3, ref a6); Swap(ref a1, ref a4); Swap(ref a2, ref a5);
            Swap(ref a4, ref a7); Swap(ref a4, ref a2); Swap(ref a6, ref a4);
            Swap(ref a4, ref a2);
            return a4;
        }

        private static void Swap(ref float a, ref float b) {
            if (b < a) {
                var t = a;
                a = b;
                b = t;
            }
        }

        public static long HotpixelFilterWithThresholding(Mat m, double threshold) {
            using (var blurred = new Mat())
            using (var diff = new Mat()) {
                long numHotpixels;
                Cv2.MedianBlur(m, blurred, 3);
                Cv2.Absdiff(m, blurred, diff);
                using (var mask = new Mat()) {
                    Cv2.Threshold(diff, mask, threshold, 1.0, ThresholdTypes.Binary);
                    numHotpixels = Cv2.CountNonZero(mask);
                    mask.ConvertTo(mask, MatType.CV_8UC1, 255);
                    Cv2.CopyTo(blurred, m, mask);
                    return numHotpixels;
                }
            }
        }

        public static long CFAHotpixelFilter(RawImageData imageData, SensorType bayerPattern, ushort threshold) {
            long numHotpixels = 0;
            int firstRowG, secondRowG;
            if (bayerPattern == SensorType.BGGR) {
                firstRowG = 1;
                secondRowG = 0;
            } else if (bayerPattern == SensorType.BGRG) {
                firstRowG = 1;
                secondRowG = 1;
            } else if (bayerPattern == SensorType.GBGR) {
                firstRowG = 0;
                secondRowG = 0;
            } else if (bayerPattern == SensorType.GBRG) {
                firstRowG = 0;
                secondRowG = 1;
            } else if (bayerPattern == SensorType.GRBG) {
                firstRowG = 0;
                secondRowG = 1;
            } else if (bayerPattern == SensorType.GRGB) {
                firstRowG = 0;
                secondRowG = 0;
            } else if (bayerPattern == SensorType.RGBG) {
                firstRowG = 1;
                secondRowG = 1;
            } else if (bayerPattern == SensorType.RGGB) {
                firstRowG = 1;
                secondRowG = 0;
            } else {
                throw new InvalidImagePropertiesException(string.Format(Loc.Instance["LblUnsupportedCfaPattern"], bayerPattern));
            }

            int intThreshold = threshold;
            for (int y = 0; y < imageData.Height; ++y) {
                bool useAdjacent;
                if (y % 2 == 0) {
                    useAdjacent = firstRowG == 0;
                } else {
                    useAdjacent = secondRowG == 0;
                }

                for (int x = 0; x < imageData.Width; ++x) {
                    if (ApplyHotpixelFilter(imageData, x: x, y: y, useAdjacent: useAdjacent, threshold: intThreshold)) {
                        ++numHotpixels;
                    }
                    useAdjacent = !useAdjacent;
                }
            }
            return numHotpixels;
        }

        private static ushort CalculateMedianPixel(RawImageData imageData, int x, int y, bool useAdjacent, ushort centerPixel) {
            var distance = useAdjacent ? 1 : 2;
            if (x < distance) {
                if (y < distance) {
                    // In the top-left corner
                    return Math.Min(centerPixel, imageData.GetPixel(x: x + distance, y: y + distance));
                } else if (y >= (imageData.Height - distance)) {
                    // In the bottom-left corner
                    return Math.Min(centerPixel, imageData.GetPixel(x: x + distance, y: y - distance));
                } else {
                    // Along left-edge
                    return Median_3(
                        centerPixel,
                        imageData.GetPixel(x: x + distance, y: y - distance),
                        imageData.GetPixel(x: x + distance, y: y + distance));
                }
            } else if (x >= (imageData.Width - distance)) {
                if (y < distance) {
                    // In the top-right corner
                    return Math.Min(centerPixel, imageData.GetPixel(x: x - distance, y: y + distance));
                } else if (y >= (imageData.Height - distance)) {
                    // In the bottom-right corner
                    return Math.Min(centerPixel, imageData.GetPixel(x: x - distance, y: y - distance));
                } else {
                    // Along right-edge
                    return Median_3(
                        centerPixel,
                        imageData.GetPixel(x: x - distance, y: y - distance),
                        imageData.GetPixel(x: x - distance, y: y + distance));
                }
            } else if (y < distance) {
                // Along top-edge
                return Median_3(
                    centerPixel,
                    imageData.GetPixel(x: x - distance, y: y + distance),
                    imageData.GetPixel(x: x + distance, y: y + distance));
            } else if (y >= (imageData.Height - distance)) {
                // Along bottom-edge
                return Median_3(
                    centerPixel,
                    imageData.GetPixel(x: x - distance, y: y - distance),
                    imageData.GetPixel(x: x + distance, y: y - distance));
            }

            // Edge and corner cases all accounted for. Now we can do a regular 5-way median
            return Median_5(
                centerPixel,
                imageData.GetPixel(x: x - distance, y: y - distance),
                imageData.GetPixel(x: x + distance, y: y - distance),
                imageData.GetPixel(x: x - distance, y: y + distance),
                imageData.GetPixel(x: x + distance, y: y + distance));
        }

        private static bool ApplyHotpixelFilter(RawImageData imageData, int x, int y, bool useAdjacent, int threshold) {
            var centerPixel = imageData.GetPixel(x: x, y: y);
            var medianPixelValue = CalculateMedianPixel(imageData, x: x, y: y, useAdjacent: useAdjacent, centerPixel: centerPixel);
            if (Math.Abs(medianPixelValue - centerPixel) >= threshold) {
                imageData.SetPixel(x: x, y: y, medianPixelValue);
                return true;
            }
            return false;
        }

        private static ushort Median_3(ushort a, ushort b, ushort c) {
            return Math.Max(Math.Min(a, b), Math.Min(c, Math.Max(a, b)));
        }

        private static ushort Median_5(ushort a, ushort b, ushort c, ushort d, ushort e) {
            ushort f = Math.Max(Math.Min(a, b), Math.Min(c, d)); // discards lowest from first 4
            ushort g = Math.Min(Math.Max(a, b), Math.Max(c, d)); // discards biggest from first 4
            return Median_3(e, f, g);
        }
    }
}