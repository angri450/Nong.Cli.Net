using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using ShapeCrawler.Drawing;
using ShapeCrawler.Extensions;
using ShapeCrawler.Presentations;
using ShapeCrawler.Units;
using P = DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;

namespace ShapeCrawler.Slides;

internal sealed class PictureShapeCollection(SlidePart slidePart, PresentationImageFiles imageFiles)
{
    internal void AddPicture(Stream imageStream) => AddPicture(0, 0, 0, 0, imageStream);

    internal void AddPicture(int x, int y, int w, int h, Stream imageStream)
    {
        var mime = DetectMimeType(imageStream);
        var imgStream = new ImageStream(imageStream);
        var hash = imgStream.Base64Hash;

        // Deduplicate: reuse existing image part if same content exists
        string imgPartRId;
        var existingPart = imageFiles.ImagePartByImageHashOrNull(hash);
        if (existingPart != null)
        {
            imgPartRId = slidePart.GetIdOfPart(existingPart);
        }
        else
        {
            imgPartRId = slidePart.AddImagePart(imageStream, mime);
        }

        var shapeId = (uint)GetNextShapeId();
        var nvpp = new P.NonVisualPictureProperties(
            new P.NonVisualDrawingProperties { Id = shapeId, Name = $"Picture {shapeId}" },
            new P.NonVisualPictureDrawingProperties(),
            new P.ApplicationNonVisualDrawingProperties());

        var blipFill = new P.BlipFill(
            new A.Blip { Embed = imgPartRId },
            new A.Stretch());

        // Use native image dimensions if w/h not specified
        long cx, cy;
        if (w > 0 && h > 0)
        {
            cx = new Points((decimal)w).AsEmus();
            cy = new Points((decimal)h).AsEmus();
        }
        else
        {
            // Default to reasonable size when dimensions unknown
            cx = new Points(300).AsEmus();
            cy = new Points(200).AsEmus();
        }

        var spPr = new P.ShapeProperties(
            new A.Transform2D(
                new A.Offset { X = new Points((decimal)x).AsEmus(), Y = new Points((decimal)y).AsEmus() },
                new A.Extents { Cx = cx, Cy = cy }),
            new A.PresetGeometry { Preset = A.ShapeTypeValues.Rectangle });

        var picture = new P.Picture(nvpp, blipFill, spPr);
        slidePart.Slide!.CommonSlideData!.ShapeTree!.Append(picture);
    }

    private static string DetectMimeType(Stream imageStream)
    {
        imageStream.Position = 0;
        var header = new byte[8];
        var read = imageStream.Read(header, 0, header.Length);
        imageStream.Position = 0;

        if (read >= 4)
        {
            // PNG: 89 50 4E 47
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                return "image/png";
            // JPEG: FF D8 FF
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return "image/jpeg";
            // GIF: 47 49 46
            if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46)
                return "image/gif";
            // BMP: 42 4D
            if (header[0] == 0x42 && header[1] == 0x4D)
                return "image/bmp";
        }
        return "image/png"; // default
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
