using Helichrysum.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Helichrysum.Cli;

public static class Program
{
    public static int Main(string[] arguments)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            var app = new CommandApp<RootCommand>();
            app.Configure(config =>
            {
                config.PropagateExceptions();
                config.SetApplicationName("helichrysum");

                config.AddCommand<ScopeAddCommand>("scope-add")
                      .WithDescription("Add a root path to the scan scope");

                config.AddCommand<ScopeListCommand>("scope-list")
                      .WithDescription("List all configured scopes");

                config.AddCommand<ScanCommand>("scan")
                      .WithDescription("Scan files within a scope");

                config.AddCommand<AnalyzeCommand>("analyze")
                      .WithDescription("Run analysis (duplicate detection) on scanned data");

                config.AddCommand<ReportCommand>("report")
                      .WithDescription("Generate a scan report (HTML or JSON)");
            });

            return app.Run(arguments);
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Helichrysum CLI terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

internal sealed class RootCommand : Command<RootCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(new FigletText("Helichrysum").Color(Color.Gold3));
        AnsiConsole.MarkupLine("[bold yellow]v0.1.0[/] — 个人数字资产整理与归档工具");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("使用 [bold]helichrysum <command> --help[/] 查看命令帮助。");
        return 0;
    }
}