using PandocCore;
using Xunit;

namespace Tests;

public class NongPandocTests
{
    [Fact]
    public void NongMarkWriter_WritesAcademicBlocks()
    {
        var doc = new NongPandocDocument
        {
            Metadata =
            {
                ["title"] = "校企共建方案书",
                ["author"] = "Nong"
            },
            Blocks =
            {
                NongHeadingBlock.FromText(1, "项目摘要", "sec-summary"),
                NongParagraphBlock.FromText("中文正文（Solanum lycopersicum）继续正文。"),
                new NongTableBlock
                {
                    Caption = "表1 任务分解",
                    Style = "three-line",
                    Headers = { "任务", "指标" },
                    Rows = { new List<string> { "论文", "SCI 二区" } }
                },
                new NongReferencesBlock
                {
                    Entries = { "[1] Smith J. Zeolite materials research. Journal of Minerals, 2024." }
                }
            }
        };

        var text = NongMarkTextWriter.Write(doc);

        Assert.Contains("title: 校企共建方案书", text);
        Assert.Contains("# 项目摘要 {#sec-summary}", text);
        Assert.Contains("style=\"three-line\"", text);
        Assert.Contains("::: references", text);
    }

    [Fact]
    public void NongMarkReader_ReadsFencedTableAndReferences()
    {
        var text = """
---
title: "校企共建方案书"
---

# 项目摘要 {#sec-summary}

中文正文（Solanum lycopersicum）继续正文。

::: table {caption="表1 任务分解" style="three-line"}
| 任务 | 指标 |
| --- | --- |
| 论文 | SCI 二区 |
:::

::: references
[1] Smith J. Zeolite materials research. Journal of Minerals, 2024.
:::
""";

        var doc = NongMarkTextReader.Read(text);

        Assert.Equal("校企共建方案书", doc.Metadata["title"]);
        var heading = Assert.IsType<NongHeadingBlock>(doc.Blocks[0]);
        Assert.Equal("sec-summary", heading.Id);
        var table = Assert.IsType<NongTableBlock>(doc.Blocks[2]);
        Assert.Equal("表1 任务分解", table.Caption);
        Assert.Equal("three-line", table.Style);
        Assert.Equal("SCI 二区", table.Rows[0][1]);
        var references = Assert.IsType<NongReferencesBlock>(doc.Blocks[3]);
        Assert.Single(references.Entries);
    }

    [Fact]
    public void RuntimePolicy_DoesNotDependOnExternalPandoc()
    {
        Assert.False(NongPandocRuntimePolicy.UsesBundledPandoc);
        Assert.False(NongPandocRuntimePolicy.RequiresExternalPandocExecutable);
        Assert.Contains("Apache-2.0", NongPandocRuntimePolicy.LicenseBoundary);
    }

    [Fact]
    public void PackageContract_AlignsSharedStreamNames()
    {
        var contract = new NongPandocPackageContract();
        var doc = new NongPandocDocument
        {
            Blocks =
            {
                NongHeadingBlock.FromText(1, "项目摘要"),
                NongParagraphBlock.FromText("正文"),
                new NongTableBlock { Headers = { "任务" }, Rows = { new List<string> { "论文" } } }
            }
        };

        var manifest = new NongPandocSliceManifest
        {
            Source = new NongPandocSourceInfo { Path = "a.docx", Format = "docx" },
            Metrics = NongPandocMetrics.FromDocument(doc)
        };

        Assert.Contains(NongPandocArtifactNames.ContentNongMark, contract.RequiredStreams);
        Assert.Contains(NongPandocArtifactNames.Diagnostics, contract.RequiredStreams);
        Assert.Contains(NongPandocArtifactNames.Format, contract.AiPrimaryReadOrder);
        Assert.Equal("content.nongmark", manifest.Streams.ContentNongMark);
        Assert.Equal("format.json", manifest.Streams.Format);
        Assert.Equal(3, manifest.Metrics.Blocks);
        Assert.Equal(1, manifest.Metrics.Tables);
    }

    [Fact]
    public void SlicePackageWriter_WritesSharedArtifacts()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nong-pandoc-slice-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var result = NongPandocSlicePackageWriter.Write(new NongPandocSliceWritePayload
            {
                OutputDirectory = dir,
                Manifest = new { schemaVersion = "test/v1", streams = NongPandocStreamPaths.Default },
                Document = new { schemaVersion = "test/v1", blocks = new[] { "p0001" } },
                ContentJsonlLines = new[] { """{"id":"p0001","kind":"paragraph"}""" },
                NongMarkText = "正文",
                Structure = new { outline = Array.Empty<object>() },
                Format = new { fonts = new[] { "SimSun" } },
                Diagnostics = new { warnings = Array.Empty<string>() },
                AssetsManifest = new { items = Array.Empty<object>() },
                TextPreview = "正文",
            });

