namespace MultiModalCore;

public sealed record ModelManifest
{
    public string SchemaVersion { get; set; } = "nong-ocr-model/v1";
    public string ModelId { get; set; } = "";
    public string Engine { get; set; } = "onnxruntime";
    public string Version { get; set; } = "";
    public List<string> Tasks { get; set; } = new();
    public bool Cloud { get; set; }
    public long SizeBytes { get; set; }
    public List<ModelFileEntry> Files { get; set; } = new();
}

public sealed record ModelFileEntry
{
    public string Path { get; set; } = "";
    public string Sha256 { get; set; } = "";
}
