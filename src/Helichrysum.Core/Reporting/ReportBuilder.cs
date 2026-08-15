namespace Helichrysum.Core.Reporting;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Helichrysum.Core.Manifest;

/// <summary>
/// Builds scan reports (JSON and HTML) from the manifest repository.
/// </summary>
public sealed class ReportBuilder
{
    private readonly ManifestRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportBuilder"/> class.
    /// </summary>
    /// <param name="repository">The manifest repository containing scan data.</param>
    public ReportBuilder(ManifestRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Builds a JSON report containing duplicate groups and scope summary.
    /// </summary>
    /// <returns>A JSON string of the report.</returns>
    public string BuildJson()
    {
        var duplicateGroups = _repository.GetDuplicateGroups();
        var groupSummaries = new List<DuplicateGroupSummary>();

        foreach (var group in duplicateGroups)
        {
            groupSummaries.Add(new DuplicateGroupSummary
            {
                HashValue = group.HashValue,
                FileCount = group.Count,
                TotalSize = group.Size * group.Count,
                Members = group.Members,
            });
        }

        var report = new ReportData
        {
            GeneratedAt = DateTimeOffset.UtcNow.ToString("O"),
            DuplicateGroupCount = groupSummaries.Count,
            DuplicateGroups = groupSummaries,
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        return JsonSerializer.Serialize(report, options);
    }

    /// <summary>
    /// Builds a self-contained HTML report.
    /// </summary>
    /// <returns>An HTML string of the report.</returns>
    public string BuildHtml()
    {
        var duplicateGroups = _repository.GetDuplicateGroups();
        string groupsHtml = BuildGroupsHtml(duplicateGroups);
        string timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh-CN\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("<title>Helichrysum 分析报告</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("  body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; margin: 20px; color: #333; }");
        sb.AppendLine("  h1 { color: #b8860b; border-bottom: 2px solid #b8860b; padding-bottom: 8px; }");
        sb.AppendLine("  .summary { background: #fdf6e3; padding: 12px; border-radius: 6px; margin-bottom: 20px; }");
        sb.AppendLine("  .group { border: 1px solid #ddd; border-radius: 6px; padding: 12px; margin-bottom: 12px; }");
        sb.AppendLine("  .group h3 { margin: 0 0 8px 0; color: #b8860b; }");
        sb.AppendLine("  .member { padding: 4px 0; font-family: monospace; font-size: 0.9em; }");
        sb.AppendLine("  .hash { color: #888; font-size: 0.8em; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<h1>Helichrysum 分析报告</h1>");
        sb.AppendLine("<div class=\"summary\">");
        sb.AppendLine("  <strong>生成时间：</strong>" + timestamp + " UTC<br>");
        sb.AppendLine("  <strong>重复组数：</strong>" + duplicateGroups.Count);
        sb.AppendLine("</div>");
        sb.Append(groupsHtml);
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string BuildGroupsHtml(List<DuplicateGroup> groups)
    {
        if (groups.Count == 0)
        {
            return "<p>未发现重复文件。</p>";
        }

        var html = new System.Text.StringBuilder();

        foreach (var group in groups)
        {
            html.AppendLine("<div class=\"group\">");
            html.AppendLine($"<h3>重复组 (共 {group.Count} 个文件，累计 {group.Size * group.Count:N0} 字节)</h3>");
            html.AppendLine($"<div class=\"hash\">Hash: {group.HashValue}</div>");

            foreach (long memberId in group.Members)
            {
                html.AppendLine($"<div class=\"member\">对象 ID: {memberId}</div>");
            }

            html.AppendLine("</div>");
        }

        return html.ToString();
    }
}

internal sealed class ReportData
{
    public required string GeneratedAt { get; init; }
    public required int DuplicateGroupCount { get; init; }
    public required List<DuplicateGroupSummary> DuplicateGroups { get; init; }
}

internal sealed class DuplicateGroupSummary
{
    public required string HashValue { get; init; }
    public required int FileCount { get; init; }
    public required long TotalSize { get; init; }
    public required List<long> Members { get; init; }
}