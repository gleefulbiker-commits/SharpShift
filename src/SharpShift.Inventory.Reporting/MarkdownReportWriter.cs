namespace SharpShift.Inventory.Reporting;

internal sealed class MarkdownReportWriter : IReportWriter
{
    public ReportFormat Format => ReportFormat.Markdown;

    public async Task<IReadOnlyList<string>> WriteCombinedAsync(ReportData data, ReportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(options);

        var outputPath = Path.Combine(options.OutputDirectory, $"{options.BaseFileName}.md");
        var content = BuildCombinedMarkdown(data);
        await File.WriteAllTextAsync(outputPath, content, cancellationToken);

        return [outputPath];
    }

    public async Task<IReadOnlyList<string>> WritePerSolutionAsync(ReportData data, ReportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(options);

        var files = new List<string>();
        foreach (var solution in data.Solutions)
        {
            var safeName = MakeSafeFileName(solution.SolutionName);
            var outputPath = Path.Combine(options.OutputDirectory, $"{options.BaseFileName}.{safeName}.md");
            var content = BuildPerSolutionMarkdown(solution, data.Analysis);
            await File.WriteAllTextAsync(outputPath, content, cancellationToken);
            files.Add(outputPath);
        }

        return files;
    }

    private static string BuildCombinedMarkdown(ReportData data)
    {
        var lines = new List<string>
        {
            "# SharpShift Modernization Report",
            string.Empty,
            $"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            string.Empty,
            "## Executive Summary",
            string.Empty
        };

        var projects = data.Solutions.SelectMany(s => s.Projects).ToList();
        lines.Add($"- Solutions: {data.Solutions.Count}");
        lines.Add($"- Projects: {projects.Count}");
        lines.Add($"- Unique dependencies: {data.Analysis?.Dependencies.Count ?? 0}");
        lines.Add($"- Dependency instances: {data.Analysis?.Dependencies.Values.Sum(d => d.TotalInstances) ?? 0}");
        lines.Add($"- Risk findings: {data.Analysis?.RiskFindings.Count ?? 0}");
        lines.Add(string.Empty);

        lines.Add("### Framework Distribution");
        lines.Add(string.Empty);
        foreach (var group in projects
                     .GroupBy(p => string.IsNullOrWhiteSpace(p.Framework) ? "Unknown" : p.Framework)
                     .OrderByDescending(g => g.Count())
                     .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"- {group.Key}: {group.Count()}");
        }
        lines.Add(string.Empty);

