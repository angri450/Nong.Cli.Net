using System.Reflection;
using System.Runtime.InteropServices;
using DiagramCore;
using DiagramCore.Models;

// Print full exception chain
static void PrintException(Exception ex, string indent = "")
{
    Console.WriteLine($"{indent}[{ex.GetType().Name}] {ex.Message}");
    if (ex is TypeInitializationException tie && tie.InnerException != null)
        PrintException(tie.InnerException, indent + "  ");
    else if (ex.InnerException != null)
        PrintException(ex.InnerException, indent + "  ");
}

string outputDir = AppDomain.CurrentDomain.BaseDirectory;

Console.WriteLine("=== Angri450.Nong.Diagram Test ===");
Console.WriteLine($"Output directory: {outputDir}");
Console.WriteLine($"Platform: {(Environment.Is64BitProcess ? "x64" : "x86")}");
Console.WriteLine();

// Try manual native library loading first
Console.WriteLine("[0] Testing native library loading...");
try
{
    string nativeDir = Path.Combine(outputDir, "runtimes", "win-x64", "native");
    if (!Directory.Exists(nativeDir))
        nativeDir = outputDir;

    string libPath = Path.Combine(nativeDir, "libSkiaSharp.dll");
    Console.WriteLine($"  Looking for: {libPath}");
    Console.WriteLine($"  Exists: {File.Exists(libPath)}");

    // Set a resolver that helps find native libraries
    NativeLibrary.SetDllImportResolver(typeof(DiagramBuilder).Assembly, (name, assembly, path) =>
    {
        Console.WriteLine($"  Resolving: '{name}' for '{assembly?.FullName}'");
        if (name == "libSkiaSharp")
        {
            var resolved = NativeLibrary.Load(libPath);
            Console.WriteLine($"  Resolved libSkiaSharp to: {libPath}");
            return resolved;
        }
        if (name == "libHarfBuzzSharp")
        {
            var resolved = NativeLibrary.Load(Path.Combine(nativeDir, "libHarfBuzzSharp.dll"));
            Console.WriteLine($"  Resolved libHarfBuzzSharp");
            return resolved;
        }
        return IntPtr.Zero;
    });

    // Try loading libSkiaSharp
    var skiaHandle = NativeLibrary.Load(libPath);
    Console.WriteLine($"  Manual load: SUCCESS (handle={skiaHandle})");

    // Try loading libHarfBuzzSharp
    var hbPath = Path.Combine(nativeDir, "libHarfBuzzSharp.dll");
    var hbHandle = NativeLibrary.Load(hbPath);
    Console.WriteLine($"  HarfBuzz load: SUCCESS (handle={hbHandle})");
}
catch (Exception ex)
{
    Console.WriteLine($"  Manual load FAILED:");
    PrintException(ex, "  ");
}
Console.WriteLine();

// ============================================================
// Test 1: Flowchart
// ============================================================
Console.WriteLine("[1/2] Generating flowchart...");
try
{
    var graph = new Graph { Title = "Simple Process Flow" };

    graph.AddNode("start", "Start");
    graph.AddNode("process", "Process Data");
    graph.AddNode("decision", "Valid?");
    graph.AddNode("end_ok", "Success");
    graph.AddNode("end_fail", "Retry");

    var startNode = graph.GetNode("start")!;
    startNode.Shape = "ellipse";
    startNode.FillColor = "#A8E6CF";

    var decisionNode = graph.GetNode("decision")!;
    decisionNode.Shape = "diamond";
    decisionNode.FillColor = "#FFD3B6";

    var endOkNode = graph.GetNode("end_ok")!;
    endOkNode.Shape = "ellipse";
    endOkNode.FillColor = "#A8E6CF";

    var endFailNode = graph.GetNode("end_fail")!;
    endFailNode.Shape = "ellipse";
    endFailNode.FillColor = "#FF8B94";

    graph.AddEdge("start", "process");
    graph.AddEdge("process", "decision");
    graph.AddEdge("decision", "end_ok", "Yes");
    graph.AddEdge("decision", "end_fail", "No");
    graph.AddEdge("end_fail", "process");

    string flowchartPath = Path.Combine(outputDir, "diagram-flowchart.png");
    DiagramBuilder.Flowchart(graph, flowchartPath);
    bool exists = File.Exists(flowchartPath);
    long size = exists ? new FileInfo(flowchartPath).Length : 0;
    Console.WriteLine($"  File: {flowchartPath}");
    Console.WriteLine($"  Exists: {exists}, Size: {size} bytes");
    Console.WriteLine($"  Flowchart: {(exists && size > 0 ? "PASS" : "FAIL")}");
}
catch (Exception ex)
{
    Console.WriteLine($"  FAIL:");
    PrintException(ex, "  ");
    Console.WriteLine($"  Stack: {ex.StackTrace}");
}
Console.WriteLine();

// ============================================================
// Test 2: Phylogenetic Tree
// ============================================================
Console.WriteLine("[2/2] Generating phylogenetic tree...");
try
{
    string newick = "(((Human:0.1,Chimp:0.08):0.05,Gorilla:0.12):0.03,(Mouse:0.25,Rat:0.22):0.08);";

    string treePath = Path.Combine(outputDir, "diagram-tree.png");
    DiagramBuilder.PhylogeneticTree(newick, treePath);
    bool exists = File.Exists(treePath);
    long size = exists ? new FileInfo(treePath).Length : 0;
    Console.WriteLine($"  File: {treePath}");
    Console.WriteLine($"  Exists: {exists}, Size: {size} bytes");
    Console.WriteLine($"  Tree: {(exists && size > 0 ? "PASS" : "FAIL")}");
}
catch (Exception ex)
{
    Console.WriteLine($"  FAIL:");
    PrintException(ex, "  ");
    Console.WriteLine($"  Stack: {ex.StackTrace}");
}

Console.WriteLine();
Console.WriteLine("=== Test complete ===");
