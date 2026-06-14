using System.Text;

namespace PdfCore;

public static class PdfNongMarkTextWriter
{
    public static string Write(PdfDocumentModel document)
    {
        var sb = new StringBuilder();
        foreach (var page in document.Pages.OrderBy(p => p.Page))
        {
            sb.AppendLine($"::: page {{#page-{page.Page:D4} number={page.Page} width={PdfUtilities.FormatDouble(page.Width)} height={PdfUtilities.FormatDouble(page.Height)} unit=pt}}");
            sb.AppendLine();

            foreach (var block in document.Blocks.Where(b => b.Page == page.Page && b.Kind != "pageBreak").OrderBy(b => b.Index))
            {
                WriteBlock(sb, block);
                sb.AppendLine();
            }

            sb.AppendLine(":::");
            sb.AppendLine();
        }

        foreach (var warning in document.Warnings)
        {
            sb.AppendLine($"::: warning {{source=inferred}}");
            sb.AppendLine(EscapeText(warning));
            sb.AppendLine(":::");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    static void WriteBlock(StringBuilder sb, PdfContentBlock block)
    {
        if (block.Kind == "heading")
        {
            sb.AppendLine($"# {InlineText(block)} {Attributes(block)}");
            return;
        }

        if (block.Kind == "image")
        {
            var caption = EscapeText(block.Text ?? "image");
            var path = string.IsNullOrWhiteSpace(block.AssetPath) ? "" : block.AssetPath.Replace('\\', '/');
            sb.AppendLine($"![{caption}]({path}){Attributes(block)}");
            return;
        }

        if (block.Kind == "table")
        {
            sb.AppendLine($"::: table {Attributes(block)}");
            sb.AppendLine(block.Text ?? "<table></table>");
            sb.AppendLine(":::");
            return;
        }

        var type = block.Kind == "ocrText" ? "ocrText" : "paragraph";
        sb.AppendLine($"::: {type} {Attributes(block)}");
        sb.AppendLine(InlineText(block));
        sb.AppendLine(":::");
    }

    static string Attributes(PdfContentBlock block)
    {
        var attrs = new List<string>
        {
            $"#{block.BlockId}",
            $"page={block.Page}",
        };
        if (block.Bbox.Length >= 4)
            attrs.Add($"bbox=\"{PdfUtilities.FormatBbox(block.Bbox)}\"");
        attrs.Add($"source={block.Source}");
        if (block.Format?.Font is { Length: > 0 } font)
            attrs.Add($"font=\"{EscapeAttr(font)}\"");
        if (block.Format?.Size is { } size)
            attrs.Add($"size={PdfUtilities.FormatDouble(size)}");
        if (block.Format?.Align is { Length: > 0 } align)
            attrs.Add($"align={align}");
        if (block.AssetId is { Length: > 0 } assetId)
            attrs.Add($"asset={assetId}");
        if (block.CaptionBlockId is { Length: > 0 } captionBlockId)
            attrs.Add($"captionBlock={captionBlockId}");
        if (block.Confidence is { Length: > 0 } confidence)
            attrs.Add($"confidence={confidence}");
        return "{" + string.Join(" ", attrs) + "}";
    }

    static string InlineText(PdfContentBlock block)
    {
        if (block.Runs.Count == 0)
            return EscapeText(block.Text ?? "");

        var sb = new StringBuilder();
        for (var i = 0; i < block.Runs.Count; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(FormatRun(block.Runs[i]));
        }

        return sb.ToString();
    }

    static string FormatRun(PdfRun run)
    {
        var text = EscapeText(run.Text);
        var bold = run.Format?.Bold == true;
        var italic = run.Format?.Italic == true;
        if (bold && italic) return $"***{text}***";
        if (bold) return $"**{text}**";
        if (italic) return $"*{text}*";
        return text;
    }

    static string EscapeText(string value) =>
        value.Replace("\\", "\\\\").Replace("\r", " ").Replace("\n", " ").Trim();

    static string EscapeAttr(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
