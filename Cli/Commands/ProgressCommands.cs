using System.CommandLine;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

public static class ProgressCommands
{
    static readonly ReportCategory[] Categories =
    [
        new("plans", "施工方案", "#2563eb"),
        new("changelog", "变更记录", "#16a34a"),
        new("debug", "用户反馈", "#dc2626"),
        new("guidance", "开发指导", "#7c3aed"),
    ];

    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("progress", "Project progress reports");
        cmd.AddCommand(CreateReport(jsonOpt));
        return cmd;
    }

    static Command CreateReport(Option<bool> jsonOpt)
    {
        var projectRootOpt = new Option<string>("--project-root", () => ".", "Project root containing log/plans, log/changelog, log/debug, and log/guidance");
        var outputOpt = new Option<string?>(new[] { "-o", "--output" }, "Optional report output directory. Defaults to <project-root>/log/reports");
        var cmd = new Command("report", "Generate HTML progress reports from project log indexes")
        {
            projectRootOpt,
            outputOpt
        };

        cmd.SetHandler((string projectRoot, string? output, bool json) =>
        {
            try
            {
                var (result, elapsed) = CliHelpers.Time(() => Generate(projectRoot, output));
                if (json)
                {
                    var jsonOutput = JsonOutput.Ok("progress report",
                        $"Generated {result.PageCount} report page(s) for {result.EntryCount} log entries",
                        new
                        {
                            schemaVersion = "nong-progress/report/v1",
                            projectRoot = result.ProjectRoot,
                            logDir = result.LogDir,
                            reportsDir = result.ReportsDir,
                            entries = result.EntryCount,
                            skipped = result.SkippedFiles,
                            pageCount = result.PageCount,
                            categories = result.CategoryCounts.Select(c => new
                            {
                                key = c.Key,
                                label = c.Label,
                                count = c.Count
                            })
                        });
                    jsonOutput.Artifacts["index"] = result.IndexHtml;
                    jsonOutput.Artifacts["reportsDir"] = result.ReportsDir;
                    jsonOutput.Artifacts["style"] = result.StyleCss;
                    jsonOutput.Metrics["entries"] = result.EntryCount;
                    jsonOutput.Metrics["pageCount"] = result.PageCount;
                    jsonOutput.Metrics["skipped"] = result.SkippedFiles.Count;
                    foreach (var skipped in result.SkippedFiles)
                    {
                        jsonOutput.Issues.Add(new Issue
                        {
                            Id = "missing_log_entry",
                            Severity = "Warning",
                            Message = skipped
                        });
                    }
                    jsonOutput.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(jsonOutput, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Generated {result.PageCount} report page(s) for {result.EntryCount} log entries.");
                    Console.WriteLine($"Index: {result.IndexHtml}");
                    foreach (var skipped in result.SkippedFiles)
                        Console.WriteLine($"Skipped: {skipped}");
                }
            }
            catch (ProgressReportException ex)
            {
                CliHelpers.WriteError("progress report", ex.Error, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("progress report", ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, projectRootOpt, outputOpt, jsonOpt);

        return cmd;
    }

    static ProgressReportResult Generate(string projectRoot, string? outputDir)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new ProgressReportException(ErrorCodes.MissingArgument with
            {
                Message = "Project root is required."
            });
        }

        var root = Path.GetFullPath(projectRoot);
        if (!Directory.Exists(root))
        {
            throw new ProgressReportException(ErrorCodes.FileNotFound with
            {
                Message = $"Project root not found: {root}"
            });
        }

        var logDir = Path.Combine(root, "log");
        if (!Directory.Exists(logDir))
        {
            throw new ProgressReportException(ErrorCodes.FileNotFound with
            {
                Message = $"Log directory not found: {logDir}"
            });
        }

        var reportsDir = string.IsNullOrWhiteSpace(outputDir)
            ? Path.Combine(logDir, "reports")
            : Path.GetFullPath(outputDir);
        var pagesDir = Path.Combine(reportsDir, "pages");

        Directory.CreateDirectory(reportsDir);
        Directory.CreateDirectory(pagesDir);

        var allEntries = ReadIndexEntries(logDir);
        var skippedFiles = new List<string>();
        var logFiles = ReadLogFiles(logDir, pagesDir, allEntries, skippedFiles);

        foreach (var category in Categories)
        {
            var catFiles = logFiles
                .Where(f => f.Category == category.Key)
                .OrderByDescending(f => f.Date)
                .ToList();
            var catHtml = BuildCategoryHtml(category, catFiles);
            File.WriteAllText(Path.Combine(reportsDir, $"{category.Key}.html"), catHtml, Encoding.UTF8);
        }

        var indexHtml = BuildIndexHtml(logFiles);
        var indexPath = Path.Combine(reportsDir, "index.html");
        var stylePath = Path.Combine(reportsDir, "style.css");
        File.WriteAllText(indexPath, indexHtml, Encoding.UTF8);
        File.WriteAllText(stylePath, BuildCss(), Encoding.UTF8);

        var categoryCounts = Categories
            .Select(c => new CategoryCount(c.Key, c.Label, logFiles.Count(f => f.Category == c.Key)))
            .ToList();

        var pageCount = logFiles.Count + Categories.Length + 1;
        return new ProgressReportResult(
            root,
            logDir,
            reportsDir,
            indexPath,
            stylePath,
            logFiles.Count,
            pageCount,
            categoryCounts,
            skippedFiles);
    }

    static List<(string Category, IndexEntry Entry)> ReadIndexEntries(string logDir)
    {
        var entries = new List<(string Category, IndexEntry Entry)>();

        foreach (var category in Categories)
        {
            var indexPath = Path.Combine(logDir, category.Key, "index.md");
            if (!File.Exists(indexPath)) continue;

            foreach (var line in File.ReadLines(indexPath, Encoding.UTF8))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("- ", StringComparison.Ordinal)) continue;

                var entry = ParseIndexLine(trimmed);
                if (entry is not null)
                    entries.Add((category.Key, entry));
            }
        }

        return entries;
    }

    static List<LogFile> ReadLogFiles(
        string logDir,
        string pagesDir,
        List<(string Category, IndexEntry Entry)> allEntries,
        List<string> skippedFiles)
    {
        var logFiles = new List<LogFile>();

        foreach (var (category, entry) in allEntries)
        {
            var mdPath = ResolveLogFile(logDir, category, entry.Date, entry.FileName);
            if (mdPath is null)
            {
                skippedFiles.Add($"{category}/{entry.FileName} not found");
                continue;
            }

            var content = File.ReadAllText(mdPath, Encoding.UTF8);
            var title = ExtractTitle(content) ?? entry.Summary;
            var logFile = new LogFile(entry.Date, entry.FileName, title, content, category);
            logFiles.Add(logFile);

            var pageHtml = BuildPageHtml(category, entry, title, content);
            var pageFileName = $"{category}-{SanitizeFileName(entry.FileName)}.html";
            File.WriteAllText(Path.Combine(pagesDir, pageFileName), pageHtml, Encoding.UTF8);
        }

        return logFiles;
    }

    static IndexEntry? ParseIndexLine(string line)
    {
        var parts = line[2..].Split('|', 4);
        if (parts.Length < 2) return null;

        var date = parts[0].Trim();
        var fileName = parts[1].Trim();
        var summary = parts.Length > 2 ? parts[2].Trim() : "";
        var status = parts.Length > 3 ? parts[3].Trim() : "";

        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            && date.Length < 10)
        {
            return null;
        }

        return new IndexEntry(date, fileName, summary, status);
    }

    static string? ExtractTitle(string markdown)
    {
        var firstLine = markdown.Split('\n')[0].Trim();
        return firstLine.StartsWith("# ", StringComparison.Ordinal) ? firstLine[2..].Trim() : null;
    }

    static string SanitizeFileName(string name)
    {
        var sanitized = name.Replace(".md", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "-", StringComparison.Ordinal)
            .Replace(":", "", StringComparison.Ordinal)
            .Replace("/", "-", StringComparison.Ordinal)
            .Replace("\\", "-", StringComparison.Ordinal);
        return sanitized;
    }

    static string? ResolveLogFile(string logDir, string category, string date, string fileName)
    {
        var path = Path.Combine(logDir, category, fileName);
        if (File.Exists(path)) return path;

        path = Path.Combine(logDir, category, $"{date}-{fileName}");
        if (File.Exists(path)) return path;

        if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            path = Path.Combine(logDir, category, fileName + ".md");
            if (File.Exists(path)) return path;

            path = Path.Combine(logDir, category, $"{date}-{fileName}.md");
            if (File.Exists(path)) return path;
        }

        return null;
    }

    static string MarkdownToHtml(string markdown)
    {
        var sb = new StringBuilder();
        var lines = markdown.Split('\n');
        var inCodeBlock = false;
        var inTable = false;
        var inList = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.Trim();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                if (inCodeBlock)
                {
                    sb.AppendLine("</code></pre>");
                    inCodeBlock = false;
                }
                else
                {
                    var codeLang = trimmed[3..].Trim();
                    sb.Append("<pre><code");
                    if (codeLang.Length > 0)
                        sb.Append($" class=\"language-{Html(codeLang)}\"");
                    sb.AppendLine(">");
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                sb.AppendLine(Html(line));
                continue;
            }

            if (trimmed.StartsWith("|", StringComparison.Ordinal) && trimmed.EndsWith("|", StringComparison.Ordinal))
            {
                if (trimmed.Contains("---", StringComparison.Ordinal)
                    && trimmed.Replace("-", "", StringComparison.Ordinal).Replace("|", "", StringComparison.Ordinal).Trim().Length == 0)
                {
                    continue;
                }

                if (!inTable)
                {
                    CloseListIfNeeded(sb, ref inList);
                    sb.AppendLine("<table>");
                    inTable = true;
                }

                var cells = trimmed.Trim('|').Split('|').Select(c => c.Trim()).ToList();
                var isHeader = i + 1 < lines.Length
                    && lines[i + 1].Trim().StartsWith("|", StringComparison.Ordinal)
                    && lines[i + 1].Contains("---", StringComparison.Ordinal);
                sb.Append("<tr>");
                foreach (var cell in cells)
                {
                    var tag = isHeader ? "th" : "td";
                    sb.Append($"<{tag}>{InlineMarkdown(cell)}</{tag}>");
                }
                sb.AppendLine("</tr>");
                continue;
            }

            if (inTable)
            {
                sb.AppendLine("</table>");
                inTable = false;
            }

            if (trimmed.StartsWith("#### ", StringComparison.Ordinal))
            {
                CloseListIfNeeded(sb, ref inList);
                sb.AppendLine($"<h4>{InlineMarkdown(trimmed[5..])}</h4>");
                continue;
            }
            if (trimmed.StartsWith("### ", StringComparison.Ordinal))
            {
                CloseListIfNeeded(sb, ref inList);
                sb.AppendLine($"<h3>{InlineMarkdown(trimmed[4..])}</h3>");
                continue;
            }
            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                CloseListIfNeeded(sb, ref inList);
                sb.AppendLine($"<h2>{InlineMarkdown(trimmed[3..])}</h2>");
                continue;
            }
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                CloseListIfNeeded(sb, ref inList);
                sb.AppendLine($"<h1>{InlineMarkdown(trimmed[2..])}</h1>");
                continue;
            }

            if (trimmed is "---" or "***" or "___")
            {
                CloseListIfNeeded(sb, ref inList);
                sb.AppendLine("<hr>");
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                if (!inList)
                {
                    sb.AppendLine("<ul>");
                    inList = true;
                }
                sb.AppendLine($"<li>{InlineMarkdown(trimmed[2..])}</li>");
                continue;
            }

            CloseListIfNeeded(sb, ref inList);

            if (trimmed.StartsWith("> ", StringComparison.Ordinal))
            {
                sb.AppendLine($"<blockquote>{InlineMarkdown(trimmed[2..])}</blockquote>");
                continue;
            }

            if (trimmed.Length == 0)
            {
                sb.AppendLine("<br>");
                continue;
            }

            sb.AppendLine($"<p>{InlineMarkdown(trimmed)}</p>");
        }

        if (inCodeBlock) sb.AppendLine("</code></pre>");
        if (inTable) sb.AppendLine("</table>");
        if (inList) sb.AppendLine("</ul>");

        return sb.ToString();
    }

    static void CloseListIfNeeded(StringBuilder sb, ref bool inList)
    {
        if (!inList) return;
        sb.AppendLine("</ul>");
        inList = false;
    }

    static string InlineMarkdown(string text)
    {
        text = Html(text);
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        text = Regex.Replace(text, @"\*(.+?)\*", "<em>$1</em>");
        text = Regex.Replace(text, @"`([^`]+)`", "<code>$1</code>");
        text = Regex.Replace(text, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\">$1</a>");
        return text;
    }

    static string Html(string text) => WebUtility.HtmlEncode(text);

    static string BuildIndexHtml(List<LogFile> allFiles)
    {
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var recent = allFiles.OrderByDescending(f => f.Date).Take(20).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("<title>Nong.NET 开发进展</title>");
        sb.AppendLine("<link rel=\"stylesheet\" href=\"style.css\">");
        sb.AppendLine("</head><body>");
        sb.AppendLine("<div class=\"container\">");

        sb.AppendLine("<header><h1>Nong.NET 开发进展报告</h1>");
        sb.AppendLine($"<p class=\"subtitle\">生成时间: {Html(now)} | 共 {allFiles.Count} 条记录</p></header>");

        sb.AppendLine("<div class=\"dashboard\">");
        foreach (var category in Categories)
        {
            var count = allFiles.Count(f => f.Category == category.Key);
            sb.AppendLine($"<a href=\"{category.Key}.html\" class=\"card\" style=\"border-left: 4px solid {category.Color}\">");
            sb.AppendLine($"<div class=\"card-count\">{count}</div>");
            sb.AppendLine($"<div class=\"card-label\">{Html(category.Label)}</div></a>");
        }
        sb.AppendLine("</div>");

        sb.AppendLine("<section><h2>最近动态</h2><div class=\"timeline\">");
        foreach (var file in recent)
        {
            var category = Categories.First(c => c.Key == file.Category);
            var pageLink = $"pages/{file.Category}-{SanitizeFileName(file.FileName)}.html";
            sb.AppendLine($"<div class=\"timeline-item\" style=\"border-left-color: {category.Color}\">");
            sb.AppendLine($"<span class=\"timeline-date\">{Html(file.Date)}</span>");
            sb.AppendLine($"<span class=\"timeline-cat\" style=\"color: {category.Color}\">[{Html(category.Label)}]</span>");
            sb.AppendLine($"<a href=\"{Html(pageLink)}\" class=\"timeline-title\">{Html(file.Title)}</a>");
            sb.AppendLine("</div>");
        }
        sb.AppendLine("</div></section>");

        foreach (var category in Categories)
        {
            var catFiles = allFiles
                .Where(f => f.Category == category.Key)
                .OrderByDescending(f => f.Date)
                .Take(10)
                .ToList();
            if (catFiles.Count == 0) continue;

            sb.AppendLine($"<section><h2>{Html(category.Label)}</h2>");
            sb.AppendLine("<table><thead><tr><th>日期</th><th>标题</th><th>状态</th></tr></thead><tbody>");
            foreach (var file in catFiles)
            {
                var pageLink = $"pages/{file.Category}-{SanitizeFileName(file.FileName)}.html";
                var statusBadge = file.Title.Contains("done", StringComparison.OrdinalIgnoreCase)
                    || file.Title.Contains("完成", StringComparison.OrdinalIgnoreCase)
                    ? "<span class=\"badge badge-done\">done</span>"
                    : "<span class=\"badge badge-active\">active</span>";
                sb.AppendLine($"<tr><td>{Html(file.Date)}</td><td><a href=\"{Html(pageLink)}\">{Html(file.Title)}</a></td><td>{statusBadge}</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
            if (allFiles.Count(f => f.Category == category.Key) > 10)
                sb.AppendLine($"<a href=\"{category.Key}.html\" class=\"more-link\">查看全部 &rarr;</a>");
            sb.AppendLine("</section>");
        }

        sb.AppendLine("<footer><p>由 nong progress report 自动生成 | 源数据: log/plans/ log/changelog/ log/debug/ log/guidance/</p></footer>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    static string BuildCategoryHtml(ReportCategory category, List<LogFile> files)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"<title>{Html(category.Label)} - Nong.NET</title>");
        sb.AppendLine("<link rel=\"stylesheet\" href=\"style.css\">");
        sb.AppendLine("</head><body><div class=\"container\">");

        sb.AppendLine("<header><a href=\"index.html\" class=\"back-link\">&larr; 返回总览</a>");
        sb.AppendLine($"<h1>{Html(category.Label)}</h1><p class=\"subtitle\">{files.Count} 条记录</p></header>");

        if (files.Count == 0)
        {
            sb.AppendLine("<p class=\"empty\">暂无记录</p>");
        }
        else
        {
            sb.AppendLine("<table><thead><tr><th>日期</th><th>标题</th><th>摘要</th></tr></thead><tbody>");
            foreach (var file in files)
            {
                var pageLink = $"pages/{file.Category}-{SanitizeFileName(file.FileName)}.html";
                var summary = file.Content.Length > 100
                    ? file.Content[..Math.Min(200, file.Content.Length)].Replace('\n', ' ') + "..."
                    : "";
                sb.AppendLine($"<tr><td>{Html(file.Date)}</td><td><a href=\"{Html(pageLink)}\">{Html(file.Title)}</a></td><td>{Html(summary)}</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        sb.AppendLine("<footer><p>由 nong progress report 自动生成</p></footer>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    static string BuildPageHtml(string categoryKey, IndexEntry entry, string title, string content)
    {
        var category = Categories.First(c => c.Key == categoryKey);
        var bodyHtml = MarkdownToHtml(content);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"<title>{Html(title)} - Nong.NET</title>");
        sb.AppendLine("<link rel=\"stylesheet\" href=\"../style.css\">");
        sb.AppendLine("</head><body><div class=\"container\">");

        sb.AppendLine("<header><a href=\"../index.html\" class=\"back-link\">&larr; 返回总览</a> | ");
        sb.AppendLine($"<a href=\"../{categoryKey}.html\" class=\"back-link\">&larr; {Html(category.Label)}</a>");
        sb.AppendLine($"<h1>{Html(title)}</h1>");
        sb.AppendLine($"<p class=\"subtitle\">{Html(entry.Date)} | <span style=\"color: {category.Color}\">{Html(category.Label)}</span></p></header>");

        sb.AppendLine("<article class=\"content\">");
        sb.AppendLine(bodyHtml);
        sb.AppendLine("</article>");

        sb.AppendLine("<footer><p>由 nong progress report 自动生成</p></footer>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    static string BuildCss()
    {
        return """
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Noto Sans SC', sans-serif; line-height: 1.6; color: #1f2937; background: #f9fafb; }
.container { max-width: 960px; margin: 0 auto; padding: 2rem 1rem; }
header { margin-bottom: 2rem; padding-bottom: 1rem; border-bottom: 2px solid #e5e7eb; }
header h1 { font-size: 1.75rem; color: #111827; }
.subtitle { color: #6b7280; font-size: 0.9rem; margin-top: 0.25rem; }
.back-link { color: #3b82f6; text-decoration: none; font-size: 0.875rem; }
.back-link:hover { text-decoration: underline; }
.dashboard { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; margin-bottom: 2rem; }
.card { display: block; background: white; border-radius: 8px; padding: 1.5rem; text-decoration: none; box-shadow: 0 1px 3px rgba(0,0,0,0.1); transition: box-shadow 0.2s; }
.card:hover { box-shadow: 0 4px 12px rgba(0,0,0,0.15); }
.card-count { font-size: 2rem; font-weight: 700; color: #111827; }
.card-label { font-size: 0.875rem; color: #6b7280; margin-top: 0.25rem; }
.timeline { margin-bottom: 1.5rem; }
.timeline-item { padding: 0.5rem 0 0.5rem 1rem; border-left: 3px solid #e5e7eb; margin-left: 0.5rem; margin-bottom: 0.25rem; }
.timeline-date { font-size: 0.8rem; color: #9ca3af; margin-right: 0.5rem; font-family: monospace; }
.timeline-cat { font-size: 0.75rem; font-weight: 600; margin-right: 0.5rem; }
.timeline-title { color: #1f2937; text-decoration: none; }
.timeline-title:hover { text-decoration: underline; color: #3b82f6; }
table { width: 100%; border-collapse: collapse; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.1); margin-bottom: 1.5rem; }
th, td { padding: 0.75rem 1rem; text-align: left; border-bottom: 1px solid #f3f4f6; }
th { background: #f9fafb; font-weight: 600; font-size: 0.8rem; text-transform: uppercase; color: #6b7280; }
td a { color: #1f2937; text-decoration: none; }
td a:hover { color: #3b82f6; text-decoration: underline; }
tr:hover td { background: #f9fafb; }
.badge { display: inline-block; padding: 0.125rem 0.5rem; border-radius: 9999px; font-size: 0.7rem; font-weight: 600; }
.badge-done { background: #d1fae5; color: #065f46; }
.badge-active { background: #dbeafe; color: #1e40af; }
.content { background: white; border-radius: 8px; padding: 2rem; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
.content h1 { font-size: 1.5rem; margin: 1.5rem 0 0.75rem; color: #111827; }
.content h2 { font-size: 1.25rem; margin: 1.25rem 0 0.5rem; color: #1f2937; border-bottom: 1px solid #e5e7eb; padding-bottom: 0.25rem; }
.content h3 { font-size: 1.1rem; margin: 1rem 0 0.5rem; color: #374151; }
.content h4 { font-size: 1rem; margin: 0.75rem 0 0.25rem; color: #4b5563; }
.content p { margin: 0.5rem 0; }
.content ul, .content ol { margin: 0.5rem 0 0.5rem 1.5rem; }
.content li { margin: 0.25rem 0; }
.content code { background: #f3f4f6; padding: 0.125rem 0.375rem; border-radius: 4px; font-family: 'Cascadia Code', 'Fira Code', monospace; font-size: 0.875rem; }
.content pre { background: #1f2937; color: #f9fafb; padding: 1rem; border-radius: 8px; overflow-x: auto; margin: 1rem 0; }
.content pre code { background: transparent; padding: 0; color: inherit; }
.content blockquote { border-left: 4px solid #d1d5db; padding: 0.5rem 1rem; margin: 1rem 0; background: #f9fafb; color: #6b7280; }
.content hr { border: none; border-top: 1px solid #e5e7eb; margin: 1.5rem 0; }
.content a { color: #3b82f6; text-decoration: none; }
.content a:hover { text-decoration: underline; }
.content strong { font-weight: 600; color: #111827; }
.content table { box-shadow: none; border: 1px solid #e5e7eb; }
.content table th { background: #f3f4f6; }
section { margin-bottom: 2rem; }
section h2 { font-size: 1.25rem; margin-bottom: 0.75rem; color: #1f2937; }
.more-link { display: inline-block; color: #3b82f6; text-decoration: none; font-size: 0.875rem; }
.more-link:hover { text-decoration: underline; }
.empty { color: #9ca3af; font-style: italic; padding: 1rem 0; }
footer { margin-top: 3rem; padding-top: 1rem; border-top: 1px solid #e5e7eb; text-align: center; }
footer p { font-size: 0.75rem; color: #9ca3af; }
@media (max-width: 640px) {
  .container { padding: 1rem 0.5rem; }
  .dashboard { grid-template-columns: repeat(2, 1fr); }
  .content { padding: 1rem; }
  table { font-size: 0.8rem; }
  th, td { padding: 0.5rem; }
}
""";
    }

    sealed record ReportCategory(string Key, string Label, string Color);
    sealed record IndexEntry(string Date, string FileName, string Summary, string Status);
    sealed record LogFile(string Date, string FileName, string Title, string Content, string Category);
    sealed record CategoryCount(string Key, string Label, int Count);

    sealed record ProgressReportResult(
        string ProjectRoot,
        string LogDir,
        string ReportsDir,
        string IndexHtml,
        string StyleCss,
        int EntryCount,
        int PageCount,
        List<CategoryCount> CategoryCounts,
        List<string> SkippedFiles);

    sealed class ProgressReportException(ErrorEntry error) : Exception(error.Message)
    {
        public ErrorEntry Error { get; } = error;
    }
}
