using Helichrysum.Core.Configuration;

namespace Helichrysum.Core.Tests;

public sealed class ConfigurationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public ConfigurationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"helichrysum_cfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
    }

    [Fact]
    public void Load_MissingFile_UsesDefaults()
    {
        var config = HelichrysumConfiguration.Load(_configPath);

        Assert.Equal("full", config.AnalysisTier);
        Assert.Equal(DeletionStrategy.DoubleBackup, config.DeletionStrategy);
        Assert.True(config.VerifyBeforeExec);
    }

    [Fact]
    public void Load_ValidFile_ReadsValues()
    {
        File.WriteAllText(_configPath, """
            {
              "analysisTier": "sampled",
              "deletionStrategy": 1,
              "verifyBeforeExec": false,
              "htmlTruncationThreshold": 1048576
            }
            """);

        var config = HelichrysumConfiguration.Load(_configPath);

        Assert.Equal("sampled", config.AnalysisTier);
        Assert.Equal(DeletionStrategy.TrashOnly, config.DeletionStrategy);
        Assert.False(config.VerifyBeforeExec);
        Assert.Equal(1048576, config.HtmlTruncationThreshold);
    }

    [Fact]
    public void Load_MalformedFile_FallsBackToDefaults()
    {
        File.WriteAllText(_configPath, "{ invalid json !!!");

        var config = HelichrysumConfiguration.Load(_configPath);

        Assert.NotNull(config);
        Assert.Equal("full", config.AnalysisTier);
    }

    [Fact]
    public void GetDeletionMode_MapsStrategy()
    {
        var config = new HelichrysumConfiguration
        {
            DeletionStrategy = DeletionStrategy.StagingOnly,
        };

        Assert.Equal("staging", config.GetDeletionMode());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) TestFileHelper.DeleteDirectoryWithRetry(_tempDir);
    }
}