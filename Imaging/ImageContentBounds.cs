namespace ImagingCore;

/// <summary>
/// Pixel-level content bounds detected by border scanning.
/// All values are in pixels from the original image edges.
/// </summary>
public record ImageContentBounds
{
    /// <summary>Original image width in pixels.</summary>
    public int OriginalWidth { get; init; }

    /// <summary>Original image height in pixels.</summary>
    public int OriginalHeight { get; init; }

    /// <summary>Pixels to crop from the left edge.</summary>
    public int CropLeft { get; init; }

    /// <summary>Pixels to crop from the top edge.</summary>
    public int CropTop { get; init; }

    /// <summary>Pixels to crop from the right edge.</summary>
    public int CropRight { get; init; }

    /// <summary>Pixels to crop from the bottom edge.</summary>
    public int CropBottom { get; init; }

    /// <summary>Content area width after cropping.</summary>
    public int ContentWidth => OriginalWidth - CropLeft - CropRight;

    /// <summary>Content area height after cropping.</summary>
    public int ContentHeight => OriginalHeight - CropTop - CropBottom;

    /// <summary>Percentage of original area saved (removed).</summary>
    public double SavedPct
    {
        get
        {
            var originalArea = (double)OriginalWidth * OriginalHeight;
            var contentArea = (double)ContentWidth * ContentHeight;
            if (originalArea <= 0) return 0;
            return Math.Round((1.0 - contentArea / originalArea) * 100, 1);
        }
    }

    /// <summary>True if any border can be cropped.</summary>
    public bool HasCropMargins => CropLeft > 0 || CropTop > 0 || CropRight > 0 || CropBottom > 0;
}
