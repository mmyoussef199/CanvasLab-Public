namespace CanvasLab.Utilities;

public static class ImageCoordinateMapper
{
    public static (int X, int Y)? MapToPixel(double x, double y, double displayWidth, double displayHeight,
        int pixelWidth, int pixelHeight)
    {
        if (displayWidth <= 0 || displayHeight <= 0 || pixelWidth <= 0 || pixelHeight <= 0 ||
            x < 0 || y < 0 || x > displayWidth || y > displayHeight)
            return null;

        var pixelX = Math.Min(pixelWidth - 1, (int)Math.Floor(x / displayWidth * pixelWidth));
        var pixelY = Math.Min(pixelHeight - 1, (int)Math.Floor(y / displayHeight * pixelHeight));
        return (pixelX, pixelY);
    }
}
