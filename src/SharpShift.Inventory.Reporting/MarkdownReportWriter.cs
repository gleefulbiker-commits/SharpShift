using SharpShift.Inventory.Core.Models;
using System.Xml.Linq;

namespace SharpShift.Inventory.Reporting;

internal sealed class MarkdownReportWriter : IReportWriter
{
    private const int CriticalRiskWeight = 10;
    private const int HighRiskWeight = 6;
    private const int MediumRiskWeight = 3;
    private const int LowRiskWeight = 1;

    private const double CriticalRiskThreshold = 8.0;
    private const double HighRiskThreshold = 5.0;
    private const double MediumRiskThreshold = 2.0;

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

    private static ExecutiveRiskAssessmentData BuildExecutiveRiskAssessmentData(
        IReadOnlyList<SharpShift.Inventory.Core.Models.ProjectInventory> projects,
        SharpShift.Inventory.Core.Models.ProjectAnalysisResult? analysis)
    {
        var severityCounts = InitializeSeverityCounts();
        var deprecatedCount = 0;

        if (analysis?.RiskFindings.Count > 0)
        {
            foreach (var finding in analysis.RiskFindings)
            {
                if (severityCounts.ContainsKey(finding.Severity))
                    severityCounts[finding.Severity]++;

                if (string.Equals(finding.Category, "Dependency.Deprecated", StringComparison.OrdinalIgnoreCase))
                    deprecatedCount++;
            }
        }

        var vulnerabilityCounts = new VulnerabilitySeverityCounts(
            severityCounts[RiskSeverity.Critical],
            severityCounts[RiskSeverity.High],
            severityCounts[RiskSeverity.Medium],
            severityCounts[RiskSeverity.Low]);

        var weightedScore = ComputeWeightedRiskScore(vulnerabilityCounts);
        var overallRisk = GetOverallRiskLabel(weightedScore);
        var outOfSupportFrameworkProjects = CountOutOfSupportFrameworkProjects(projects);
        var legacyProjectFiles = CountLegacyProjectFiles(projects);

        return new ExecutiveRiskAssessmentData(
            overallRisk,
            vulnerabilityCounts,
            deprecatedCount,
            weightedScore,
            outOfSupportFrameworkProjects,
            legacyProjectFiles);
    }

    private static int CountOutOfSupportFrameworkProjects(IReadOnlyList<SharpShift.Inventory.Core.Models.ProjectInventory> projects)
    {
        return projects.Count(project => ClassifyFrameworkSupportStatus(project.Framework) == FrameworkSupportStatus.OutOfSupport);
    }

    private static int CountLegacyProjectFiles(IReadOnlyList<SharpShift.Inventory.Core.Models.ProjectInventory> projects)
    {
        var legacyCount = 0;

        foreach (var project in projects)
        {
            if (IsLegacyProjectFile(project))
                legacyCount++;
        }

        return legacyCount;
    }

