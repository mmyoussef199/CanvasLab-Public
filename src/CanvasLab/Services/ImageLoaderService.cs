using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CanvasLab.Models;
using SkiaSharp;

namespace CanvasLab.Services;

public sealed class ImageLoaderService : IImageLoaderService
{
    private const long MaximumPixels = 100_000_000;

    public Task<ImageDocument> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return Task.Run(() => LoadCore(filePath, cancellationToken), cancellationToken);
    }

    internal static BitmapSource DecodeToBitmapSource(ReadOnlySpan<byte> encodedBytes,
        out ImageFileFormat format, out bool hasAlpha, CancellationToken cancellationToken = default)
    {
        using var data = SKData.CreateCopy(encodedBytes);
        using var codec = SKCodec.Create(data) ?? throw new ImageLoadException("That file is damaged or is not a supported JPEG, PNG, or WebP image.");
        format = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Jpeg => ImageFileFormat.Jpeg,
            SKEncodedImageFormat.Png => ImageFileFormat.Png,
            SKEncodedImageFormat.Webp => ImageFileFormat.WebP,
            _ => throw new ImageLoadException("CanvasLab can open JPEG, PNG, and WebP images.")
        };

        var sourceInfo = codec.Info;
        if (sourceInfo.Width <= 0 || sourceInfo.Height <= 0 || (long)sourceInfo.Width * sourceInfo.Height > MaximumPixels)
            throw new ImageLoadException("That image is too large for CanvasLab to open safely.");
        var decodeInfo = new SKImageInfo(sourceInfo.Width, sourceInfo.Height, SKColorType.Bgra8888,
            SKAlphaType.Unpremul, SKColorSpace.CreateSrgb());
        var pixels = new byte[checked(decodeInfo.RowBytes * decodeInfo.Height)];
        var decodeResult = codec.GetPixels(decodeInfo, pixels);
        if (decodeResult is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            throw new ImageLoadException("That file is damaged or is not a supported image.");
        cancellationToken.ThrowIfCancellationRequested();

        var (oriented, width, height) = ApplyOrientation(pixels, sourceInfo.Width, sourceInfo.Height,
            codec.EncodedOrigin, cancellationToken);
        hasAlpha = false;
        for (var index = 3; index < oriented.Length; index += 4)
            if (oriented[index] != 255) { hasAlpha = true; break; }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, oriented, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static ImageDocument LoadCore(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(filePath))
            throw new ImageLoadException("That image could not be found. It may have been moved or deleted.");
        try
        {
            var fileInfo = new FileInfo(filePath);
            byte[] encodedBytes;
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                       FileShare.Read | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan))
            {
                if (stream.Length > int.MaxValue)
                    throw new ImageLoadException("That image is too large for CanvasLab to open safely.");
                encodedBytes = new byte[stream.Length];
                stream.ReadExactly(encodedBytes);
            }
            cancellationToken.ThrowIfCancellationRequested();
            var bitmap = DecodeToBitmapSource(encodedBytes, out var format, out var hasAlpha, cancellationToken);
            return new ImageDocument(fileInfo.FullName, fileInfo.Name, fileInfo.Length,
                bitmap.PixelWidth, bitmap.PixelHeight, bitmap, format, hasAlpha);
        }
        catch (ImageLoadException) { throw; }
        catch (UnauthorizedAccessException ex) { throw new ImageLoadException("CanvasLab does not have permission to open that image.", ex); }
        catch (IOException ex) { throw new ImageLoadException("CanvasLab could not read that image. Close other apps using it and try again.", ex); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        { throw new ImageLoadException("That file is damaged or is not a supported image.", ex); }
    }

    private static (byte[] Pixels, int Width, int Height) ApplyOrientation(byte[] source, int width, int height,
        SKEncodedOrigin origin, CancellationToken cancellationToken)
    {
        if (origin == SKEncodedOrigin.TopLeft) return (source, width, height);
        var swapsAxes = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var targetWidth = swapsAxes ? height : width;
        var targetHeight = swapsAxes ? width : height;
        var target = new byte[checked(targetWidth * targetHeight * 4)];
        for (var y = 0; y < height; y++)
        {
            if ((y & 127) == 0) cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                var (dx, dy) = origin switch
                {
                    SKEncodedOrigin.TopRight => (width - 1 - x, y),
                    SKEncodedOrigin.BottomRight => (width - 1 - x, height - 1 - y),
                    SKEncodedOrigin.BottomLeft => (x, height - 1 - y),
                    SKEncodedOrigin.LeftTop => (y, x),
                    SKEncodedOrigin.RightTop => (height - 1 - y, x),
                    SKEncodedOrigin.RightBottom => (height - 1 - y, width - 1 - x),
                    SKEncodedOrigin.LeftBottom => (y, width - 1 - x),
                    _ => (x, y)
                };
                Buffer.BlockCopy(source, (y * width + x) * 4, target, (dy * targetWidth + dx) * 4, 4);
            }
        }
        return (target, targetWidth, targetHeight);
    }
}
