using Helichrysum.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Helichrysum.Cli;

public static class Program
{
    internal const string Version = "0.1.0";
    internal static readonly string? GitHash = GetGitHash();

    public static int Main(string[] arguments)
    {
        // Handle --version directly (before Spectre.Console.Cli parses args).
        if (arguments.Length > 0 && arguments[0] is "--version" or "-v")
        {
            AnsiConsole.MarkupLine($"[bold]Helichrysum v{Version}[/] (git {GitHash ?? "unknown"})");
            return 0;
        }

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
                      .WithDescription("添加一个根路径到扫描范围");

                config.AddCommand<ScopeListCommand>("scope-list")
                      .WithDescription("列出所有已配置的扫描范围");

                config.AddCommand<ScanCommand>("scan")
                      .WithDescription("扫描指定范围内的文件");

                config.AddCommand<AnalyzeCommand>("analyze")
                      .WithDescription("分析扫描数据（重复检测、生成处理计划）");

                config.AddCommand<ReportCommand>("report")
                      .WithDescription("生成扫描报告（HTML 或 JSON）");

                config.AddCommand<PlanListCommand>("plan-list")
                      .WithDescription("列出所有处理计划");

                config.AddCommand<PlanShowCommand>("plan-show")
                      .WithDescription("查看处理计划详情");

                config.AddCommand<PlanDryRunCommand>("plan-dry-run")
                      .WithDescription("模拟执行处理计划");

                config.AddCommand<ExecCommand>("exec")
                      .WithDescription("执行处理计划（需 --confirm 确认）");
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

    private static string? GetGitHash()
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --short=12 HEAD",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            string? output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(1000);
            return string.IsNullOrEmpty(output) ? null : output;
        }
        catch
        {
            return null;
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
        AnsiConsole.MarkupLine($"[bold yellow]v{Program.Version} (git {Program.GitHash ?? "unknown"})[/] — 个人数字资产整理与归档工具");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("使用 [bold]helichrysum <command> --help[/] 查看命令帮助。");
        return 0;
    }
}