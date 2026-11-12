using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CanvasLab.Models;

public sealed record PixelColor(byte Alpha, byte Red, byte Green, byte Blue, int X, int Y)
{
    public string HexCode => $"#{Red:X2}{Green:X2}{Blue:X2}";
    public string RgbText => $"RGB {Red}, {Green}, {Blue}";
    public string AlphaText => Alpha == 255 ? "Alpha 255 (100%)" : $"Alpha {Alpha} ({Alpha / 255d:P0})";
    public string PositionText => $"Pixel {X}, {Y}";
    public SolidColorBrush SwatchBrush { get; } = CreateBrush(Alpha, Red, Green, Blue);

    private static SolidColorBrush CreateBrush(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}

public sealed record RepresentativeColor(byte Red, byte Green, byte Blue, double CoveragePercentage)
{
    public string HexCode => $"#{Red:X2}{Green:X2}{Blue:X2}";
    public string CoverageText => $"{CoveragePercentage:0.#}%";
    public SolidColorBrush SwatchBrush { get; } = CreateBrush(Red, Green, Blue);

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

public sealed record ColorAnalysisResult(
    IReadOnlyList<RepresentativeColor> MainColors,
    int SampledVisiblePixels,
    int TransparentPixelsExcluded);
public enum ImageFileFormat { Jpeg, Png, WebP }

public sealed record ImageOptimizationRequest(ImageDocument Document, int TargetWidth, int TargetHeight,
    int ImageQuality, ImageFileFormat OutputFormat);

public sealed record ImageOptimizationResult(byte[] EncodedBytes, BitmapSource PreviewImage,
    ImageFileFormat OutputFormat, int PixelWidth, int PixelHeight, long OriginalSizeBytes, string SourcePath)
{
    public long OptimizedSizeBytes => EncodedBytes.LongLength;
}

public sealed record SaveImageSelection(string FilePath, bool OverwriteApproved);

