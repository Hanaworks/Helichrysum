namespace Helichrysum.Cli.Commands;

using System.ComponentModel;
using Helichrysum.Core.Manifest;
using Helichrysum.Core.Scope;
using Helichrysum.Core.Scanning;
using Microsoft.Extensions.Logging;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class ScanCommand : Command<ScanCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Scope name (comma-separated for multiple).")]
        [CommandOption("-s|--scope")]
        public required string Scope { get; init; }

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

        var scope = new ScopeConfiguration();
        string[] scopeNames = settings.Scope.Split(',', StringSplitOptions.TrimEntries);
        bool allExist = true;

        foreach (string name in scopeNames)
        {
            if (Directory.Exists(name))
            {
                scope.AddRoot(name);
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]错误：[/]目录不存在: {name}");
                allExist = false;
            }
        }

        if (!allExist)
        {
            return 1;
        }

        AnsiConsole.MarkupLine("[yellow]开始扫描...[/]");

        using var repository = ManifestRepository.Open(manifestPath);
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(dispose: false));
        var scanner = new Scanner(scope, loggerFactory.CreateLogger<Scanner>());
        var progress = new ScanProgressReporter();

        var scanOptions = new ScanOptions();
        var objects = new List<FilesystemObject>();

        var cts = new CancellationTokenSource();
        var scanTask = ScanAsync(scanner, scanOptions, progress, objects, cts.Token);

        // Wait for the scan to complete.
        scanTask.GetAwaiter().GetResult();

        // Batch write to manifest.
        repository.BatchInsertObjects(objects);
        repository.SetManifestMeta("created_at", DateTimeOffset.UtcNow.ToString("O"));
        repository.SetManifestMeta("tool_version", "0.1.0");

        AnsiConsole.MarkupLine($"[green]✓[/] 扫描完成。共发现 [bold]{objects.Count(o => o.Kind == "RegularFile")}[/] 个文件。");
        return 0;
    }

    private static async Task ScanAsync(
        Scanner scanner, ScanOptions options,
        ScanProgressReporter progress, List<FilesystemObject> objects,
        CancellationToken ct)
    {
        await foreach (var obj in scanner.ScanAsync(options, progress, ct))
        {
            lock (objects)
            {
                objects.Add(obj);
            }
        }
    }

    private sealed class ScanProgressReporter : IProgress<ScanProgress>
    {
        private int _lastReported;

        public void Report(ScanProgress value)
        {
            // Report every 100 files to avoid console spam.
            if (value.FilesScanned - _lastReported >= 100)
            {
                _lastReported = value.FilesScanned;
                AnsiConsole.MarkupLine($"  [dim]已扫描: {value.FilesScanned} 个文件 | 当前: {value.CurrentPath}[/]");
            }
        }
    }
}