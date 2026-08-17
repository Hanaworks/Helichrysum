namespace Helichrysum.Core.Reporting;

using System.Collections.Generic;
using Helichrysum.Core.Manifest;
using Helichrysum.Core.Serialization;

/// <summary>
/// Builds scan reports (JSON and HTML) from the manifest repository.
/// </summary>
public sealed class ReportBuilder
{
    private readonly ManifestRepository _repository;
    private long _htmlTruncationThreshold = 20 * 1024 * 1024; // 20 MB default

    public ReportBuilder(ManifestRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Sets the HTML report truncation threshold (F-Report-6c).
    /// </summary>
    public ReportBuilder WithTruncationThreshold(long thresholdBytes)
    {
        _htmlTruncationThreshold = thresholdBytes;
        return this;
    }

    /// <summary>
    /// Builds a JSON report with snapshot age, scope summary, and duplicate groups.
    /// </summary>
    public string BuildJson()
    {
        var duplicateGroups = _repository.GetDuplicateGroups();
        string? createdRaw = _repository.GetManifestMeta("created_at");
        string? snapshotAge = ComputeSnapshotAge(createdRaw);

        // Scope summary.
        int totalFiles = _repository.GetAllFiles().Count;
        long totalSize = _repository.GetAllFiles().Sum(f => f.Size ?? 0);

        var groupSummaries = duplicateGroups.Select(g => new DuplicateGroupSummary
        {
            HashValue = g.HashValue,
            FileCount = g.Count,
            TotalSize = g.Size * g.Count,
            Members = g.Members,
            Resolution = "Equality",
        }).ToList();

        var report = new ReportData
        {
            GeneratedAt = DateTimeOffset.UtcNow.ToString("O"),
            SnapshotAge = snapshotAge ?? "unknown",
            TotalFiles = totalFiles,
            TotalSize = totalSize,
            DuplicateGroupCount = groupSummaries.Count,
            DuplicateGroups = groupSummaries,
        };

        return Serialization.JsonService.SerializeCamelCase(report);
    }

    /// <summary>
    /// Builds a self-contained HTML report with directory tree, snapshot age, and filtering.
    /// </summary>
    public string BuildHtml()
    {
        var duplicateGroups = _repository.GetDuplicateGroups();
        string? createdRaw = _repository.GetManifestMeta("created_at");
        string? snapshotAge = ComputeSnapshotAge(createdRaw);
        string timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        int totalFiles = _repository.GetAllFiles().Count;
        string groupsHtml = BuildGroupsHtml(_repository, duplicateGroups);
        string treeHtml = BuildDirectoryTreeHtml(_repository);

        // Check truncation threshold.
        bool truncated = false;
        string displayGroups = groupsHtml;
        if (displayGroups.Length > _htmlTruncationThreshold)
        {
            displayGroups = displayGroups[..(int)_htmlTruncationThreshold]
                + "\n<!-- 报告已截断，完整详情保留在 manifest 中 -->\n"
                + "<p class=\"truncated\">报告数据量过大，详细列表已截断。请使用 JSON 格式或通过 manifest 查询完整数据。</p>";
            truncated = true;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"zh-CN\"><head>");
        sb.AppendLine("<meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("<title>Helichrysum 分析报告</title><style>");
        sb.AppendLine("  body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;margin:20px;color:#333}");
        sb.AppendLine("  h1{color:#b8860b;border-bottom:2px solid #b8860b;padding-bottom:8px}");
        sb.AppendLine("  .summary{background:#fdf6e3;padding:12px;border-radius:6px;margin-bottom:20px;font-size:0.9em}");
        sb.AppendLine("  .age{color:#b8860b;font-weight:bold}");
        sb.AppendLine("  .group{border:1px solid #ddd;border-radius:6px;padding:12px;margin-bottom:12px}");
        sb.AppendLine("  .group h3{margin:0 0 8px 0;color:#b8860b;font-size:1em}");
        sb.AppendLine("  .hash{color:#888;font-size:0.8em;word-break:break-all}");
        sb.AppendLine("  .member{padding:4px 0;font-family:monospace;font-size:0.9em}");
        sb.AppendLine("  .truncated{background:#fff3cd;padding:12px;border-radius:6px;margin:12px 0}");
        sb.AppendLine("  .filter-bar{background:#f0f0f0;padding:8px 12px;border-radius:6px;margin-bottom:16px;font-size:0.9em}");
        sb.AppendLine("  .filter-bar select,.filter-bar input{padding:4px 8px;margin-right:8px;border:1px solid #ddd;border-radius:4px}");
        sb.AppendLine("  .tree-node{padding:2px 0 2px 20px;border-left:1px solid #eee;margin:2px 0}");
        sb.AppendLine("  .tree-root{padding:4px 0;font-weight:bold;color:#b8860b}");
        sb.AppendLine("  .diff{margin-top:8px;background:#fafafa;border:1px solid #eee;border-radius:4px;padding:8px;font-family:monospace;font-size:0.85em}");
        sb.AppendLine("  .diff h4{margin:0 0 6px 0;color:#666;font-size:0.9em}");
        sb.AppendLine("  .diff-added{color:#1a7f1a;background:#e8f5e8;padding:1px 4px}");
        sb.AppendLine("  .diff-removed{color:#a33;background:#fdeaea;padding:1px 4px;text-decoration:line-through}");
        sb.AppendLine("  .diff-more{color:#888;font-style:italic}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<h1>Helichrysum 分析报告</h1>");
        sb.AppendLine("<div class=\"summary\">");
        sb.AppendLine($"  <span class=\"age\">快照年龄：{snapshotAge ?? "未知"}</span><br>");
        sb.AppendLine($"  生成时间：{timestamp} UTC<br>");
        sb.AppendLine($"  总文件数：{totalFiles} | 重复组数：{duplicateGroups.Count}");
        if (truncated) sb.AppendLine("<br><span class=\"truncated\">报告已截断（超过 20MB）</span>");
        sb.AppendLine("</div>");

        // Filter bar.
        sb.AppendLine("<div class=\"filter-bar\">");
        sb.AppendLine("  <label>筛选：</label>");
        sb.AppendLine("  <select id=\"filterType\" onchange=\"applyFilter()\">");
        sb.AppendLine("    <option value=\"all\">全部</option>");
        sb.AppendLine("    <option value=\"duplicate\">仅重复组</option>");
        sb.AppendLine("  </select>");
        sb.AppendLine("  <input type=\"text\" id=\"searchPath\" placeholder=\"搜索路径...\" oninput=\"applyFilter()\">");
        sb.AppendLine("</div>");

        // Directory tree.
        sb.AppendLine("<h2>目录结构</h2>");
        sb.AppendLine("<div id=\"treeView\">");
        sb.Append(treeHtml);
        sb.AppendLine("</div>");

        sb.AppendLine("<h2>重复组</h2>");
        sb.AppendLine("<div id=\"groupsContainer\">");
        sb.Append(displayGroups);
        sb.AppendLine("</div>");

        // Client-side filtering.
        sb.AppendLine("<script>");
        sb.AppendLine("function applyFilter(){var t=document.getElementById('filterType').value;var q=document.getElementById('searchPath').value.toLowerCase();var gs=document.querySelectorAll('#groupsContainer>.group');gs.forEach(function(g){var show=true;if(t==='all'||t==='duplicate')show=true;if(q&&!g.innerHTML.toLowerCase().includes(q))show=false;g.style.display=show?'':'none'});}");
        sb.AppendLine("</script>");

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    /// <summary>
    /// Exports the report to a SQLite database at the given path.
    /// </summary>
    public void ExportSqlite(string outputPath)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={outputPath};Pooling=False;");
        connection.Open();
        using var cmd = connection.CreateCommand();

        cmd.CommandText = "CREATE TABLE IF NOT EXISTS report_duplicates (group_id INTEGER PRIMARY KEY, hash_value TEXT, file_count INTEGER, total_size INTEGER, member_ids TEXT);";
        cmd.ExecuteNonQuery();

        var groups = _repository.GetDuplicateGroups();
        foreach (var group in groups)
        {
            cmd.CommandText = "INSERT INTO report_duplicates (hash_value, file_count, total_size, member_ids) VALUES ($h, $c, $s, $m);";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$h", group.HashValue);
            cmd.Parameters.AddWithValue("$c", group.Count);
            cmd.Parameters.AddWithValue("$s", group.Size * group.Count);
            cmd.Parameters.AddWithValue("$m", string.Join(",", group.Members));
            cmd.ExecuteNonQuery();
        }
    }

    private static string? ComputeSnapshotAge(string? createdRaw)
    {
        if (createdRaw == null || !DateTimeOffset.TryParse(createdRaw, out var created))
            return null;

        var age = DateTimeOffset.UtcNow - created;
        if (age.TotalMinutes < 1) return "刚刚";
        if (age.TotalHours < 1) return $"{(int)age.TotalMinutes} 分钟前";
        if (age.TotalDays < 1) return $"{(int)age.TotalHours} 小时前";
        if (age.TotalDays < 30) return $"{(int)age.TotalDays} 天前";
        if (age.TotalDays < 365) return $"{(int)(age.TotalDays / 30)} 个月前";
        return $"{(int)(age.TotalDays / 365)} 年前";
    }

    private static string BuildGroupsHtml(ManifestRepository repository, List<DuplicateGroup> groups)
    {
        if (groups.Count == 0) return "<p>未发现重复文件。</p>";

        var allFiles = repository.GetAllFiles();
        var pathById = allFiles.ToDictionary(f => f.Id);

        var html = new System.Text.StringBuilder();
        foreach (var group in groups)
        {
            html.AppendLine("<div class=\"group\">");
            html.AppendLine($"<h3>重复组 (共 {group.Count} 个文件，累计 {group.Size * group.Count:N0} 字节)</h3>");
            html.AppendLine($"<div class=\"hash\">Hash: {group.HashValue}</div>");
            foreach (long memberId in group.Members)
            {
                string display = pathById.TryGetValue(memberId, out var obj) ? obj.Path : $"对象 ID: {memberId}";
                html.AppendLine($"<div class=\"member\">{EscapeHtml(display)}</div>");
            }

            // Diff view for text files within the group.
            html.Append(BuildDiffHtml(repository, group));

            html.AppendLine("</div>");
        }
        return html.ToString();
    }

    /// <summary>
    /// Builds an HTML directory tree from the manifest, showing each directory
    /// with its aggregate file count.
    /// </summary>
    private static string BuildDirectoryTreeHtml(ManifestRepository repository)
    {
        var dirCounts = repository.GetDirectoryTree();

        if (dirCounts.Count == 0) return "<p>无目录信息。</p>";

        // Sort directories by segment depth so parents appear before children.
        var orderedDirs = dirCounts.Keys
            .OrderBy(d => d.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).Length)
            .ToList();

        var html = new System.Text.StringBuilder();

        foreach (var dir in orderedDirs)
        {
            // Emit full path as a tree node with folder icon and count.
            string display = dir.Length > 60 ? "..." + dir[^57..] : dir;
            html.AppendLine($"<div class=\"tree-node\">📁 {EscapeHtml(display)} <span style=\"color:#888;font-size:0.8em\">({dirCounts[dir]} 文件)</span></div>");
        }

        return html.ToString();
    }

    /// <summary>
    /// Builds a text diff view between two files, for display in the HTML report.
    /// Returns a simple line-by-line diff using modified LCS.
    /// </summary>
    private static string BuildDiffHtml(ManifestRepository repository, DuplicateGroup group)
    {
        if (group.Members.Count < 2) return string.Empty;

        var fileA = repository.GetObjectById(group.Members[0]);
        var fileB = repository.GetObjectById(group.Members[1]);

        if (fileA == null || fileB == null) return string.Empty;
        if (!File.Exists(fileA.CanonicalPath) || !File.Exists(fileB.CanonicalPath)) return string.Empty;

        // Only diff text files (small enough to read).
        if (fileA.Size > 1_000_000 || fileB.Size > 1_000_000) return string.Empty;

        string[] linesA;
        string[] linesB;
        try
        {
            linesA = File.ReadAllLines(fileA.CanonicalPath);
            linesB = File.ReadAllLines(fileB.CanonicalPath);
        }
        catch
        {
            return string.Empty;
        }

        // Simple diff: mark lines present in A but missing in B as removed,
        // lines present in B but missing in A as added.
        var setB = new HashSet<string>(linesB, StringComparer.Ordinal);
        var setA = new HashSet<string>(linesA, StringComparer.Ordinal);

        var html = new System.Text.StringBuilder();
        string nameA = Path.GetFileName(fileA.Path);
        string nameB = Path.GetFileName(fileB.Path);

        html.AppendLine("<div class=\"diff\">");
        html.AppendLine($"<h4>内容差异对比：{EscapeHtml(nameA)} ⟷ {EscapeHtml(nameB)}</h4>");

        int shown = 0;
        foreach (string line in linesA)
        {
            if (!setB.Contains(line))
            {
                html.AppendLine($"<div class=\"diff-line diff-removed\">- {EscapeHtml(line)}</div>");
                if (++shown >= 50) { html.AppendLine("<div class=\"diff-more\">… 差异过多，已截断</div>"); break; }
            }
        }
        foreach (string line in linesB)
        {
            if (!setA.Contains(line))
            {
                html.AppendLine($"<div class=\"diff-line diff-added\">+ {EscapeHtml(line)}</div>");
                if (++shown >= 50) { html.AppendLine("<div class=\"diff-more\">… 差异过多，已截断</div>"); break; }
            }
        }

        if (shown == 0)
        {
            html.AppendLine("<div class=\"diff-line\">内容相同（仅路径或元数据差异）</div>");
        }

        html.AppendLine("</div>");
        return html.ToString();
    }

    private static string EscapeHtml(string text)
    {
        return System.Net.WebUtility.HtmlEncode(text);
    }
}

internal sealed class ReportData
{
    public required string GeneratedAt { get; init; }
    public required string SnapshotAge { get; init; }
    public required int TotalFiles { get; init; }
    public required long TotalSize { get; init; }
    public required int DuplicateGroupCount { get; init; }
    public required List<DuplicateGroupSummary> DuplicateGroups { get; init; }
}

internal sealed class DuplicateGroupSummary
{
    public required string HashValue { get; init; }
    public required int FileCount { get; init; }
    public required long TotalSize { get; init; }
    public required List<long> Members { get; init; }
    public string? Resolution { get; init; }
}