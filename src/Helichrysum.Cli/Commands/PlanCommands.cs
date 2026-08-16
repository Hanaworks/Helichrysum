namespace Helichrysum.Cli.Commands;

using System.ComponentModel;
using Helichrysum.Core.Execution;
using Helichrysum.Core.Manifest;
using Helichrysum.Core.Planning;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class PlanListCommand : Command<PlanListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to manifest database.")]
        [CommandOption("-m|--manifest")]
        public string? ManifestPath { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        string plansDir = GetPlansDir(settings.ManifestPath);

        if (!Directory.Exists(plansDir))
        {
            AnsiConsole.MarkupLine("[yellow]暂无处理计划。[/]");
            return 0;
        }

        var planFiles = Directory.GetFiles(plansDir, "plan_*.json");
        if (planFiles.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]暂无处理计划。[/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("ID");
        table.AddColumn("文件");
        table.AddColumn("大小");

        foreach (string file in planFiles)
        {
            var info = new FileInfo(file);
            string name = Path.GetFileNameWithoutExtension(file).Replace("plan_", "");
            table.AddRow(name, Path.GetFileName(file), info.Length.ToString("N0") + " bytes");
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private static string GetPlansDir(string? manifestPath)
    {
        string baseDir = manifestPath != null
            ? Path.GetDirectoryName(manifestPath)!
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".helichrysum", "manifests");

        return baseDir;
    }
}

public sealed class PlanShowCommand : Command<PlanShowCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Plan ID (filename without extension).")]
        [CommandArgument(0, "<plan-id>")]
        public required string PlanId { get; init; }

        [Description("Path to manifest database.")]
        [CommandOption("-m|--manifest")]
        public string? ManifestPath { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        string planPath = GetPlanPath(settings.PlanId, settings.ManifestPath);

        if (!File.Exists(planPath))
        {
            AnsiConsole.MarkupLine($"[red]错误：[/]未找到计划: {settings.PlanId}");
            return 1;
        }

        string json = File.ReadAllText(planPath);
        var plan = ProcessingPlan.FromJson(json);

        if (plan == null)
        {
            AnsiConsole.MarkupLine("[red]错误：[/]计划文件格式错误。");
            return 1;
        }

        var panel = new Panel(
            Align.Left(new Markup(
                $"[bold]计划 ID:[/] {plan.Id}\n" +
                $"[bold]创建时间:[/] {plan.CreatedAt:yyyy-MM-dd HH:mm:ss}\n" +
                $"[bold]动作数:[/] {plan.Actions.Count}\n" +
                $"[bold]冲突数:[/] {plan.Conflicts.Count}\n" +
                $"[bold]回滚步骤:[/] {plan.RollbackSteps.Count}")))
        {
            Header = new PanelHeader("计划详情"),
            Border = BoxBorder.Rounded,
        };

        AnsiConsole.Write(panel);

        if (plan.Conflicts.Count > 0)
        {
            AnsiConsole.MarkupLine("[red]⚠ 存在冲突:[/]");
            foreach (var conflict in plan.Conflicts)
            {
                AnsiConsole.MarkupLine($"  [yellow]×[/] {conflict.Description}");
            }
        }

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
}

public sealed class PlanDryRunCommand : Command<PlanDryRunCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Plan ID.")]
        [CommandArgument(0, "<plan-id>")]
        public required string PlanId { get; init; }

        [Description("Path to manifest database.")]
        [CommandOption("-m|--manifest")]
        public string? ManifestPath { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        string planPath = GetPlanPath(settings.PlanId, settings.ManifestPath);

        if (!File.Exists(planPath))
        {
            AnsiConsole.MarkupLine($"[red]错误：[/]未找到计划: {settings.PlanId}");
            return 1;
        }

        string json = File.ReadAllText(planPath);
        var plan = ProcessingPlan.FromJson(json);

        if (plan == null)
        {
            AnsiConsole.MarkupLine("[red]错误：[/]计划文件格式错误。");
            return 1;
        }

        // Dry-run: show what would happen without executing.
        var table = new Table();
        table.AddColumn("动作");
        table.AddColumn("对象 ID");
        table.AddColumn("目标");
        table.AddColumn("状态");

        foreach (var action in plan.Actions)
        {
            string status = action.ActionType == "MoveToTrash" ? "将移入回收站" : "保留";
            table.AddRow(action.ActionType, action.ObjectId.ToString(), action.DestinationPath ?? "-", status);
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[dim]共 {plan.Actions.Count} 个动作。执行 --confirm 确认执行。[/]");
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
}