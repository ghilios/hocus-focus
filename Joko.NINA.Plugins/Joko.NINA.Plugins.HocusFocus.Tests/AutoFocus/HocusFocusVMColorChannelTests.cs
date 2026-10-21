using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Joko.Plugins.HocusFocus.AutoFocus;
using NINA.Joko.Plugins.HocusFocus.Interfaces;
using NINA.Joko.Plugins.HocusFocus.StarDetection;
using NINA.Joko.Plugins.HocusFocus.Tests.Synthetic;
using NINA.Joko.Plugins.HocusFocus.Utility;
using NINA.Joko.Plugins.HocusFocus.Tests.TestDoubles;
using NINA.WPF.Base.ViewModel.AutoFocus;
using NUnit.Framework;
using System;
using System.Linq;

namespace NINA.Joko.Plugins.HocusFocus.Tests.AutoFocus;

[TestFixture]
public class HocusFocusVMColorChannelTests {

    [TestCase(12, -3, 27, "R +12  G −3  B +27")]
    [TestCase(0, 0, 0, "R 0  G 0  B 0")]
    [TestCase(null, -1, null, "R —  G −1  B —")]
    public void FormatChannelFocusDeltas_SignedStepsWithDashForMissing(int? red, int? green, int? blue, string expected) {
        Assert.That(HocusFocusVM.FormatChannelFocusDeltas(red, green, blue), Is.EqualTo(expected));
    }

    private static AutoFocusFitting HyperbolicFitting(double vertex) {
        var points = SyntheticFocusCurveSamples.SymmetricHyperbolaPoints(x0: vertex, y0: 0.0, a: 2.0, b: 80.0, xStart: vertex - 100, xStep: 25, count: 9);
        var fit = AlglibHyperbolicFitting.Create(new AlglibAPI(), HyperbolicFitModel.Symmetric, points, 25, false);
        Assert.That(fit.Solve(), Is.True);
        return new AutoFocusFitting() {
            Method = AFMethodEnum.STARHFR,
            CurveFittingType = AFCurveFittingEnum.HYPERBOLIC,
            HyperbolicFitting = fit
        };
    }

    internal static AutoFocusColorChannels Channels(int?[] deltas, bool withFits = true) {
        return new AutoFocusColorChannels() {
            Channels = Enumerable.Range(0, 3).Select(c => new AutoFocusColorChannelCurve() {
                Fittings = withFits ? HyperbolicFitting(5000 + 10 * c) : null,
                Points = new[] { 5050, 4950, 5000 }.Select(x => new AutoFocusRegionPoint() {
                    FocuserPosition = x,
                    Measurement = new MeasureAndError() { Measure = 2.0 + c + Math.Abs(x - 5000) / 50.0, Stdev = 0.1 }
                }).ToArray(),
                FocusDelta = deltas[c]
            }).ToArray()
        };
    }

    [Test]
    public void ApplyColorChannels_LiveEvent_ShowsPointsAndCurvesButNoDeltas() {
        var vm = new MediatorBundle().BuildHocusFocusVM();
        vm.ApplyColorChannels(Channels(new int?[] { 7, -1, -5 }), finalized: false);

        Assert.Multiple(() => {
            Assert.That(vm.ShowChannelCurves, Is.True);
            Assert.That(vm.PlotRedFocusPoints.Select(p => p.X), Is.EqualTo(new[] { 4950.0, 5000.0, 5050.0 }), "sorted by position");
            Assert.That(vm.PlotBlueFocusPoints.Select(p => p.Y).First(), Is.EqualTo(5.0));
            Assert.That(vm.RedFitting, Is.Not.Null);
            Assert.That(vm.ChannelFocusDeltaText, Is.Null, "deltas only exist once the run is finalized");
            Assert.That(vm.HasChannelFocusDeltas, Is.False);
        });
    }

    [Test]
    public void ApplyColorChannels_Finalized_ShowsTheDeltaRow() {
        var vm = new MediatorBundle().BuildHocusFocusVM();
        vm.ApplyColorChannels(Channels(new int?[] { 7, -1, null }), finalized: true);

        Assert.Multiple(() => {
            Assert.That(vm.ChannelFocusDeltaText, Is.EqualTo("R +7  G −1  B —"));
            Assert.That(vm.HasChannelFocusDeltas, Is.True);
        });
    }

    [Test]
    public void ApplyColorChannels_Null_ClearsEverything() {
        var vm = new MediatorBundle().BuildHocusFocusVM();
        vm.ApplyColorChannels(Channels(new int?[] { 7, -1, -5 }), finalized: true);

        vm.ApplyColorChannels(null, finalized: true);

        Assert.Multiple(() => {
            Assert.That(vm.ShowChannelCurves, Is.False);
            Assert.That(vm.PlotRedFocusPoints, Is.Empty);
            Assert.That(vm.PlotGreenFocusPoints, Is.Empty);
            Assert.That(vm.PlotBlueFocusPoints, Is.Empty);
            Assert.That(vm.RedFitting, Is.Null);
            Assert.That(vm.HasChannelFocusDeltas, Is.False);
        });
    }

    [TestCase(AFCurveFittingEnum.HYPERBOLIC, true)]
    [TestCase(AFCurveFittingEnum.TRENDHYPERBOLIC, true)]
    [TestCase(AFCurveFittingEnum.TRENDLINES, false)]
    public void ActiveChannelCurve_FollowsTheFittingType(AFCurveFittingEnum fitting, bool expectCurve) {
        var fittings = HyperbolicFitting(5000);
        fittings.CurveFittingType = fitting;
        Assert.That(HocusFocusVM.ActiveChannelCurve(fittings) != null, Is.EqualTo(expectCurve));
    }

    [Test]
    public void Report_ColorChannels_RoundTripThroughJson() {
        var report = new HocusFocusReport() { ColorChannels = ReportColorChannels.From(Channels(new int?[] { 7, null, -5 })) };

        var reloaded = JsonConvert.DeserializeObject<HocusFocusReport>(JsonConvert.SerializeObject(report));

        Assert.Multiple(() => {
            Assert.That(reloaded.ColorChannels, Is.Not.Null);
            Assert.That(reloaded.ColorChannels.All.Select(c => c.FocusDelta), Is.EqualTo(new int?[] { 7, null, -5 }));
            Assert.That(reloaded.ColorChannels.Green.MeasurePoints.Select(p => p.Position), Is.EqualTo(new[] { 5050.0, 4950.0, 5000.0 }));
            Assert.That(reloaded.ColorChannels.Blue.MeasurePoints[0].Value, Is.EqualTo(5.0), "blue at 5050: 2 + 2 + 50/50");
            Assert.That(reloaded.ColorChannels.Blue.MeasurePoints[0].Error, Is.EqualTo(0.1));
        });
    }

    [Test]
    public void Report_WithoutColorChannels_DeserializesToNull() {
        var reloaded = JsonConvert.DeserializeObject<HocusFocusReport>("{\"FinalHFR\": 1.5}");
        Assert.That(reloaded.ColorChannels, Is.Null);
    }

    [Test]
    public void ReportColorChannels_FromNull_IsNull() {
        Assert.That(ReportColorChannels.From(null), Is.Null);
    }
}
