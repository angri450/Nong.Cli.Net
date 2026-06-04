using System.Text;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using D = DocumentFormat.OpenXml.Drawing;

namespace PptxCore;

/// <summary>
/// Read pptx content directly via OpenXML SDK.
/// </summary>
public static class PptxReader
{
    /// <summary>
    /// Extract all text from a pptx file.
    /// </summary>
    public static PptxReadResult Read(string path)
    {
        using var doc = PresentationDocument.Open(path, false);
        var presPart = doc.PresentationPart!;
        var slides = new List<PptxSlideText>();
        var allText = new StringBuilder();

        // Use SlideIdList for correct slide order, not SlideParts enumeration
        var slideIdList = presPart.Presentation.SlideIdList;
        if (slideIdList == null) return new PptxReadResult { Text = "", Slides = slides };

        int idx = 0;
        foreach (var slideId in slideIdList.ChildElements.OfType<SlideId>())
        {
            idx++;
            var slidePart = presPart.GetPartById(slideId.RelationshipId!) as SlidePart;
            if (slidePart == null) continue;
            var slideTexts = new List<string>();
            var title = "";

            // Try to get title from title placeholder
            var titleShape = slidePart.Slide.CommonSlideData?.ShapeTree?.Elements<Shape>()
                .FirstOrDefault(s => s.NonVisualShapeProperties
                    ?.ApplicationNonVisualDrawingProperties
                    ?.PlaceholderShape?.Type?.Value == PlaceholderValues.Title);
            if (titleShape != null)
            {
                title = string.Join(" ", titleShape.Descendants<D.Text>().Select(t => t.Text));
            }

            // Extract all text from all elements
            foreach (var text in slidePart.Slide.Descendants<D.Text>())
            {
                if (!string.IsNullOrWhiteSpace(text.Text))
                {
                    slideTexts.Add(text.Text);
                    allText.AppendLine(text.Text);
                }
            }

            slides.Add(new PptxSlideText
            {
                Index = idx,
                Title = title,
                Texts = slideTexts
            });
        }

        return new PptxReadResult
        {
            Text = allText.ToString().TrimEnd(),
            Slides = slides
        };
    }

    /// <summary>
    /// List slide structure: count shapes, texts, pictures, tables, charts per slide.
    /// </summary>
    public static PptxSlidesResult Slides(string path)
    {
        using var doc = PresentationDocument.Open(path, false);
        var presPart = doc.PresentationPart!;
        var slides = new List<PptxSlideInfo>();

        var slideIdList = presPart.Presentation.SlideIdList;
        if (slideIdList == null) return new PptxSlidesResult { Slides = slides };

        int idx = 0;
        foreach (var slideId in slideIdList.ChildElements.OfType<SlideId>())
        {
            idx++;
            var slidePart = presPart.GetPartById(slideId.RelationshipId!) as SlidePart;
            if (slidePart == null) continue;
            var tree = slidePart.Slide.CommonSlideData?.ShapeTree;
            if (tree == null)
            {
                slides.Add(new PptxSlideInfo { Index = idx });
                continue;
            }

            var elements = tree.ChildElements;
            int shapeCount = 0, textCount = 0, pictureCount = 0, tableCount = 0, chartCount = 0;
            var title = "";

            foreach (var el in elements)
            {
                if (el is Shape s)
                {
                    shapeCount++;
                    if (s.TextBody != null) textCount++;

                    var ph = s.NonVisualShapeProperties
                        ?.ApplicationNonVisualDrawingProperties
                        ?.PlaceholderShape;
                    if (ph?.Type?.Value == PlaceholderValues.Title && string.IsNullOrEmpty(title))
                    {
                        title = string.Join(" ", s.Descendants<D.Text>().Select(t => t.Text));
                    }
                }
                else if (el is Picture)
                {
                    pictureCount++;
                }
                else if (el is GraphicFrame gf)
                {
                    var uri = gf.Graphic?.GraphicData?.Uri?.Value ?? "";
                    if (uri.Contains("/table")) tableCount++;
                    else if (uri.Contains("/chart")) chartCount++;
                    else shapeCount++;
                }
                else if (el is GroupShape gs)
                {
                    shapeCount++;
                    // Inspect group contents
                    foreach (var inner in gs.ChildElements)
                    {
                        if (inner is Shape ishp) { shapeCount++; if (ishp.TextBody != null) textCount++; }
                        else if (inner is Picture) pictureCount++;
                        else if (inner is GraphicFrame igf)
                        {
                            var iuri = igf.Graphic?.GraphicData?.Uri?.Value ?? "";
                            if (iuri.Contains("/table")) tableCount++;
                            else if (iuri.Contains("/chart")) chartCount++;
                            else shapeCount++;
                        }
                    }
                }
                else if (el is ConnectionShape)
                {
                    shapeCount++;
                }
                else
                {
                    shapeCount++;
                }
            }

            slides.Add(new PptxSlideInfo
            {
                Index = idx,
                ShapeCount = shapeCount,
                TextCount = textCount,
                PictureCount = pictureCount,
                TableCount = tableCount,
                ChartCount = chartCount,
                Title = title
            });
        }

        return new PptxSlidesResult { Slides = slides };
    }
}

public class PptxReadResult
{
    public string Text { get; set; } = "";
    public List<PptxSlideText> Slides { get; set; } = new();
}

public class PptxSlideText
{
    public int Index { get; set; }
    public string Title { get; set; } = "";
    public List<string> Texts { get; set; } = new();
}

public class PptxSlidesResult
{
    public List<PptxSlideInfo> Slides { get; set; } = new();
}

public class PptxSlideInfo
{
    public int Index { get; set; }
    public int ShapeCount { get; set; }
    public int TextCount { get; set; }
    public int PictureCount { get; set; }
    public int TableCount { get; set; }
    public int ChartCount { get; set; }
    public string Title { get; set; } = "";
}
