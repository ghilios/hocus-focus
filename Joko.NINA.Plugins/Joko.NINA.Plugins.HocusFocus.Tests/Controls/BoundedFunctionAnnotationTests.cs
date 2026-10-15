using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NINA.Joko.Plugins.HocusFocus.Controls;
using NUnit.Framework;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;

namespace NINA.Joko.Plugins.HocusFocus.Tests.Controls;

[TestFixture]
public class BoundedFunctionAnnotationTests {

    // Plot area 100..500 x 50..250, so the band is X in [-300, 900] and Y in [-150, 450].
    private static readonly OxyRect PlotArea = new OxyRect(100, 50, 400, 200);

    [Test]
    public void ClampToBand_LeavesInBandPointsUntouched() {
        var pts = new List<ScreenPoint> {
            new ScreenPoint(100, 50),
            new ScreenPoint(300, 150),
            new ScreenPoint(-300, -150),
            new ScreenPoint(900, 450)
        };

        var clamped = BoundedFunctionAnnotationModel.ClampToBand(pts, PlotArea);

        Assert.That(clamped, Is.EqualTo(pts));
    }

    [Test]
    public void ClampToBand_ClampsFarPointsToTheBandEdge() {
        var pts = new List<ScreenPoint> {
            new ScreenPoint(300, 1e10),
            new ScreenPoint(300, -1e10),
            new ScreenPoint(-1e9, 150),
            new ScreenPoint(1e9, 150)
        };

        var clamped = BoundedFunctionAnnotationModel.ClampToBand(pts, PlotArea);

        Assert.That(clamped, Is.EqualTo(new[] {
            new ScreenPoint(300, 450),
            new ScreenPoint(300, -150),
            new ScreenPoint(-300, 150),
            new ScreenPoint(900, 150)
        }));
    }

    [Test]
    public void ClampToBand_PreservesNaN() {
        var pts = new List<ScreenPoint> { new ScreenPoint(300, double.NaN), new ScreenPoint(double.NaN, 150) };

        var clamped = BoundedFunctionAnnotationModel.ClampToBand(pts, PlotArea);

        Assert.Multiple(() => {
            Assert.That(clamped[0].X, Is.EqualTo(300));
            Assert.That(clamped[0].Y, Is.NaN);
            Assert.That(clamped[1].X, Is.NaN);
            Assert.That(clamped[1].Y, Is.EqualTo(150));
        });
    }

    [Test]
    public void ClampToBand_MapsInfinityToTheBandEdge() {
        var pts = new List<ScreenPoint> {
            new ScreenPoint(300, double.PositiveInfinity),
            new ScreenPoint(300, double.NegativeInfinity),
            new ScreenPoint(double.NegativeInfinity, 150),
            new ScreenPoint(double.PositiveInfinity, 150)
        };

        var clamped = BoundedFunctionAnnotationModel.ClampToBand(pts, PlotArea);

        Assert.That(clamped, Is.EqualTo(new[] {
            new ScreenPoint(300, 450),
            new ScreenPoint(300, -150),
            new ScreenPoint(-300, 150),
            new ScreenPoint(900, 150)
        }));
    }

    // The degenerate intermediate Uneven Blend fit from issue #217 (report 2026-10-07--03-24-33 at 5 points): x0 pinned
    // to the sweep edge and b on its 0.001 lower bound, so the left branch reaches ~1e8 HFR within the axis range. c and
    // y0 are elided in the issue; any well-conditioned right branch reproduces it.
    private static double Issue217UnevenBlend(double x) {
        const double x0 = 279518.987, y0 = 0.0, a = 2.781, b = 0.001, c = 20000.0, stepSize = 16000.0;
        var t = Math.Clamp((x0 - x) / stepSize, 0.0, 1.0);
        var left = t * a / b * Math.Sqrt((x - x0) * (x - x0) + b * b);
        var right = (1.0 - t) * a / c * Math.Sqrt((x - x0) * (x - x0) + c * c);
        return left + right + y0;
    }

    private static double WellBehavedHyperbola(double x) {
        const double x0 = 280000.0, a = 2.5, b = 20000.0;
        return a / b * Math.Sqrt((x - x0) * (x - x0) + b * b);
    }

    private const int SvgWidth = 600, SvgHeight = 400;
    private const string CurveStroke = "stroke:rgb(255,0,255)";

    private static PlotModel AfChartWith(FunctionAnnotation annotation, Func<double, double> equation) {
        var model = new PlotModel();
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Minimum = 240000, Maximum = 320000 });
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Minimum = 2, Maximum = 6 });
        annotation.Equation = equation;
        annotation.Color = OxyColors.Magenta;
        annotation.LineStyle = LineStyle.Dash;
        model.Annotations.Add(annotation);
        return model;
    }

    // Every coordinate of every polyline stroked in the curve's color.
    private static double[] CurveCoordinates(string svg) {
        var coordinates = new List<double>();
        foreach (Match polyline in Regex.Matches(svg, "<polyline[^>]*>")) {
            var element = polyline.Value;
            if (!element.Contains(CurveStroke, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }
            var points = Regex.Match(element, "points=\"([^\"]*)\"").Groups[1].Value;
            coordinates.AddRange(points
                .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => double.Parse(s, CultureInfo.InvariantCulture)));
        }
        return coordinates.ToArray();
    }

    private static string RenderSvg(PlotModel model) => SvgExporter.ExportToString(model, SvgWidth, SvgHeight, isDocument: false);

    [Test]
    public void Issue217_DegenerateUnevenBlendFit_RendersAsABoundedPath() {
        var model = AfChartWith(new BoundedFunctionAnnotationModel(), Issue217UnevenBlend);

        var coordinates = CurveCoordinates(RenderSvg(model));

        // The band is at most one plot width/height beyond the plot area, which sits inside the exported canvas.
        Assert.Multiple(() => {
            Assert.That(coordinates, Is.Not.Empty);
            Assert.That(coordinates.Min(), Is.GreaterThanOrEqualTo(-Math.Max(SvgWidth, SvgHeight)));
            Assert.That(coordinates.Max(), Is.LessThanOrEqualTo(2 * Math.Max(SvgWidth, SvgHeight)));
        });
    }

    [Test]
    public void Issue217_DegenerateUnevenBlendFit_PlainFunctionAnnotationProducesAGiantPath() {
        // Control: documents the bug the bounded annotation exists to fix. If OxyPlot ever starts clipping annotation
        // geometry itself, this fails and the bounded annotation can be reconsidered.
        var model = AfChartWith(new FunctionAnnotation(), Issue217UnevenBlend);

        var coordinates = CurveCoordinates(RenderSvg(model));

        Assert.That(coordinates.Max(Math.Abs), Is.GreaterThan(1e7));
    }

    [Test]
    public void WellBehavedCurve_RendersIdenticallyThroughBothAnnotationTypes() {
        var bounded = RenderSvg(AfChartWith(new BoundedFunctionAnnotationModel(), WellBehavedHyperbola));
        var plain = RenderSvg(AfChartWith(new FunctionAnnotation(), WellBehavedHyperbola));

        Assert.Multiple(() => {
            Assert.That(CurveCoordinates(bounded), Is.Not.Empty);
            Assert.That(bounded, Is.EqualTo(plain));
        });
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void WpfElement_WrapsTheBoundedModel() {
        var annotation = new BoundedFunctionAnnotation();

        Assert.That(annotation.InternalAnnotation, Is.InstanceOf<BoundedFunctionAnnotationModel>());
    }
}
