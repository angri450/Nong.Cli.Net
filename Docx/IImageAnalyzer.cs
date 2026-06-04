namespace DocxCore;

/// <summary>
/// Interface for image analysis. The concrete implementation (MultiModal.ImageAnalyzer)
/// is wired by the coordinator. WordSlice accepts null and writes "not available".
/// </summary>
public interface IImageAnalyzer
{
    /// <summary>Analyze an image file. Returns null if analysis is not possible.</summary>
    ImageAnalysis? Analyze(string imagePath);
}
