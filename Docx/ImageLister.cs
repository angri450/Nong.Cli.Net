using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;

namespace DocxCore;

/// <summary>
/// Lists all images in a .docx document. For each image found in
/// MainDocumentPart.ImageParts AND referenced in the body, returns metadata
/// including dimensions via ImageHeaderReader. Optionally extracts images to disk.
/// Uses stable IDs: img0001+ and UsedBy (paragraph blockIds).
/// </summary>
public static class ImageLister
{
    public static ImageListResult ListImages(string docxPath, string? outputDir = null)
    {
        var images = new List<ImageInfo>();

        using var doc = WordprocessingDocument.Open(docxPath, false);
        var mainPart = doc.MainDocumentPart;
        if (mainPart == null) return new ImageListResult(images, "0 images (no main document part)");

        var body = mainPart.Document?.Body;

        // Build relId -> paragraph blockIds mapping by iterating body paragraphs
        var relToBlockIds = new Dictionary<string, List<string>>();
        if (body != null)
        {
            int hCounter = 0, pCounter = 0;
            foreach (var para in body.Elements<Paragraph>())
            {
                string blockId = AssignBlockId(para, ref hCounter, ref pCounter);

                foreach (var blip in para.Descendants<A.Blip>())
                {
                    if (blip.Embed?.Value is string rid && rid.Length > 0)
                    {
                        if (!relToBlockIds.ContainsKey(rid))
                            relToBlockIds[rid] = new List<string>();
                        if (!relToBlockIds[rid].Contains(blockId))
                            relToBlockIds[rid].Add(blockId);
                    }
                }
            }
        }

        // Ensure output directory if extracting
        if (outputDir != null)
            Directory.CreateDirectory(outputDir);

        int index = 0;
        foreach (var imagePart in mainPart.ImageParts)
        {
            index++;

            string relId = mainPart.GetIdOfPart(imagePart);
            var usedBy = relToBlockIds.TryGetValue(relId, out var ids)
                ? ids
                : new List<string>();

            // Get dimensions via temp file
            int? width = null;
            int? height = null;
            string tempPath = Path.GetTempFileName();
            try
            {
                using (var stream = imagePart.GetStream())
                using (var fs = File.Create(tempPath))
                {
                    stream.CopyTo(fs);
                }
                var dims = ImageHeaderReader.GetDimensions(tempPath);
                width = dims.Width;
                height = dims.Height;
            }
            catch
            {
                // Unsupported or corrupt image - leave dimensions null
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }

            string fileNameHint = ExtractFileName(imagePart.Uri);
            string id = $"img{index:D4}";

            images.Add(new ImageInfo(
                Id: id,
                ContentType: imagePart.ContentType,
                Width: width,
                Height: height,
                FileName: fileNameHint,
                InternalRelationshipId: relId,
                UsedBy: usedBy
            ));

            // Extract to output directory if requested
            if (outputDir != null)
            {
                string ext = ContentTypeToExtension(imagePart.ContentType);
                string outName = string.IsNullOrEmpty(fileNameHint)
                    ? $"{id}{ext}"
                    : $"{System.IO.Path.GetFileNameWithoutExtension(fileNameHint)}{ext}";

                // Avoid overwriting: append counter if file exists
                string outPath = System.IO.Path.Combine(outputDir, outName);
                int dedup = 1;
                while (File.Exists(outPath))
                {
                    string baseName = System.IO.Path.GetFileNameWithoutExtension(outName);
                    outPath = System.IO.Path.Combine(outputDir, $"{baseName}_{dedup}{ext}");
                    dedup++;
                }

                using (var stream = imagePart.GetStream())
                using (var fs = File.Create(outPath))
                {
                    stream.CopyTo(fs);
                }
            }
        }

        string summary = images.Count == 0
            ? "0 images"
            : $"{images.Count} image{(images.Count == 1 ? "" : "s")}";

        return new ImageListResult(images, summary);
    }

    /// <summary>Assign a stable block ID (h0001+ or p0001+) to a paragraph.</summary>
    private static string AssignBlockId(Paragraph para, ref int hCounter, ref int pCounter)
    {
        var styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        bool isHeading = IsHeadingStyle(styleId)
            || (para.ParagraphProperties?.OutlineLevel?.Val?.Value is int ol && ol >= 0);

        if (isHeading)
            return $"h{++hCounter:D4}";
        else
            return $"p{++pCounter:D4}";
    }

    private static bool IsHeadingStyle(string? styleId) => styleId switch
    {
        "Heading1" or "1" or "heading1" or "标题1" or "标题 1" or "Heading 1" => true,
        "Heading2" or "2" or "heading2" or "标题2" or "标题 2" or "Heading 2" => true,
        "Heading3" or "3" or "heading3" or "标题3" or "标题 3" or "Heading 3" => true,
        _ => !string.IsNullOrEmpty(styleId) && styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase),
    };

    private static string ExtractFileName(Uri uri)
    {
        // OpenXML part URIs are usually relative (for example
        // /word/media/image1.png). Uri.Segments throws for relative URIs, so
        // use the original text and split it manually.
        var raw = uri.OriginalString;
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var normalized = raw.Replace('\\', '/').TrimEnd('/');
        var slash = normalized.LastIndexOf('/');
        var last = slash >= 0 ? normalized[(slash + 1)..] : normalized;
        // Uri may encode spaces etc.; decode
        return Uri.UnescapeDataString(last);
    }

    private static string ContentTypeToExtension(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/bmp" => ".bmp",
        "image/tiff" => ".tiff",
        "image/x-wmf" => ".wmf",
        "image/x-emf" => ".emf",
        "image/svg+xml" => ".svg",
        _ => ".bin"
    };
}
