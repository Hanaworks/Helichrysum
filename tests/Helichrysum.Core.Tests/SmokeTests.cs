using Microsoft.Extensions.DependencyInjection;

namespace Helichrysum.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void Infrastructure_Is_Initialized()
    {
        // Verify that the DI infrastructure and service registration work.
        var services = new ServiceCollection();
        services.AddHelichrysumCore();
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider);
    }

    [Fact]
    public void Fixture_Directory_Exists()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "fixtures", "backup1", "readme.txt");

        Assert.True(File.Exists(fixturePath),
            $"Fixture file not found at expected path: {fixturePath}");
    }

    [Fact]
    public void Equality_Same_File_Matches()
    {
        var fixtureDir = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "fixtures");

        var fileA = Path.Combine(fixtureDir, "backup1", "readme.txt");
        var fileB = Path.Combine(fixtureDir, "backup2", "readme.txt");

        var hashA = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(fileA)));
        var hashB = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(fileB)));

        Assert.Equal(hashA, hashB);
    }
}