    private static FrameworkSupportStatus ClassifyFrameworkSupportStatus(string? framework)
    {
        if (string.IsNullOrWhiteSpace(framework))
            return FrameworkSupportStatus.Unknown;

        var value = framework.Trim();

        if (value.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
            return FrameworkSupportStatus.OutOfSupport;

        if (value.StartsWith(".NET Core", StringComparison.OrdinalIgnoreCase))
            return FrameworkSupportStatus.OutOfSupport;

        if (value.StartsWith(".NET Standard", StringComparison.OrdinalIgnoreCase))
            return FrameworkSupportStatus.Unknown;

        if (!value.StartsWith(".NET ", StringComparison.OrdinalIgnoreCase))
            return FrameworkSupportStatus.Unknown;

        var version = value[5..].Trim();
        var majorToken = version.Split('.', StringSplitOptions.RemoveEmptyEntries)[0];
        if (!int.TryParse(majorToken, out var major))
            return FrameworkSupportStatus.Unknown;

        if (major >= 8)
            return FrameworkSupportStatus.Supported;

        if (major == 7)
            return FrameworkSupportStatus.NearEndOfSupport;

        return FrameworkSupportStatus.OutOfSupport;
    }

    private static bool IsLegacyProjectFile(ProjectInventory project)
    {
        return project.ProjectType.Contains("Legacy", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<RiskSeverity, int> InitializeSeverityCounts()
    {
        return new Dictionary<RiskSeverity, int>
        {
            [RiskSeverity.Critical] = 0,
            [RiskSeverity.High] = 0,
            [RiskSeverity.Medium] = 0,
            [RiskSeverity.Low] = 0
        };
    }

    private static double ComputeWeightedRiskScore(VulnerabilitySeverityCounts counts)
    {
        var totalFindings = counts.Critical + counts.High + counts.Medium + counts.Low;
        if (totalFindings == 0)
            return 0;

        var weightedTotal =
            (counts.Critical * CriticalRiskWeight) +
            (counts.High * HighRiskWeight) +
            (counts.Medium * MediumRiskWeight) +
            (counts.Low * LowRiskWeight);

        return Math.Round((double)weightedTotal / totalFindings, 2, MidpointRounding.AwayFromZero);
    }

    private static string GetOverallRiskLabel(double weightedScore)
    {
        if (weightedScore >= CriticalRiskThreshold)
            return "Critical";

        if (weightedScore >= HighRiskThreshold)
            return "High";

        if (weightedScore >= MediumRiskThreshold)
            return "Medium";

        return "Low";
    }

    private readonly record struct VulnerabilitySeverityCounts(int Critical, int High, int Medium, int Low);

    private readonly record struct ExecutiveRiskAssessmentData(
        string OverallRisk,
        VulnerabilitySeverityCounts Vulnerabilities,
        int DeprecatedPackageFindings,
        double WeightedRiskScore,
        int OutOfSupportFrameworkProjects,
        int LegacyProjectFiles);

    private enum FrameworkSupportStatus
    {
        Unknown,
        Supported,
        NearEndOfSupport,
        OutOfSupport
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
        var dashboard = BuildExecutiveRiskAssessmentData(projects, data.Analysis);
        lines.Add($"- Solutions: {data.Solutions.Count}");
        lines.Add($"- Projects: {projects.Count}");
        lines.Add($"- Unique dependencies: {data.Analysis?.Dependencies.Count ?? 0}");
        lines.Add($"- Dependency instances: {data.Analysis?.Dependencies.Values.Sum(d => d.TotalInstances) ?? 0}");
        lines.Add($"- Risk findings: {data.Analysis?.RiskFindings.Count ?? 0}");
        lines.Add(string.Empty);

        lines.Add("## Executive Risk Assessment");
        lines.Add(string.Empty);
        lines.Add($"Overall Risk: {dashboard.OverallRisk}");
        lines.Add(string.Empty);
        lines.Add($"- Critical Vulnerabilities: {dashboard.Vulnerabilities.Critical}");
        lines.Add($"- High Vulnerabilities: {dashboard.Vulnerabilities.High}");
        lines.Add($"- Medium Vulnerabilities: {dashboard.Vulnerabilities.Medium}");
        lines.Add($"- Low Vulnerabilities: {dashboard.Vulnerabilities.Low}");
        lines.Add(string.Empty);
        lines.Add($"- Deprecated Packages: {dashboard.DeprecatedPackageFindings}");
        lines.Add($"- Out-of-Support Frameworks: {dashboard.OutOfSupportFrameworkProjects} projects");
        lines.Add($"- Legacy Project Files: {dashboard.LegacyProjectFiles} projects");
        lines.Add(string.Empty);
        // Replace the following lines with the generated recommended priority list
        lines.Add("Recommended Priority:");
        lines = BuildRecommendedPriorities(lines, data.Analysis, projects);
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
        var dashboard = BuildExecutiveRiskAssessmentData(solution.Projects, analysis);

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

        lines.Add("## Executive Risk Assessment");
        lines.Add(string.Empty);
        lines.Add($"Overall Risk: {dashboard.OverallRisk}");
        lines.Add(string.Empty);
        lines.Add($"- Critical Vulnerabilities: {dashboard.Vulnerabilities.Critical}");
        lines.Add($"- High Vulnerabilities: {dashboard.Vulnerabilities.High}");
        lines.Add($"- Medium Vulnerabilities: {dashboard.Vulnerabilities.Medium}");
        lines.Add($"- Low Vulnerabilities: {dashboard.Vulnerabilities.Low}");
        lines.Add(string.Empty);
        lines.Add($"- Deprecated Packages: {dashboard.DeprecatedPackageFindings}");
        lines.Add($"- Out-of-Support Frameworks: {dashboard.OutOfSupportFrameworkProjects} projects");
        lines.Add($"- Legacy Project Files: {dashboard.LegacyProjectFiles} projects");
        lines.Add(string.Empty);
        // Replace the following lines with the generated recommended priority list
        lines.Add("Recommended Priority:");
        lines = BuildRecommendedPriorities(lines, analysis, solution.Projects);
        lines.Add(string.Empty);

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

    private static List<string> BuildRecommendedPriorities(List<string> lines, ProjectAnalysisResult analysis, List<ProjectInventory> projects)
    {
        if (analysis?.RiskFindings.Count > 0)
        {
            var vulnerablePackages = analysis.RiskFindings
                .Where(r => string.Equals(r.Category, "Dependency.Vulnerability", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.Severity)
                .Select(r => r.Description)
                .Distinct()
                .ToList();
            if (vulnerablePackages.Count > 0)
            {
                lines.Add("Address vulnerable packages:");
                foreach (var pkg in vulnerablePackages)
                {
                    lines.Add($"- {pkg}");
                }
            }
            var deprecatedPackages = analysis.RiskFindings
                .Where(r => string.Equals(r.Category, "Dependency.Deprecated", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Description)
                .Distinct()
                .ToList();
            if (deprecatedPackages.Count > 0)
            {
                lines.Add("Upgrade or remove deprecated packages:");
                foreach (var pkg in deprecatedPackages)
                {
                    lines.Add($"- {pkg}");
                }
            }
        }
        var outOfSupportProjects = projects
            .Where(p => ClassifyFrameworkSupportStatus(p.Framework) == FrameworkSupportStatus.OutOfSupport)
            .Select(p => p.ProjectName)
            .Distinct()
            .ToList();
        if (outOfSupportProjects.Count > 0)
        {
            lines.Add("Upgrade out-of-support framework projects:");
            foreach (var proj in outOfSupportProjects)
            {
                lines.Add($"- {proj}");
            }
        }
        return lines;
    }
}
