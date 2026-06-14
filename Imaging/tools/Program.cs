using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Text.Json;
using DocxCore;
using ImagingCore;
using Nong.Cli.Common;

var jsonOpt = new Option<bool>("--json", () => false, "Output structured JSON");
var root = new RootCommand("nong-imaging — image analysis and cropping");
root.AddGlobalOption(jsonOpt);

var imageFileArg = new Argument<string>("file", "Image file path");
var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output path");

var cropCmd = new Command("crop", "Auto-crop blank margins from an image") { imageFileArg, outOpt };
cropCmd.SetHandler((string file, string? output, bool json) =>
{
    try
    {
        if (!File.Exists(file))
        {
            CliHelpers.WriteError("crop", ErrorCodes.FileNotFound with { Message = $"File not found: {file}" }, json);
            return;
        }

        var bytes = File.ReadAllBytes(file);
        var processor = new ImageProcessor();
        var result = processor.AutoCrop(bytes);
        var ext = Path.GetExtension(file);
        var outPath = output ?? Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(file)) ?? ".",
            Path.GetFileNameWithoutExtension(file) + ".cropped" + ext);
        CliHelpers.EnsureParentDir(outPath);
        File.WriteAllBytes(outPath, result);

        if (json)
        {
            var o = JsonOutput.Ok("crop", "Cropped", new { output = Path.GetFullPath(outPath) });
            o.Artifacts["image"] = Path.GetFullPath(outPath);
            Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
        }
        else
        {
            Console.WriteLine($"Cropped: {outPath}");
        }
    }
    catch (Exception ex)
    {
        CliHelpers.WriteError("crop", ErrorCodes.InternalError with { Message = ex.Message }, json);
    }
}, imageFileArg, outOpt, jsonOpt);

var analyzeCmd = new Command("analyze", "Analyze an image for crop margins") { imageFileArg };
analyzeCmd.SetHandler((string file, bool json) =>
{
    try
    {
        if (!File.Exists(file))
        {
            CliHelpers.WriteError("analyze", ErrorCodes.FileNotFound with { Message = $"File not found: {file}" }, json);
            return;
        }

        var bounds = AnalyzeImageBytes(File.ReadAllBytes(file));
        if (json)
        {
            var o = JsonOutput.Ok("analyze", "Analyzed", new { image = Path.GetFullPath(file), bounds });
            Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
        }
        else
        {
            Console.WriteLine($"{bounds.OriginalWidth}x{bounds.OriginalHeight} -> {bounds.ContentWidth}x{bounds.ContentHeight} ({bounds.SavedPct}% saved)");
        }
    }
    catch (Exception ex)
    {
        CliHelpers.WriteError("analyze", ErrorCodes.InternalError with { Message = ex.Message }, json);
    }
}, imageFileArg, jsonOpt);

var docxArg = new Argument<string>("file", "DOCX file path");
var analyzeOpt = new Option<bool>("--analyze", "Analyze embedded images without modifying the file");
var cropOpt = new Option<bool>("--crop", "Auto-crop embedded images and write a new DOCX");
var imagesCmd = new Command("images", "Analyze or crop embedded images in a DOCX") { docxArg, outOpt, analyzeOpt, cropOpt };
imagesCmd.SetHandler((string file, string? output, bool analyze, bool crop, bool json) =>
{
    var err = CliHelpers.ValidateDocxFile(file);
    if (err != null)
    {
        CliHelpers.WriteError("images", err, json);
        return;
    }

    if (!analyze && !crop)
    {
        CliHelpers.WriteError("images", ErrorCodes.MissingArgument with { Message = "Specify --analyze or --crop." }, json);
        return;
    }

    try
    {
        if (analyze)
        {
            AnalyzeDocxImages(file, json);
            return;
        }

        CropDocxImages(file, output, json);
    }
    catch (Exception ex)
    {
        CliHelpers.WriteError("images", ErrorCodes.InternalError with { Message = ex.Message }, json);
    }
}, docxArg, outOpt, analyzeOpt, cropOpt, jsonOpt);

