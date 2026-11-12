namespace CanvasLab.Services;

public sealed class ImageLoadException : Exception
{
    public ImageLoadException(string message) : base(message) { }
    public ImageLoadException(string message, Exception innerException) : base(message, innerException) { }
}

