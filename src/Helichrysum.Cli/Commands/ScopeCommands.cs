namespace Helichrysum.Cli.Commands;

using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class ScopeAddCommand : Command<ScopeAddCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to add as a scope root.")]
        [CommandArgument(0, "<path>")]
        public required string Path { get; init; }

        [Description("Optional name for the scope.")]
        [CommandOption("-n|--name")]
        public string? Name { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        string path = System.IO.Path.GetFullPath(settings.Path);
        string name = settings.Name ?? System.IO.Path.GetFileName(path);

        if (!System.IO.Directory.Exists(path))
        {
            AnsiConsole.MarkupLine("[red]错误：[/]目录不存在或无法访问");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]✓[/] 已添加 Scope: [bold]{name}[/] → {path}");
        return 0;
    }
}

public sealed class ScopeListCommand : Command<ScopeListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[yellow]Scope 列表（CLI 端）[/]");
        AnsiConsole.MarkupLine("（待 manifest 持久化后展示保存的 scope）");
        return 0;
    }
}