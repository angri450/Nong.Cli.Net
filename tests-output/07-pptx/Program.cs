using PptxCore;

try
{
    var outputDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                    ?? Directory.GetCurrentDirectory();

    var outputPath = Path.Combine(outputDir, "pptx-basic.pptx");

    Console.WriteLine($"[TEST] Pptx PresentationBuilder basic test");
    Console.WriteLine($"[TEST] Output: {outputPath}");

    var builder = SlideBuilder.Create()
        .Theme(ThemePreset.Professional)
        .PageNumbers(true);

    builder.AddTitleSlide(slide =>
    {
        slide.Title("Pptx Integration Test")
             .Subtitle("Testing PresentationBuilder API")
             .Author("Angri450.Nong Test Suite");
    });

    builder.AddContentSlide(slide =>
    {
        slide.Title("Test Results")
             .Bullets(
                 "SlideBuilder.Create() works correctly",
                 "AddTitleSlide creates proper title slides",
                 "AddContentSlide creates proper content slides",
                 "Save() produces a valid PPTX file");
    });

    var savedPath = builder.Save(outputPath);
    var fileInfo = new FileInfo(savedPath);

    if (!fileInfo.Exists)
    {
        Console.WriteLine($"[FAIL] Output file not found: {savedPath}");
        Environment.Exit(1);
    }

    if (fileInfo.Length == 0)
    {
        Console.WriteLine($"[FAIL] Output file is empty");
        Environment.Exit(1);
    }

    Console.WriteLine($"[PASS] Presentation saved: {savedPath} ({fileInfo.Length} bytes)");
    Environment.Exit(0);
}
catch (Exception ex)
{
    Console.WriteLine($"[FAIL] Exception: {ex}");
    Environment.Exit(1);
}