            Assert.Equal(Path.GetFullPath(dir), result.OutputDirectory);
            Assert.True(File.Exists(Path.Combine(dir, "manifest.json")));
            Assert.True(File.Exists(Path.Combine(dir, "document.json")));
            Assert.True(File.Exists(Path.Combine(dir, "content.jsonl")));
            Assert.True(File.Exists(Path.Combine(dir, "content.nongmark")));
            Assert.True(File.Exists(Path.Combine(dir, "structure.json")));
            Assert.True(File.Exists(Path.Combine(dir, "format.json")));
            Assert.True(File.Exists(Path.Combine(dir, "diagnostics.json")));
            Assert.True(File.Exists(Path.Combine(dir, "assets", "manifest.json")));
            Assert.True(File.Exists(Path.Combine(dir, "preview", "content.txt")));
            Assert.Equal(result.ManifestPath, Path.GetFullPath(Path.Combine(dir, "manifest.json")));
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void SlicePackageWriter_RejectsEmptyRequiredArtifact()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nong-pandoc-empty-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var ex = Assert.Throws<NongPandocSliceWriteException>(() =>
                NongPandocSlicePackageWriter.Write(new NongPandocSliceWritePayload
                {
                    OutputDirectory = dir,
                    Manifest = new { schemaVersion = "test/v1" },
                    Document = new { schemaVersion = "test/v1" },
                    ContentJsonlLines = Array.Empty<string>(),
                    NongMarkText = "正文",
                    Structure = new { outline = Array.Empty<object>() },
                    Format = new { fonts = Array.Empty<string>() },
                    Diagnostics = new { warnings = Array.Empty<string>() },
                    AssetsManifest = new { items = Array.Empty<object>() },
                }));

            Assert.Contains("content.jsonl", ex.Message);
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void SlicePackageReader_ReadsSharedArtifactsInAiOrder()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nong-pandoc-read-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var manifest = new NongPandocSliceManifest
            {
                Source = new NongPandocSourceInfo { Path = "a.docx", Format = "docx" },
                Metrics = new NongPandocMetrics { Blocks = 1, Paragraphs = 1, Warnings = 1 },
                Warnings = new List<string> { "sample warning" },
            };

            NongPandocSlicePackageWriter.Write(new NongPandocSliceWritePayload
            {
                OutputDirectory = dir,
                Manifest = manifest,
                Document = new { schemaVersion = "test/v1", blocks = new[] { "p0001" } },
                ContentJsonlLines = new[] { """{"id":"p0001","kind":"paragraph","text":"正文"}""" },
                NongMarkText = "正文\n",
                Structure = new { schemaVersion = "test/structure/v1", outline = Array.Empty<object>() },
                Format = new { schemaVersion = "test/format/v1", fonts = new[] { "SimSun" } },
                Diagnostics = new { schemaVersion = "test/diagnostics/v1", warnings = new[] { "sample warning" } },
                AssetsManifest = new { schemaVersion = "test/assets/v1", items = Array.Empty<object>() },
                TextPreview = "正文\n",
            });

            var result = NongPandocSlicePackageReader.Read(dir);

            Assert.Equal("nong-pandoc/package/v1", result.Manifest.SchemaVersion);
            Assert.Equal("docx", result.Manifest.Source.Format);
            Assert.Equal("正文\n", result.ContentNongMark);
            Assert.Equal(new[]
            {
                NongPandocArtifactNames.ContentNongMark,
                NongPandocArtifactNames.Structure,
                NongPandocArtifactNames.Format,
                NongPandocArtifactNames.Diagnostics,
            }, result.AiReadOrder);
            Assert.True(result.Artifacts.ContainsKey(NongPandocArtifactNames.Structure));
            Assert.True(result.Summary.PreviewAvailable);
            Assert.Equal(1, result.Summary.Metrics.Blocks);
            Assert.Equal("test/structure/v1", result.Structure.RootElement.GetProperty("schemaVersion").GetString());
            Assert.Equal("test/format/v1", result.Format.RootElement.GetProperty("schemaVersion").GetString());
            Assert.Equal("test/diagnostics/v1", result.Diagnostics.RootElement.GetProperty("schemaVersion").GetString());
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void SlicePackageReader_RejectsUnsupportedSchemaVersion()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nong-pandoc-bad-schema-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            NongPandocSlicePackageWriter.Write(new NongPandocSliceWritePayload
            {
                OutputDirectory = dir,
                Manifest = new { schemaVersion = "legacy/v1", streams = NongPandocStreamPaths.Default },
                Document = new { schemaVersion = "test/v1", blocks = new[] { "p0001" } },
                ContentJsonlLines = new[] { """{"id":"p0001","kind":"paragraph"}""" },
                NongMarkText = "正文",
                Structure = new { outline = Array.Empty<object>() },
                Format = new { fonts = Array.Empty<string>() },
                Diagnostics = new { warnings = Array.Empty<string>() },
                AssetsManifest = new { items = Array.Empty<object>() },
            });

            var ex = Assert.Throws<NongPandocSliceReadException>(() => NongPandocSlicePackageReader.Read(dir));
            Assert.Contains("Unsupported slice schemaVersion", ex.Message);
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void SlicePackageReader_RejectsMissingRequiredStream()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nong-pandoc-missing-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            NongPandocSlicePackageWriter.Write(new NongPandocSliceWritePayload
            {
                OutputDirectory = dir,
                Manifest = new NongPandocSliceManifest(),
                Document = new { schemaVersion = "test/v1", blocks = new[] { "p0001" } },
                ContentJsonlLines = new[] { """{"id":"p0001","kind":"paragraph"}""" },
                NongMarkText = "正文",
                Structure = new { outline = Array.Empty<object>() },
                Format = new { fonts = Array.Empty<string>() },
                Diagnostics = new { warnings = Array.Empty<string>() },
                AssetsManifest = new { items = Array.Empty<object>() },
            });
            File.Delete(Path.Combine(dir, "format.json"));

            var ex = Assert.Throws<NongPandocSliceReadException>(() => NongPandocSlicePackageReader.Read(dir));
            Assert.Contains("format.json", ex.Message);
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }
}
