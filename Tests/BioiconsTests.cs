using Bioicons;
using Xunit;

namespace Tests;

public class BioiconsTests : IDisposable
{
    private readonly string _tempDir;

    public BioiconsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bioicons-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void GetCategories_Returns6Categories()
    {
        var categories = IconProvider.GetCategories();
        Assert.Equal(6, categories.Count);
    }

    [Fact]
    public void GetCategories_ContainsExpectedNames()
    {
        var categories = IconProvider.GetCategories();
        Assert.Contains("Biology", categories);
        Assert.Contains("Chemistry", categories);
        Assert.Contains("Medical", categories);
        Assert.Contains("LabEquipment", categories);
        Assert.Contains("Arrows", categories);
        Assert.Contains("Experimental", categories);
    }

    [Fact]
    public void GetIcons_Biology_Returns10Icons()
    {
        var icons = IconProvider.GetIcons("Biology");
        Assert.Equal(10, icons.Count);
    }

    [Fact]
    public void GetIcons_Biology_ContainsCell()
    {
        var icons = IconProvider.GetIcons("Biology");
        Assert.Contains("cell", icons);
    }

    [Fact]
    public void GetSvg_Biology_Cell_ReturnsValidSvg()
    {
        var svg = IconProvider.GetSvg("Biology", "cell");

        Assert.NotNull(svg);
        Assert.NotEmpty(svg);
        Assert.Contains("<svg", svg);
    }

    [Fact]
    public void GetSvg_InvalidCategory_Throws()
    {
        Assert.Throws<ArgumentException>(() => IconProvider.GetSvg("NonExistent", "cell"));
    }

    [Fact]
    public void GetSvg_InvalidIcon_Throws()
    {
        Assert.Throws<ArgumentException>(() => IconProvider.GetSvg("Biology", "nonexistent_icon"));
    }

    [Fact]
    public void SaveSvg_WritesFile()
    {
        var outPath = Path.Combine(_tempDir, "cell.svg");
        IconProvider.SaveSvg("Biology", "cell", outPath);

        Assert.True(File.Exists(outPath), "SVG file should exist");
        var content = File.ReadAllText(outPath);
        Assert.Contains("<svg", content);
    }

    [Fact]
    public void GetIcons_InvalidCategory_Throws()
    {
        Assert.Throws<ArgumentException>(() => IconProvider.GetIcons("FakeCategory"));
    }

    [Fact]
    public void GetSvg_AllBiologyIcons_ReturnValidSvg()
    {
        var icons = IconProvider.GetIcons("Biology");
        foreach (var icon in icons)
        {
            var svg = IconProvider.GetSvg("Biology", icon);
            Assert.NotEmpty(svg);
            Assert.Contains("<svg", svg);
        }
    }
}
