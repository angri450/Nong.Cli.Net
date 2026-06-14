using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using M = DocumentFormat.OpenXml.Math;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Nong.Cli.Tests;

public class WordCommandTests
{
    static readonly XNamespace WordprocessingNs = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    static string NongDll => Path.Combine(RepoRoot, "Cli", "bin", "Release", "net8.0", "nong.dll");

    static string TestAsset(string relativePath)
    {
        var outputCopy = Path.Combine(AppContext.BaseDirectory, "TestAssets", relativePath);
        if (File.Exists(outputCopy))
            return outputCopy;

        return Path.Combine(RepoRoot, "Cli.Tests", "TestAssets", relativePath);
    }

    (string json, int exitCode) Run(params string[] args)
    {
        var result = CliTestToolPath.RunDotnetCli(
            RepoRoot,
            NongDll,
            timeoutMs: 60000,
            captureStdErr: true,
            environment: null,
            args);
        return (result.StdOut, result.ExitCode);
    }

    JsonDocument Parse(string json) => JsonDocument.Parse(json);

    void RequireCli()
    {
        Assert.True(File.Exists(NongDll),
            "nong.dll not found. Build first: dotnet build Cli/NongCli.csproj -c Release");
    }

    // ===== Fixture helpers =====

    /// <summary>Create a minimal .docx with a single "Hello World" paragraph.</summary>
    static string CreateMinimalDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-minimal-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        doc.AddMainDocumentPart();
        doc.MainDocumentPart.Document = new Document(new Body(
            new Paragraph(new Run(new Text("Hello World")))
        ));
        return path;
    }

    /// <summary>Create a test docx with one paragraph. Caller must delete.</summary>
    static string CreateTestDocx() => CreateMinimalDocx();

    /// <summary>Create a .docx with one Heading1 paragraph followed by one body paragraph.</summary>
    static string CreateHeadingThenBodyDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-heading-body-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        doc.AddMainDocumentPart();
        doc.MainDocumentPart.Document = new Document(new Body(
            new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
                new Run(new Text("Heading One"))),
            new Paragraph(new Run(new Text("Body One")))
        ));
        return path;
    }

    /// <summary>Create a docx with paragraph layout and table border formatting.</summary>
    static string CreateFormattedDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-formatted-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        doc.AddMainDocumentPart();

        var paragraph = new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Center },
                new Indentation { FirstLine = "420", Left = "120" },
                new SpacingBetweenLines
                {
                    Line = "360",
                    LineRule = LineSpacingRuleValues.Exact,
                    Before = "120",
                    After = "240"
                },
                new KeepNext()),
            new Run(
                new RunProperties(
                    new RunFonts { EastAsia = "宋体", Ascii = "Times New Roman" },
                    new FontSize { Val = "21" }),
                new Text("Formatted paragraph")));

        var table = new Table(
            new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableJustification { Val = TableRowAlignmentValues.Center },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                    new LeftBorder { Val = BorderValues.Nil },
                    new BottomBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                    new RightBorder { Val = BorderValues.Nil },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "000000" },
                    new InsideVerticalBorder { Val = BorderValues.Nil })),
            new TableRow(
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = "2500", Type = TableWidthUnitValues.Pct },
                        new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                        new TableCellBorders(new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "000000" })),
                    new Paragraph(new Run(new Text("A")))),
                new TableCell(new Paragraph(new Run(new Text("B"))))));

        doc.MainDocumentPart.Document = new Document(new Body(paragraph, table));
        return path;
    }

    static string CreateLongTableDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-long-table-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        doc.AddMainDocumentPart();

        var table = new Table(
            new TableRow(
                new TableCell(new Paragraph(new Run(new Text("指标")))),
                new TableCell(new Paragraph(new Run(new Text("说明"))))));
        for (var i = 1; i <= 5; i++)
        {
            table.Append(new TableRow(
                new TableCell(new Paragraph(new Run(new Text($"R{i}")))),
                new TableCell(new Paragraph(new Run(new Text($"说明内容{i}"))))));
        }

        doc.MainDocumentPart.Document = new Document(new Body(table));
        return path;
    }

    static string CreateLatinParenthesisDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-latin-parenthesis-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        doc.AddMainDocumentPart();
        doc.MainDocumentPart.Document = new Document(new Body(
            new Paragraph(new Run(
                new RunProperties(new Italic()),
                new Text("番茄")),
                new Run(
                    new RunProperties(new Italic()),
                    new Text("（")),
                new Run(
                    new RunProperties(new Italic()),
                    new Text("Solanum lycopersicum")),
                new Run(
                    new RunProperties(new Italic()),
                    new Text("）机制需要斜体拉丁名。英文括号(IF 6.1)不应斜体。"))),
            new Paragraph(new Run(
                new RunProperties(new Italic()),
                new Text("辣椒疫霉菌 Phytophthora capsici 需要自动补括号。技术括号（RNA-seq）不应斜体。"))),
            new Paragraph(new Run(
                new RunProperties(new Italic()),
                new Text("番茄 Solanum lycopersicum cv. 中蔬4号需要保留cv.前空格。")))));
        return path;
    }

    static string CreateFormatAuditSourceDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-format-audit-source-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        doc.AddMainDocumentPart();

        var table = new Table(
            new TableRow(
                new TableCell(new Paragraph(new Run(new Text("指标")))),
                new TableCell(new Paragraph(new Run(new Text("结果"))))),
            new TableRow(
                new TableCell(new Paragraph(new Run(new Text("材料吸附性能")))),
                new TableCell(new Paragraph(new Run(new Text("稳定提升"))))));

        doc.MainDocumentPart.Document = new Document(new Body(
            new Paragraph(new Run(new Text("校企共建沸石基矿物材料教授工作站方案书"))),
            new Paragraph(new Run(new Text("一、项目摘要"))),
            new Paragraph(new Run(new Text("2.1 研究对象"))),
            new Paragraph(new Run(new Text("本研究以番茄（Solanum lycopersicum）为示例，验证N2O、H2O2与O2-格式审计证据。"))),
            new Paragraph(new Run(new Text("表1 试验结果统计"))),
            table));
        return path;
    }

    static string CreateChemicalFormulaDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-chemical-formula-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        doc.AddMainDocumentPart();
        doc.MainDocumentPart.Document = new Document(new Body(
            new Paragraph(new Run(new Text("2026年沸石保氮减少N2O排放，同时记录H2O2、CO2和表头C1。")))));
        return path;
    }

    static string CreateShadedCaptionTableDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-shaded-caption-table-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        doc.AddMainDocumentPart();

        static Shading BlueShading() => new() { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "D9EAF7" };

        var caption = new Paragraph(
            new ParagraphProperties(
                BlueShading(),
                new Indentation { FirstLine = "480" }),
            new Run(
                new RunProperties(BlueShading()),
                new Text("表1 试验结果统计")));

        var table = new Table(
            new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                BlueShading()),
            new TableRow(
                new TableCell(
                    new TableCellProperties(BlueShading()),
                    new Paragraph(
                        new ParagraphProperties(
                            BlueShading(),
                            new Indentation { FirstLine = "480" }),
                        new Run(new RunProperties(BlueShading()), new Text("指标")))),
                new TableCell(
                    new TableCellProperties(BlueShading()),
                    new Paragraph(
                        new ParagraphProperties(
                            BlueShading(),
                            new Indentation { FirstLine = "480" }),
                        new Run(new RunProperties(BlueShading()), new Text("结果"))))));

        table.Append(new TableRow(
            new TableCell(
                new TableCellProperties(BlueShading()),
                new Paragraph(
                    new ParagraphProperties(
                        BlueShading(),
                        new Indentation { FirstLine = "480" }),
                    new Run(new RunProperties(BlueShading()), new Text("沸石材料吸附性能稳定")))),
            new TableCell(
                new TableCellProperties(BlueShading()),
                new Paragraph(
                    new ParagraphProperties(
                        BlueShading(),
                        new Indentation { FirstLine = "480" }),
                    new Run(new RunProperties(BlueShading()), new Text("达到预期指标"))))));

        doc.MainDocumentPart.Document = new Document(new Body(caption, table));
        return path;
    }

    static string CreateWideTableDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-wide-table-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        doc.AddMainDocumentPart();

        var table = new Table(
            new TableRow(
                new TableCell(new Paragraph(new Run(new Text("样品")))),
                new TableCell(new Paragraph(new Run(new Text("C1")))),
                new TableCell(new Paragraph(new Run(new Text("C2")))),
                new TableCell(new Paragraph(new Run(new Text("C3")))),
                new TableCell(new Paragraph(new Run(new Text("C4"))))));
        table.Append(new TableRow(
            new TableCell(new Paragraph(new Run(new Text("S1")))),
            new TableCell(new Paragraph(new Run(new Text("1")))),
            new TableCell(new Paragraph(new Run(new Text("2")))),
            new TableCell(new Paragraph(new Run(new Text("3")))),
            new TableCell(new Paragraph(new Run(new Text("4"))))));

        doc.MainDocumentPart.Document = new Document(new Body(table));
        return path;
    }

    /// <summary>Create a docx with legacy Word/WPS compatibility artifacts fixed by word fix-order.</summary>
    static string CreateLegacyCompatibilityDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-legacy-compat-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();

        var stylesPart = main.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = new Styles(
            new Style(
                new StyleName { Val = "Normal" },
                new ParagraphProperties(new Justification { Val = JustificationValues.Both }),
                new NextParagraphStyle { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Normal",
                Default = true,
            },
            new Style(
                new StyleName { Val = "Normal Table" },
                new TableProperties(new TableStyle { Val = "NormalTable" }))
            {
                Type = StyleValues.Table,
                StyleId = "NormalTable",
                Default = true,
            });

        var table = new Table(
            new TableProperties(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }),
            new TableGrid(new GridColumn { Width = "5000" }),
            new TableRow(
                new TableRowProperties(),
                new PreviousTablePropertyExceptions(
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }),
                new TableCell(
                    new TableCellProperties(new NoWrap { Val = OnOffOnlyValues.Off }),
                    new Paragraph(new Run(new Text("legacy cell"))))));

        main.Document = new Document(new Body(
            new Paragraph(new Run(new Text("legacy compatibility"))),
            table,
            new SectionProperties(
                new Columns { Space = "720" },
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 1440, Right = 1440, Bottom = 1440, Left = 1440 })));

        return path;
    }

    /// <summary>Create a docx with legacy tblLook attributes and tcPr after cell content.</summary>
    static string CreateDirtyTableDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-dirty-table-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();

        var stylesPart = main.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = new Styles();
        stylesPart.Styles.InnerXml =
            @"<w:style xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"" w:type=""table"" w:styleId=""DirtyTableStyle"">
  <w:name w:val=""Dirty Table Style"" />
  <w:tblPr><w:tblBorders><w:top w:val=""single"" w:sz=""4"" /></w:tblBorders></w:tblPr>
  <w:tcPr><w:tcW w:w=""5000"" w:type=""pct"" /></w:tcPr>
</w:style>";

        main.Document = new Document(new Body());

        var body = main.Document.Body!;
        body.InnerXml =
            @"<w:tbl xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:tblPr>
    <w:tblW w:w=""5000"" w:type=""pct"" />
    <w:tblLook w:val=""04A0"" w:firstRow=""1"" w:lastRow=""0"" w:firstColumn=""1"" w:lastColumn=""0"" w:noHBand=""0"" w:noVBand=""1"" />
  </w:tblPr>
  <w:tblGrid><w:gridCol w:w=""3000"" /></w:tblGrid>
  <w:tr>
    <w:tc>
      <w:p><w:r><w:t>dirty cell</w:t></w:r></w:p>
      <w:tcPr><w:tcW w:w=""5000"" w:type=""pct"" /></w:tcPr>
    </w:tc>
  </w:tr>
