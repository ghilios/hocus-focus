using NINA.Joko.Plugins.HocusFocus.Interfaces;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NINA.Joko.Plugins.HocusFocus.Tests.StarDetection {

    /// <summary>
    /// Determinism guard and pre-change baseline for the star detector.
    ///
    /// <list type="bullet">
    ///   <item><term>Determinism</term><description>
    ///     Running <c>Detect</c> twice on the same small synthetic field must produce the
    ///     identical <see cref="StarDetectorEquivalence.Signature"/>.
    ///   </description></item>
    ///   <item><term>Baseline</term><description>
    ///     The current detector must reproduce the committed golden signature captured on the
    ///     <c>ghilios/af-star-detection-parallelization</c> branch before any production changes.
    ///     Later parallelization tasks must still match this baseline to prove results are unchanged.
    ///   </description></item>
    /// </list>
    ///
    /// The field is deliberately small (512×512, 49 stars) so this test runs in a few seconds in the
    /// normal suite (it is NOT <c>[Explicit]</c>).
    /// </summary>
    [TestFixture]
    public class StarDetectorEquivalenceTests {

        // ── Golden baseline signature ─────────────────────────────────────────────────────────────────
        // Captured by running Detect_SmallField_MatchesGoldenBaseline with PrintGolden = true
        // on the ghilios/af-star-detection-parallelization branch before any production changes.
        // DO NOT edit this constant unless you are intentionally changing detection behavior and
        // re-baselining — this is the "before" fingerprint that parallelization tasks must match.
        //
        // If this test breaks after a production change, re-run with PrintGolden = true,
        // verify the new signature is expected, and update this constant in a separate commit.
        private const string GoldenSignature =
            "count=49\r\n" +
            "star 288.0279 31.9638 2.871573 0.6647\r\n" +
            "star 96.0011 31.9780 2.907053 0.6945\r\n" +
            "star 31.9656 31.9792 2.896020 0.6954\r\n" +
            "star 160.0209 31.9889 2.885873 0.7174\r\n" +
            "star 351.9849 31.9997 2.868631 0.6631\r\n" +
            "star 223.9698 32.0049 2.907331 0.6879\r\n" +
            "star 416.0370 32.0059 2.898318 0.6942\r\n" +
            "star 160.0541 95.9033 2.929885 0.7177\r\n" +
            "star 287.9858 95.9528 2.874486 0.7108\r\n" +
            "star 352.0204 95.9934 2.891848 0.6835\r\n" +
            "star 31.9721 95.9983 2.837971 0.7169\r\n" +
            "star 223.9983 96.0131 2.866080 0.6905\r\n" +
            "star 415.9818 96.0401 2.892428 0.6874\r\n" +
            "star 95.9851 96.0587 2.893108 0.6857\r\n" +
            "star 288.0171 159.9733 2.937465 0.6967\r\n" +
            "star 31.9912 159.9758 2.877141 0.7029\r\n" +
            "star 352.0551 159.9789 2.884754 0.7074\r\n" +
            "star 160.0201 159.9860 2.885021 0.6885\r\n" +
            "star 415.9908 159.9910 2.914447 0.6875\r\n" +
            "star 96.0258 160.0187 2.905924 0.6874\r\n" +
            "star 223.9934 160.0961 2.933493 0.6660\r\n" +
            "star 159.9828 223.9436 2.880364 0.6943\r\n" +
            "star 31.9407 223.9529 2.901138 0.6907\r\n" +
            "star 95.9718 223.9774 2.874091 0.7181\r\n" +
            "star 351.9268 223.9857 2.902850 0.6742\r\n" +
            "star 415.9561 224.0298 2.914731 0.6892\r\n" +
            "star 288.0134 224.0638 2.926995 0.6717\r\n" +
            "star 223.9822 224.0889 2.923208 0.6894\r\n" +
            "star 352.0486 287.9606 2.889138 0.7228\r\n" +
            "star 96.1010 287.9628 2.944550 0.6770\r\n" +
            "star 32.0415 287.9830 2.886508 0.7070\r\n" +
            "star 224.0210 288.0054 2.867574 0.7171\r\n" +
            "star 160.0739 288.0143 2.907087 0.7176\r\n" +
            "star 415.9796 288.0294 2.875630 0.7082\r\n" +
            "star 288.0229 288.0354 2.896999 0.7046\r\n" +
            "star 159.9836 351.9803 2.873619 0.6806\r\n" +
            "star 95.9840 351.9804 2.870847 0.7009\r\n" +
            "star 31.9612 351.9912 2.855246 0.7044\r\n" +
            "star 224.0045 351.9914 2.832358 0.7194\r\n" +
            "star 288.0735 352.0143 2.901216 0.6960\r\n" +
            "star 351.9571 352.0177 2.902316 0.7063\r\n" +
            "star 415.9383 352.0568 2.882020 0.6757\r\n" +
            "star 224.0065 415.9763 2.924396 0.6689\r\n" +
            "star 32.0111 415.9790 2.868362 0.6963\r\n" +
            "star 287.9844 415.9964 2.874314 0.7007\r\n" +
            "star 416.0832 415.9965 2.888569 0.7204\r\n" +
            "star 159.9676 415.9969 2.882939 0.7311\r\n" +
            "star 96.0369 416.0231 2.911770 0.6866\r\n" +
            "star 352.0111 416.0304 2.948229 0.7156\r\n" +
            "StructureCandidates=49\r\n" +
            "TotalDetected=49\r\n" +
            "TooSmall=0\r\n" +
            "OnBorder=0\r\n" +
            "TooLowHFR=0\r\n" +
            "HFRAnalysisFailed=0\r\n" +
            "PSFFitFailed=0\r\n" +
            "OutsideROI=0\r\n" +
            "SaturatedPixelCount=0\r\n" +
            "HotpixelCount=221643\r\n" +
            "MeasurementHotpixelCount=0\r\n" +
            "RelaxationAdmittedCount=0\r\n" +
            "TooDistortedBounds=[]\r\n" +
            "DegenerateBounds=[]\r\n" +
            "SaturatedBounds=[]\r\n" +
            "LowSensitivityBounds=[]\r\n" +
            "NotCenteredBounds=[]\r\n" +
            "TooFlatBounds=[]\r\n" +
            "TooElongatedBounds=[]\r\n" +
            "BloomSuppressedBounds=[]\r\n" +
            "ContaminatedBounds=[]\r\n";

        // Set to true locally to print the signature (then copy it into GoldenSignature above).
        // Must be false when committed.
        private const bool PrintGolden = false;

        [Test]
        public async Task Detect_SmallField_IsDeterministic() {
            var p = StarDetectorEquivalence.StandardParams();

            using var field1 = StarDetectorEquivalence.BuildSmallField();
            var result1 = await StarDetectorEquivalence.RunDetect(field1, p);
            var sig1 = StarDetectorEquivalence.Signature(result1);

            using var field2 = StarDetectorEquivalence.BuildSmallField();
            var result2 = await StarDetectorEquivalence.RunDetect(field2, p);
            var sig2 = StarDetectorEquivalence.Signature(result2);

            TestContext.Progress.WriteLine($"[determinism] detected={result1.DetectedStars.Count}");
            Assert.That(sig2, Is.EqualTo(sig1),
                "Detect must be deterministic: two runs on the same field must yield identical signatures");
        }

        [Test]
        public async Task Detect_SmallField_MatchesGoldenBaseline() {
            var p = StarDetectorEquivalence.StandardParams();
            using var field = StarDetectorEquivalence.BuildSmallField();
            var result = await StarDetectorEquivalence.RunDetect(field, p);
            var sig = StarDetectorEquivalence.Signature(result);

            if (PrintGolden) {
                // Emit the full signature to the test output so it can be copied into GoldenSignature.
                TestContext.Progress.WriteLine("=== GOLDEN SIGNATURE (copy into GoldenSignature const) ===");
                TestContext.Progress.WriteLine(sig);
                TestContext.Progress.WriteLine("=== END GOLDEN ===");
                // Skip assertion when printing — the point is to capture the value.
                Assert.Ignore("PrintGolden=true: golden signature printed above; set PrintGolden=false and paste the value into GoldenSignature.");
                return;
            }

            Assert.That(sig, Is.EqualTo(GoldenSignature),
                "Detected stars and metrics must exactly match the committed pre-change baseline. " +
                "If this fails after a parallelization change, the change has altered results — " +
                "investigate before updating the baseline.");
        }

        /// <summary>
        /// Bit-identical gate for the spatially-adaptive binarization rollout: with
        /// <see cref="StarDetectorParams.LocallyAdaptiveBinarization"/> OFF the binarization seam runs the exact
        /// legacy scalar path (the new coarse grids are never even computed), so the full detected-star + metrics
        /// signature MUST equal the committed pre-change baseline. This is the guarantee that lets the option's
        /// default be flipped ON safely while keeping the boolean as an off-switch.
        /// </summary>
        [Test]
        public async Task Detect_AdaptiveBinarizationOff_MatchesLegacyBaseline() {
            var p = StarDetectorEquivalence.StandardParams();
            p.LocallyAdaptiveBinarization = false;
            using var field = StarDetectorEquivalence.BuildSmallField();
            var result = await StarDetectorEquivalence.RunDetect(field, p);
            var sig = StarDetectorEquivalence.Signature(result);

            Assert.That(sig, Is.EqualTo(GoldenSignature),
                "LocallyAdaptiveBinarization=false must be byte-for-byte identical to the legacy baseline.");
        }

        /// <summary>
        /// End-to-end smoke test for the ON path: on a spatially-uniform field the local-median + NC·local-σ surface
        /// degenerates to ~the global threshold, so adaptive binarization must execute and recover the same bright
        /// stars as the legacy path. This is a behavioural check (same stars, within a fraction of a pixel), NOT a
        /// bit-identical one — the estimators differ (block median / 1.4826·MAD vs histogram-median / kappa-sigma),
        /// so a candidate at the pixel margin may differ. The real ON-path validation is the AF-bank audit (Step 6).
        /// </summary>
        [Test]
        public async Task Detect_AdaptiveBinarizationOn_RecoversSameStarsOnUniformField() {
            var pOff = StarDetectorEquivalence.StandardParams();
            var pOn = StarDetectorEquivalence.StandardParams();
            pOn.LocallyAdaptiveBinarization = true;
            pOn.AdaptiveNoiseBlockSize = 128;

            using var field1 = StarDetectorEquivalence.BuildSmallField();
            var off = await StarDetectorEquivalence.RunDetect(field1, pOff);
            using var field2 = StarDetectorEquivalence.BuildSmallField();
            var on = await StarDetectorEquivalence.RunDetect(field2, pOn);

            TestContext.Progress.WriteLine($"[adaptive] off={off.DetectedStars.Count} on={on.DetectedStars.Count}");
            Assert.That(on.DetectedStars.Count, Is.EqualTo(off.DetectedStars.Count).Within(1),
                "adaptive ON should recover essentially the same star count on a uniform field");
            foreach (var s in off.DetectedStars) {
                Assert.That(
                    on.DetectedStars.Any(t => Math.Abs(t.Center.X - s.Center.X) < 0.5 && Math.Abs(t.Center.Y - s.Center.Y) < 0.5),
                    Is.True, $"adaptive ON dropped a star near ({s.Center.X:F1},{s.Center.Y:F1})");
            }
        }
    }
}
