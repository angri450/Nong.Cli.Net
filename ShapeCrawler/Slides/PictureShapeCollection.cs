using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using ShapeCrawler.Extensions;
using ShapeCrawler.Presentations;
using P = DocumentFormat.OpenXml.Presentation;

namespace ShapeCrawler.Slides;

internal sealed class PictureShapeCollection(SlidePart slidePart, PresentationImageFiles imageFiles)
{
    internal void AddPicture(Stream imageStream)
    {
        throw new NotImplementedException("PictureShapeCollection.AddPicture requires rewrite (Magick.NET → SkiaSharp/ImageSharp).");
    }

    private int GetNextShapeId()
    {
        var shapeIds = slidePart.Slide!
            .Descendants<P.NonVisualDrawingProperties>()
            .Select(p => p.Id?.Value ?? 0U)
            .ToArray();

        return shapeIds.Length > 0 ? (int)shapeIds.Max() + 1 : 1;
    }
}
