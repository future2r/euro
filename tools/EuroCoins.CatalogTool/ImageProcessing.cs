using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace EuroCoins.CatalogTool;

/// <summary>
/// Normalizes coin images for the app: JPEG, at most <see cref="MaxEdge"/> pixels per side.
/// </summary>
internal static class ImageProcessing
{
    /// <summary>The maximum width and height of a catalog image.</summary>
    public const int MaxEdge = 540;

    private const long JpegQuality = 85;

    /// <summary>
    /// Writes a downscaled JPEG copy of an image (JPEG, PNG or GIF). Smaller images are only re-encoded.
    /// </summary>
    public static void SaveNormalized(byte[] imageData, string targetPath)
    {
        using var stream = new MemoryStream(imageData);
        using var source = Image.FromStream(stream);
        var scale = Math.Min(1.0, (double)MaxEdge / Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var target = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(target))
        {
            graphics.Clear(Color.White); // Transparent PNGs get the white JPEG background.
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(source, 0, 0, width, height);
        }

        var encoder = ImageCodecInfo.GetImageEncoders().First(e => e.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, JpegQuality);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        target.Save(targetPath, encoder, parameters);
    }
}
