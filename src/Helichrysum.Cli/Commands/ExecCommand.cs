namespace Helichrysum.Cli.Commands;

using System.ComponentModel;
using Helichrysum.Core.Execution;
using Helichrysum.Core.Manifest;
using Helichrysum.Core.Planning;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class ExecCommand : Command<ExecCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Plan ID.")]
        [CommandArgument(0, "<plan-id>")]
        public required string PlanId { get; init; }

        [Description("Confirm execution.")]
        [CommandOption("--confirm")]
        public bool Confirm { get; init; }

        [Description("Path to manifest database.")]
        [CommandOption("-m|--manifest")]
        public string? ManifestPath { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // F-Exec-1: Must have --confirm to execute.
        if (!settings.Confirm)
        {
            AnsiConsole.MarkupLine("[red]错误：[/]必须使用 --confirm 确认执行。先执行 [bold]dry-run[/] 预览效果。");
            return 1;
        }

        string planPath = GetPlanPath(settings.PlanId, settings.ManifestPath);
        string manifestPath = GetManifestPath(settings.ManifestPath);

        if (!File.Exists(planPath))
        {
            AnsiConsole.MarkupLine($"[red]错误：[/]未找到计划: {settings.PlanId}");
            return 1;
        }

        if (!File.Exists(manifestPath))
        {
            AnsiConsole.MarkupLine("[red]错误：[/]manifest 数据库不存在。");
            return 1;
        }

        string json = File.ReadAllText(planPath);
        var plan = ProcessingPlan.FromJson(json);

        if (plan == null)
        {
            AnsiConsole.MarkupLine("[red]错误：[/]计划文件格式错误。");
            return 1;
        }

        AnsiConsole.MarkupLine("[yellow]正在执行计划...[/]");

        using var repository = ManifestRepository.Open(manifestPath);

        // Resolve object IDs to file paths.
        var allFiles = repository.GetAllFiles();
        var pathMap = allFiles.ToDictionary(f => f.Id, f => f.CanonicalPath);

        var executor = new Executor();
        int successCount = executor.ExecutePlan(plan, id =>
        {
            if (pathMap.TryGetValue(id, out var path))
            {
                string hash = Helichrysum.Core.Hashing.HashService.ComputeSha256(path);
                return (path, hash);
            }
            return null;
        });

        // Log results.
        var table = new Table();
        table.AddColumn("动作");
        table.AddColumn("状态");
        table.AddColumn("信息");

        foreach (var entry in executor.Log)
        {
            string color = entry.Status == "Completed" ? "green" : entry.Status == "Skipped" ? "yellow" : "red";
            table.AddRow(entry.ActionType, $"[{color}]{entry.Status}[/]", entry.Message);
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[green]✓[/] 执行完成。成功: {successCount}/{plan.Actions.Count}");

        return 0;
    }

    private static string GetPlanPath(string planId, string? manifestPath)
    {
        string baseDir = manifestPath != null
            ? Path.GetDirectoryName(manifestPath)!
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".helichrysum", "manifests");

        return Path.Combine(baseDir, $"plan_{planId}.json");
    }

    private static string GetManifestPath(string? manifestPath)
    {
        return manifestPath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".helichrysum", "manifests", "default.sqlite");
    }
}