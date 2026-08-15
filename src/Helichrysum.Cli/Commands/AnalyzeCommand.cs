namespace Helichrysum.Cli.Commands;

using System.ComponentModel;
using Helichrysum.Core.Analysis;
using Helichrysum.Core.Hashing;
using Helichrysum.Core.Manifest;
using Helichrysum.Core.Reporting;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class AnalyzeCommand : Command<AnalyzeCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Analysis depth: metadata, sampled, or full.")]
        [CommandOption("-t|--tier")]
        [DefaultValue("full")]
        public required string Tier { get; init; }

        [Description("Path to manifest database.")]
        [CommandOption("-m|--manifest")]
        public string? ManifestPath { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        string manifestPath = settings.ManifestPath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".helichrysum", "manifests", "default.sqlite");

        if (!File.Exists(manifestPath))
        {
            AnsiConsole.MarkupLine("[red]错误：[/]manifest 数据库不存在，请先执行 scan。");
            return 1;
        }

        AnsiConsole.MarkupLine($"[yellow]开始分析 (tier: {settings.Tier})...[/]");

        using var repository = ManifestRepository.Open(manifestPath);

        // Phase 1: Compute SHA256 hashes for all files.
        AnsiConsole.MarkupLine("  [dim]正在计算文件摘要...[/]");
        var allFiles = repository.GetAllFiles();
        int hashed = 0;

        foreach (var file in allFiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                string sha256 = HashService.ComputeSha256(file.CanonicalPath);
                repository.InsertHash(new HashRecord
                {
                    ObjectId = file.Id,
                    Tier = "FullHash",
                    HashValue = sha256,
                    BytesRead = file.Size ?? 0,
                    ComputedAt = DateTimeOffset.UtcNow,
                });
                hashed++;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]  ×[/] 无法读取文件: {file.Path} ({ex.Message})");
            }
        }

        // Phase 2: Run duplicate detection.
        var detector = new ExactDuplicateDetector(repository);
        var relations = detector.Detect();

        AnsiConsole.MarkupLine($"[green]✓[/] 分析完成。发现 [bold]{relations.Count}[/] 个重复组。");
        return 0;
    }
}

public sealed class ReportCommand : Command<ReportCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Output format: html or json.")]
        [CommandOption("-f|--format")]
        [DefaultValue("html")]
        public required string Format { get; init; }

        [Description("Output file path.")]
        [CommandOption("-o|--out")]
        public string? OutputPath { get; init; }

        [Description("Path to manifest database.")]
        [CommandOption("-m|--manifest")]
        public string? ManifestPath { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        string manifestPath = settings.ManifestPath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".helichrysum", "manifests", "default.sqlite");

        if (!File.Exists(manifestPath))
        {
            AnsiConsole.MarkupLine("[red]错误：[/]manifest 数据库不存在，请先执行 scan。");
            return 1;
        }

        using var repository = ManifestRepository.Open(manifestPath);
        var builder = new ReportBuilder(repository);

        string outputPath = settings.OutputPath
            ?? $"helichrysum_report.{settings.Format}";

        if (settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            string json = builder.BuildJson();
            File.WriteAllText(outputPath, json);
            AnsiConsole.MarkupLine($"[green]✓[/] JSON 报告已生成: {outputPath}");
        }
        else
        {
            string html = builder.BuildHtml();
            File.WriteAllText(outputPath, html);
            AnsiConsole.MarkupLine($"[green]✓[/] HTML 报告已生成: {outputPath}");
        }

        return 0;
    }
}