</w:tbl>";

        return path;
    }

    static string CreateControlledDirtyAcademicDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-controlled-dirty-academic-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();

        var stylesPart = main.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = new Styles();
        stylesPart.Styles.InnerXml =
            @"<w:style xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"" w:type=""paragraph"" w:default=""1"" w:styleId=""Normal""><w:name w:val=""Normal"" /></w:style>
  <w:style w:type=""table"" w:styleId=""DirtyTableStyle"">
    <w:name w:val=""Dirty Table Style"" />
    <w:tblPr><w:tblBorders><w:top w:val=""single"" w:sz=""4"" /></w:tblBorders></w:tblPr>
    <w:tcPr><w:tcW w:w=""5000"" w:type=""pct"" /></w:tcPr>
  </w:style>";

        var settingsPart = main.AddNewPart<DocumentSettingsPart>();
        settingsPart.Settings = new Settings(
            new ThemeFontLanguages { Val = "zh-CN" },
            new M.MathProperties(),
            new Rsids(new RsidRoot { Val = "00112233" }));

        main.Document = new Document(new Body());
        main.Document.Body!.InnerXml =
            @"<w:p xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:r><w:rPr><w:rFonts w:eastAsia=""仿宋"" w:ascii=""Calibri""/><w:sz w:val=""22""/></w:rPr><w:t>校企共建沸石基矿物材料教授工作站方案书</w:t></w:r>
</w:p>
<w:p xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:r><w:rPr><w:rFonts w:eastAsia=""仿宋"" w:ascii=""Calibri""/><w:sz w:val=""18""/></w:rPr><w:t>一、项目摘要</w:t></w:r>
</w:p>
<w:p xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:pPr><w:spacing w:line=""120"" w:lineRule=""exact""/><w:shd w:val=""clear"" w:fill=""D9EAF7""/></w:pPr>
  <w:r><w:rPr><w:rFonts w:eastAsia=""微软雅黑"" w:ascii=""Calibri""/><w:sz w:val=""18""/></w:rPr><w:t>本研究以番茄（Solanum lycopersicum）为对象，验证N2O、H2O2与O2-格式。</w:t></w:r>
</w:p>
<w:p xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:pPr><w:ind w:firstLine=""480""/><w:shd w:val=""clear"" w:fill=""D9EAF7""/></w:pPr>
  <w:r><w:rPr><w:shd w:val=""clear"" w:fill=""D9EAF7""/></w:rPr><w:t>表1 试验结果统计</w:t></w:r>
</w:p>
<w:tbl xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:tblPr>
    <w:tblStyle w:val=""DirtyTableStyle"" />
    <w:tblW w:w=""5000"" w:type=""pct"" />
    <w:tblBorders>
      <w:left w:val=""single"" w:sz=""4"" />
      <w:insideV w:val=""single"" w:sz=""4"" />
    </w:tblBorders>
    <w:shd w:val=""clear"" w:fill=""D9EAF7""/>
    <w:tblLook w:val=""04A0"" w:firstRow=""1"" w:lastRow=""0"" w:firstColumn=""1"" w:lastColumn=""0"" w:noHBand=""0"" w:noVBand=""1"" />
  </w:tblPr>
  <w:tblGrid><w:gridCol w:w=""2500"" /><w:gridCol w:w=""2500"" /></w:tblGrid>
  <w:tr>
    <w:tc>
      <w:p><w:pPr><w:ind w:firstLine=""480""/><w:shd w:val=""clear"" w:fill=""D9EAF7""/></w:pPr><w:r><w:rPr><w:shd w:val=""clear"" w:fill=""D9EAF7""/></w:rPr><w:t>指标</w:t></w:r></w:p>
      <w:tcPr><w:tcW w:w=""2500"" w:type=""pct""/><w:shd w:val=""clear"" w:fill=""D9EAF7""/></w:tcPr>
    </w:tc>
    <w:tc>
      <w:p><w:pPr><w:ind w:firstLine=""480""/></w:pPr><w:r><w:t>结果</w:t></w:r></w:p>
      <w:tcPr><w:tcW w:w=""2500"" w:type=""pct""/></w:tcPr>
    </w:tc>
  </w:tr>
  <w:tr>
    <w:tc><w:p><w:pPr><w:ind w:firstLine=""480""/></w:pPr><w:r><w:t>材料吸附性能</w:t></w:r></w:p><w:tcPr><w:tcW w:w=""2500"" w:type=""pct""/></w:tcPr></w:tc>
    <w:tc><w:p><w:pPr><w:ind w:firstLine=""480""/></w:pPr><w:r><w:t>稳定提升</w:t></w:r></w:p><w:tcPr><w:tcW w:w=""2500"" w:type=""pct""/></w:tcPr></w:tc>
  </w:tr>
</w:tbl>
<w:sectPr xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:cols w:space=""720"" />
  <w:docGrid w:type=""lines"" w:linePitch=""120"" />
