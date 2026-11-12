using System.Windows.Media.Imaging;

namespace CanvasLab.Models;

public sealed record ImageDocument(string FilePath, string FileName, long FileSizeBytes,
    int PixelWidth, int PixelHeight, BitmapSource ImageSource,
    ImageFileFormat SourceFormat = ImageFileFormat.Png, bool HasAlpha = false);

