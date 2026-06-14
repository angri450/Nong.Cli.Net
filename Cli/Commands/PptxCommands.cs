using System.CommandLine;
using System.IO.Compression;
using System.Text.Json;
using Nong.Cli.Common;
using PptxCore;

namespace Nong.Cli.Commands;

/// <summary>Pptx command group: read, slides.</summary>
public static class PptxCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("pptx", "PowerPoint operations");
        cmd.AddCommand(CreateRead(jsonOpt));
        cmd.AddCommand(CreateSlides(jsonOpt));
        cmd.AddCommand(CreateDissect(jsonOpt));
        cmd.AddCommand(CreateCreatePptx(jsonOpt));
        return cmd;
    }

    // ===== pptx read =====

    static Command CreateRead(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pptx file");
        var cmd = new Command("read", "Extract slide text") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = ValidatePptxFile(file);
            if (err != null) { CliHelpers.WriteError("pptx read", err, json); return; }

            try
            {
                var (result, elapsed) = CliHelpers.Time(() => PptxReader.Read(file));

                if (json)
                {
                    var data = new
                    {
                        text = result.Text,
                        slides = result.Slides.Select(s => new
                        {
                            index = s.Index,
                            title = s.Title,
                            texts = s.Texts,
                            background = s.Background,
                            runs = s.Runs
                        }).ToList()
                    };
                    var metrics = new Dictionary<string, object>
                    {
                        ["slides"] = result.Slides.Count,
                        ["textBlocks"] = result.Slides.Sum(s => s.Texts.Count),
                        ["characters"] = result.Text.Length
                    };
                    var output = JsonOutput.Ok("pptx read", $"Extracted {result.Slides.Count} slides, {metrics["textBlocks"]} text blocks", data);
                    foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.Write(result.Text);
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("pptx read", ErrorCodes.ReadFailed with { Message = ex.Message }, json);
            }

        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== pptx slides =====

    static Command CreateSlides(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pptx file");
        var cmd = new Command("slides", "List slide structure") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = ValidatePptxFile(file);
            if (err != null) { CliHelpers.WriteError("pptx slides", err, json); return; }

            try
            {
                var (result, elapsed) = CliHelpers.Time(() => PptxReader.Slides(file));

                if (json)
                {
                    var data = new
                    {
                        slides = result.Slides.Select(s => new
                        {
                            index = s.Index,
                            shapeCount = s.ShapeCount,
                            textCount = s.TextCount,
                            pictureCount = s.PictureCount,
                            tableCount = s.TableCount,
                            chartCount = s.ChartCount,
                            title = s.Title
                        }).ToList()
                    };
                    var metrics = new Dictionary<string, object>
                    {
                        ["slides"] = result.Slides.Count,
                        ["totalShapes"] = result.Slides.Sum(s => s.ShapeCount),
                        ["totalPictures"] = result.Slides.Sum(s => s.PictureCount),
                        ["totalTables"] = result.Slides.Sum(s => s.TableCount),
                        ["totalCharts"] = result.Slides.Sum(s => s.ChartCount)
                    };
                    var output = JsonOutput.Ok("pptx slides", $"Analyzed {result.Slides.Count} slides", data);
                    foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
                    output.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
                }
                else
                {
                    foreach (var s in result.Slides)
                    {
                        Console.WriteLine($"Slide {s.Index}: {s.ShapeCount} shapes, {s.TextCount} text, {s.PictureCount} pics, {s.TableCount} tables, {s.ChartCount} charts");
                        if (!string.IsNullOrEmpty(s.Title))
                            Console.WriteLine($"  Title: {s.Title}");
                    }
                }
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("pptx slides", ErrorCodes.ReadFailed with { Message = ex.Message }, json);
            }

        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== pptx dissect =====

    static Command CreateDissect(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .pptx file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output directory for NongPandoc slice") { IsRequired = true };
        var cmd = new Command("dissect", "Slice pptx into a NongPandoc package") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            var err = ValidatePptxFile(file);
            if (err != null) { CliHelpers.WriteError("pptx dissect", err, json); return; }

            try
            {
                CliHelpers.EnsureParentDir(Path.Combine(output, ".keep"));
                var (result, elapsed) = CliHelpers.Time(() => PptxSlice.Slice(file, output));
                if (json)
                {
                    var o = JsonOutput.Ok("pptx dissect",
                        $"Sliced: {result.SlideCount} slides, {result.BlockCount} blocks",
                        new { outputDir = result.OutputDir, slideCount = result.SlideCount, blockCount = result.BlockCount, warnings = result.Warnings });
                    o.Artifacts["dir"] = Path.GetFullPath(output);
                    o.Metrics["slides"] = result.SlideCount;
                    o.Metrics["blocks"] = result.BlockCount;
                    o.Metrics["warnings"] = result.Warnings.Count;
                    o.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Sliced to {Path.GetFullPath(output)}: {result.SlideCount} slides, {result.BlockCount} blocks");
                    foreach (var warning in result.Warnings)
                        Console.Error.WriteLine($"[WARN] {warning}");
                }
            }
            catch (FileNotFoundException ex)
            {
                CliHelpers.WriteError("pptx dissect", ErrorCodes.FileNotFound with { Message = ex.Message }, json);
            }
            catch (InvalidDataException ex)
            {
                CliHelpers.WriteError("pptx dissect", ErrorCodes.UnsupportedFormat with { Message = ex.Message }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("pptx dissect", ErrorCodes.ReadFailed with { Message = ex.Message }, json);
            }

        }, fileArg, outOpt, jsonOpt);

        return cmd;
    }

    // ===== pptx create =====

    static Command CreateCreatePptx(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to spec JSON");
        var outOpt = new Option<string>("-o", "Output .pptx path") { IsRequired = true };
        var cmd = new Command("create", "Create pptx from JSON spec") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("pptx create", err, json); return; }

            try
            {
                var jsonText = File.ReadAllText(file);
                var spec = JsonSerializer.Deserialize<PptxCreateSpec>(jsonText, CliHelpers.JsonOpts);
                if (spec?.Slides == null || spec.Slides.Count == 0)
                {
                    CliHelpers.WriteError("pptx create",
                        ErrorCodes.ValidationFailed with { Message = "slides array must be non-empty." }, json);
                    return;
                }

                CliHelpers.EnsureParentDir(output);
                var (slideCount, elapsed) = CliHelpers.Time<int>(() =>
                {
                    BuildPptx(output, spec.Slides);
                    return spec.Slides.Count;
                });

                var aerr = CliHelpers.CheckArtifact(output, "PPTX");
                if (aerr != null) { CliHelpers.WriteError("pptx create", aerr, json); return; }

                if (json)
                {
                    var o = JsonOutput.Ok("pptx create",
                        $"Created PPTX with {slideCount} slides", new { slides = slideCount });
                    o.Artifacts["pptx"] = Path.GetFullPath(output);
                    o.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else { Console.WriteLine($"Created: {Path.GetFullPath(output)} ({slideCount} slides)"); }
            }
            catch (JsonException jex) { CliHelpers.WriteError("pptx create", ErrorCodes.ValidationFailed with { Message = $"Invalid JSON: {jex.Message}" }, json); }
            catch (Exception ex) { CliHelpers.WriteError("pptx create", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    /// <summary>Validate .pptx extension.</summary>
    static ErrorEntry? ValidatePptxFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ErrorCodes.MissingArgument with { Message = "File path is required." };
        if (!File.Exists(path))
            return ErrorCodes.FileNotFound with { Message = $"File not found: {path}" };
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".pptx")
            return ErrorCodes.UnsupportedFormat with { Message = $"Expected .pptx file, got: {ext}" };
        return null;
    }

    static void BuildPptx(string outputPath, List<PptxSlideSpec> slides)
    {
        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);

        // [Content_Types].xml
        var ct = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/ppt/presentation.xml"" ContentType=""application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml""/>
  <Override PartName=""/ppt/slideMasters/slideMaster1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml""/>
  <Override PartName=""/ppt/slideLayouts/slideLayout1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml""/>
  <Override PartName=""/ppt/theme/theme1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.theme+xml""/>";
        for (int i = 0; i < slides.Count; i++)
            ct += System.Environment.NewLine + "  <Override PartName=\"/ppt/slides/slide" + (i + 1) + ".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slide+xml\"/>";
        ct += "\n</Types>";
        WriteZipEntry(zip, "[Content_Types].xml", ct);

        // _rels/.rels
        WriteZipEntry(zip, "_rels/.rels", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""ppt/presentation.xml""/>
</Relationships>");

        // ppt/_rels/presentation.xml.rels
        var presRels = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster"" Target=""slideMasters/slideMaster1.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme"" Target=""theme/theme1.xml""/>";
        for (int i = 0; i < slides.Count; i++)
            presRels += System.Environment.NewLine + "  <Relationship Id=\"rId" + (100 + i) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide\" Target=\"slides/slide" + (i + 1) + ".xml\"/>";
        presRels += "\n</Relationships>";
        WriteZipEntry(zip, "ppt/_rels/presentation.xml.rels", presRels);

        // ppt/presentation.xml
        var presXml = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<p:presentation xmlns:a=""http://schemas.openxmlformats.org/drawingml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"" xmlns:p=""http://schemas.openxmlformats.org/presentationml/2006/main"">
  <p:sldMasterIdLst><p:sldMasterId id=""2147483648"" r:id=""rId1""/></p:sldMasterIdLst>
  <p:sldIdLst>";
        for (int i = 0; i < slides.Count; i++)
            presXml += System.Environment.NewLine + "    <p:sldId id=\"" + (256 + i) + "\" r:id=\"rId" + (100 + i) + "\"/>";
        presXml += $@"
  </p:sldIdLst>
  <p:sldSz cx=""12192000"" cy=""6858000""/>
</p:presentation>";
        WriteZipEntry(zip, "ppt/presentation.xml", presXml);

        // ppt/slideMasters/slideMaster1.xml
        WriteZipEntry(zip, "ppt/slideMasters/_rels/slideMaster1.xml.rels", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout"" Target=""../slideLayouts/slideLayout1.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme"" Target=""../theme/theme1.xml""/>
</Relationships>");
        WriteZipEntry(zip, "ppt/slideMasters/slideMaster1.xml", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<p:sldMaster xmlns:a=""http://schemas.openxmlformats.org/drawingml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"" xmlns:p=""http://schemas.openxmlformats.org/presentationml/2006/main"">
  <p:cSld><p:spTree><p:nvGrpSpPr><p:cNvPr id=""1"" name=""""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/></p:spTree></p:cSld>
  <p:sldLayoutIdLst><p:sldLayoutId id=""2147483649"" r:id=""rId1""/></p:sldLayoutIdLst>
</p:sldMaster>");

        // ppt/slideLayouts/slideLayout1.xml
        WriteZipEntry(zip, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster"" Target=""../slideMasters/slideMaster1.xml""/>
</Relationships>");
        WriteZipEntry(zip, "ppt/slideLayouts/slideLayout1.xml", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<p:sldLayout xmlns:a=""http://schemas.openxmlformats.org/drawingml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"" xmlns:p=""http://schemas.openxmlformats.org/presentationml/2006/main"">
  <p:cSld><p:spTree><p:nvGrpSpPr><p:cNvPr id=""1"" name=""""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/></p:spTree></p:cSld>
</p:sldLayout>");

        // ppt/theme/theme1.xml (minimal)
        WriteZipEntry(zip, "ppt/theme/theme1.xml", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<a:theme xmlns:a=""http://schemas.openxmlformats.org/drawingml/2006/main"" name=""Default"">
  <a:themeElements>
    <a:clrScheme name=""Default""><a:dk1><a:sysClr val=""windowText"" lastClr=""000000""/></a:dk1><a:lt1><a:sysClr val=""window"" lastClr=""FFFFFF""/></a:lt1><a:dk2><a:srgbClr val=""44546A""/></a:dk2><a:lt2><a:srgbClr val=""E7E6E6""/></a:lt2><a:accent1><a:srgbClr val=""4472C4""/></a:accent1><a:accent2><a:srgbClr val=""ED7D31""/></a:accent2><a:accent3><a:srgbClr val=""A5A5A5""/></a:accent3><a:accent4><a:srgbClr val=""FFC000""/></a:accent4><a:accent5><a:srgbClr val=""5B9BD5""/></a:accent5><a:accent6><a:srgbClr val=""70AD47""/></a:accent6><a:hlink><a:srgbClr val=""0563C1""/></a:hlink><a:folHlink><a:srgbClr val=""954F72""/></a:folHlink></a:clrScheme>
    <a:fontScheme name=""Default""><a:majorFont><a:latin typeface=""Calibri Light""/><a:ea typeface=""""/><a:cs typeface=""""/></a:majorFont><a:minorFont><a:latin typeface=""Calibri""/><a:ea typeface=""""/><a:cs typeface=""""/></a:minorFont></a:fontScheme>
    <a:fmtScheme name=""Default""><a:fillStyleLst><a:solidFill><a:schemeClr val=""phClr""/></a:solidFill></a:fillStyleLst><a:lnStyleLst><a:ln w=""6350"" cap=""flat"" cmpd=""sng"" algn=""ctr""><a:solidFill><a:schemeClr val=""phClr""/></a:solidFill></a:ln></a:lnStyleLst><a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst><a:bgFillStyleLst><a:solidFill><a:schemeClr val=""phClr""/></a:solidFill></a:bgFillStyleLst></a:fmtScheme>
  </a:themeElements>
</a:theme>");

        // Generate slides
        for (int i = 0; i < slides.Count; i++)
        {
            var s = slides[i];
            var shapes = new System.Text.StringBuilder();
            int shapeId = 1;
            long y = 500000; // EMU from top

            if (!string.IsNullOrEmpty(s.Title))
            {
                shapes.AppendLine(BuildTextBox(shapeId++, s.Title, 800000, y, 10500000, 700000, true));
                y += 900000;
            }
            if (!string.IsNullOrEmpty(s.Subtitle))
            {
                shapes.AppendLine(BuildTextBox(shapeId++, s.Subtitle, 800000, y, 10500000, 450000, false));
                y += 600000;
            }
            if (s.Items != null)
            {
                foreach (var item in s.Items)
                {
                    shapes.AppendLine(BuildTextBox(shapeId++, $"  {item}", 800000, y, 10500000, 350000, false));
                    y += 400000;
                }
            }

            var slideXml = $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<p:sld xmlns:a=""http://schemas.openxmlformats.org/drawingml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"" xmlns:p=""http://schemas.openxmlformats.org/presentationml/2006/main"">
  <p:cSld><p:spTree><p:nvGrpSpPr><p:cNvPr id=""1"" name=""""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>
{shapes}
  </p:spTree></p:cSld>
</p:sld>";
            WriteZipEntry(zip, $"ppt/slides/slide{i + 1}.xml", slideXml);
        }
    }

    static string BuildTextBox(int id, string text, long x, long y, long w, long h, bool isBold)
    {
        return $@"    <p:sp>
      <p:nvSpPr><p:cNvPr id=""{id}"" name=""Text""/><p:cNvSpPr txBox=""1""/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x=""{x}"" y=""{y}""/><a:ext cx=""{w}"" cy=""{h}""/></a:xfrm><a:prstGeom prst=""rect""><a:avLst/></a:prstGeom></p:spPr>
      <p:txBody><a:bodyPr/><a:lstStyle/><a:p>
        <a:r><a:rPr lang=""zh-CN"" sz=""{(isBold ? 3600 : 1800)}"" b=""{(isBold ? "1" : "0")}"" dirty=""0""/><a:t>{System.Net.WebUtility.HtmlEncode(text)}</a:t></a:r>
      </a:p></p:txBody>
    </p:sp>";
    }

    static void WriteZipEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new System.IO.StreamWriter(entry.Open(), System.Text.Encoding.UTF8);
        writer.Write(content);
    }
}

internal class PptxCreateSpec
{
    public string? Theme { get; set; }
    public List<PptxSlideSpec> Slides { get; set; } = new();
}

internal class PptxSlideSpec
{
    public string? Kind { get; set; }
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? Author { get; set; }
    public List<string>? Items { get; set; }
}