</w:sectPr>";

        return path;
    }

    static string CreateLongWideTableDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-long-wide-table-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        doc.AddMainDocumentPart();

        var table = new Table(
            new TableRow(
                new TableCell(new Paragraph(new Run(new Text("样品")))),
                new TableCell(new Paragraph(new Run(new Text("C1")))),
                new TableCell(new Paragraph(new Run(new Text("C2")))),
                new TableCell(new Paragraph(new Run(new Text("C3")))),
                new TableCell(new Paragraph(new Run(new Text("C4")))),
                new TableCell(new Paragraph(new Run(new Text("C5"))))));
        for (var i = 1; i <= 5; i++)
        {
            table.Append(new TableRow(
                new TableCell(new Paragraph(new Run(new Text($"S{i}")))),
                new TableCell(new Paragraph(new Run(new Text($"{i}-1")))),
                new TableCell(new Paragraph(new Run(new Text($"{i}-2")))),
                new TableCell(new Paragraph(new Run(new Text($"{i}-3")))),
                new TableCell(new Paragraph(new Run(new Text($"{i}-4")))),
                new TableCell(new Paragraph(new Run(new Text($"{i}-5"))))));
        }

        doc.MainDocumentPart!.Document = new Document(new Body(
            new Paragraph(new Run(new Text("表1 过长过宽试验表"))),
            table));
        return path;
    }

    static string CreateTinyPng()
    {
        var path = Path.Combine(Path.GetTempPath(), "nong-test-image-" + Guid.NewGuid().ToString("N")[..8] + ".png");
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    static string CreateVmlImageDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-vml-image-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var pngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        var imagePart = main.AddImagePart("image/png");
        using (var s = imagePart.GetStream(FileMode.Create, FileAccess.Write))
            s.Write(pngBytes, 0, pngBytes.Length);
        var rid = main.GetIdOfPart(imagePart);
        var pictRun = new Run();
        pictRun.InnerXml =
            $@"<w:pict xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"" xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:o=""urn:schemas-microsoft-com:office:office"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships""><v:shape id=""_x0000_i1025"" type=""#_x0000_t75"" style=""width:10pt;height:10pt""><v:imagedata r:id=""{rid}"" o:title=""formula"" /></v:shape></w:pict>";

        main.Document = new Document(new Body(
            new Paragraph(new Run(new Text("before"))),
            new Paragraph(pictRun),
            new Paragraph(new Run(new Text("after")))));
        return path;
    }

    static string CreateUnlinkedVmlImageDocx()
    {
        var path = Path.Combine(Path.GetTempPath(), "test-vml-unlinked-image-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        var pictRun = new Run();
        pictRun.InnerXml =
            @"<w:pict xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"" xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:o=""urn:schemas-microsoft-com:office:office""><v:shape id=""_x0000_i1026"" type=""#_x0000_t75"" style=""width:10pt;height:10pt""><v:imagedata o:title=""formula"" /></v:shape></w:pict>";

        main.Document = new Document(new Body(
            new Paragraph(new Run(new Text("before"))),
            new Paragraph(pictRun),
            new Paragraph(new Run(new Text("after")))));
        return path;
    }

    static string CreateFakeTtf()
    {
        var path = Path.Combine(Path.GetTempPath(), "nong-test-font-" + Guid.NewGuid().ToString("N")[..8] + ".ttf");
        File.WriteAllBytes(path, Enumerable.Range(0, 128).Select(i => (byte)i).ToArray());
        return path;
    }

    // ===== Test 1: word dissect --json basic =====

    [Fact]
    public void WordDissect_Json_ReturnsOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        try
        {
            var (json, exit) = Run("word", "dissect", docx, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("word dissect", doc.RootElement.GetProperty("command").GetString());
        }
        finally { try { File.Delete(docx); } catch { } }
    }

    // ===== Test 2: word outline --json =====

    [Fact]
    public void WordOutline_Json_ReturnsOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        try
        {
            var (json, exit) = Run("word", "outline", docx, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("word outline", doc.RootElement.GetProperty("command").GetString());
        }
        finally { try { File.Delete(docx); } catch { } }
    }

    // ===== Test 3: word images --json =====

    [Fact]
    public void WordImages_Json_ReturnsOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        try
        {
            var (json, exit) = Run("word", "images", docx, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
        }
        finally { try { File.Delete(docx); } catch { } }
    }

    // ===== Test 4: word comments --json (empty doc) =====

    [Fact]
    public void WordComments_Json_ReturnsOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        try
        {
            var (json, exit) = Run("word", "comments", docx, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
        }
        finally { try { File.Delete(docx); } catch { } }
    }

    // ===== Test 5: word revisions --json (empty doc) =====

    [Fact]
    public void WordRevisions_Json_ReturnsOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        try
        {
            var (json, exit) = Run("word", "revisions", docx, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
        }
        finally { try { File.Delete(docx); } catch { } }
    }

    // ===== Test 6: word infer-format with valid Chinese format =====

    [Fact]
    public void WordInferFormat_Valid_ReturnsOk()
    {
        RequireCli();
        var (json, exit) = Run("word", "infer-format", "黑体 四号", "--json");
        Assert.Equal(0, exit);

        using var doc = Parse(json);
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
    }

    // ===== Test 7: word infer-format with empty input =====

    [Fact]
    public void WordInferFormat_Empty_Returns_E006()
    {
        RequireCli();
        var (json, exit) = Run("word", "infer-format", "", "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E006", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 8: word add-paragraph with missing file =====

    [Fact]
    public void WordAddParagraph_MissingFile_Returns_E001()
    {
        RequireCli();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var (json, exit) = Run("word", "add-paragraph", "nonexistent.docx",
            "--spec", "{\"text\":\"Hello\"}", "-o", outPath, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 9: word add-table with missing file =====

    [Fact]
    public void WordAddTable_MissingFile_Returns_E001()
    {
        RequireCli();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var (json, exit) = Run("word", "add-table", "nonexistent.docx",
            "--spec", "{\"headers\":[\"A\"],\"rows\":[[\"1\"]]}", "-o", outPath, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 10: word add-math with missing file =====

    [Fact]
    public void WordAddMath_MissingFile_Returns_E001()
    {
        RequireCli();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var (json, exit) = Run("word", "add-math", "nonexistent.docx",
            "--latex", "x^2", "-o", outPath, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 11: word fix-order with missing file =====

    [Fact]
    public void WordFixOrder_MissingFile_Returns_E001()
    {
        RequireCli();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var (json, exit) = Run("word", "fix-order", "nonexistent.docx", "-o", outPath, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 12: word protect with missing file =====

    [Fact]
    public void WordProtect_MissingFile_Returns_E001()
    {
        RequireCli();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var (json, exit) = Run("word", "protect", "nonexistent.docx", "-o", outPath, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 13: word embed-font with missing file =====

    [Fact]
    public void WordEmbedFont_MissingFile_Returns_E001()
    {
        RequireCli();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        // docx file is checked first, so missing docx returns E001
        // (before the missing font file is even checked)
        var (json, exit) = Run("word", "embed-font", "nonexistent.docx", "nonexistent.ttf",
            "-o", outPath, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    // ===== Test 14: word merge with less than 2 files =====

    [Fact]
    public void WordMerge_OneFile_ReturnsError()
    {
        RequireCli();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var (json, exit) = Run("word", "merge", "nonexistent.docx", "-o", outPath, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        var code = doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString();
        Assert.True(code is "E003" or "E001",
            $"Expected E003 (missing argument) or E001 (file not found), got {code}");
    }

    // ===== Test 15: word add-paragraph with invalid JSON spec =====

    [Fact]
    public void WordAddParagraph_InvalidSpec_Returns_E006()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "add-paragraph", docx,
                "--spec", "{not valid json", "-o", outPath, "--json");
            Assert.NotEqual(0, exit);

            using var doc = Parse(json);
            Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("E006", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    // ===== Test 16: canonical word add paragraph with spec file =====

    [Fact]
    public void WordAddParagraph_CanonicalSpecFile_ReturnsOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var specPath = Path.Combine(Path.GetTempPath(), "nong-para-spec-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            File.WriteAllText(specPath, "{\"text\":\"canonical spec file paragraph\",\"style\":\"Normal\"}");
            var (json, exit) = Run("word", "add", "paragraph", docx,
                "--spec", specPath, "-o", outPath, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("word add paragraph", doc.RootElement.GetProperty("command").GetString());
            Assert.True(File.Exists(outPath));
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(specPath); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    // ===== Test 17: canonical word add math with --after =====

    [Fact]
    public void WordAddMath_CanonicalAfter_ReturnsOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "add", "math", docx,
                "--latex", "E=mc^2", "-o", outPath, "--after", "p0001", "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("word add math", doc.RootElement.GetProperty("command").GetString());
            Assert.True(File.Exists(outPath));
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    // ===== Test 18: word dissect -o (WordSlice) =====

    [Fact]
    public void WordDissect_Output_WritesSevenFiles()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var outDir = Path.Combine(Path.GetTempPath(), "nong-slice-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (json, exit) = Run("word", "dissect", docx, "-o", outDir, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("word dissect", doc.RootElement.GetProperty("command").GetString());

            // Verify canonical streams plus plain text preview.
            Assert.True(File.Exists(Path.Combine(outDir, "manifest.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "document.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "content.nongmark")));
            Assert.True(File.Exists(Path.Combine(outDir, "preview", "content.txt")));
            Assert.False(File.Exists(Path.Combine(outDir, "content.md")));
            Assert.True(File.Exists(Path.Combine(outDir, "content.jsonl")));
            Assert.True(File.Exists(Path.Combine(outDir, "structure.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "format.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "diagnostics.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "assets", "manifest.json")));

            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(outDir, "manifest.json")));
            Assert.Equal("nong-pandoc/package/v1", manifest.RootElement.GetProperty("schemaVersion").GetString());
            Assert.Equal("docx", manifest.RootElement.GetProperty("source").GetProperty("format").GetString());
            Assert.True(manifest.RootElement.GetProperty("source").TryGetProperty("sha256", out _));
            Assert.True(manifest.RootElement.TryGetProperty("createdAt", out _));
            Assert.Equal("content.nongmark", manifest.RootElement.GetProperty("streams").GetProperty("contentNongMark").GetString());
            Assert.Equal("preview/content.txt", manifest.RootElement.GetProperty("streams").GetProperty("textPreview").GetString());
            Assert.Equal("diagnostics.json", manifest.RootElement.GetProperty("streams").GetProperty("diagnostics").GetString());

            var firstLine = File.ReadLines(Path.Combine(outDir, "content.jsonl")).First();
            using var lineDoc = JsonDocument.Parse(firstLine);
            Assert.Equal("p0001", lineDoc.RootElement.GetProperty("id").GetString());
            Assert.Equal("p0001", lineDoc.RootElement.GetProperty("blockId").GetString());
            Assert.Equal(0, lineDoc.RootElement.GetProperty("index").GetInt32());

            using var structure = JsonDocument.Parse(File.ReadAllText(Path.Combine(outDir, "structure.json")));
            var firstEntry = structure.RootElement.GetProperty("blockIndex").EnumerateObject().First().Value;
            var provenance = firstEntry.GetProperty("provenance");
            Assert.Equal("docx", provenance.GetProperty("format").GetString());
            Assert.Equal(0, provenance.GetProperty("position").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(provenance.GetProperty("source").GetString()));
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { if (Directory.Exists(outDir)) Directory.Delete(outDir, true); } catch { }
        }
    }

    [Fact]
    public void WordDissect_VmlImage_SurfacesImageBlockAndWarning()
    {
        RequireCli();
        var docx = CreateVmlImageDocx();
        var sliceDir = Path.Combine(Path.GetTempPath(), "nong-test-vml-slice-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (json, exit) = Run("word", "dissect", docx, "-o", sliceDir, "--json");
            Assert.Equal(0, exit);

            using var dissectDoc = Parse(json);
            Assert.Equal("ok", dissectDoc.RootElement.GetProperty("status").GetString());
            Assert.True(dissectDoc.RootElement.GetProperty("data").GetProperty("warnings").GetArrayLength() > 0);

            var imageLine = File.ReadLines(Path.Combine(sliceDir, "content.jsonl"))
                .First(line => line.Contains("\"kind\":\"image\""));
            using var imageDoc = Parse(imageLine);
            Assert.Equal("vml", imageDoc.RootElement.GetProperty("source").GetString());
            Assert.Equal("img0001", imageDoc.RootElement.GetProperty("blockId").GetString());
            Assert.True(imageDoc.RootElement.TryGetProperty("assetPath", out var assetPath));
            Assert.Contains("assets/", assetPath.GetString());

            var nongmark = File.ReadAllText(Path.Combine(sliceDir, "content.nongmark"));
            Assert.Contains("![formula]", nongmark);

            using var assetManifest = Parse(File.ReadAllText(Path.Combine(sliceDir, "assets", "manifest.json")));
            Assert.Equal(1, assetManifest.RootElement.GetProperty("items").GetArrayLength());
            Assert.Equal("img0001", imageDoc.RootElement.GetProperty("id").GetString());
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { if (Directory.Exists(sliceDir)) Directory.Delete(sliceDir, true); } catch { }
        }
    }

    [Fact]
    public void WordImages_VmlImage_ReturnsVmlSource()
    {
        RequireCli();
        var docx = CreateVmlImageDocx();
        try
        {
            var (json, exit) = Run("word", "images", docx, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            var images = doc.RootElement.GetProperty("data").GetProperty("images");
            Assert.Equal(1, images.GetArrayLength());
            Assert.Equal("vml", images[0].GetProperty("source").GetString());
            Assert.False(string.IsNullOrWhiteSpace(images[0].GetProperty("internalRelationshipId").GetString()));
            Assert.Contains("p0002", images[0].GetProperty("usedBy").EnumerateArray().Select(e => e.GetString()));
            Assert.True(images[0].GetProperty("extractable").GetBoolean());
        }
        finally
        {
            try { File.Delete(docx); } catch { }
        }
    }

    [Fact]
    public void WordImages_UnlinkedVmlImage_ReturnsNonExtractableReference()
    {
        RequireCli();
        var docx = CreateUnlinkedVmlImageDocx();
        try
        {
            var (json, exit) = Run("word", "images", docx, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            var root = doc.RootElement;
            var data = root.GetProperty("data");
            var images = data.GetProperty("images");
            Assert.Equal(1, images.GetArrayLength());
            Assert.Equal("vml", images[0].GetProperty("source").GetString());
            Assert.Equal("", images[0].GetProperty("internalRelationshipId").GetString());
            Assert.False(images[0].GetProperty("extractable").GetBoolean());
            Assert.Contains("p0002", images[0].GetProperty("usedBy").EnumerateArray().Select(e => e.GetString()));
            Assert.Contains("relationship id", images[0].GetProperty("warning").GetString());
            Assert.True(data.GetProperty("warnings").GetArrayLength() > 0);
            Assert.True(root.GetProperty("issues").GetArrayLength() > 0);
        }
        finally
        {
            try { File.Delete(docx); } catch { }
        }
    }

    [Fact]
    public void WordCheck_Doc_ReturnsConversionRequired()
    {
        RequireCli();
        var docPath = Path.Combine(Path.GetTempPath(), "nong-test-legacy-" + Guid.NewGuid().ToString("N")[..8] + ".doc");
        File.WriteAllBytes(docPath, [0xD0, 0xCF, 0x11, 0xE0]);
        try
        {
            var (json, exit) = Run("word", "check", docPath, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            var data = doc.RootElement.GetProperty("data");
            Assert.Equal("doc", data.GetProperty("inputFormat").GetString());
            Assert.False(data.GetProperty("canProcessDirectly").GetBoolean());
            Assert.Equal("unavailable_until_conversion", data.GetProperty("blockIdStatus").GetString());
        }
        finally
        {
            try { File.Delete(docPath); } catch { }
        }
    }

    [Fact]
    public void WordCheck_VmlDocx_ReportsVmlWarning()
    {
        RequireCli();
        var docx = CreateVmlImageDocx();
        try
        {
            var (json, exit) = Run("word", "check", docx, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            var data = doc.RootElement.GetProperty("data");
            Assert.Equal("docx", data.GetProperty("inputFormat").GetString());
            Assert.True(data.GetProperty("canProcessDirectly").GetBoolean());
            Assert.Equal(1, data.GetProperty("vmlImages").GetInt32());
            Assert.True(data.GetProperty("warnings").GetArrayLength() > 0);
        }
        finally
        {
            try { File.Delete(docx); } catch { }
        }
    }

    [Fact]
    public void WordConvert_Docx_CopiesToExplicitOutput()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-convert-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "convert", docx, "-o", outPath, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("copy", doc.RootElement.GetProperty("data").GetProperty("engine").GetString());
            Assert.True(File.Exists(outPath));
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    // ===== Test 19: word dissect --output alias =====

    [Fact]
    public void WordDissect_OutputLongAlias_WritesSevenFiles()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var outDir = Path.Combine(Path.GetTempPath(), "nong-slice-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (json, exit) = Run("word", "dissect", docx, "--output", outDir, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.True(File.Exists(Path.Combine(outDir, "manifest.json")));
            Assert.True(File.Exists(Path.Combine(outDir, "content.jsonl")));
            Assert.True(File.Exists(Path.Combine(outDir, "assets", "manifest.json")));
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { if (Directory.Exists(outDir)) Directory.Delete(outDir, true); } catch { }
        }
    }

    // ===== Test 20: parser errors keep JSON/E003 envelope =====

    [Fact]
    public void WordAddParagraph_MissingSpec_ReturnsJson_E003()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "add", "paragraph", docx, "-o", outPath, "--json");
            Assert.NotEqual(0, exit);

            using var doc = Parse(json);
            Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal("word add paragraph", doc.RootElement.GetProperty("command").GetString());
            Assert.Equal("E003", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    // ===== Test 21: --after p0001 uses dissect semantic paragraph ID, not raw body index =====

    [Fact]
    public void WordAddParagraph_AfterSemanticParagraph_DoesNotTargetHeading()
    {
        RequireCli();
        var docx = CreateHeadingThenBodyDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "add", "paragraph", docx,
                "--spec", "{\"text\":\"Inserted After Body\"}", "-o", outPath, "--after", "p0001", "--json");
            Assert.Equal(0, exit);

            using var jsonDoc = Parse(json);
            Assert.Equal("p0002", jsonDoc.RootElement.GetProperty("data").GetProperty("blockId").GetString());

            using var outDoc = WordprocessingDocument.Open(outPath, false);
            var texts = outDoc.MainDocumentPart!.Document.Body!.Elements<Paragraph>()
                .Select(p => p.InnerText)
                .Where(t => !string.IsNullOrEmpty(t))
                .ToArray();
            Assert.Equal(new[] { "Heading One", "Body One", "Inserted After Body" }, texts);
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    // ===== Test 22: add math result ID matches dissect equation ID =====

    [Fact]
    public void WordAddMath_DissectSeesM0001Equation()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-math-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var sliceDir = Path.Combine(Path.GetTempPath(), "nong-slice-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (json, exit) = Run("word", "add", "math", docx,
                "--latex", "E=mc^2", "-o", outPath, "--json");
            Assert.Equal(0, exit);

            using var addDoc = Parse(json);
            Assert.Equal("m0001", addDoc.RootElement.GetProperty("data").GetProperty("blockId").GetString());

            var (_, dissectExit) = Run("word", "dissect", outPath, "-o", sliceDir, "--json");
            Assert.Equal(0, dissectExit);

            var jsonl = File.ReadAllLines(Path.Combine(sliceDir, "content.jsonl"));
            Assert.Contains(jsonl, line => line.Contains("\"kind\":\"equation\"") && line.Contains("\"id\":\"m0001\""));
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
            try { if (Directory.Exists(sliceDir)) Directory.Delete(sliceDir, true); } catch { }
        }
    }

    // ===== Test 23: add table emits schema-valid OOXML on a minimal valid docx =====

    [Fact]
    public void WordAddTable_MinimalDoc_ValidatesOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-table-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "add", "table", docx,
                "--spec", "{\"caption\":\"Table 1\",\"headers\":[\"A\",\"B\"],\"rows\":[[\"1\",\"2\"]]}",
                "-o", outPath, "--json");
            Assert.Equal(0, exit);

            using var addDoc = Parse(json);
            Assert.Equal("t0001", addDoc.RootElement.GetProperty("data").GetProperty("blockId").GetString());

            var (validateJson, validateExit) = Run("word", "validate", outPath, "--json");
            Assert.Equal(0, validateExit);

            using var validateDoc = Parse(validateJson);
            Assert.Equal("ok", validateDoc.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    // ===== Test 24: word dissect preserves paragraph and table formatting =====

    [Fact]
    public void WordDissect_Output_IncludesParagraphAndTableFormatting()
    {
        RequireCli();
        var docx = CreateFormattedDocx();
        var sliceDir = Path.Combine(Path.GetTempPath(), "nong-test-format-slice-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var (_, exit) = Run("word", "dissect", docx, "-o", sliceDir, "--json");
            Assert.Equal(0, exit);

            var paragraphLine = File.ReadLines(Path.Combine(sliceDir, "content.jsonl"))
                .First(line => line.Contains("Formatted paragraph"));
            using var paragraphDoc = Parse(paragraphLine);
            var paragraphFormat = paragraphDoc.RootElement.GetProperty("format");
            Assert.False(string.IsNullOrWhiteSpace(paragraphFormat.GetProperty("alignment").GetString()));
            Assert.Equal("420", paragraphFormat.GetProperty("firstLineIndent").GetString());
            Assert.Equal("360", paragraphFormat.GetProperty("lineSpacing").GetString());
            Assert.False(string.IsNullOrWhiteSpace(paragraphFormat.GetProperty("lineRule").GetString()));

            var tableLine = File.ReadLines(Path.Combine(sliceDir, "content.jsonl"))
                .First(line => line.Contains("\"kind\":\"table\""));
            using var tableDoc = Parse(tableLine);
            var tableFormat = tableDoc.RootElement.GetProperty("format");
            Assert.Equal("5000", tableFormat.GetProperty("width").GetString());
            Assert.True(tableFormat.GetProperty("borders").GetProperty("top").GetProperty("size").GetUInt32() > 0);
            Assert.True(tableFormat.GetProperty("borders").GetProperty("insideH").GetProperty("size").GetUInt32() > 0);

            using var formatDoc = Parse(File.ReadAllText(Path.Combine(sliceDir, "format.json")));
            var firstTable = formatDoc.RootElement.GetProperty("tables")[0];
            Assert.True(firstTable.TryGetProperty("format", out var formatTable));
            Assert.Equal("5000", formatTable.GetProperty("width").GetString());
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { if (Directory.Exists(sliceDir)) Directory.Delete(sliceDir, true); } catch { }
        }
    }

    [Fact]
    public void WordFixOrder_LegacyCompatibilityArtifacts_ValidatesOk()
    {
        RequireCli();
        var docx = CreateLegacyCompatibilityDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-fixed-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "fix-order", docx, "-o", outPath, "--json");
            Assert.True(exit == 0, json);

            using var fixDoc = Parse(json);
            Assert.Equal("ok", fixDoc.RootElement.GetProperty("status").GetString());
            Assert.True(fixDoc.RootElement.GetProperty("data").GetProperty("fixedElements").GetInt32() > 0);

            var (validateJson, validateExit) = Run("word", "validate", outPath, "--json");
            Assert.True(validateExit == 0, validateJson);

            using var validateDoc = Parse(validateJson);
            Assert.Equal("ok", validateDoc.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void WordFixOrder_DirtyTableLookAndCellProperties_ValidatesOk()
    {
        RequireCli();
        var docx = CreateDirtyTableDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-dirty-table-fixed-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "fix-order", docx, "-o", outPath, "--json");
            Assert.True(exit == 0, json);

            using var fixDoc = Parse(json);
            Assert.Equal("ok", fixDoc.RootElement.GetProperty("status").GetString());
            Assert.True(fixDoc.RootElement.GetProperty("data").GetProperty("fixedElements").GetInt32() > 0);

            var (validateJson, validateExit) = Run("word", "validate", outPath, "--json");
            Assert.True(validateExit == 0, validateJson);

            using var validateDoc = Parse(validateJson);
            Assert.Equal("ok", validateDoc.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void WordImages_AfterAddImage_ReturnsOneImage()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var image = CreateTinyPng();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-image-doc-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (_, addExit) = Run("word", "add-image", docx, "--src", image, "-o", outPath, "--json");
            Assert.Equal(0, addExit);

            var (json, exit) = Run("word", "images", outPath, "--json");
            Assert.Equal(0, exit);

            using var doc = Parse(json);
            Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal(1, doc.RootElement.GetProperty("data").GetProperty("images").GetArrayLength());
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(image); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void WordEmbedFont_MinimalDoc_ValidatesOk()
    {
        RequireCli();
        var docx = CreateTestDocx();
        var font = CreateFakeTtf();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-test-font-doc-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "embed-font", docx, font, "-o", outPath, "--name", "NongFakeFont", "--json");
            Assert.Equal(0, exit);

            using var embedDoc = Parse(json);
            Assert.Equal("ok", embedDoc.RootElement.GetProperty("status").GetString());

            var (validateJson, validateExit) = Run("word", "validate", outPath, "--json");
            Assert.Equal(0, validateExit);

            using var validateDoc = Parse(validateJson);
            Assert.Equal("ok", validateDoc.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(font); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void WordCreate_NongMark_WritesValidDocx()
    {
        RequireCli();
        var dir = Path.Combine(Path.GetTempPath(), "nong-nongmark-create-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        var source = Path.Combine(dir, "paper.nongmark");
        var outPath = Path.Combine(dir, "paper.docx");
        File.WriteAllText(source, """
---
title: 校企共建沸石基矿物材料教授工作站方案书
author: Nong
date: 2026-06-07
---

# 研究基础

中文正文（Zeolite mineral material）需要保留中文宋体和英文 Times New Roman 语义。

::: table {caption="表1 任务分解"}
| 任务 | 指标 |
| --- | --- |
| 论文 | SCI 二区 |
| 专利 | 授权 |
:::

::: references
[1] Smith J. Zeolite materials research. Journal of Minerals, 2024.
:::
""");

        try
        {
            var (json, exit) = Run("word", "create", source, "-o", outPath, "--json");
            Assert.True(exit == 0, json);

            using var createDoc = Parse(json);
            Assert.Equal("ok", createDoc.RootElement.GetProperty("status").GetString());
            Assert.Equal("word create", createDoc.RootElement.GetProperty("command").GetString());
            Assert.True(File.Exists(outPath));

            var (validateJson, validateExit) = Run("word", "validate", outPath, "--json");
            Assert.True(validateExit == 0, validateJson);

            var (readJson, readExit) = Run("word", "read", outPath, "--json");
            Assert.True(readExit == 0, readJson);
            using var readDoc = Parse(readJson);
            var text = readDoc.RootElement.GetProperty("data").GetProperty("text").GetString() ?? "";
            Assert.Contains("研究基础", text);
            Assert.Contains("参考文献", text);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void WordCreate_MissingFile_Returns_E001()
    {
        RequireCli();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-create-missing-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var (json, exit) = Run("word", "create", "missing.nongmark", "-o", outPath, "--json");
        Assert.NotEqual(0, exit);

        using var doc = Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("E001", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void WordCreate_InvalidOutputExtension_Returns_E006()
    {
        RequireCli();
        var source = Path.Combine(Path.GetTempPath(), "nong-create-invalid-" + Guid.NewGuid().ToString("N")[..8] + ".nongmark");
        File.WriteAllText(source, "# Title");
        try
        {
            var (json, exit) = Run("word", "create", source, "-o", "out.txt", "--json");
            Assert.NotEqual(0, exit);

            using var doc = Parse(json);
            Assert.Equal("E006", doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally
        {
            try { File.Delete(source); } catch { }
        }
    }

    [Fact]
    public void WordAcademicFormat_MinimalDoc_ValidatesOk()
    {
        RequireCli();
        var docx = CreateFormattedDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-academic-format-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "academic-format", docx, "-o", outPath, "--json");
            Assert.True(exit == 0, json);

            using var formatDoc = Parse(json);
            Assert.Equal("ok", formatDoc.RootElement.GetProperty("status").GetString());
            Assert.True(formatDoc.RootElement.GetProperty("data").GetProperty("tablesFormatted").GetInt32() > 0);

            var (validateJson, validateExit) = Run("word", "validate", outPath, "--json");
            Assert.True(validateExit == 0, validateJson);
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void WordAcademicFormat_ZeoliteRegression_ValidatesAndKeepsFormatEvidence()
    {
        RequireCli();
        var docx = TestAsset(Path.Combine("WordRegression", "dirty-ooxml", "zeolite-workstation-beautified-dirty.docx"));
        Assert.True(File.Exists(docx), $"Missing regression asset: {docx}");

        var dir = Path.Combine(Path.GetTempPath(), "nong-zeolite-regression-" + Guid.NewGuid().ToString("N")[..8]);
        var outPath = Path.Combine(dir, "zeolite.academic.docx");
        var sliceDir = Path.Combine(dir, "slice");
        Directory.CreateDirectory(dir);
        try
        {
            var (formatJson, formatExit) = Run("word", "academic-format", docx, "-o", outPath, "--json");
            Assert.True(formatExit == 0, formatJson);

            var (validateJson, validateExit) = Run("word", "validate", outPath, "--json");
            Assert.True(validateExit == 0, validateJson);

            var (dissectJson, dissectExit) = Run("word", "dissect", outPath, "-o", sliceDir, "--json");
            Assert.True(dissectExit == 0, dissectJson);

            var documentXml = ReadZipEntry(outPath, "word/document.xml");
            var stylesXml = ReadZipEntry(outPath, "word/styles.xml");
            Assert.Empty(Regex.Matches(documentXml, @"w:(firstRow|lastRow|firstColumn|lastColumn|noHBand|noVBand)="));
            Assert.Equal(0, CountMisplacedTableCellProperties(documentXml));
            Assert.Equal(0, CountDirectStyleTableCellProperties(stylesXml));
            Assert.Contains("w:lineRule=\"atLeast\"", documentXml);
            Assert.Contains("<w:insideH w:val=\"nil\"", documentXml);
            Assert.Contains("<w:insideV w:val=\"nil\"", documentXml);

            var contentJsonl = File.ReadAllText(Path.Combine(sliceDir, "content.jsonl"));
            Assert.Contains("\"fontAscii\":\"Times New Roman\"", contentJsonl);
            Assert.Contains("\"lineSpacing\":\"480\"", contentJsonl);

            var format = File.ReadAllText(Path.Combine(sliceDir, "format.json"));
            Assert.Contains("\"tables\"", format);
            Assert.Contains("\"Times New Roman\"", format);

            var paragraphs = BodyParagraphs(documentXml);
            var title = FirstParagraphContaining(paragraphs, "校企共建");
            Assert.Equal("center", ParagraphJustification(title));
            Assert.Null(ParagraphIndent(title)?.Attribute(WordprocessingNs + "firstLine")?.Value);
            Assert.Equal("44", FirstRunProperty(title, "sz")?.Attribute(WordprocessingNs + "val")?.Value);
            Assert.Equal("黑体", FirstRunProperty(title, "rFonts")?.Attribute(WordprocessingNs + "eastAsia")?.Value);

            var subtitle = FirstParagraphContaining(paragraphs, "编制日期");
            Assert.Equal("center", ParagraphJustification(subtitle));
            Assert.Null(ParagraphIndent(subtitle)?.Attribute(WordprocessingNs + "firstLine")?.Value);
            Assert.Equal("24", FirstRunProperty(subtitle, "sz")?.Attribute(WordprocessingNs + "val")?.Value);

            var heading1 = FirstParagraphContaining(paragraphs, "一、项目摘要");
            Assert.Equal("center", ParagraphJustification(heading1));
            Assert.Null(ParagraphIndent(heading1)?.Attribute(WordprocessingNs + "firstLine")?.Value);
            Assert.Equal("32", FirstRunProperty(heading1, "sz")?.Attribute(WordprocessingNs + "val")?.Value);
            Assert.Equal("黑体", FirstRunProperty(heading1, "rFonts")?.Attribute(WordprocessingNs + "eastAsia")?.Value);
            Assert.NotNull(FirstRunProperties(heading1)?.Element(WordprocessingNs + "b"));

            var heading2 = FirstParagraphContaining(paragraphs, "2.1 产业痛点");
            Assert.Equal("left", ParagraphJustification(heading2));
            Assert.Null(ParagraphIndent(heading2)?.Attribute(WordprocessingNs + "firstLine")?.Value);
            Assert.Equal("28", FirstRunProperty(heading2, "sz")?.Attribute(WordprocessingNs + "val")?.Value);

            var bodyParagraph = FirstParagraphContaining(paragraphs, "本项目以");
            Assert.Equal("both", ParagraphJustification(bodyParagraph));
            Assert.Equal("480", ParagraphIndent(bodyParagraph)?.Attribute(WordprocessingNs + "firstLine")?.Value);
            Assert.Equal("480", ParagraphSpacing(bodyParagraph)?.Attribute(WordprocessingNs + "line")?.Value);
            Assert.Equal("atLeast", ParagraphSpacing(bodyParagraph)?.Attribute(WordprocessingNs + "lineRule")?.Value);
            Assert.Equal("24", FirstRunProperty(bodyParagraph, "sz")?.Attribute(WordprocessingNs + "val")?.Value);
            Assert.Equal("宋体", FirstRunProperty(bodyParagraph, "rFonts")?.Attribute(WordprocessingNs + "eastAsia")?.Value);
            Assert.Equal("Times New Roman", FirstRunProperty(bodyParagraph, "rFonts")?.Attribute(WordprocessingNs + "ascii")?.Value);

            var document = XDocument.Parse(documentXml);
            var firstTable = document.Descendants(WordprocessingNs + "tbl").First();
            var tableProperties = firstTable.Element(WordprocessingNs + "tblPr");
            Assert.Equal("fixed", tableProperties?.Element(WordprocessingNs + "tblLayout")?.Attribute(WordprocessingNs + "type")?.Value);
            Assert.NotNull(tableProperties?.Element(WordprocessingNs + "tblCellMar"));
            var tableBorders = tableProperties?.Element(WordprocessingNs + "tblBorders");
            Assert.Equal("single", TableBorder(tableBorders, "top")?.Attribute(WordprocessingNs + "val")?.Value);
            Assert.Equal("12", TableBorder(tableBorders, "top")?.Attribute(WordprocessingNs + "sz")?.Value);
            Assert.Equal("single", TableBorder(tableBorders, "bottom")?.Attribute(WordprocessingNs + "val")?.Value);
            Assert.Equal("12", TableBorder(tableBorders, "bottom")?.Attribute(WordprocessingNs + "sz")?.Value);
            Assert.Equal("nil", TableBorder(tableBorders, "left")?.Attribute(WordprocessingNs + "val")?.Value);
            Assert.Equal("nil", TableBorder(tableBorders, "right")?.Attribute(WordprocessingNs + "val")?.Value);
            Assert.Equal("nil", TableBorder(tableBorders, "insideH")?.Attribute(WordprocessingNs + "val")?.Value);
            Assert.Equal("nil", TableBorder(tableBorders, "insideV")?.Attribute(WordprocessingNs + "val")?.Value);
            Assert.NotNull(firstTable.Element(WordprocessingNs + "tr")?.Element(WordprocessingNs + "trPr")?.Element(WordprocessingNs + "tblHeader"));
            var firstCellProperties = firstTable.Descendants(WordprocessingNs + "tcPr").First();
            Assert.NotNull(firstCellProperties.Element(WordprocessingNs + "tcMar"));
            Assert.Empty(firstTable.Descendants(WordprocessingNs + "shd"));
            var headerBottom = firstCellProperties.Element(WordprocessingNs + "tcBorders")?.Element(WordprocessingNs + "bottom");
            Assert.Equal("single", headerBottom?.Attribute(WordprocessingNs + "val")?.Value);
            Assert.Equal("6", headerBottom?.Attribute(WordprocessingNs + "sz")?.Value);

            var longTableParagraph = firstTable.Descendants(WordprocessingNs + "p")
                .FirstOrDefault(p => ParagraphText(p).Length >= 12 || ParagraphText(p).Contains('：'));
            Assert.NotNull(longTableParagraph);
            Assert.Equal("left", ParagraphJustification(longTableParagraph!));
            Assert.Equal("0", ParagraphIndent(longTableParagraph!)?.Attribute(WordprocessingNs + "firstLine")?.Value);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void WordFormatAudit_AcademicFormattedDoc_ReturnsVisibleEvidence()
    {
        RequireCli();
        var docx = CreateFormatAuditSourceDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-format-audit-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (formatJson, formatExit) = Run("word", "academic-format", docx, "-o", outPath, "--json");
            Assert.True(formatExit == 0, formatJson);

            var (auditJson, auditExit) = Run("word", "format-audit", outPath, "--fail-on-warning", "--min-score", "95", "--json");
            Assert.True(auditExit == 0, auditJson);

            using var auditDoc = Parse(auditJson);
            var root = auditDoc.RootElement;
            Assert.Equal("ok", root.GetProperty("status").GetString());
            Assert.Equal("word format-audit", root.GetProperty("command").GetString());
            var data = root.GetProperty("data");
            Assert.Equal("nong-word/format-audit/v1", data.GetProperty("schemaVersion").GetString());
            Assert.Equal("academic", data.GetProperty("profile").GetString());
            Assert.Equal("pass", data.GetProperty("statusLevel").GetString());
            Assert.True(data.GetProperty("score").GetInt32() >= 90);

            var summary = data.GetProperty("summary");
            Assert.True(summary.GetProperty("headings").GetInt32() >= 2);
            Assert.Equal(1, summary.GetProperty("tables").GetInt32());

            var headings = data.GetProperty("headings");
            Assert.True(headings.GetProperty("byLevel").TryGetProperty("1", out _));
            Assert.True(headings.GetProperty("byLevel").TryGetProperty("2", out _));
            Assert.Contains(headings.GetProperty("samples").EnumerateArray(),
                h => h.GetProperty("text").GetString()!.Contains("项目摘要", StringComparison.Ordinal)
                    && h.GetProperty("fontEastAsia").GetString() == "黑体");

            var body = data.GetProperty("body");
            Assert.True(body.GetProperty("twoCharFirstLineIndent").GetInt32() >= 1);
            Assert.True(body.GetProperty("justified").GetInt32() >= 1);
            Assert.Contains(body.GetProperty("samples").EnumerateArray(),
                p => p.GetProperty("fontEastAsia").GetString() == "宋体"
                    && p.GetProperty("fontAscii").GetString() == "Times New Roman"
                    && p.GetProperty("firstLineIndent").GetString() == "480");

            var fonts = data.GetProperty("fonts");
            Assert.True(fonts.GetProperty("eastAsiaFonts").TryGetProperty("宋体", out _));
            Assert.True(fonts.GetProperty("asciiFonts").TryGetProperty("Times New Roman", out _));

            var lineSpacing = data.GetProperty("lineSpacing");
            Assert.True(lineSpacing.GetProperty("paragraphRules").TryGetProperty("atLeast", out _));
            Assert.True(lineSpacing.GetProperty("paragraphLines").TryGetProperty("480", out _));

            var tables = data.GetProperty("tables");
            Assert.Equal(1, tables.GetProperty("threeLineLike").GetInt32());
            var table = tables.GetProperty("samples")[0];
            Assert.True(table.GetProperty("threeLineLike").GetBoolean());
            Assert.Equal(12, table.GetProperty("topBorderSize").GetInt32());
            Assert.Equal(6, table.GetProperty("headerBottomBorderSize").GetInt32());
            Assert.Equal(12, table.GetProperty("bottomBorderSize").GetInt32());

            var latinNames = data.GetProperty("latinNames");
            Assert.True(latinNames.GetProperty("candidates").GetInt32() >= 1);
            Assert.True(latinNames.GetProperty("italicized").GetInt32() >= 1);
            Assert.Contains(latinNames.GetProperty("samples").EnumerateArray(),
                s => s.GetProperty("latinName").GetString() == "Solanum lycopersicum"
                    && s.GetProperty("insideParentheses").GetBoolean()
                    && s.GetProperty("italic").GetBoolean()
                    && s.GetProperty("fontAscii").GetString() == "Times New Roman");

            var chemistry = data.GetProperty("chemistry");
            Assert.True(chemistry.GetProperty("candidates").GetInt32() >= 3);
            Assert.Equal(
                chemistry.GetProperty("candidates").GetInt32(),
                chemistry.GetProperty("subscripted").GetInt32());
            Assert.Contains(chemistry.GetProperty("samples").EnumerateArray(),
                s => s.GetProperty("formula").GetString() == "N2O"
                    && s.GetProperty("subscriptedDigits").GetBoolean());
            Assert.Contains(chemistry.GetProperty("samples").EnumerateArray(),
                s => s.GetProperty("formula").GetString() == "H2O2"
                    && s.GetProperty("subscriptedDigits").GetBoolean());
            Assert.Contains(chemistry.GetProperty("samples").EnumerateArray(),
                s => s.GetProperty("formula").GetString() == "O2-"
                    && s.GetProperty("subscriptedDigits").GetBoolean());

            Assert.Equal(0, root.GetProperty("issues").GetArrayLength());
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void WordFormatAudit_GateOptions_ReturnE006WithFullAuditData()
    {
        RequireCli();
        var docx = CreateMinimalDocx();
        try
        {
            var (json, exit) = Run("word", "format-audit", docx, "--fail-on-warning", "--min-score", "100", "--json");

            Assert.NotEqual(0, exit);
            using var doc = Parse(json);
            var root = doc.RootElement;
            Assert.Equal("error", root.GetProperty("status").GetString());
            Assert.Equal("word format-audit", root.GetProperty("command").GetString());
            Assert.Equal("E006", root.GetProperty("errors")[0].GetProperty("code").GetString());
            Assert.True(root.GetProperty("data").TryGetProperty("statusLevel", out _));
            Assert.True(root.GetProperty("data").TryGetProperty("headings", out _));
            Assert.True(root.GetProperty("data").TryGetProperty("body", out _));
            Assert.True(root.GetProperty("data").TryGetProperty("tables", out _));
            Assert.True(root.GetProperty("data").TryGetProperty("chemistry", out _));
            Assert.True(root.GetProperty("metrics").GetProperty("gateFailed").GetBoolean());
            Assert.Contains(root.GetProperty("issues").EnumerateArray(),
                issue => issue.GetProperty("id").GetString() == "format_audit_gate"
                    && issue.GetProperty("severity").GetString() == "error");
        }
        finally
        {
            try { File.Delete(docx); } catch { }
        }
    }

    [Fact]
    public void WordDissect_AcademicFormattedDoc_IncludesFormatAuditVisualEvidence()
    {
        RequireCli();
        var docx = CreateFormatAuditSourceDocx();
        var dir = Path.Combine(Path.GetTempPath(), "nong-dissect-format-evidence-" + Guid.NewGuid().ToString("N")[..8]);
        var outPath = Path.Combine(dir, "academic.docx");
        var sliceDir = Path.Combine(dir, "slice");
        Directory.CreateDirectory(dir);
        try
        {
            var (formatJson, formatExit) = Run("word", "academic-format", docx, "-o", outPath, "--json");
            Assert.True(formatExit == 0, formatJson);

            var (dissectJson, dissectExit) = Run("word", "dissect", outPath, "-o", sliceDir, "--json");
            Assert.True(dissectExit == 0, dissectJson);

            using var format = Parse(File.ReadAllText(Path.Combine(sliceDir, "format.json")));
            var visual = format.RootElement.GetProperty("visualEvidence");
            Assert.Equal("docx", visual.GetProperty("format").GetString());
            Assert.Equal("pass", visual.GetProperty("audit").GetProperty("statusLevel").GetString());
            Assert.True(visual.GetProperty("headings").GetArrayLength() >= 2);
            Assert.True(visual.GetProperty("body").GetArrayLength() >= 1);
            Assert.Contains(visual.GetProperty("fonts").EnumerateArray(),
                f => f.GetString()!.Contains("宋体", StringComparison.Ordinal));
            Assert.Contains(visual.GetProperty("lineSpacing").EnumerateArray(),
                l => l.GetString()!.Contains("rule=atLeast", StringComparison.Ordinal));
            Assert.Contains(visual.GetProperty("tables").EnumerateArray(),
                t => t.GetString()!.Contains("threeLine=True", StringComparison.Ordinal)
                    && t.GetString()!.Contains("header=6", StringComparison.Ordinal)
                    && t.GetString()!.Contains("shading=0", StringComparison.Ordinal)
                    && t.GetString()!.Contains("cellIndent=0", StringComparison.Ordinal));
            Assert.Contains(visual.GetProperty("latinNames").EnumerateArray(),
                l => l.GetString()!.Contains("Solanum lycopersicum", StringComparison.Ordinal)
                    && l.GetString()!.Contains("italic=True", StringComparison.Ordinal));
            Assert.Contains(visual.GetProperty("chemistry").EnumerateArray(),
                c => c.GetString()!.Contains("N2O", StringComparison.Ordinal)
                    && c.GetString()!.Contains("subscriptedDigits=True", StringComparison.Ordinal));
            Assert.Equal(
                visual.GetProperty("audit").GetProperty("chemistryCandidates").GetString(),
                visual.GetProperty("audit").GetProperty("chemistrySubscripted").GetString());
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void WordAcademicFormat_ControlledDirtyOoxmlMatrix_RepairsVisualAndSchemaEvidence()
    {
        RequireCli();
        var docx = CreateControlledDirtyAcademicDocx();
        var dir = Path.Combine(Path.GetTempPath(), "nong-controlled-dirty-" + Guid.NewGuid().ToString("N")[..8]);
        var outPath = Path.Combine(dir, "controlled.academic.docx");
        Directory.CreateDirectory(dir);
        try
        {
            var (formatJson, formatExit) = Run("word", "academic-format", docx, "-o", outPath, "--json");
            Assert.True(formatExit == 0, formatJson);

            var (validateJson, validateExit) = Run("word", "validate", outPath, "--json");
            Assert.True(validateExit == 0, validateJson);

            var (auditJson, auditExit) = Run("word", "format-audit", outPath, "--fail-on-warning", "--min-score", "95", "--json");
            Assert.True(auditExit == 0, auditJson);
            using var audit = Parse(auditJson);
            var data = audit.RootElement.GetProperty("data");
            Assert.Equal("pass", data.GetProperty("statusLevel").GetString());
            Assert.True(data.GetProperty("score").GetInt32() >= 95);
            Assert.True(data.GetProperty("lineSpacing").GetProperty("paragraphLines").TryGetProperty("480", out _));
            Assert.False(data.GetProperty("lineSpacing").GetProperty("documentGridDetected").GetBoolean());

            var tables = data.GetProperty("tables");
            Assert.Equal(1, tables.GetProperty("total").GetInt32());
            Assert.Equal(1, tables.GetProperty("threeLineLike").GetInt32());
            Assert.Equal(0, tables.GetProperty("withShading").GetInt32());
            Assert.Equal(1, tables.GetProperty("headerRowsRepeated").GetInt32());

            var chemistry = data.GetProperty("chemistry");
            Assert.True(chemistry.GetProperty("candidates").GetInt32() >= 3);
            Assert.Equal(chemistry.GetProperty("candidates").GetInt32(), chemistry.GetProperty("subscripted").GetInt32());
            Assert.Contains(chemistry.GetProperty("samples").EnumerateArray(),
                s => s.GetProperty("formula").GetString() == "N2O"
                    && s.GetProperty("subscriptedDigits").GetBoolean());

            var documentXml = ReadZipEntry(outPath, "word/document.xml");
            var stylesXml = ReadZipEntry(outPath, "word/styles.xml");
            Assert.DoesNotContain("<w:docGrid", documentXml);
            Assert.Empty(Regex.Matches(documentXml, @"w:(firstRow|lastRow|firstColumn|lastColumn|noHBand|noVBand)="));
            Assert.Equal(0, CountMisplacedTableCellProperties(documentXml));
            Assert.Equal(0, CountDirectStyleTableCellProperties(stylesXml));
            Assert.DoesNotContain(XDocument.Parse(documentXml).Descendants(WordprocessingNs + "tbl").Descendants(WordprocessingNs + "shd"),
                _ => true);

            var document = XDocument.Parse(documentXml);
            AssertFormulaDigitsSubscript(document, "N2O");
            AssertFormulaDigitsSubscript(document, "H2O2");
            AssertFormulaDigitsSubscript(document, "O2-");
            AssertThreeLineTableBorders(document.Descendants(WordprocessingNs + "tbl").Single());

            var settingsXml = ReadZipEntry(outPath, "word/settings.xml");
            AssertMathSettingsOrder(settingsXml);
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Theory]
    [InlineData("zeolite-workstation-handwritten.docx", 18)]
    [InlineData("zeolite-workstation-original.docx", 17)]
    [InlineData("zeolite-workstation-beautified.docx", 17)]
    public void WordAcademicFormat_RealZeoliteRegressionAssets_AuditAndOoxmlEvidence(string fileName, int expectedTables)
    {
        RequireCli();
        var docx = TestAsset(Path.Combine("WordRegression", "academic-format", fileName));
        Assert.True(File.Exists(docx), $"Missing regression asset: {docx}");

        var dir = Path.Combine(Path.GetTempPath(), "nong-real-zeolite-" + Guid.NewGuid().ToString("N")[..8]);
        var outPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(fileName) + ".academic.docx");
        var sliceDir = Path.Combine(dir, "slice");
        Directory.CreateDirectory(dir);
        try
        {
            var (formatJson, formatExit) = Run("word", "academic-format", docx, "-o", outPath, "--json");
            Assert.True(formatExit == 0, formatJson);

            var (validateJson, validateExit) = Run("word", "validate", outPath, "--json");
            Assert.True(validateExit == 0, validateJson);

            var (auditJson, auditExit) = Run("word", "format-audit", outPath, "--json");
            Assert.True(auditExit == 0, auditJson);
            using var audit = Parse(auditJson);
            var data = audit.RootElement.GetProperty("data");
            Assert.Equal("pass", data.GetProperty("statusLevel").GetString());
            Assert.True(data.GetProperty("score").GetInt32() >= 95);

            var tables = data.GetProperty("tables");
            Assert.Equal(expectedTables, tables.GetProperty("total").GetInt32());
            Assert.Equal(expectedTables, tables.GetProperty("threeLineLike").GetInt32());
            Assert.Equal(0, tables.GetProperty("withShading").GetInt32());
            Assert.Equal(expectedTables, tables.GetProperty("headerRowsRepeated").GetInt32());
            foreach (var sample in tables.GetProperty("samples").EnumerateArray())
            {
                Assert.True(sample.GetProperty("threeLineLike").GetBoolean());
                Assert.Equal(12, sample.GetProperty("topBorderSize").GetInt32());
                Assert.Equal(6, sample.GetProperty("headerBottomBorderSize").GetInt32());
                Assert.Equal(12, sample.GetProperty("bottomBorderSize").GetInt32());
                Assert.Equal(0, sample.GetProperty("shadingCount").GetInt32());
                Assert.Equal(0, sample.GetProperty("cellFirstLineIndentCount").GetInt32());
                Assert.True(sample.GetProperty("headerRowsRepeated").GetBoolean());
            }

            var chemistry = data.GetProperty("chemistry");
            Assert.True(chemistry.GetProperty("candidates").GetInt32() >= 3);
            Assert.Equal(
                chemistry.GetProperty("candidates").GetInt32(),
                chemistry.GetProperty("subscripted").GetInt32());

            var documentXml = ReadZipEntry(outPath, "word/document.xml");
            Assert.DoesNotContain("<w:docGrid", documentXml);
            var document = XDocument.Parse(documentXml);
            var bodyTables = document.Root?
                .Element(WordprocessingNs + "body")
                ?.Elements(WordprocessingNs + "tbl")
                .ToList() ?? new List<XElement>();
            Assert.Equal(expectedTables, bodyTables.Count);
            foreach (var table in bodyTables)
                AssertThreeLineTableBorders(table);
            Assert.DoesNotContain(document.Descendants(WordprocessingNs + "tbl").Descendants(WordprocessingNs + "shd"),
                _ => true);
            Assert.DoesNotContain(document.Descendants(WordprocessingNs + "tbl").Descendants(WordprocessingNs + "p"),
                p => ParagraphIndent(p)?.Attribute(WordprocessingNs + "firstLine")?.Value is string value && value != "0");

            AssertFormulaDigitsSubscript(document, "N2O");
            AssertFormulaDigitsSubscript(document, "H2O2");
            AssertFormulaDigitsSubscript(document, "O2-");

            var settingsXml = ReadZipEntry(outPath, "word/settings.xml");
            AssertMathSettingsOrder(settingsXml);

            var (dissectJson, dissectExit) = Run("word", "dissect", outPath, "-o", sliceDir, "--json");
            Assert.True(dissectExit == 0, dissectJson);
            using var sliceFormat = Parse(File.ReadAllText(Path.Combine(sliceDir, "format.json")));
            var visual = sliceFormat.RootElement.GetProperty("visualEvidence");
            Assert.Equal("pass", visual.GetProperty("audit").GetProperty("statusLevel").GetString());
            Assert.True(visual.GetProperty("headings").GetArrayLength() > 0);
            Assert.True(visual.GetProperty("body").GetArrayLength() > 0);
            Assert.True(visual.GetProperty("fonts").GetArrayLength() > 0);
            Assert.True(visual.GetProperty("lineSpacing").GetArrayLength() > 0);
            Assert.Contains(visual.GetProperty("tables").EnumerateArray(),
                t => t.GetString()!.Contains("threeLine=True", StringComparison.Ordinal)
                    && t.GetString()!.Contains("header=6", StringComparison.Ordinal));
            if (data.GetProperty("latinNames").GetProperty("candidates").GetInt32() > 0)
            {
                Assert.True(visual.GetProperty("latinNames").GetArrayLength() > 0);
                Assert.Contains(visual.GetProperty("latinNames").EnumerateArray(),
                    l => l.GetString()!.Contains("italic=True", StringComparison.Ordinal));
            }
            Assert.Contains(visual.GetProperty("chemistry").EnumerateArray(),
                c => c.GetString()!.Contains("N2O", StringComparison.Ordinal)
                    && c.GetString()!.Contains("subscriptedDigits=True", StringComparison.Ordinal));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void WordAcademicFormat_LatinInsideParentheses_ItalicizesOnlyInnerLatinText()
    {
        RequireCli();
        var docx = CreateLatinParenthesisDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-latin-parenthesis-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "academic-format", docx, "-o", outPath, "--json");
            Assert.True(exit == 0, json);

            var documentXml = ReadZipEntry(outPath, "word/document.xml");
            var document = XDocument.Parse(documentXml);
            var paragraph = document.Descendants(WordprocessingNs + "p")
                .First(p => ParagraphText(p).Contains("Solanum lycopersicum", StringComparison.Ordinal));
            var runs = paragraph.Elements(WordprocessingNs + "r").ToList();
            var chineseRun = runs.First(r => ParagraphText(r) == "番茄");
            var openParenRun = runs.First(r => ParagraphText(r) == "（");
            var latinNameRun = runs.First(r => ParagraphText(r).Contains("Solanum lycopersicum", StringComparison.Ordinal));
            var closeParenRun = runs.SkipWhile(r => r != latinNameRun).First(r => ParagraphText(r) == "）");
            var suffixRun = runs.First(r => ParagraphText(r).StartsWith("机制需要", StringComparison.Ordinal));

            Assert.Null(chineseRun.Element(WordprocessingNs + "rPr")?.Element(WordprocessingNs + "i"));
            Assert.Null(openParenRun.Element(WordprocessingNs + "rPr")?.Element(WordprocessingNs + "i"));
            Assert.NotNull(latinNameRun.Element(WordprocessingNs + "rPr")?.Element(WordprocessingNs + "i"));
            Assert.Equal("Times New Roman", latinNameRun.Element(WordprocessingNs + "rPr")?.Element(WordprocessingNs + "rFonts")?.Attribute(WordprocessingNs + "ascii")?.Value);
            Assert.Null(closeParenRun.Element(WordprocessingNs + "rPr")?.Element(WordprocessingNs + "i"));
            Assert.Null(suffixRun.Element(WordprocessingNs + "rPr")?.Element(WordprocessingNs + "i"));
            Assert.Equal(1, runs.Count(r => r.Element(WordprocessingNs + "rPr")?.Element(WordprocessingNs + "i") != null));

            var bareParagraph = document.Descendants(WordprocessingNs + "p")
                .First(p => ParagraphText(p).Contains("Phytophthora capsici", StringComparison.Ordinal));
            Assert.Contains("辣椒疫霉菌（Phytophthora capsici）需要自动补括号", ParagraphText(bareParagraph));
            var bareRuns = bareParagraph.Elements(WordprocessingNs + "r").ToList();
            var bareOpenParenRun = bareRuns.First(r => ParagraphText(r) == "（");
            var bareLatinNameRun = bareRuns.First(r => ParagraphText(r).Contains("Phytophthora capsici", StringComparison.Ordinal));
            var bareCloseParenRun = bareRuns.SkipWhile(r => r != bareLatinNameRun).First(r => ParagraphText(r) == "）");
            var technicalParenRun = bareRuns.First(r => ParagraphText(r).Contains("RNA-seq", StringComparison.Ordinal));

            Assert.Null(bareOpenParenRun.Element(WordprocessingNs + "rPr")?.Element(WordprocessingNs + "i"));
            Assert.NotNull(bareLatinNameRun.Element(WordprocessingNs + "rPr")?.Element(WordprocessingNs + "i"));
            Assert.Equal("Times New Roman", bareLatinNameRun.Element(WordprocessingNs + "rPr")?.Element(WordprocessingNs + "rFonts")?.Attribute(WordprocessingNs + "ascii")?.Value);
            Assert.Null(bareCloseParenRun.Element(WordprocessingNs + "rPr")?.Element(WordprocessingNs + "i"));
            Assert.Null(technicalParenRun.Element(WordprocessingNs + "rPr")?.Element(WordprocessingNs + "i"));

            var cultivarParagraph = document.Descendants(WordprocessingNs + "p")
                .First(p => ParagraphText(p).Contains("中蔬4号", StringComparison.Ordinal));
            Assert.Contains("番茄（Solanum lycopersicum） cv. 中蔬4号", ParagraphText(cultivarParagraph));
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void WordAcademicFormat_ChemicalFormulaDigits_SubscriptOnlyFormulaNumbers()
    {
        RequireCli();
        var docx = CreateChemicalFormulaDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-chemical-formula-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "academic-format", docx, "-o", outPath, "--json");
            Assert.True(exit == 0, json);

            var documentXml = ReadZipEntry(outPath, "word/document.xml");
            var document = XDocument.Parse(documentXml);
            var paragraph = document.Descendants(WordprocessingNs + "p")
                .First(p => ParagraphText(p).Contains("N2O", StringComparison.Ordinal));

            Assert.Equal("2026年沸石保氮减少N2O排放，同时记录H2O2、CO2和表头C1。", ParagraphText(paragraph));
            AssertRunSubscript(paragraph, "N", false);
            AssertRunSubscript(paragraph, "2", true);
            AssertRunSubscript(paragraph, "O", false);
            AssertRunSubscript(paragraph, "H", false);
            AssertRunSubscript(paragraph, "C", false);
            Assert.Equal(4, paragraph.Elements(WordprocessingNs + "r")
                .Count(run => ParagraphText(run) == "2" && IsSubscriptRun(run)));
            Assert.DoesNotContain(paragraph.Elements(WordprocessingNs + "r"),
                run => ParagraphText(run).Contains("2026", StringComparison.Ordinal)
                    && IsSubscriptRun(run));
            Assert.DoesNotContain(paragraph.Elements(WordprocessingNs + "r"),
                run => ParagraphText(run) == "1" && IsSubscriptRun(run));

            var (validateJson, validateExit) = Run("word", "validate", outPath, "--json");
            Assert.True(validateExit == 0, validateJson);
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void WordAcademicFormat_TableCaptionAndCells_RemoveShadingAndCellIndent()
    {
        RequireCli();
        var docx = CreateShadedCaptionTableDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-shaded-caption-table-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "academic-format", docx, "-o", outPath, "--json");
            Assert.True(exit == 0, json);

            var documentXml = ReadZipEntry(outPath, "word/document.xml");
            var document = XDocument.Parse(documentXml);
            var caption = document.Descendants(WordprocessingNs + "p")
                .First(p => ParagraphText(p).Contains("表1 试验结果统计", StringComparison.Ordinal));
            var table = document.Descendants(WordprocessingNs + "tbl").First();

            Assert.Equal("center", ParagraphJustification(caption));
            Assert.Null(ParagraphIndent(caption)?.Attribute(WordprocessingNs + "firstLine")?.Value);
            Assert.Empty(caption.Descendants(WordprocessingNs + "shd"));
            Assert.Equal("21", FirstRunProperty(caption, "sz")?.Attribute(WordprocessingNs + "val")?.Value);
            Assert.Equal("宋体", FirstRunProperty(caption, "rFonts")?.Attribute(WordprocessingNs + "eastAsia")?.Value);

            Assert.Empty(table.Descendants(WordprocessingNs + "shd"));
            foreach (var paragraph in table.Descendants(WordprocessingNs + "p"))
            {
                Assert.Equal("0", ParagraphIndent(paragraph)?.Attribute(WordprocessingNs + "firstLine")?.Value);
            }

            AssertThreeLineTableBorders(table);
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void WordTableReflow_LongTable_SplitsRowsWithContinuationHeader()
    {
        RequireCli();
        var docx = CreateLongTableDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-table-reflow-long-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "table-reflow", docx, "-o", outPath, "--max-rows", "2", "--json");
            Assert.True(exit == 0, json);

            using var reflowDoc = Parse(json);
            Assert.Equal("ok", reflowDoc.RootElement.GetProperty("status").GetString());
            Assert.Equal(1, reflowDoc.RootElement.GetProperty("data").GetProperty("longTablesSplit").GetInt32());
            Assert.Equal(3, reflowDoc.RootElement.GetProperty("data").GetProperty("outputTables").GetInt32());

            var (validateJson, validateExit) = Run("word", "validate", outPath, "--json");
            Assert.True(validateExit == 0, validateJson);

            var documentXml = ReadZipEntry(outPath, "word/document.xml");
            var document = XDocument.Parse(documentXml);
            var tables = document.Descendants(WordprocessingNs + "tbl").ToList();
            Assert.Equal(3, tables.Count);
            Assert.Equal(2, document.Descendants(WordprocessingNs + "p").Count(p => ParagraphText(p).Contains("续表", StringComparison.Ordinal)));
            foreach (var table in tables)
            {
                Assert.NotNull(table.Element(WordprocessingNs + "tr")?.Element(WordprocessingNs + "trPr")?.Element(WordprocessingNs + "tblHeader"));
                AssertThreeLineTableBorders(table);
            }
            Assert.Equal("6", TableBorder(tables[0].Element(WordprocessingNs + "tblPr")?.Element(WordprocessingNs + "tblBorders"), "bottom")?.Attribute(WordprocessingNs + "sz")?.Value);
            Assert.Equal("6", TableBorder(tables[1].Element(WordprocessingNs + "tblPr")?.Element(WordprocessingNs + "tblBorders"), "bottom")?.Attribute(WordprocessingNs + "sz")?.Value);
            Assert.Equal("12", TableBorder(tables[2].Element(WordprocessingNs + "tblPr")?.Element(WordprocessingNs + "tblBorders"), "bottom")?.Attribute(WordprocessingNs + "sz")?.Value);
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void WordTableReflow_WideTable_SplitsColumnsAndRepeatsLeftColumns()
    {
        RequireCli();
        var docx = CreateWideTableDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-table-reflow-wide-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "table-reflow", docx, "-o", outPath, "--max-cols", "3", "--repeat-left-cols", "1", "--json");
            Assert.True(exit == 0, json);

            using var reflowDoc = Parse(json);
            Assert.Equal("ok", reflowDoc.RootElement.GetProperty("status").GetString());
            Assert.Equal(1, reflowDoc.RootElement.GetProperty("data").GetProperty("wideTablesSplit").GetInt32());
            Assert.Equal(2, reflowDoc.RootElement.GetProperty("data").GetProperty("outputTables").GetInt32());

            var (validateJson, validateExit) = Run("word", "validate", outPath, "--json");
            Assert.True(validateExit == 0, validateJson);

            var documentXml = ReadZipEntry(outPath, "word/document.xml");
            var document = XDocument.Parse(documentXml);
            var tables = document.Descendants(WordprocessingNs + "tbl").ToList();
            Assert.Equal(2, tables.Count);
            var firstHeader = tables[0].Elements(WordprocessingNs + "tr").First().Elements(WordprocessingNs + "tc").Select(CellText).ToArray();
            var secondHeader = tables[1].Elements(WordprocessingNs + "tr").First().Elements(WordprocessingNs + "tc").Select(CellText).ToArray();
            Assert.Equal(new[] { "样品", "C1", "C2" }, firstHeader);
            Assert.Equal(new[] { "样品", "C3", "C4" }, secondHeader);
            foreach (var table in tables)
                AssertThreeLineTableBorders(table);
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void WordTableReflow_LongWideTable_SplitsRowsAndColumnsWithContinuationEvidence()
    {
        RequireCli();
        var docx = CreateLongWideTableDocx();
        var outPath = Path.Combine(Path.GetTempPath(), "nong-table-reflow-long-wide-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var (json, exit) = Run("word", "table-reflow", docx, "-o", outPath,
                "--max-rows", "2", "--max-cols", "3", "--repeat-left-cols", "1", "--json");
            Assert.True(exit == 0, json);

            using var reflowDoc = Parse(json);
            var data = reflowDoc.RootElement.GetProperty("data");
            Assert.Equal(1, data.GetProperty("longTablesSplit").GetInt32());
            Assert.Equal(1, data.GetProperty("wideTablesSplit").GetInt32());
            Assert.Equal(9, data.GetProperty("outputTables").GetInt32());
            Assert.Equal(8, data.GetProperty("continuationLabelsInserted").GetInt32());

            var (validateJson, validateExit) = Run("word", "validate", outPath, "--json");
            Assert.True(validateExit == 0, validateJson);

            var documentXml = ReadZipEntry(outPath, "word/document.xml");
            var document = XDocument.Parse(documentXml);
            var tables = document.Descendants(WordprocessingNs + "tbl").ToList();
            Assert.Equal(9, tables.Count);
            Assert.Equal(8, document.Descendants(WordprocessingNs + "p").Count(p => ParagraphText(p).Contains("续表", StringComparison.Ordinal)));

            var headers = tables
                .Select(table => table.Elements(WordprocessingNs + "tr").First().Elements(WordprocessingNs + "tc").Select(CellText).ToArray())
                .ToArray();
            Assert.Equal(new[] { "样品", "C1", "C2" }, headers[0]);
            Assert.Equal(new[] { "样品", "C3", "C4" }, headers[3]);
            Assert.Equal(new[] { "样品", "C5" }, headers[6]);

            for (var i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                Assert.NotNull(table.Element(WordprocessingNs + "tr")?.Element(WordprocessingNs + "trPr")?.Element(WordprocessingNs + "tblHeader"));
                AssertThreeLineTableBorders(table);

                var bottomSize = TableBorder(table.Element(WordprocessingNs + "tblPr")?.Element(WordprocessingNs + "tblBorders"), "bottom")
                    ?.Attribute(WordprocessingNs + "sz")
                    ?.Value;
                Assert.Equal(i % 3 == 2 ? "12" : "6", bottomSize);
            }
        }
        finally
        {
            try { File.Delete(docx); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    static string ReadZipEntry(string docxPath, string entryName)
    {
        using var archive = ZipFile.OpenRead(docxPath);
        var entry = archive.GetEntry(entryName) ?? throw new FileNotFoundException(entryName, docxPath);
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    static int CountMisplacedTableCellProperties(string documentXml)
    {
        var doc = XDocument.Parse(documentXml);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return doc.Descendants(w + "tc")
            .Count(tc =>
            {
                var names = tc.Elements().Select(e => e.Name.LocalName).ToList();
                return names.IndexOf("tcPr") > 0;
            });
    }

    static int CountDirectStyleTableCellProperties(string stylesXml)
    {
        var doc = XDocument.Parse(stylesXml);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return doc.Descendants(w + "style")
            .Count(style => style.Elements(w + "tcPr").Any());
    }

    static List<XElement> BodyParagraphs(string documentXml)
    {
        var doc = XDocument.Parse(documentXml);
        return doc.Descendants(WordprocessingNs + "body")
            .Elements(WordprocessingNs + "p")
            .ToList();
    }

    static XElement FirstParagraphContaining(IEnumerable<XElement> paragraphs, string text) =>
        paragraphs.First(p => ParagraphText(p).Contains(text, StringComparison.Ordinal));

    static string ParagraphText(XElement paragraph) =>
        string.Concat(paragraph.Descendants(WordprocessingNs + "t").Select(t => t.Value));

    static string? ParagraphJustification(XElement paragraph) =>
        paragraph.Element(WordprocessingNs + "pPr")
            ?.Element(WordprocessingNs + "jc")
            ?.Attribute(WordprocessingNs + "val")
            ?.Value;

    static XElement? ParagraphIndent(XElement paragraph) =>
        paragraph.Element(WordprocessingNs + "pPr")?.Element(WordprocessingNs + "ind");

    static XElement? ParagraphSpacing(XElement paragraph) =>
        paragraph.Element(WordprocessingNs + "pPr")?.Element(WordprocessingNs + "spacing");

    static XElement? FirstRunProperties(XElement paragraph) =>
        paragraph.Descendants(WordprocessingNs + "r")
            .Select(run => run.Element(WordprocessingNs + "rPr"))
            .FirstOrDefault(runProperties => runProperties != null);

    static XElement? FirstRunProperty(XElement paragraph, string localName) =>
        FirstRunProperties(paragraph)?.Element(WordprocessingNs + localName);

    static XElement? TableBorder(XElement? borders, string localName) =>
        borders?.Element(WordprocessingNs + localName);

    static string CellText(XElement cell) =>
        string.Concat(cell.Descendants(WordprocessingNs + "t").Select(t => t.Value));

    static bool IsSubscriptRun(XElement run) =>
        run.Element(WordprocessingNs + "rPr")
            ?.Element(WordprocessingNs + "vertAlign")
            ?.Attribute(WordprocessingNs + "val")
            ?.Value == "subscript";

    static void AssertRunSubscript(XElement paragraph, string text, bool expected)
    {
        var run = paragraph.Elements(WordprocessingNs + "r")
            .First(r => ParagraphText(r) == text);
        Assert.Equal(expected, IsSubscriptRun(run));
    }

    static void AssertFormulaDigitsSubscript(XDocument document, string formula)
    {
        var paragraph = document.Descendants(WordprocessingNs + "p")
            .FirstOrDefault(p => ParagraphText(p).Contains(formula, StringComparison.Ordinal));
        Assert.NotNull(paragraph);
        AssertFormulaDigitsSubscript(paragraph!, formula);
    }

    static void AssertFormulaDigitsSubscript(XElement paragraph, string formula)
    {
        var chars = paragraph.Elements(WordprocessingNs + "r")
            .SelectMany(run => ParagraphText(run).Select(ch => (Text: ch, IsSubscript: IsSubscriptRun(run))))
            .ToList();
        var text = new string(chars.Select(c => c.Text).ToArray());
        var start = text.IndexOf(formula, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Formula '{formula}' not found in paragraph: {text}");

        for (var i = 0; i < formula.Length; i++)
        {
            var actual = chars[start + i].IsSubscript;
            if (char.IsDigit(formula[i]))
                Assert.True(actual, $"Formula digit '{formula[i]}' in '{formula}' should be subscript.");
            else
                Assert.False(actual, $"Formula non-digit '{formula[i]}' in '{formula}' should not be subscript.");
        }
    }

    static void AssertMathSettingsOrder(string settingsXml)
    {
        var settings = XDocument.Parse(settingsXml).Root;
        Assert.NotNull(settings);
        var names = settings!.Elements().Select(QualifiedSettingsName).ToList();
        AssertBeforeIfBothPresent(names, "w:rsids", "m:mathPr");
        AssertBeforeIfBothPresent(names, "m:mathPr", "w:themeFontLang");
        AssertBeforeIfBothPresent(names, "m:mathPr", "w:clrSchemeMapping");
        AssertBeforeIfBothPresent(names, "m:mathPr", "w14:docId");
        AssertBeforeIfBothPresent(names, "m:mathPr", "w15:docId");
    }

    static string QualifiedSettingsName(XElement element)
    {
        if (element.Name.NamespaceName == WordprocessingNs.NamespaceName)
            return "w:" + element.Name.LocalName;
        if (element.Name.NamespaceName == "http://schemas.openxmlformats.org/officeDocument/2006/math")
            return "m:" + element.Name.LocalName;
        if (element.Name.NamespaceName == "http://schemas.microsoft.com/office/word/2010/wordml")
            return "w14:" + element.Name.LocalName;
        if (element.Name.NamespaceName == "http://schemas.microsoft.com/office/word/2012/wordml")
            return "w15:" + element.Name.LocalName;
        return element.Name.LocalName;
    }

    static void AssertBeforeIfBothPresent(List<string> names, string before, string after)
    {
        var beforeIndex = names.IndexOf(before);
        var afterIndex = names.IndexOf(after);
        if (beforeIndex >= 0 && afterIndex >= 0)
            Assert.True(beforeIndex < afterIndex, $"{before} should appear before {after} in settings.xml.");
    }

    static void AssertThreeLineTableBorders(XElement table)
    {
        var borders = table.Element(WordprocessingNs + "tblPr")?.Element(WordprocessingNs + "tblBorders");
        Assert.Equal("single", TableBorder(borders, "top")?.Attribute(WordprocessingNs + "val")?.Value);
        Assert.Equal("12", TableBorder(borders, "top")?.Attribute(WordprocessingNs + "sz")?.Value);
        Assert.Equal("single", TableBorder(borders, "bottom")?.Attribute(WordprocessingNs + "val")?.Value);
        Assert.Equal("nil", TableBorder(borders, "left")?.Attribute(WordprocessingNs + "val")?.Value);
        Assert.Equal("nil", TableBorder(borders, "right")?.Attribute(WordprocessingNs + "val")?.Value);
        Assert.Equal("nil", TableBorder(borders, "insideH")?.Attribute(WordprocessingNs + "val")?.Value);
        Assert.Equal("nil", TableBorder(borders, "insideV")?.Attribute(WordprocessingNs + "val")?.Value);
        Assert.Empty(table.Descendants(WordprocessingNs + "shd"));
        foreach (var paragraph in table.Descendants(WordprocessingNs + "p"))
            Assert.Equal("0", ParagraphIndent(paragraph)?.Attribute(WordprocessingNs + "firstLine")?.Value);
        var headerBottom = table.Element(WordprocessingNs + "tr")
            ?.Element(WordprocessingNs + "tc")
            ?.Element(WordprocessingNs + "tcPr")
            ?.Element(WordprocessingNs + "tcBorders")
            ?.Element(WordprocessingNs + "bottom");
        Assert.Equal("single", headerBottom?.Attribute(WordprocessingNs + "val")?.Value);
        Assert.Equal("6", headerBottom?.Attribute(WordprocessingNs + "sz")?.Value);
    }
}
