using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CanvasLab.Models;
using CanvasLab.Services;
using CanvasLab.Utilities;
using CanvasLab.ViewModels;

namespace CanvasLab.SmokeTests;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--generate-sample")
        {
            GenerateSample(args[1]);
            Console.WriteLine($"Generated {args[1]}");
            return 0;
        }
        if (args.Length == 2 && args[0] == "--generate-color-sample")
        {
            GenerateColorSample(args[1]);
            Console.WriteLine($"Generated {args[1]}");
            return 0;
        }
        if (args.Length == 2 && args[0] == "--inspect-image")
        {
            var image = await new ImageLoaderService().LoadAsync(args[1]);
            Console.WriteLine($"Format={image.SourceFormat}; Width={image.PixelWidth}; Height={image.PixelHeight}; Bytes={image.FileSizeBytes}; Alpha={image.HasAlpha}");
            return 0;
        }

        var failures = new List<string>();
        await Run("File size formatting", TestSizes, failures);
        await Run("PNG load and metadata", TestPng, failures);
        await Run("JPEG load and metadata", TestJpeg, failures);
        await Run("Missing file message", TestMissing, failures);
        await Run("Unsupported file message", TestUnsupported, failures);
        await Run("Exact pixel sampling and alpha", TestExactPixelSampling, failures);
        await Run("Zoom coordinate mapping", TestCoordinateMapping, failures);
        await Run("Known palette proportions", TestPaletteProportions, failures);
        await Run("Deterministic gradient palette", TestDeterministicGradient, failures);
        await Run("Copy Color command", TestCopyColorCommand, failures);
        await Run("WebP load and signature detection", TestWebP, failures);
        await Run("Resize output dimensions", TestResize, failures);
        await Run("Proportional dimension updates", TestProportions, failures);
        await Run("PNG disables image quality", TestPngDisablesQuality, failures);
        await Run("JPEG quality changes encoded size", TestQuality, failures);
        await Run("PNG transparency preservation", TestPngTransparency, failures);
        await Run("JPEG transparency flattening", TestJpegTransparency, failures);
        await Run("WebP transparency preservation", TestWebPTransparency, failures);
        await Run("Invalid dimensions rejected", TestInvalidDimensions, failures);
        await Run("Canceled optimization stops", TestCancellation, failures);
        await Run("EXIF orientation applied", TestOrientation, failures);
        await Run("Safe export and source preservation", TestSafeExport, failures);
        Console.WriteLine(failures.Count == 0 ? "All CanvasLab smoke tests passed." : $"{failures.Count} test(s) failed.");
        foreach (var failure in failures) Console.WriteLine($"  - {failure}");
        return failures.Count == 0 ? 0 : 1;
    }

    private static void GenerateSample(string path)
    {
        const int width = 900, height = 600;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var offset = (y * width + x) * 4;
            pixels[offset] = (byte)(40 + 150 * y / height);
            pixels[offset + 1] = (byte)(70 + 140 * x / width);
            pixels[offset + 2] = (byte)(160 + 80 * (width - x) / width);
            pixels[offset + 3] = 255;
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path); encoder.Save(stream);
    }

    private static void GenerateColorSample(string path)
    {
        const int width = 400, height = 300;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var offset = (y * width + x) * 4;
            var (red, green, blue, alpha) = x switch
            {
                < 200 => ((byte)235, (byte)64, (byte)52, (byte)255),
                < 320 => ((byte)42, (byte)178, (byte)91, (byte)255),
                < 380 => ((byte)45, (byte)105, (byte)220, (byte)180),
                _ => ((byte)255, (byte)255, (byte)255, (byte)0)
            };
            pixels[offset] = blue;
            pixels[offset + 1] = green;
            pixels[offset + 2] = red;
            pixels[offset + 3] = alpha;
        }
        SavePng(path, BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4));
    }

    private static Task TestSizes()
    {
        Equal("0 B", FileSizeFormatter.Format(0));
        Equal("1.00 KB", FileSizeFormatter.Format(1024));
        Equal("1.00 MB", FileSizeFormatter.Format(1024 * 1024));
        return Task.CompletedTask;
    }

    private static async Task TestPng()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "generated-sample.png");
        try
        {
            byte[] pixels = [0x30,0x80,0xE8,0xFF, 0xF0,0xB0,0x30,0xFF, 0x50,0xD0,0x80,0xFF, 0xE8,0x58,0x70,0xFF];
            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, pixels, 8);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            await using (var stream = File.Create(path)) encoder.Save(stream);
            var document = await new ImageLoaderService().LoadAsync(path);
            Equal(2, document.PixelWidth); Equal(2, document.PixelHeight);
            True(document.FileSizeBytes > 0, "Generated PNG should not be empty.");
            True(document.ImageSource.IsFrozen, "Loaded image should be frozen for cross-thread use.");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static async Task TestJpeg()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "generated-sample.jpg");
        try
        {
            byte[] pixels = [0x30,0x80,0xE8,0xFF, 0xF0,0xB0,0x30,0xFF, 0x50,0xD0,0x80,0xFF, 0xE8,0x58,0x70,0xFF];
            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, pixels, 8);
            var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            await using (var stream = File.Create(path)) encoder.Save(stream);
            var document = await new ImageLoaderService().LoadAsync(path);
            Equal(2, document.PixelWidth); Equal(2, document.PixelHeight);
            True(document.FileSizeBytes > 0, "Generated JPEG should not be empty.");
            True(document.ImageSource.IsFrozen, "Loaded image should be frozen for cross-thread use.");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static async Task TestMissing()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "missing.png");
        var ex = await Throws<ImageLoadException>(() => new ImageLoaderService().LoadAsync(path));
        True(ex.Message.Contains("could not be found", StringComparison.OrdinalIgnoreCase), "Missing-file message is unclear.");
    }

    private static async Task TestUnsupported()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "unsupported.gif");
        await File.WriteAllTextAsync(path, "not an image");
        try
        {
            var ex = await Throws<ImageLoadException>(() => new ImageLoaderService().LoadAsync(path));
            True(ex.Message.Contains("JPEG, PNG, or WebP", StringComparison.OrdinalIgnoreCase), "Supported formats were not named.");
        }
        finally { File.Delete(path); }
    }

    private static Task TestExactPixelSampling()
    {
        byte[] pixels = [0x33, 0x22, 0x11, 0x80, 0xCC, 0xBB, 0xAA, 0xFF];
        var document = MakeDocument(2, 1, pixels);
        var selected = new ColorAnalysisService().SamplePixel(document, 0, 0);
        Equal("#112233", selected.HexCode);
        Equal((byte)128, selected.Alpha);
        Equal(0, selected.X); Equal(0, selected.Y);
        return Task.CompletedTask;
    }

    private static Task TestCoordinateMapping()
    {
        Equal((100, 50), ImageCoordinateMapper.MapToPixel(50.25, 25.25, 200, 100, 400, 200)!.Value);
        Equal((100, 50), ImageCoordinateMapper.MapToPixel(200.5, 101, 800, 400, 400, 200)!.Value);
        Equal((399, 199), ImageCoordinateMapper.MapToPixel(800, 400, 800, 400, 400, 200)!.Value);
        True(ImageCoordinateMapper.MapToPixel(-1, 0, 200, 100, 400, 200) is null, "Outside coordinates must be rejected.");
        return Task.CompletedTask;
    }

    private static async Task TestPaletteProportions()
    {
        var pixels = new byte[101 * 4];
        for (var x = 0; x < 101; x++)
        {
            var offset = x * 4;
            (pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]) = x switch
            {
                < 60 => ((byte)0, (byte)0, (byte)255, (byte)255),
                < 90 => ((byte)0, (byte)255, (byte)0, (byte)255),
                < 100 => ((byte)255, (byte)0, (byte)0, (byte)255),
                _ => ((byte)255, (byte)255, (byte)255, (byte)0)
            };
        }
        var result = await new ColorAnalysisService().AnalyzeAsync(MakeDocument(101, 1, pixels));
        Equal(3, result.MainColors.Count);
        Equal(1, result.TransparentPixelsExcluded);
        Near(60, result.MainColors.Single(color => color.HexCode == "#FF0000").CoveragePercentage, 0.01);
        Near(30, result.MainColors.Single(color => color.HexCode == "#00FF00").CoveragePercentage, 0.01);
        Near(10, result.MainColors.Single(color => color.HexCode == "#0000FF").CoveragePercentage, 0.01);
    }

    private static async Task TestDeterministicGradient()
    {
        const int width = 256;
        var pixels = new byte[width * 4];
        for (var x = 0; x < width; x++)
        {
            var offset = x * 4;
            pixels[offset] = (byte)(255 - x);
            pixels[offset + 1] = (byte)(x / 2);
            pixels[offset + 2] = (byte)x;
            pixels[offset + 3] = 255;
        }
        var service = new ColorAnalysisService();
        var document = MakeDocument(width, 1, pixels);
        var first = await service.AnalyzeAsync(document);
        var second = await service.AnalyzeAsync(document);
        Equal(10, first.MainColors.Count);
        Equal(string.Join('|', first.MainColors.Select(ColorSignature)), string.Join('|', second.MainColors.Select(ColorSignature)));
    }

    private static async Task TestCopyColorCommand()
    {
        byte[] pixels = [0x33, 0x22, 0x11, 0xFF];
        var document = MakeDocument(1, 1, pixels);
        var clipboard = new TestClipboard();
        var viewModel = new MainWindowViewModel(new TestLoader(document), new TestDialog(), new ColorAnalysisService(), clipboard);
        await viewModel.LoadImageAsync("synthetic.png");
        viewModel.PickColorsCommand.Execute(null);
        viewModel.SelectImagePixel(0, 0);
        viewModel.CopySelectedColorCommand.Execute(null);
        Equal("#112233", clipboard.Text);
    }

    private static async Task TestWebP()
    {
        var source = MakePatternDocument(32, 20, false);
        var result = await Optimize(source, 32, 20, 80, ImageFileFormat.WebP);
        var path = Path.Combine(AppContext.BaseDirectory, "signature-test.png");
        try
        {
            await File.WriteAllBytesAsync(path, result.EncodedBytes);
            var loaded = await new ImageLoaderService().LoadAsync(path);
            Equal(ImageFileFormat.WebP, loaded.SourceFormat);
            Equal(32, loaded.PixelWidth); Equal(20, loaded.PixelHeight);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static async Task TestResize()
    {
        var result = await Optimize(MakePatternDocument(80, 40, false), 20, 10, 80, ImageFileFormat.Png);
        Equal(20, result.PreviewImage.PixelWidth); Equal(10, result.PreviewImage.PixelHeight);
    }

    private static async Task TestProportions()
    {
        var document = MakePatternDocument(400, 200, false);
        var vm = new MainWindowViewModel(new TestLoader(document), new TestDialog(), new ColorAnalysisService(), new TestClipboard());
        await vm.LoadImageAsync("synthetic.png");
        vm.TargetWidth = "100";
        Equal("50", vm.TargetHeight);
        vm.TargetHeight = "25";
        Equal("50", vm.TargetWidth);
    }

    private static Task TestPngDisablesQuality()
    {
        var vm = new MainWindowViewModel(new TestLoader(MakePatternDocument(8, 8, false)), new TestDialog(), new ColorAnalysisService(), new TestClipboard());
        vm.OutputFormat = ImageFileFormat.Png;
        Equal(false, vm.IsQualityEnabled);
        vm.OutputFormat = ImageFileFormat.Jpeg;
        Equal(true, vm.IsQualityEnabled);
        return Task.CompletedTask;
    }

    private static async Task TestQuality()
    {
        var source = MakePatternDocument(256, 256, false);
        var low = await Optimize(source, 256, 256, 15, ImageFileFormat.Jpeg);
        var high = await Optimize(source, 256, 256, 95, ImageFileFormat.Jpeg);
        True(low.OptimizedSizeBytes < high.OptimizedSizeBytes, "Lower JPEG quality should produce fewer bytes for the synthetic pattern.");
    }

    private static async Task TestPngTransparency() => await VerifyAlpha(ImageFileFormat.Png, true);
    private static async Task TestWebPTransparency() => await VerifyAlpha(ImageFileFormat.WebP, true);
    private static async Task TestJpegTransparency() => await VerifyAlpha(ImageFileFormat.Jpeg, false);

    private static async Task VerifyAlpha(ImageFileFormat format, bool expectedAlpha)
    {
        var result = await Optimize(MakePatternDocument(16, 16, true), 16, 16, 85, format);
        var pixels = new byte[16 * 16 * 4];
        result.PreviewImage.CopyPixels(pixels, 16 * 4, 0);
        var hasAlpha = Enumerable.Range(0, 16 * 16).Any(i => pixels[i * 4 + 3] < 255);
        Equal(expectedAlpha, hasAlpha);
    }

    private static async Task TestInvalidDimensions()
    {
        var source = MakePatternDocument(4, 4, false);
        await Throws<ImageOptimizationException>(() => new ImageOptimizationService().OptimizeAsync(
            new ImageOptimizationRequest(source, 0, 4, 80, ImageFileFormat.Png)));
    }

    private static async Task TestCancellation()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Throws<OperationCanceledException>(() => new ImageOptimizationService().OptimizeAsync(
            new ImageOptimizationRequest(MakePatternDocument(64, 64, false), 32, 32, 80, ImageFileFormat.WebP), cancellation.Token));
    }

    private static async Task TestOrientation()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "oriented.jpg");
        try
        {
            byte[] pixels = [0,0,255,255, 0,255,0,255, 255,0,0,255, 255,255,255,255, 0,0,0,255, 128,128,128,255];
            var bitmap = BitmapSource.Create(3, 2, 96, 96, PixelFormats.Bgra32, null, pixels, 12);
            var encoder = new JpegBitmapEncoder { QualityLevel = 100 }; encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var memory = new MemoryStream(); encoder.Save(memory);
            var jpeg = memory.ToArray();
            byte[] exif = [0xFF,0xE1,0x00,0x22, 0x45,0x78,0x69,0x66,0,0, 0x4D,0x4D,0,0x2A,0,0,0,8, 0,1, 0x01,0x12,0,3,0,0,0,1,0,6,0,0, 0,0,0,0];
            var oriented = new byte[jpeg.Length + exif.Length];
            Buffer.BlockCopy(jpeg, 0, oriented, 0, 2); Buffer.BlockCopy(exif, 0, oriented, 2, exif.Length); Buffer.BlockCopy(jpeg, 2, oriented, 2 + exif.Length, jpeg.Length - 2);
            await File.WriteAllBytesAsync(path, oriented);
            var loaded = await new ImageLoaderService().LoadAsync(path);
            Equal(2, loaded.PixelWidth); Equal(3, loaded.PixelHeight);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static async Task TestSafeExport()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "safe-export"); Directory.CreateDirectory(folder);
        var sourcePath = Path.Combine(folder, "original.png"); var outputPath = Path.Combine(folder, "result.webp");
        try
        {
            await File.WriteAllTextAsync(sourcePath, "original bytes");
            var source = MakePatternDocument(20, 20, true) with { FilePath = sourcePath };
            var result = await Optimize(source, 10, 10, 80, ImageFileFormat.WebP);
            var service = new ImageOptimizationService();
            await Throws<ImageOptimizationException>(() => service.SaveAsync(result, sourcePath, true));
            Equal("original bytes", await File.ReadAllTextAsync(sourcePath));
            await service.SaveAsync(result, outputPath, false);
            True(File.Exists(outputPath) && new FileInfo(outputPath).Length == result.OptimizedSizeBytes, "Exported bytes do not match the preview result.");
            await Throws<ImageOptimizationException>(() => service.SaveAsync(result, outputPath, false));
            await service.SaveAsync(result, outputPath, true);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    private static Task<ImageOptimizationResult> Optimize(ImageDocument source, int width, int height, int quality, ImageFileFormat format) =>
        new ImageOptimizationService().OptimizeAsync(new ImageOptimizationRequest(source, width, height, quality, format));

    private static ImageDocument MakePatternDocument(int width, int height, bool transparency)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var i = (y * width + x) * 4;
            pixels[i] = (byte)((x * 17 + y * 11) & 255); pixels[i + 1] = (byte)((x * 5 + y * 23) & 255);
            pixels[i + 2] = (byte)((x * 29 + y * 3) & 255); pixels[i + 3] = transparency && x < width / 2 ? (byte)64 : (byte)255;
        }
        return MakeDocument(width, height, pixels);
    }

    private static ImageDocument MakeDocument(int width, int height, byte[] pixels)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return new ImageDocument("synthetic.png", "synthetic.png", pixels.Length, width, height, bitmap);
    }

    private static void SavePng(string path, BitmapSource bitmap)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static string ColorSignature(RepresentativeColor color) => $"{color.HexCode}:{color.CoveragePercentage:F6}";

    private static async Task Run(string name, Func<Task> test, ICollection<string> failures)
    {
        try { await test(); Console.WriteLine($"PASS  {name}"); }
        catch (Exception ex) { failures.Add($"{name}: {ex.Message}"); Console.WriteLine($"FAIL  {name}"); }
    }
    private static void Equal<T>(T expected, T actual)
    { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected '{expected}', got '{actual}'."); }
    private static void True(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Near(double expected, double actual, double tolerance)
    { if (Math.Abs(expected - actual) > tolerance) throw new InvalidOperationException($"Expected {expected} ± {tolerance}, got {actual}."); }
    private static async Task<T> Throws<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T ex) { return ex; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private sealed class TestLoader(ImageDocument document) : IImageLoaderService
    {
        public Task<ImageDocument> LoadAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(document);
    }

    private sealed class TestDialog : IFileDialogService
    {
        public string? ChooseImageToOpen() => null;
        public SaveImageSelection? ChooseImageToSave(string sourceFileName, ImageFileFormat format) => null;
    }

    private sealed class TestClipboard : IClipboardService
    {
        public string? Text { get; private set; }
        public void SetText(string text) => Text = text;
    }
}