root.AddCommand(cropCmd);
root.AddCommand(analyzeCmd);
root.AddCommand(imagesCmd);
await new CommandLineBuilder(root).UseDefaults().Build().InvokeAsync(args);

static ImageContentBounds AnalyzeImageBytes(byte[] bytes)
{
    var processor = new ImageProcessor();
    return processor.Analyze(bytes);
}

static void AnalyzeDocxImages(string file, bool json)
{
    var images = DocxImageEditor.ExtractImageBytes(file);
    var results = new List<DocxImageAnalyzeResult>();

    foreach (var img in images)
    {
        try
        {
            var bounds = AnalyzeImageBytes(img.Bytes);
            results.Add(new DocxImageAnalyzeResult(
                img.ImageId,
                img.ContentType,
                img.RelationshipId,
                bounds.OriginalWidth,
                bounds.OriginalHeight,
                bounds.CropLeft,
                bounds.CropTop,
                bounds.CropRight,
                bounds.CropBottom,
                bounds.ContentWidth,
                bounds.ContentHeight,
                bounds.SavedPct,
                bounds.HasCropMargins,
                null));
        }
        catch (Exception ex)
        {
            results.Add(new DocxImageAnalyzeResult(
                img.ImageId,
                img.ContentType,
                img.RelationshipId,
                0, 0, 0, 0, 0, 0, 0, 0, 0, false,
                ex.Message));
        }
    }

    var summary = $"{results.Count(r => r.HasCropMargins)}/{results.Count} images have crop margins";
    if (json)
    {
        var output = JsonOutput.Ok("images", summary, new { images = results });
        output.Metrics["images"] = results.Count;
        output.Metrics["croppable"] = results.Count(r => r.HasCropMargins);
        Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
    }
    else
    {
        Console.WriteLine(summary);
        foreach (var r in results)
        {
            if (!string.IsNullOrEmpty(r.Error))
                Console.Error.WriteLine($"[ERR] {r.ImageId}: {r.Error}");
            else if (r.HasCropMargins)
                Console.WriteLine($"  {r.ImageId} crop: L={r.CropLeft} T={r.CropTop} R={r.CropRight} B={r.CropBottom} saved={r.SavedPct}% ({r.ContentWidth}x{r.ContentHeight})");
            else
                Console.WriteLine($"  {r.ImageId} no crop margins");
        }
    }
}

static void CropDocxImages(string file, string? output, bool json)
{
    var outputPath = output ?? Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(file)) ?? ".",
        Path.GetFileNameWithoutExtension(file) + ".cropped.docx");
    CliHelpers.EnsureParentDir(outputPath);

    var processor = new ImageProcessor();
    var result = DocxImageEditor.ReplaceImages(file, outputPath, (_, _, bytes) => processor.AutoCrop(bytes));
    var summary = $"Cropped: {result.Changed.Count}/{result.Total} images, skipped: {result.Skipped.Count}";

    if (json)
    {
        var outputJson = JsonOutput.Ok("images", summary, new
        {
            output = Path.GetFullPath(outputPath),
            changed = result.Changed,
            skipped = result.Skipped,
            total = result.Total
        });
        outputJson.Artifacts["docx"] = Path.GetFullPath(outputPath);
        outputJson.Metrics["changed"] = result.Changed.Count;
        outputJson.Metrics["skipped"] = result.Skipped.Count;
        outputJson.Metrics["total"] = result.Total;
        Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
    }
    else
    {
        Console.WriteLine($"{summary} -> {outputPath}");
    }
}

sealed record DocxImageAnalyzeResult(
    string ImageId,
    string ContentType,
    string RelationshipId,
    int OriginalWidth,
    int OriginalHeight,
    int CropLeft,
    int CropTop,
    int CropRight,
    int CropBottom,
    int ContentWidth,
    int ContentHeight,
    double SavedPct,
    bool HasCropMargins,
    string? Error);
