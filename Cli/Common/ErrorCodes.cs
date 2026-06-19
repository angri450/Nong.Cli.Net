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
    public static readonly ErrorEntry Timeout = new("E010", "timeout", "Operation timed out.");
    public static readonly ErrorEntry NetworkError = new("E011", "network_error", "Network request failed.");
    public static readonly ErrorEntry AuthFailed = new("E012", "auth_failed", "Authentication or authorization failed.");
    public static readonly ErrorEntry RateLimit = new("E013", "rate_limit", "API rate limit exceeded.");
    public static readonly ErrorEntry DiskFull = new("E014", "disk_full", "Insufficient disk space.");
    public static readonly ErrorEntry PermissionDenied = new("E015", "permission_denied", "Permission denied for the requested operation.");
    public static readonly ErrorEntry ServiceUnavailable = new("E016", "service_unavailable", "External service is unavailable.");
    public static readonly ErrorEntry ConfigError = new("E017", "config_error", "Configuration is invalid or missing.");
    public static readonly ErrorEntry SchemaValidationFailed = new("E018", "schema_validation_failed", "Input does not match the expected JSON schema.");
    public static readonly ErrorEntry RepairFailed = new("E019", "repair_failed", "Automatic repair attempt failed.");

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
        "E010" => Timeout,
        "E011" => NetworkError,
        "E012" => AuthFailed,
        "E013" => RateLimit,
        "E014" => DiskFull,
        "E015" => PermissionDenied,
        "E016" => ServiceUnavailable,
        "E017" => ConfigError,
        "E018" => SchemaValidationFailed,
        "E019" => RepairFailed,
        _ => new ErrorEntry(code, "unknown", $"Unknown error code: {code}")
    };

    public static ErrorEntry FromException(Exception ex) => ex switch
    {
        TimeoutException => Timeout,
        System.Net.Http.HttpRequestException => NetworkError,
        UnauthorizedAccessException => PermissionDenied,
        System.IO.IOException when ex.Message.Contains("disk full", StringComparison.OrdinalIgnoreCase) => DiskFull,
        ArgumentException => ValidationFailed,
        InvalidOperationException => InternalError,
        NotImplementedException => NotImplemented,
        _ => InternalError
    };
}

public sealed record ErrorEntry(string Code, string Name, string Message);
