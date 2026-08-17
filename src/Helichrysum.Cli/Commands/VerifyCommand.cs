namespace Helichrysum.Cli.Commands;

using System.ComponentModel;
using Helichrysum.Core.Hashing;
using Helichrysum.Core.Manifest;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class VerifyCommand : Command<VerifyCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to manifest database.")]
        [CommandOption("-m|--manifest")]
        public string? ManifestPath { get; init; }

        [Description("Output format: table or json.")]
        [CommandOption("-f|--format")]
        [DefaultValue("table")]
        public required string Format { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        string manifestPath = settings.ManifestPath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".helichrysum", "manifests", "default.sqlite");

        if (!File.Exists(manifestPath))
        {
            AnsiConsole.MarkupLine("[red]错误：[/]manifest 数据库不存在。");
            return 1;
        }

        AnsiConsole.MarkupLine("[yellow]正在验证归档完整性...[/]");

        using var repository = ManifestRepository.Open(manifestPath);
        var allFiles = repository.GetAllFiles();
        int total = allFiles.Count;
        int passed = 0;
        int failed = 0;
        int missing = 0;

        var failedFiles = new List<(string Path, string ExpectedHash, string ActualHash)>();

        foreach (var file in allFiles)
        {
            if (cancellationToken.IsCancellationRequested) break;

            string? storedHash = repository.GetHashByObjectId(file.Id);

            if (storedHash == null)
            {
                missing++;
                continue;
            }

            if (!File.Exists(file.CanonicalPath))
            {
                missing++;
                continue;
            }

            try
            {
                string currentHash = HashService.ComputeSha256(file.CanonicalPath);

                if (currentHash == storedHash)
                {
                    passed++;
                }
                else
                {
                    failed++;
                    failedFiles.Add((file.CanonicalPath, storedHash, currentHash));
                }
            }
            catch
            {
                failed++;
                failedFiles.Add((file.CanonicalPath, storedHash, "ERROR"));
            }
        }

        // Summary.
        var summaryPanel = new Panel(
            Align.Left(new Markup(
                $"[bold]总文件:[/] {total}\n" +
                $"[green]✓ 完好:[/] {passed}\n" +
                (failed > 0 ? $"[red]✗ 损坏:[/] {failed}\n" : "") +
                (missing > 0 ? $"[yellow]⚠ 缺失:[/] {missing}\n" : ""))))
        {
            Header = new PanelHeader("完整性验证结果"),
            Border = BoxBorder.Rounded,
        };

        AnsiConsole.Write(summaryPanel);

        if (failedFiles.Count > 0)
        {
            var table = new Table();
            table.AddColumn("文件");
            table.AddColumn("期望 Hash");
            table.AddColumn("当前 Hash");

            foreach (var (path, expected, actual) in failedFiles.Take(20))
            {
                table.AddRow(
                    path.Length > 60 ? "..." + path[^57..] : path,
                    expected[..12] + "...",
                    actual.Length > 12 ? actual[..12] + "..." : actual);
            }

            if (failedFiles.Count > 20)
            {
                table.AddRow("...", $"还有 {failedFiles.Count - 20} 个", "");
            }

            AnsiConsole.Write(table);
        }

        if (failed == 0 && missing == 0)
        {
            AnsiConsole.MarkupLine("[green]✓ 所有文件完整性验证通过。[/]");
            return 0;
        }

        if (failed > 0)
        {
            AnsiConsole.MarkupLine($"[red]✗ {failed} 个文件已损坏或内容已变更。[/]");
        }

        if (missing > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠ {missing} 个文件已缺失（路径不存在）。[/]");
        }

        return failed > 0 ? 1 : 0;
    }
}