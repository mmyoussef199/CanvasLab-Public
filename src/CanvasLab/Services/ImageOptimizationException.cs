namespace CanvasLab.Services;

public sealed class ImageOptimizationException : Exception
{
    public ImageOptimizationException(string message, Exception? innerException = null) : base(message, innerException) { }
}
