using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CanvasLab.Models;
using SkiaSharp;

namespace CanvasLab.Services;

public sealed class ImageOptimizationService : IImageOptimizationService
{
    public Task<ImageOptimizationResult> OptimizeAsync(ImageOptimizationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => OptimizeCore(request, cancellationToken), cancellationToken);

    public async Task SaveAsync(ImageOptimizationResult result, string filePath, bool overwriteApproved,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        if (string.Equals(fullPath, Path.GetFullPath(result.SourcePath), StringComparison.OrdinalIgnoreCase))
            throw new ImageOptimizationException("Choose a different file name. CanvasLab will not overwrite the original image.");
        if (File.Exists(fullPath) && !overwriteApproved)
            throw new ImageOptimizationException("A file with that name already exists. Choose another name or approve replacing it.");
        var directory = Path.GetDirectoryName(fullPath) ?? throw new ImageOptimizationException("Choose a valid folder for the saved image.");
        if (!Directory.Exists(directory)) throw new ImageOptimizationException("The selected folder no longer exists.");
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, result.EncodedBytes, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullPath, overwriteApproved);
        }
        catch (OperationCanceledException) { throw; }
        catch (UnauthorizedAccessException ex) { throw new ImageOptimizationException("CanvasLab does not have permission to save in that folder.", ex); }
        catch (IOException ex) { throw new ImageOptimizationException("CanvasLab could not save the image. Check the file name and available space, then try again.", ex); }
        finally { try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { } }
    }

    private static ImageOptimizationResult OptimizeCore(ImageOptimizationRequest request, CancellationToken cancellationToken)
    {
        if (request.TargetWidth is < 1 or > 50_000 || request.TargetHeight is < 1 or > 50_000 ||
            (long)request.TargetWidth * request.TargetHeight > 100_000_000)
            throw new ImageOptimizationException("Choose image dimensions between 1 and 50,000 pixels.");
        if (request.ImageQuality is < 1 or > 100)
            throw new ImageOptimizationException("Choose an image quality between 1 and 100.");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var source = ToSkBitmap(request.Document.ImageSource);
            using var resized = new SKBitmap(new SKImageInfo(request.TargetWidth, request.TargetHeight,
                SKColorType.Bgra8888, SKAlphaType.Unpremul, SKColorSpace.CreateSrgb()));
            if (!source.ScalePixels(resized, new SKSamplingOptions(SKCubicResampler.Mitchell)))
                throw new ImageOptimizationException("CanvasLab could not resize that image.");
            cancellationToken.ThrowIfCancellationRequested();
            using var encodeBitmap = request.OutputFormat == ImageFileFormat.Jpeg ? FlattenOnWhite(resized) : null;
            var bitmap = encodeBitmap ?? resized;
            using var pixmap = bitmap.PeekPixels();
            using var data = request.OutputFormat switch
            {
                ImageFileFormat.Jpeg => pixmap.Encode(SKEncodedImageFormat.Jpeg, request.ImageQuality),
                ImageFileFormat.Png => pixmap.Encode(SKEncodedImageFormat.Png, 100),
                ImageFileFormat.WebP => pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, request.ImageQuality)),
                _ => null
            };
            if (data is null) throw new ImageOptimizationException("CanvasLab could not create the smaller image.");
            var bytes = data.ToArray();
            var preview = ImageLoaderService.DecodeToBitmapSource(bytes, out _, out _, cancellationToken);
            return new ImageOptimizationResult(bytes, preview, request.OutputFormat,
                request.TargetWidth, request.TargetHeight, request.Document.FileSizeBytes, request.Document.FilePath);
        }
        catch (ImageOptimizationException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw new ImageOptimizationException("CanvasLab could not create the smaller image.", ex); }
    }

    private static SKBitmap ToSkBitmap(BitmapSource source)
    {
        BitmapSource converted = source.Format == PixelFormats.Bgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[checked(converted.PixelWidth * converted.PixelHeight * 4)];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        var bitmap = new SKBitmap(new SKImageInfo(converted.PixelWidth, converted.PixelHeight,
            SKColorType.Bgra8888, SKAlphaType.Unpremul, SKColorSpace.CreateSrgb()));
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        return bitmap;
    }

    private static SKBitmap FlattenOnWhite(SKBitmap source)
    {
        var flattened = new SKBitmap(new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888,
            SKAlphaType.Opaque, SKColorSpace.CreateSrgb()));
        using var canvas = new SKCanvas(flattened);
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(source, new SKRect(0, 0, source.Width, source.Height), new SKSamplingOptions());
        canvas.Flush();
        return flattened;
    }
}
