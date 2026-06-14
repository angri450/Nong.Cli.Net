using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace DocxCore;

/// <summary>
/// Replaces images in a DOCX file with modified versions.
/// Used by nong word images --crop to swap cropped images back into the package.
/// </summary>
public static class DocxImageEditor
{
    /// <summary>
    /// Replace images in a DOCX using a transform function.
    /// The function receives (relationshipId, contentType, originalBytes) and returns modified bytes.
    /// Source file is never modified; output goes to a new file.
    /// </summary>
    public static DocxImageEditResult ReplaceImages(
        string inputPath,
        string outputPath,
        Func<string, string, byte[], byte[]> transform)
    {
        var changed = new List<string>();
        var skipped = new List<string>();
        int total = 0;

        File.Copy(inputPath, outputPath, overwrite: true);

        using var doc = WordprocessingDocument.Open(outputPath, true);
        var mainPart = doc.MainDocumentPart;
        if (mainPart == null)
            return new DocxImageEditResult(changed, skipped, total);

        var body = mainPart.Document?.Body;
        foreach (var imagePart in mainPart.ImageParts.ToList())
        {
            total++;
            string relId = mainPart.GetIdOfPart(imagePart);
            string contentType = imagePart.ContentType;
            byte[] originalBytes;
            using (var stream = imagePart.GetStream())
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                originalBytes = ms.ToArray();
            }

            byte[] newBytes;
            try
            {
                newBytes = transform(relId, contentType, originalBytes);
            }
            catch
            {
                skipped.Add(relId);
                continue;
            }

            // Write modified bytes back
            using (var stream = imagePart.GetStream(FileMode.Create, FileAccess.Write))
            {
                stream.Write(newBytes, 0, newBytes.Length);
                stream.SetLength(newBytes.Length);
            }

            changed.Add(relId);
        }

        return new DocxImageEditResult(changed, skipped, total);
    }

    /// <summary>
    /// Extract image bytes by relationship ID for analysis.
    /// </summary>
    public static List<DocxImageBytes> ExtractImageBytes(string docxPath)
    {
        var result = new List<DocxImageBytes>();
        using var doc = WordprocessingDocument.Open(docxPath, false);
        var mainPart = doc.MainDocumentPart;
        if (mainPart == null) return result;

        int index = 0;
        foreach (var imagePart in mainPart.ImageParts)
        {
            index++;
            string relId = mainPart.GetIdOfPart(imagePart);
            string id = $"img{index:D4}";

            using var stream = imagePart.GetStream();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);

            result.Add(new DocxImageBytes(
                ImageId: id,
                RelationshipId: relId,
                ContentType: imagePart.ContentType,
                Extension: ContentTypeToExt(imagePart.ContentType),
                Bytes: ms.ToArray()
            ));
        }
        return result;
    }

    private static string ContentTypeToExt(string ct) => ct switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/bmp" => ".bmp",
        _ => ".bin"
    };
}

public sealed record DocxImageEditResult(List<string> Changed, List<string> Skipped, int Total);

public sealed record DocxImageBytes(string ImageId, string RelationshipId, string ContentType, string Extension, byte[] Bytes);
