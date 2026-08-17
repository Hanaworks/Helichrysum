namespace Helichrysum.Core.Configuration;

using System.Text.Json;

/// <summary>
/// Deletion backup strategies (F-Exec-9).
/// </summary>
public enum DeletionStrategy
{
    /// <summary>Trash + Staging both (default, safest).</summary>
    DoubleBackup,

    /// <summary>Trash only — fast, relies on user trust.</summary>
    TrashOnly,

    /// <summary>Staging only — doesn't touch system trash.</summary>
    StagingOnly,
}

/// <summary>
/// Application configuration loaded from a JSON file.
/// All settings have sensible defaults so the file is optional.
/// </summary>
public sealed class HelichrysumConfiguration
{
    /// <summary>Default analysis depth (metadata | sampled | full).</summary>
    public string AnalysisTier { get; set; } = "full";

    /// <summary>Deletion backup strategy (F-Exec-9).</summary>
    public DeletionStrategy DeletionStrategy { get; set; } = DeletionStrategy.DoubleBackup;

    /// <summary>Max degree of parallelism for scanning.</summary>
    public int ScanParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>Whether to verify object hash before execution (TOCTOU, F-Exec-11).</summary>
    public bool VerifyBeforeExec { get; set; } = true;

    /// <summary>Trash directory override.</summary>
    public string? TrashDirectory { get; set; }

    /// <summary>Staging directory override.</summary>
    public string? StagingDirectory { get; set; }

    /// <summary>Default manifest directory.</summary>
    public string? ManifestDirectory { get; set; }

    /// <summary>Report HTML truncation threshold in bytes (F-Report-6c).</summary>
    public long HtmlTruncationThreshold { get; set; } = 20 * 1024 * 1024;

    /// <summary>Loads configuration from the default location (~/.helichrysum/config.json),
    /// falling back to defaults if the file does not exist.</summary>
    public static HelichrysumConfiguration Load()
    {
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".helichrysum");

        string defaultConfigPath = Path.Combine(baseDir, "config.json");

        // If a path was provided via env var, use it.
        string? envPath = Environment.GetEnvironmentVariable("HELICHRYSUM_CONFIG");
        string configPath = envPath ?? defaultConfigPath;

        return Load(configPath);
    }

    /// <summary>Loads configuration from a specific path.</summary>
    public static HelichrysumConfiguration Load(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return new HelichrysumConfiguration();
        }

        try
        {
            string json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<HelichrysumConfiguration>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return config ?? new HelichrysumConfiguration();
        }
        catch
        {
            // Config file malformed — fall back to defaults.
            return new HelichrysumConfiguration();
        }
    }

    /// <summary>Gets the deployment strategy for the executor based on config.</summary>
    public string GetDeletionMode()
    {
        return DeletionStrategy switch
        {
            DeletionStrategy.DoubleBackup => "trash+staging",
            DeletionStrategy.TrashOnly => "trash",
            DeletionStrategy.StagingOnly => "staging",
            _ => "trash+staging",
        };
    }
}