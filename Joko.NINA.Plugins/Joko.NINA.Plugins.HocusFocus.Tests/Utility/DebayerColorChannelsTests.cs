using NINA.Core.Enum;
using NINA.Image.ImageAnalysis;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NSubstitute;
using NUnit.Framework;
using System.Windows.Media;

namespace NINA.Joko.Plugins.HocusFocus.Tests.Utility;

/// <summary>
/// Pins the NINA API assumption behind the per-channel focus curves: <c>ImageUtility.Debayer</c> with
/// <c>saveColorChannels: true</c> returns R/G/B planes at FULL sensor resolution, in the same row-major layout as
/// the luminance plane, so star geometry from the luminance pass carries over to each colour plane unscaled.
/// </summary>
[TestFixture]
public class DebayerColorChannelsTests {
    // Deliberately non-square so a transposed plane would be caught.
    private const int Width = 64;
    private const int Height = 48;
    private const ushort RedLevel = 1000;
    private const ushort GreenLevel = 2000;
    private const ushort BlueLevel = 3000;

    /// <summary>A flat RGGB mosaic: R at (even row, even col), G at the two mixed sites, B at (odd, odd).</summary>
    private static ushort[] BuildFlatRggbMosaic() {
        var mosaic = new ushort[Width * Height];
        for (var y = 0; y < Height; ++y) {
            for (var x = 0; x < Width; ++x) {
                var isEvenRow = (y % 2) == 0;
                var isEvenCol = (x % 2) == 0;
                mosaic[y * Width + x] = isEvenRow
                    ? (isEvenCol ? RedLevel : GreenLevel)
                    : (isEvenCol ? GreenLevel : BlueLevel);
            }
        }
        return mosaic;
    }

    private static void AssertFullResolutionPlanes(ushort[] red, ushort[] green, ushort[] blue) {
        Assert.Multiple(() => {
            Assert.That(red, Has.Length.EqualTo(Width * Height), "Red plane must be full resolution");
            Assert.That(green, Has.Length.EqualTo(Width * Height), "Green plane must be full resolution");
            Assert.That(blue, Has.Length.EqualTo(Width * Height), "Blue plane must be full resolution");
            // Interior pixels only: the debayer kernel may treat the outermost rows/columns specially.
            for (var y = 2; y < Height - 2; ++y) {
                for (var x = 2; x < Width - 2; ++x) {
                    var i = y * Width + x;
                    Assert.That(red[i], Is.EqualTo(RedLevel).Within(1), $"Red at ({x},{y})");
                    Assert.That(green[i], Is.EqualTo(GreenLevel).Within(1), $"Green at ({x},{y})");
                    Assert.That(blue[i], Is.EqualTo(BlueLevel).Within(1), $"Blue at ({x},{y})");
                }
            }
        });
    }

    [Test]
    public void ImageUtilityDebayer_SaveColorChannels_ReturnsFullResolutionRgbPlanes() {
        var props = new ImageProperties(Width, Height, 16, isBayered: true, gain: 0, offset: 0);
        var bitmapSource = ImageUtility.CreateSourceFromArray(new ImageArray(BuildFlatRggbMosaic()), props, PixelFormats.Gray16);

        var debayered = ImageUtility.Debayer(bitmapSource, pf: System.Drawing.Imaging.PixelFormat.Format16bppGrayScale,
            saveColorChannels: true, saveLumChannel: true, bayerPattern: SensorType.RGGB);

        AssertFullResolutionPlanes(debayered.Data.Red, debayered.Data.Green, debayered.Data.Blue);
        Assert.That(debayered.Data.Lum, Has.Length.EqualTo(Width * Height));
    }

    [Test]
    public void RenderedImageDebayer_SaveColorChannels_ExposesFullResolutionRgbPlanes() {
        var profileService = Substitute.For<IProfileService>();
        var starDetection = Substitute.For<IStarDetection>();
        starDetection.CreateAnalysis().Returns(_ => new StarDetectionAnalysis());
        var imageData = new BaseImageData(BuildFlatRggbMosaic(), Width, Height, 16, isBayered: true, new ImageMetaData(),
            profileService, starDetection, Substitute.For<IStarAnnotator>());

        var debayered = imageData.RenderImage().Debayer(saveColorChannels: true, saveLumChannel: true, bayerPattern: SensorType.RGGB);

        AssertFullResolutionPlanes(debayered.DebayeredData.Red, debayered.DebayeredData.Green, debayered.DebayeredData.Blue);
    }
}
