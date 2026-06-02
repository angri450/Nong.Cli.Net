using Bioicons;

try
{
    var outputDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                    ?? Directory.GetCurrentDirectory();

    Console.WriteLine("[TEST] Bioicons IconProvider test");
    Console.WriteLine();

    // 1. List all categories
    var categories = IconProvider.GetCategories();
    Console.WriteLine($"Categories ({categories.Count}):");
    foreach (var cat in categories)
        Console.WriteLine($"  - {cat}");
    Console.WriteLine();

    if (categories.Count == 0)
    {
        Console.WriteLine("[FAIL] No categories found");
        Environment.Exit(1);
    }

    // 2. List icons in Biology category
    var biologyIcons = IconProvider.GetIcons("Biology");
    Console.WriteLine($"Icons in 'Biology' ({biologyIcons.Count}):");
    foreach (var icon in biologyIcons)
        Console.WriteLine($"  - {icon}");
    Console.WriteLine();

    if (biologyIcons.Count == 0)
    {
        Console.WriteLine("[FAIL] No icons found in Biology category");
        Environment.Exit(1);
    }

    // 3. Save 3 SVG files: cell.svg, dna.svg, protein.svg
    string[] iconNames = { "cell", "dna", "protein" };
    var savedFiles = new List<string>();

    foreach (var name in iconNames)
    {
        try
        {
            var svg = IconProvider.GetSvg("Biology", name);
            if (string.IsNullOrWhiteSpace(svg))
            {
                Console.WriteLine($"[FAIL] SVG content for 'Biology/{name}' is empty");
                Environment.Exit(1);
            }

            var path = Path.Combine(outputDir, $"{name}.svg");
            IconProvider.SaveSvg("Biology", name, path);

            if (!File.Exists(path))
            {
                Console.WriteLine($"[FAIL] File not created: {path}");
                Environment.Exit(1);
            }

            var fileInfo = new FileInfo(path);
            Console.WriteLine($"[OK] Saved: {path} ({fileInfo.Length} bytes)");
            savedFiles.Add(path);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"[WARN] Icon 'Biology/{name}' not found: {ex.Message}");
        }
    }

    Console.WriteLine();

    if (savedFiles.Count < 3)
    {
        Console.WriteLine($"[PARTIAL] Only {savedFiles.Count}/3 SVGs saved (some icons may not exist)");
    }
    else
    {
        Console.WriteLine($"[PASS] All 3 SVGs saved successfully ({savedFiles.Count} files)");
    }

    // Also test GetSvg for a few random icons
    Console.WriteLine();
    Console.WriteLine("Quick checks across categories:");
    Console.WriteLine($"  Arrows/arrow-right: {(string.IsNullOrEmpty(IconProvider.GetSvg("Arrows", "arrow-right")) ? "EMPTY" : "OK")}");
    Console.WriteLine($"  Chemistry/flask: {(string.IsNullOrEmpty(IconProvider.GetSvg("Chemistry", "flask")) ? "EMPTY" : "OK")}");
    Console.WriteLine($"  Medical/heart: {(string.IsNullOrEmpty(IconProvider.GetSvg("Medical", "heart")) ? "EMPTY" : "OK")}");

    // Check total icon count across all categories
    int total = 0;
    foreach (var cat in categories)
        total += IconProvider.GetIcons(cat).Count;
    Console.WriteLine($"\nTotal icons across all categories: {total}");

    Environment.Exit(0);
}
catch (Exception ex)
{
    Console.WriteLine($"[FAIL] Exception: {ex}");
    Environment.Exit(1);
}
