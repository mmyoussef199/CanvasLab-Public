using CanvasLab.Models;

namespace CanvasLab.Services;

public interface IColorAnalysisService
{
    Task<ColorAnalysisResult> AnalyzeAsync(ImageDocument document, CancellationToken cancellationToken = default);
    PixelColor SamplePixel(ImageDocument document, int x, int y);
}

