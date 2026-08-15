using Helichrysum.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Spectre.Console;

namespace Helichrysum.Cli;

public static class Program
{
    public static void Main(string[] arguments)
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console(outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}")
                                              .CreateLogger();

        try
        {
            var host = Host.CreateDefaultBuilder(arguments)
                           .UseSerilog()
                           .ConfigureServices((context, services) =>
                           {
                               services.AddHelichrysumCore();
                           })
                           .Build();

            AnsiConsole.Write(new FigletText("Helichrysum").Color(Color.Gold3));

            AnsiConsole.MarkupLine("[bold yellow]v0.1.0[/] — 个人数字资产整理与归档工具");
            AnsiConsole.MarkupLine("切片 0 骨架 · 代码基础设施已就绪");
            AnsiConsole.WriteLine();

            Log.Information("Helichrysum CLI initialized successfully");
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Helichrysum CLI terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}