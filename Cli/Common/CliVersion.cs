namespace Nong.Cli.Common;

public static class CliVersion
{
    public const string Current = "12.0.0";
    // V11: AOT compatibility note — System.CommandLine reflection-based manifest
    // must be replaced with source-generated manifest for NativeAOT builds.
    // See V8 ManifestBuilder.Reflect for the data-driven entry point.
    public const bool AotReady = false; // set to true when source-gen manifest replaces reflection
}
