#region "copyright"

/*
    Copyright © 2021 - 2026 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Enum;
using NINA.Joko.Plugins.HocusFocus.AutoFocus;
using NINA.Joko.Plugins.HocusFocus.Interfaces;
using NINA.Joko.Plugins.HocusFocus.Tests.AutoFocus.Harness;
using NINA.WPF.Base.ViewModel.AutoFocus;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static NINA.Joko.Plugins.HocusFocus.Tests.AutoFocus.AutoFocusEngineSweepBatteryTests;

namespace NINA.Joko.Plugins.HocusFocus.Tests.AutoFocus {

    /// <summary>
    /// Per-colour-channel (OSC) focus curves through the real engine on the deterministic sweep harness. The pixel step
    /// (<see cref="AutoFocusEngine.MeasureColorChannelPlanes"/>) is scripted: each channel reports a hyperbola whose
    /// vertex sits at a known offset from the luminance focus, so the engine's pooling, fitting, windowing and delta
    /// arithmetic are what is under test. The central invariant: the channel curves are display only, so the
    /// luminance result and the focuser trajectory are identical with the option on or off.
    /// </summary>
    [TestFixture]
    public class AutoFocusEngineColorChannelTests {
        private const int Focus = 10000;
        private const int StepSize = 5;
        private const int OffsetSteps = 5;

        // Channel vertex offsets from the luminance focus, in focuser positions.
        private const int RedOffset = 15;
        private const int GreenOffset = 0;
        private const int BlueOffset = -10;

        [SetUp]
        public void ResetStaticGuardBefore() {
            AutoFocusEngine.ResetAutoFocusInProgressForTests();
        }

        [TearDown]
        public void ResetStaticGuardAfter() {
            AutoFocusEngine.ResetAutoFocusInProgressForTests();
        }

        private static DegradationScenario SteepScenario() => new DegradationScenario(Focus, StepSize) {
            BaselineHfr = pos => Math.Sqrt(1.0 + Math.Pow(0.12 * (pos - Focus), 2.0))
        };

        private static double ChannelHfr(int position, int vertexOffset, double scale) =>
            scale * Math.Sqrt(1.0 + Math.Pow(0.12 * (position - Focus - vertexOffset), 2.0));

        private sealed class ColorRun {
            public SweepHarness Harness { get; init; }
            public AutoFocusResult Result { get; set; }
            public int PlaneMeasurements;
            public ConcurrentQueue<AutoFocusMeasurementPointCompletedEventArgs> PointEvents { get; } = new ConcurrentQueue<AutoFocusMeasurementPointCompletedEventArgs>();
        }

        /// <param name="blueStarless">When true the blue plane never yields a measurable point (zero HFR).</param>
        /// <param name="corruptBeyond">When set, every channel reports half its true HFR at positions above this one — a
        /// distortion that drags any fit that includes those points.</param>
        private static async Task<ColorRun> Run(bool measureColorChannels, bool isBayered = true, int start = Focus, bool blueStarless = false, int? corruptBeyond = null) {
            var scenario = SteepScenario();
            var options = DefaultSweepOptions(scenario, OffsetSteps);
            options.MeasureColorChannels = measureColorChannels;
            var harness = BuildHarness(scenario, options, start, Focus - 5000, Focus + 5000, isBayered: isBayered);
            harness.Detection.ReturnHocusFocusResults = true;

            var run = new ColorRun() { Harness = harness };
            harness.Engine.MeasureColorChannelPlanes = (image, bayerPattern, stars, detectorParams) => {
                Interlocked.Increment(ref run.PlaneMeasurements);
                Assert.That(bayerPattern, Is.EqualTo(SensorType.RGGB), "the debayered frame's own CFA pattern is used");
                var position = harness.Ledger.PositionFor(image);
                var distortion = corruptBeyond.HasValue && position > corruptBeyond.Value ? 0.5 : 1.0;
                return new[] {
                    new MeasureAndError() { Measure = distortion * ChannelHfr(position, RedOffset, 1.10), Stdev = 0.1 },
                    new MeasureAndError() { Measure = distortion * ChannelHfr(position, GreenOffset, 1.00), Stdev = 0.1 },
                    blueStarless
                        ? new MeasureAndError() { Measure = 0.0, Stdev = 0.0 }
                        : new MeasureAndError() { Measure = distortion * ChannelHfr(position, BlueOffset, 1.25), Stdev = 0.1 }
                };
            };
            harness.Engine.MeasurementPointCompleted += (_, e) => run.PointEvents.Enqueue(e);

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            run.Result = await harness.Engine.Run(options, imagingFilter: null, token: cts.Token, progress: null);
            Assert.That(run.Result, Is.Not.Null);
            return run;
        }

        [TestCase(Focus)]
        [TestCase(Focus + 20 * StepSize)] // lopsided: luminance window-excludes the far points; channels must follow
        public async Task OptionOn_RecoversEachChannelsFocusOffset(int start) {
            var run = await Run(measureColorChannels: true, start: start);
            var region = run.Result.RegionResults[0];
            var channels = region.ColorChannels;

            Assert.That(run.Result.Succeeded, Is.True);
            Assert.That(channels, Is.Not.Null, "an OSC run with the option on carries channel curves");
            Assert.Multiple(() => {
                Assert.That(channels[ColorChannel.Red].FocusDelta, Is.EqualTo(RedOffset).Within(2), "R vertex offset");
                Assert.That(channels[ColorChannel.Green].FocusDelta, Is.EqualTo(GreenOffset).Within(2), "G vertex offset");
                Assert.That(channels[ColorChannel.Blue].FocusDelta, Is.EqualTo(BlueOffset).Within(2), "B vertex offset");
                Assert.That(channels.Channels.All(c => c.Fittings?.HyperbolicFitting != null), Is.True, "every channel has a final fit");
                Assert.That(channels.Channels.All(c => c.Fittings.SelectedHyperbolicFitModel == region.Fittings.SelectedHyperbolicFitModel), Is.True,
                    "channels are fitted with the model luminance selected");
                // Every completed luminance point has a channel point at the same position, pooled from the same frames.
                var luminancePositions = run.PointEvents.Select(e => e.FocuserPosition).OrderBy(p => p).ToArray();
                Assert.That(channels[ColorChannel.Green].Points.Select(p => p.FocuserPosition).ToArray(), Is.EqualTo(luminancePositions));
            });
        }

        [Test]
        public async Task OptionOn_WindowExcludedLuminancePoints_AreExcludedFromTheChannelFits() {
            // The lopsided start samples out to +25 steps; luminance window-excludes everything past ~+6 steps. The
            // channels are corrupted (HFR halved) past +7 steps, so the deltas come back accurate only if the channel
            // fits drop exactly the points luminance dropped.
            var corruptBeyond = Focus + 7 * StepSize;
            var run = await Run(measureColorChannels: true, start: Focus + 20 * StepSize, corruptBeyond: corruptBeyond);
            var region = run.Result.RegionResults[0];
            var excluded = region.WindowExcludedPoints.Select(p => p.FocuserPosition).ToHashSet();
            var green = region.ColorChannels[ColorChannel.Green];
            var corrupted = green.Points.Select(p => p.FocuserPosition).Where(p => p > corruptBeyond).ToArray();

            Assert.That(corrupted, Is.Not.Empty, "precondition: the sweep measured corrupted far points");
            Assert.That(excluded, Is.SupersetOf(corrupted), "precondition: luminance window-excluded every corrupted position");
            Assert.Multiple(() => {
                Assert.That(region.ColorChannels[ColorChannel.Red].FocusDelta, Is.EqualTo(RedOffset).Within(2));
                Assert.That(green.FocusDelta, Is.EqualTo(GreenOffset).Within(2));
                Assert.That(region.ColorChannels[ColorChannel.Blue].FocusDelta, Is.EqualTo(BlueOffset).Within(2));
            });
        }

        [TestCase(Focus)]
        [TestCase(Focus + 20 * StepSize)]
        public async Task LuminanceResultAndTrajectory_AreIdenticalWithTheOptionOnOrOff(int start) {
            var on = await Run(measureColorChannels: true, start: start);
            var off = await Run(measureColorChannels: false, start: start);
            var onRegion = on.Result.RegionResults[0];
            var offRegion = off.Result.RegionResults[0];

            Assert.Multiple(() => {
                Assert.That(on.Result.Succeeded, Is.EqualTo(off.Result.Succeeded));
                Assert.That(onRegion.EstimatedFinalFocuserPosition, Is.EqualTo(offRegion.EstimatedFinalFocuserPosition), "bit-identical luminance vertex");
                Assert.That(onRegion.EstimatedFinalHFR, Is.EqualTo(offRegion.EstimatedFinalHFR));
                Assert.That(on.Harness.Focuser.MoveHistory.ToArray(), Is.EqualTo(off.Harness.Focuser.MoveHistory.ToArray()).AsCollection,
                    "identical focuser trajectory — channel data never steers the sweep");
                Assert.That(onRegion.WindowExcludedPoints.Select(p => p.FocuserPosition), Is.EquivalentTo(offRegion.WindowExcludedPoints.Select(p => p.FocuserPosition)));
                Assert.That(onRegion.RejectedPoints.Select(p => p.FocuserPosition), Is.EquivalentTo(offRegion.RejectedPoints.Select(p => p.FocuserPosition)));
                Assert.That(on.Harness.Focuser.Position, Is.EqualTo(off.Harness.Focuser.Position), "same final focuser position");
            });
        }

        [Test]
        public async Task OptionOff_ProducesNoChannelData_AndNeverMeasuresPlanes() {
            var run = await Run(measureColorChannels: false);
            Assert.Multiple(() => {
                Assert.That(run.PlaneMeasurements, Is.Zero);
                Assert.That(run.Result.RegionResults[0].ColorChannels, Is.Null);
                Assert.That(run.PointEvents.All(e => e.ColorChannels == null), Is.True);
            });
        }

        [Test]
        public async Task NonBayeredFrames_ProduceNoChannelData_AndTheRunStillSucceeds() {
            var run = await Run(measureColorChannels: true, isBayered: false);
            Assert.Multiple(() => {
                Assert.That(run.Result.Succeeded, Is.True);
                Assert.That(run.PlaneMeasurements, Is.Zero);
                Assert.That(run.Result.RegionResults[0].ColorChannels, Is.Null);
            });
        }

        [Test]
        public async Task OptionOn_PointEventsCarryTheChannelCurvesAsTheyBuild() {
            var run = await Run(measureColorChannels: true);
            var events = run.PointEvents.ToArray();
            Assert.Multiple(() => {
                Assert.That(events.All(e => e.ColorChannels != null), Is.True);
                Assert.That(events.Last().ColorChannels[ColorChannel.Red].Points, Has.Length.EqualTo(events.Length));
                Assert.That(events.Last().ColorChannels[ColorChannel.Red].Fittings, Is.Not.Null, "live fits exist once enough points are in");
                Assert.That(events.All(e => e.ColorChannels.Channels.All(c => c.FocusDelta == null)), Is.True,
                    "deltas only exist after finalization");
            });
        }

        [Test]
        public async Task AChannelWithNoMeasurablePoints_HasNoDelta_WhileTheOthersDo() {
            var run = await Run(measureColorChannels: true, blueStarless: true);
            var channels = run.Result.RegionResults[0].ColorChannels;
            Assert.Multiple(() => {
                Assert.That(run.Result.Succeeded, Is.True);
                Assert.That(channels[ColorChannel.Blue].FocusDelta, Is.Null);
                Assert.That(channels[ColorChannel.Blue].Fittings, Is.Null);
                Assert.That(channels[ColorChannel.Red].FocusDelta, Is.Not.Null);
                Assert.That(channels[ColorChannel.Green].FocusDelta, Is.Not.Null);
            });
        }
    }
}
