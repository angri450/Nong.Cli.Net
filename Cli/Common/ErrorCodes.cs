namespace Nong.Cli.Common;

/// <summary>
/// CLI error codes. Numeric for machines, string name for logs and model readability.
/// </summary>
public static class ErrorCodes
{
    public static readonly ErrorEntry FileNotFound = new("E001", "file_not_found", "The specified file does not exist.");
    public static readonly ErrorEntry UnsupportedFormat = new("E002", "unsupported_format", "The file format is not supported.");
    public static readonly ErrorEntry MissingArgument = new("E003", "missing_argument", "A required argument is missing.");
    public static readonly ErrorEntry InternalError = new("E004", "internal_error", "An internal error occurred.");
    public static readonly ErrorEntry DependencyMissing = new("E005", "dependency_missing", "A required dependency is not installed.");
    public static readonly ErrorEntry ValidationFailed = new("E006", "validation_failed", "Validation check failed.");
    public static readonly ErrorEntry ReadFailed = new("E007", "read_failed", "Failed to read the document.");
    public static readonly ErrorEntry WriteFailed = new("E008", "write_failed", "Failed to write the output file.");
    public static readonly ErrorEntry NotImplemented = new("E009", "not_implemented", "This command is not yet implemented.");

    public static ErrorEntry FromCode(string code) => code switch
    {
        "E001" => FileNotFound,
        "E002" => UnsupportedFormat,
        "E003" => MissingArgument,
        "E004" => InternalError,
        "E005" => DependencyMissing,
        "E006" => ValidationFailed,
        "E007" => ReadFailed,
        "E008" => WriteFailed,
        "E009" => NotImplemented,
        _ => new ErrorEntry(code, "unknown", $"Unknown error code: {code}")
    };
}

public sealed record ErrorEntry(string Code, string Name, string Message);
