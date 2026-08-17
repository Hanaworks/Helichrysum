namespace Helichrysum.Cli;

using Helichrysum.Core.Configuration;

/// <summary>
/// Resolves default paths from the machine-level configuration.
/// </summary>
public static class DefaultPaths
{
    /// <summary>Gets the default manifest path from config.</summary>
    public static string ManifestPath(HelichrysumConfiguration? config = null)
    {
        config ??= HelichrysumConfiguration.Load();

        return config.ManifestDirectory is { } dir
            ? Path.Combine(dir, "default.sqlite")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".helichrysum", "manifests", "default.sqlite");
    }

    /// <summary>Gets the plans directory for a given manifest path.</summary>
    public static string PlansDir(string manifestPath)
    {
        return Path.GetDirectoryName(manifestPath) ?? ".";
    }
}