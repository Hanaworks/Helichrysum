namespace Helichrysum.Cli.Commands;

using Helichrysum.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class ConfigShowCommand : Command<ConfigShowCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var config = HelichrysumConfiguration.Load();

        var table = new Table();
        table.AddColumn("配置项");
        table.AddColumn("当前值");

        table.AddRow("AnalysisTier", config.AnalysisTier);
        table.AddRow("DeletionStrategy", config.GetDeletionMode());
        table.AddRow("ScanParallelism", config.ScanParallelism.ToString());
        table.AddRow("VerifyBeforeExec", config.VerifyBeforeExec ? "true" : "false");
        table.AddRow("TrashDirectory", config.TrashDirectory ?? "~/.helichrysum/trash");
        table.AddRow("StagingDirectory", config.StagingDirectory ?? "~/.helichrysum/staging");
        table.AddRow("ManifestDirectory", config.ManifestDirectory ?? "~/.helichrysum/manifests");
        table.AddRow("HtmlTruncationThreshold", (config.HtmlTruncationThreshold / 1024 / 1024) + " MB");

        AnsiConsole.Write(table);

        string configPath = Environment.GetEnvironmentVariable("HELICHRYSUM_CONFIG")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".helichrysum", "config.json");

        AnsiConsole.MarkupLine($"[dim]配置文件: {configPath} (不存在则使用默认值)[/]");
        return 0;
    }
}