        lines.Add("### Project Type Distribution");
        lines.Add(string.Empty);
        foreach (var group in projects
                     .GroupBy(p => string.IsNullOrWhiteSpace(p.ProjectType) ? "Unknown" : p.ProjectType)
                     .OrderByDescending(g => g.Count())
                     .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"- {group.Key}: {group.Count()}");
        }
        lines.Add(string.Empty);

        lines.Add("### Risk Severity Breakdown");
        lines.Add(string.Empty);
        if (data.Analysis?.RiskFindings.Count > 0)
        {
            foreach (var group in data.Analysis.RiskFindings
                         .GroupBy(r => r.Severity)
                         .OrderByDescending(g => (int)g.Key))
            {
                lines.Add($"- {group.Key}: {group.Count()}");
            }
        }
        else
        {
            lines.Add("- No risk findings available.");
        }
        lines.Add(string.Empty);

        lines.Add("### Dependency Health Summary");
        lines.Add(string.Empty);
        var deprecatedCount = data.Analysis?.RiskFindings.Count(r =>
            string.Equals(r.Category, "Dependency.Deprecated", StringComparison.OrdinalIgnoreCase)) ?? 0;
        var vulnerableCount = data.Analysis?.RiskFindings.Count(r =>
            string.Equals(r.Category, "Dependency.Vulnerability", StringComparison.OrdinalIgnoreCase)) ?? 0;
        lines.Add($"- Deprecated package findings: {deprecatedCount}");
        lines.Add($"- Vulnerability findings: {vulnerableCount}");
        lines.Add(string.Empty);

        lines.Add("## Solutions");
        lines.Add(string.Empty);
        foreach (var solution in data.Solutions)
        {
            lines.Add($"### {solution.SolutionName}");
            lines.Add(string.Empty);
            lines.Add($"- Path: {solution.SolutionPath}");
            lines.Add($"- Projects: {solution.Projects.Count}");
            if (solution.Summary.Count > 0)
            {
                lines.Add("- Summary:");
                foreach (var kvp in solution.Summary.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    lines.Add($"  - {kvp.Key}: {kvp.Value}");
                }
            }
            lines.Add(string.Empty);

            lines.Add("| Project | Type | Framework | Path | Dependencies | ");
            lines.Add("|---|---|---|---|---:|");
            foreach (var project in solution.Projects.OrderBy(p => p.ProjectName, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add($"| {Escape(project.ProjectName)} | {Escape(project.ProjectType)} | {Escape(project.Framework)} | {Escape(project.ProjectPath)} | {project.Dependencies.Count} |");
            }
            lines.Add(string.Empty);
        }

        lines.Add("## Dependency Summary");
        lines.Add(string.Empty);
        if (data.Analysis?.Dependencies.Count > 0)
        {
            lines.Add("| Name | Type | Source | Instances | Versions |");
            lines.Add("|---|---|---|---:|---|");
            foreach (var dep in data.Analysis.Dependencies.Values
                         .OrderByDescending(d => d.TotalInstances)
                         .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                var versions = dep.Version is null || dep.Version.Count == 0
                    ? string.Empty
                    : string.Join(", ", dep.Version.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase).Select(v => $"{v.Key} ({v.Value})"));
                lines.Add($"| {Escape(dep.Name)} | {Escape(dep.DependencyType)} | {Escape(dep.Source)} | {dep.TotalInstances} | {Escape(versions)} |");
            }
        }
        else
        {
            lines.Add("No dependency analysis data available.");
        }
        lines.Add(string.Empty);

        lines.Add("## Risk Findings");
        lines.Add(string.Empty);
        if (data.Analysis?.RiskFindings.Count > 0)
        {
            lines.Add("| Category | Severity | Description | Evidence |");
            lines.Add("|---|---|---|---|");
            foreach (var finding in data.Analysis.RiskFindings)
            {
                lines.Add($"| {Escape(finding.Category)} | {Escape(finding.Severity.ToString())} | {Escape(finding.Description)} | {Escape(finding.Evidence)} |");
            }
        }
        else
        {
            lines.Add("No risk findings available.");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string Escape(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private static string BuildPerSolutionMarkdown(SharpShift.Inventory.Core.Models.SolutionInventory solution, SharpShift.Inventory.Core.Models.ProjectAnalysisResult? analysis)
    {
        var lines = new List<string>
        {
            $"# SharpShift Solution Report: {solution.SolutionName}",
            string.Empty,
            $"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            string.Empty,
            "## Solution Overview",
            string.Empty,
            $"- Solution Name: {solution.SolutionName}",
            $"- Solution Path: {solution.SolutionPath}",
            $"- Project Count: {solution.Projects.Count}",
            string.Empty
        };

        if (solution.Summary.Count > 0)
        {
            lines.Add("### Summary Metadata");
            lines.Add(string.Empty);
            foreach (var kvp in solution.Summary.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add($"- {kvp.Key}: {kvp.Value}");
            }
            lines.Add(string.Empty);
        }

        lines.Add("## Risk Findings");
        lines.Add(string.Empty);
        if (analysis?.RiskFindings.Count > 0)
        {
            lines.Add("| Category | Severity | Description | Evidence |");
            lines.Add("|---|---|---|---|");
            foreach (var finding in analysis.RiskFindings)
            {
                lines.Add($"| {Escape(finding.Category)} | {Escape(finding.Severity.ToString())} | {Escape(finding.Description)} | {Escape(finding.Evidence)} |");
            }
        }
        else
        {
            lines.Add("No risk findings available.");
        }
        lines.Add(string.Empty);

        lines.Add("## Projects");
        lines.Add(string.Empty);
        lines.Add("| Project | Type | Framework | Path | Dependencies |");
        lines.Add("|---|---|---|---|---:|");
        foreach (var project in solution.Projects.OrderBy(p => p.ProjectName, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"| {Escape(project.ProjectName)} | {Escape(project.ProjectType)} | {Escape(project.Framework)} | {Escape(project.ProjectPath)} | {project.Dependencies.Count} |");
        }
        lines.Add(string.Empty);

        lines.Add("## Analysis Note");
        lines.Add(string.Empty);
        if (analysis is null)
        {
            lines.Add("No analysis data was available when this report was generated.");
        }
        else
        {
            lines.Add("Current analysis output is aggregated at estate level. Dependency and risk metrics are global and not attributed to this solution only.");
            lines.Add(string.Empty);
            lines.Add($"- Global unique dependencies: {analysis.Dependencies.Count}");
            lines.Add($"- Global dependency instances: {analysis.Dependencies.Values.Sum(d => d.TotalInstances)}");
            lines.Add($"- Global risk findings: {analysis.RiskFindings.Count}");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string MakeSafeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "solution";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string([.. value.Select(c => invalidChars.Contains(c) ? '_' : c)]);
        return string.IsNullOrWhiteSpace(sanitized) ? "solution" : sanitized;
    }
}
