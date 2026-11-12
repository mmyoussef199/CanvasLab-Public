using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CanvasLab.Models;

namespace CanvasLab.Services;

public sealed class ColorAnalysisService : IColorAnalysisService
{
    private const int MaxAnalysisDimension = 512;
    private const int PaletteSize = 10;
    private const int Iterations = 12;

    public Task<ColorAnalysisResult> AnalyzeAsync(ImageDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Task.Run(() => AnalyzeCore(document.ImageSource, cancellationToken), cancellationToken);
    }

    public PixelColor SamplePixel(ImageDocument document, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(document);
        if ((uint)x >= document.PixelWidth || (uint)y >= document.PixelHeight)
            throw new ArgumentOutOfRangeException(nameof(x), "The selected point is outside the image.");

        var source = ToBgra32(document.ImageSource);
        var pixel = new byte[4];
        source.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return new PixelColor(pixel[3], pixel[2], pixel[1], pixel[0], x, y);
    }

    private static ColorAnalysisResult AnalyzeCore(BitmapSource original, CancellationToken cancellationToken)
    {
        var source = ToBgra32(original);
        var largest = Math.Max(source.PixelWidth, source.PixelHeight);
        if (largest > MaxAnalysisDimension)
        {
            var scale = MaxAnalysisDimension / (double)largest;
            var scaled = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            scaled.Freeze();
            source = scaled;
        }

        var stride = checked(source.PixelWidth * 4);
        var pixels = new byte[checked(stride * source.PixelHeight)];
        source.CopyPixels(pixels, stride, 0);

        var bins = new Dictionary<int, Bin>();
        long totalWeight = 0;
        var visible = 0;
        var transparent = 0;

        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            if ((offset & 0x3FFFF) == 0) cancellationToken.ThrowIfCancellationRequested();
            var alpha = pixels[offset + 3];
            if (alpha == 0) { transparent++; continue; }

            var blue = pixels[offset];
            var green = pixels[offset + 1];
            var red = pixels[offset + 2];
            var key = (red >> 3 << 10) | (green >> 3 << 5) | (blue >> 3);
            if (!bins.TryGetValue(key, out var bin)) bin = new Bin();
            bin.Weight += alpha;
            bin.Red += (long)red * alpha;
            bin.Green += (long)green * alpha;
            bin.Blue += (long)blue * alpha;
            bins[key] = bin;
            totalWeight += alpha;
            visible++;
        }

        if (bins.Count == 0)
            return new ColorAnalysisResult([], 0, transparent);

        var points = bins.OrderBy(pair => pair.Key)
            .Select(pair => new ColorPoint(
                pair.Value.Red / (double)pair.Value.Weight,
                pair.Value.Green / (double)pair.Value.Weight,
                pair.Value.Blue / (double)pair.Value.Weight,
                pair.Value.Weight,
                pair.Key))
            .ToArray();

        var centers = SeedCenters(points, Math.Min(PaletteSize, points.Length));
        var assignments = new int[points.Length];

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var accumulators = new ClusterAccumulator[centers.Length];
            for (var i = 0; i < points.Length; i++)
            {
                var cluster = Nearest(points[i], centers);
                assignments[i] = cluster;
                accumulators[cluster].Add(points[i]);
            }

            for (var i = 0; i < centers.Length; i++)
                if (accumulators[i].Weight > 0)
                    centers[i] = accumulators[i].Center;
        }

        var final = new ClusterAccumulator[centers.Length];
        for (var i = 0; i < points.Length; i++) final[assignments[i]].Add(points[i]);

        var colors = final.Where(cluster => cluster.Weight > 0)
            .Select(cluster => new RepresentativeColor(
                ToByte(cluster.Red / cluster.Weight),
                ToByte(cluster.Green / cluster.Weight),
                ToByte(cluster.Blue / cluster.Weight),
                cluster.Weight * 100d / totalWeight))
            .OrderByDescending(color => color.CoveragePercentage)
            .ThenBy(color => color.HexCode, StringComparer.Ordinal)
            .ToArray();

        return new ColorAnalysisResult(colors, visible, transparent);
    }

    private static BitmapSource ToBgra32(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgra32) return source;
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    private static Center[] SeedCenters(ColorPoint[] points, int count)
    {
        var centers = new List<Center>(count);
        var first = points.OrderByDescending(point => point.Weight).ThenBy(point => point.Key).First();
        centers.Add(new Center(first.Red, first.Green, first.Blue));
        while (centers.Count < count)
        {
            var next = points.Select(point => new
                {
                    Point = point,
                    Score = centers.Min(center => Distance(point, center)) * point.Weight
                })
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Point.Key)
                .First().Point;
            centers.Add(new Center(next.Red, next.Green, next.Blue));
        }
        return centers.ToArray();
    }

    private static int Nearest(ColorPoint point, Center[] centers)
    {
        var nearest = 0;
        var distance = Distance(point, centers[0]);
        for (var i = 1; i < centers.Length; i++)
        {
            var candidate = Distance(point, centers[i]);
            if (candidate < distance) { distance = candidate; nearest = i; }
        }
        return nearest;
    }

    private static double Distance(ColorPoint point, Center center)
    {
        var red = point.Red - center.Red;
        var green = point.Green - center.Green;
        var blue = point.Blue - center.Blue;
        return red * red + green * green + blue * blue;
    }

    private static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);

    private struct Bin { public long Weight, Red, Green, Blue; }
    private readonly record struct ColorPoint(double Red, double Green, double Blue, long Weight, int Key);
    private readonly record struct Center(double Red, double Green, double Blue);
    private struct ClusterAccumulator
    {
        public long Weight;
        public double Red, Green, Blue;
        public readonly Center Center => new(Red / Weight, Green / Weight, Blue / Weight);
        public void Add(ColorPoint point)
        {
            Weight += point.Weight;
            Red += point.Red * point.Weight;
            Green += point.Green * point.Weight;
            Blue += point.Blue * point.Weight;
        }
    }
}
