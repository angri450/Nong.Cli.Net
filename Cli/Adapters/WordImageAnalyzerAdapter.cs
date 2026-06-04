namespace Nong.Cli.Adapters;

using MultiModalCore;
using DocxCore;

/// <summary>
/// Bridges DocxCore.IImageAnalyzer to MultiModalCore.ImageAnalyzer.
/// Lives in CLI layer to avoid circular dependency (Docx can't reference MultiModal).
/// </summary>
public class WordImageAnalyzerAdapter : IImageAnalyzer
{
    readonly ImageAnalyzer _analyzer = new();

    /// <summary>
    /// Analyze an image file using the pure .NET ImageAnalyzer.
    /// Returns null if analysis is not possible.
    /// </summary>
    public ImageAnalysis? Analyze(string imagePath)
    {
        try
        {
            var layout = _analyzer.Analyze(imagePath);
            return new ImageAnalysis
            {
                Engine = "ImageAnalyzer",
                NetLocal = true,
                Regions = layout.Regions.Select(r => new ImageAnalysisRegion
                {
                    X = r.X,
                    Y = r.Y,
                    Width = r.Width,
                    Height = r.Height,
                    Type = r.Type.ToString()
                }).ToList()
            };
        }
        catch
        {
            return null;
        }
    }
}
