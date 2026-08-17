namespace Helichrysum.Cli.Commands;

using System.ComponentModel;
using Helichrysum.Core.Analysis;
using Helichrysum.Core.Configuration;
using Helichrysum.Core.Hashing;
using Helichrysum.Core.Manifest;
using Helichrysum.Core.Planning;
using Helichrysum.Core.Reporting;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class AnalyzeCommand : Command<AnalyzeCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Analysis depth: metadata, sampled, or full.")]
        [CommandOption("-t|--tier")]
        public string? Tier { get; init; }

        [Description("Path to manifest database.")]
        [CommandOption("-m|--manifest")]
        public string? ManifestPath { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var config = HelichrysumConfiguration.Load();
        string tier = settings.Tier ?? config.AnalysisTier;

        string manifestPath = settings.ManifestPath ?? DefaultPaths.ManifestPath(config);

        if (!File.Exists(manifestPath))
        {
            AnsiConsole.MarkupLine("[red]错误：[/]manifest 数据库不存在，请先执行 scan。");
            return 1;
        }

        AnsiConsole.MarkupLine($"[yellow]开始分析 (tier: {tier})...[/]");

        using var repository = ManifestRepository.Open(manifestPath);

        // Phase 1: Compute SHA256 hashes for all files.
        AnsiConsole.MarkupLine("  [dim]正在计算文件摘要...[/]");
        var allFiles = repository.GetAllFiles();
        int hashed = 0;

        foreach (var file in allFiles)
        {
            if (cancellationToken.IsCancellationRequested) break;

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

        // Phase 2: Run all detectors.
        AnsiConsole.MarkupLine("  [dim]正在检测重复文件...[/]");
        var exactDetector = new ExactDuplicateDetector(repository);
        var exactRelations = exactDetector.Detect();

        // Phase 3: Generate plan from results.
        var duplicateGroups = repository.GetDuplicateGroups();
        var plan = PlanGenerator.Generate(duplicateGroups, repository);

        string planJson = plan.ToJson();
        string planPath = Path.Combine(
            Path.GetDirectoryName(manifestPath)!,
            "plan_" + plan.Id + ".json");
        File.WriteAllText(planPath, planJson);

        AnsiConsole.MarkupLine($"[green]✓[/] 分析完成。发现 [bold]{exactRelations.Count}[/] 个重复组，[bold]{plan.Actions.Count}[/] 个待处理动作。");
        AnsiConsole.MarkupLine($"[dim]计划已保存: {planPath}[/]");
        return 0;
    }
}

public sealed class ReportCommand : Command<ReportCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Output format: html, json, or sqlite.")]
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
        var config = HelichrysumConfiguration.Load();
        string manifestPath = settings.ManifestPath ?? DefaultPaths.ManifestPath(config);

        if (!File.Exists(manifestPath))
        {
            AnsiConsole.MarkupLine("[red]错误：[/]manifest 数据库不存在，请先执行 scan。");
            return 1;
        }

        using var repository = ManifestRepository.Open(manifestPath);
        var builder = new ReportBuilder(repository)
            .WithTruncationThreshold(config.HtmlTruncationThreshold);

        string outputPath = settings.OutputPath ?? $"helichrysum_report.{settings.Format}";

        if (settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            string json = builder.BuildJson();
            File.WriteAllText(outputPath, json);
            AnsiConsole.MarkupLine($"[green]✓[/] JSON 报告已生成: {outputPath}");
        }
        else if (settings.Format.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            builder.ExportSqlite(outputPath);
            AnsiConsole.MarkupLine($"[green]✓[/] SQLite 报告已生成: {outputPath}");
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