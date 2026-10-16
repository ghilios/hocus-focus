#region "copyright"

/*
    Copyright © 2021 - 2026 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using OxyPlot;
using System;
using System.Collections.Generic;

namespace NINA.Joko.Plugins.HocusFocus.Controls {

    /// <summary>
    /// A <see cref="OxyPlot.Annotations.FunctionAnnotation"/> whose screen points are clamped to a band one plot
    /// width/height around the plot area (issue #217).
    ///
    /// OxyPlot 2.x hands annotation geometry to the render context unclipped, and <c>FunctionAnnotation</c> bounds X to
    /// the visible axis but leaves Y alone. A steep but legitimate fit (a hyperbola with b on its lower bound) then
    /// becomes a polyline ~1e10 px long, and WPF computes the stroked bounds of that dashed path on the UI thread for
    /// minutes. Clamping only moves the samples that are already far off-screen; the error is at most one sample step
    /// on the segment that leaves the plot, which is invisible.
    /// </summary>
    public class BoundedFunctionAnnotationModel : OxyPlot.Annotations.FunctionAnnotation {

        protected override IList<ScreenPoint> GetScreenPoints() {
            return ClampToBand(base.GetScreenPoints(), PlotModel.PlotArea);
        }

        /// <summary>
        /// Clamps each point to [Left − Width, Right + Width] × [Top − Height, Bottom + Height]. ±∞ maps to the band
        /// edge; NaN is left as is so the line still breaks where the function is undefined.
        /// </summary>
        internal static IList<ScreenPoint> ClampToBand(IList<ScreenPoint> pts, OxyRect plotArea) {
            var minX = plotArea.Left - plotArea.Width;
            var maxX = plotArea.Right + plotArea.Width;
            var minY = plotArea.Top - plotArea.Height;
            var maxY = plotArea.Bottom + plotArea.Height;
            var clamped = new List<ScreenPoint>(pts.Count);
            foreach (var p in pts) {
                // Math.Clamp passes NaN through and maps ±∞ to the bound.
                clamped.Add(new ScreenPoint(Math.Clamp(p.X, minX, maxX), Math.Clamp(p.Y, minY, maxY)));
            }
            return clamped;
        }
    }

    /// <summary>
    /// Drop-in replacement for <c>oxy:FunctionAnnotation</c> backed by <see cref="BoundedFunctionAnnotationModel"/>.
    /// The base <c>SynchronizeProperties</c> casts <c>InternalAnnotation</c> to the Core <c>FunctionAnnotation</c>,
    /// which the bounded model derives from, so every existing dependency property keeps working.
    /// </summary>
    public class BoundedFunctionAnnotation : OxyPlot.Wpf.FunctionAnnotation {

        public BoundedFunctionAnnotation() {
            InternalAnnotation = new BoundedFunctionAnnotationModel();
        }
    }
}
