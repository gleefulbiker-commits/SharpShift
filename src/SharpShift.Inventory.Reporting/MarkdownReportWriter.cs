using SharpShift.Inventory.Core.Models;
using SharpShift.Inventory.Core.Utilities;
using System.Xml.Linq;

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

    private static ReportDataUtils.ExecutiveRiskAssessmentData BuildExecutiveRiskAssessmentData(
        IReadOnlyList<SharpShift.Inventory.Core.Models.ProjectInventory> projects,
        SharpShift.Inventory.Core.Models.ProjectAnalysisResult? analysis)
    {
        var severityCounts = ReportDataUtils.InitializeSeverityCounts();
        var deprecatedCount = 0;

        if (analysis?.RiskFindings.Count > 0)
        {
            foreach (var finding in analysis.RiskFindings)
            {
                if (string.Equals(finding.Category, "Dependency.Vulnerability", StringComparison.OrdinalIgnoreCase)
                    && severityCounts.TryGetValue(finding.Severity, out var count))
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

        var weightedScore = ReportDataUtils.ComputeWeightedRiskScore(vulnerabilityCounts);
        var overallRisk = ReportDataUtils.GetOverallRiskLabel(weightedScore);
        var outOfSupportFrameworkProjects = ReportDataUtils.GetOutOfSupportFrameworkProjects(projects);
        var legacyProjectFiles = ReportDataUtils.CountLegacyProjectFiles(projects);

        return new ReportDataUtils.ExecutiveRiskAssessmentData(
            overallRisk,
            vulnerabilityCounts,
            deprecatedCount,
            weightedScore,
            outOfSupportFrameworkProjects,
            legacyProjectFiles);
    }

    private static List<ReportDataUtils.TopFinding> BuildTopFindings(IReadOnlyList<ProjectInventory> projects, ProjectAnalysisResult? analysis)
    {
        if (analysis?.RiskFindings.Count is not > 0)
            return [];

        var dependencyFindings = analysis.RiskFindings
            .Where(r => r.Category.StartsWith("Dependency.", StringComparison.OrdinalIgnoreCase))
            .Select(r =>
            {
                var packageId = string.IsNullOrWhiteSpace(r.PackageId)
                    ? TryParsePackageId(r.Description)
                    : r.PackageId!;
                var packageVersion = string.IsNullOrWhiteSpace(r.PackageVersion)
                    ? TryParsePackageVersion(r.Description)
                    : r.PackageVersion;
                var advisoryId = string.IsNullOrWhiteSpace(r.AdvisoryId)
                    ? TryParseAdvisoryId(r.Evidence)
                    : r.AdvisoryId;

                return new
                {
                    Finding = r,
                    PackageId = packageId,
                    PackageVersion = packageVersion,
                    AdvisoryId = advisoryId
                };
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.PackageId))
            .ToList();

        if (dependencyFindings.Count == 0)
            return [];

        return [..dependencyFindings
            .GroupBy(x => x.PackageId!, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var versions = group
                    .Select(x => x.PackageVersion)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                    .Cast<string>()
                    .ToList();

                var advisories = group
                    .Where(x => !string.IsNullOrWhiteSpace(x.AdvisoryId))
                    .Select(x => new ReportDataUtils.AdvisoryReference(x.AdvisoryId!, x.Finding.CvssScore))
                    .Distinct()
                    .OrderBy(a => a.AdvisoryId, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var categories = group
                    .GroupBy(x => x.Finding.Category, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new ReportDataUtils.CategorySummary(g.Key, g.Count()))
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var highestSeverity = group.Max(x => x.Finding.Severity);
                var affectedProjects = CountAffectedProjects(projects, group.Key);
                var recommendation = BuildTopFindingRecommendation(group.Key, highestSeverity, versions);
                var rankScore = (group.Count() * 2) + (int)highestSeverity * 3 + (affectedProjects * 2);

                return new ReportDataUtils.TopFinding(
                    group.Key,
                    affectedProjects,
                    versions,
                    advisories,
                    categories,
                    highestSeverity,
                    recommendation,
                    rankScore);
            })
            .OrderByDescending(x => x.RankScore)
            .ThenByDescending(x => x.AffectedProjects)
            .ThenBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase)
            .Take(10)];
    }

    private static ReportDataUtils.ModernizationScorecardData BuildModernizationScorecard(
        IReadOnlyList<SolutionInventory> solutions,
        IReadOnlyList<ProjectInventory> projects,
        ProjectAnalysisResult? analysis)
    {

        var frameworkCurrency = ReportDataUtils.ClampScore(ReportDataUtils.ComputeFrameworkCurrencyScore(projects));
        var packageHealth = ReportDataUtils.ClampScore(ReportDataUtils.ComputePackageHealthScore(analysis));
        var securityPosture = ReportDataUtils.ClampScore(ReportDataUtils.ComputeSecurityPostureScore(analysis));
        var dependencyHygiene = ReportDataUtils.ClampScore(ReportDataUtils.ComputeDependencyHygieneScore(projects, analysis));
        var repositoryModernity = ReportDataUtils.ClampScore(ReportDataUtils.ComputeRepositoryModernityScore(solutions, projects));
        var overallScore = ReportDataUtils.ClampScore(ReportDataUtils.ComputeOverallModernizationScore(frameworkCurrency, packageHealth, securityPosture, dependencyHygiene, repositoryModernity));

        return new ReportDataUtils.ModernizationScorecardData(
            frameworkCurrency,
            packageHealth,
            securityPosture,
            dependencyHygiene,
            repositoryModernity,
            overallScore);
    }

    private static void AddTopFindingsSection(List<string> lines, List<ReportDataUtils.TopFinding> findings)
    {
        lines.Add("## Top Findings");
        lines.Add(string.Empty);

        if (findings.Count == 0)
        {
            lines.Add("No dependency findings available.");
            lines.Add(string.Empty);
            return;
        }

        for (var i = 0; i < findings.Count; i++)
        {
            var finding = findings[i];
            lines.Add($"### {i + 1}. {finding.PackageId}");
            lines.Add($"Affected Projects: {finding.AffectedProjects}");
            lines.Add(string.Empty);
            lines.Add("Affected Versions:");
            if (finding.AffectedVersions.Count == 0)
                lines.Add("- Unknown");
            else
            {
                foreach (var version in finding.AffectedVersions)
                    lines.Add($"- {version}");
            }

            lines.Add(string.Empty);
            lines.Add("Advisories:");
            if (finding.Advisories.Count == 0)
                lines.Add("- None");
            else
            {
                foreach (var advisory in finding.Advisories)
                {
                    var cvss = advisory.CvssScore.HasValue ? $" (CVSS {advisory.CvssScore.Value:0.0})" : string.Empty;
                    lines.Add($"- {advisory.AdvisoryId}{cvss}");
                }
            }

            lines.Add(string.Empty);
            lines.Add("Categories:");
            foreach (var category in finding.Categories)
                lines.Add($"- {category.Category}: {category.Count}");

            lines.Add(string.Empty);
            lines.Add("Recommendation:");
            lines.Add(finding.Recommendation);
            lines.Add(string.Empty);
        }
    }

    private static void AddModernizationScorecardSection(List<string> lines, ReportDataUtils.ModernizationScorecardData scorecard)
    {
        lines.Add("## Modernization Scorecard");
        lines.Add(string.Empty);
        lines.Add("The Modernization Scorecard provides a high-level overview of the health and modernization status of the analyzed solutions and projects. Each area is scored on a scale from 0 to 100, with higher scores indicating better health and modernization practices.");
        lines.Add(string.Empty);
        lines.Add("| Area | Score |");
        lines.Add("|---|---:|");
        lines.Add($"| Framework Currency | {scorecard.FrameworkCurrency} |");
        lines.Add($"| Package Health | {scorecard.PackageHealth} |");
        lines.Add($"| Security Posture | {scorecard.SecurityPosture} |");
        lines.Add($"| Dependency Hygiene | {scorecard.DependencyHygiene} |");
        lines.Add($"| Repository Modernity | {scorecard.RepositoryModernity} |");
        lines.Add(string.Empty);
        lines.Add($"Overall Score: {scorecard.OverallScore} / 100");
        lines.Add(string.Empty);
    }

    private static int CountAffectedProjects(IReadOnlyList<ProjectInventory> projects, string packageId)
    {
        return projects
            .Where(project => project.Dependencies.Any(d =>
                string.Equals(d.DependencyType, "Package", StringComparison.OrdinalIgnoreCase)
                && string.Equals(d.Name, packageId, StringComparison.OrdinalIgnoreCase)))
            .Select(project => string.IsNullOrWhiteSpace(project.ProjectPath) ? project.ProjectName : project.ProjectPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private static string BuildTopFindingRecommendation(string packageId, RiskSeverity highestSeverity, List<string> versions)
    {
        if (highestSeverity >= RiskSeverity.High)
            return $"Upgrade {packageId} to the latest secure release as soon as possible.";

        if (versions.Count > 1)
            return $"Standardize {packageId} to a single supported version.";

        return $"Review {packageId} and plan remediation in the next maintenance cycle.";
    }

    private static string? TryParsePackageId(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var parts = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : null;
    }

    private static string? TryParsePackageVersion(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var parts = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1].TrimEnd(':') : null;
    }

    private static string? TryParseAdvisoryId(string? evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence))
            return null;

        return evidence.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
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
        var topFindings = BuildTopFindings(projects, data.Analysis);
        var scorecard = BuildModernizationScorecard(data.Solutions, projects, data.Analysis);
        lines.Add($"- Solutions: {data.Solutions.Count}");
        lines.Add($"- Projects: {projects.Count}");
        lines.Add($"- Unique dependencies: {data.Analysis?.Dependencies.Count ?? 0}");
        lines.Add($"- Dependency instances: {data.Analysis?.Dependencies.Values.Sum(d => d.TotalInstances) ?? 0}");
        lines.Add($"- Risk findings: {data.Analysis?.RiskFindings.Count ?? 0}");
        lines.Add(string.Empty);

        lines.Add("## Executive Risk Assessment");
        lines.Add(string.Empty);
        lines.Add("Note: Analysis findings are currently aggregated at estate level and may include findings outside any single solution.");
        lines.Add(string.Empty);
        lines.Add($"Overall Risk: {dashboard.OverallRisk}");
        lines.Add($"- Weighted Risk Score: {dashboard.WeightedRiskScore} (Weighted total is {ReportDataUtils.ComputeWeightedTotal(dashboard.Vulnerabilities)} across {ReportDataUtils.ComputeTotalFindings(dashboard.Vulnerabilities)} findings)");
        lines.Add(string.Empty);
        lines.Add($"- Critical Vulnerabilities: {dashboard.Vulnerabilities.Critical}");
        lines.Add($"- High Vulnerabilities: {dashboard.Vulnerabilities.High}");
        lines.Add($"- Medium Vulnerabilities: {dashboard.Vulnerabilities.Medium}");
        lines.Add($"- Low Vulnerabilities: {dashboard.Vulnerabilities.Low}");
        lines.Add(string.Empty);
        lines.Add($"- Deprecated Packages: {dashboard.DeprecatedPackageFindings}");
        lines.Add($"- Out-of-Support Frameworks: {dashboard.OutOfSupportFrameworkProjects.Count()} projects");
        lines.Add($"- Legacy Project Files: {dashboard.LegacyProjectFiles} projects");
        lines.Add($"- Solutions Not In Version Control: {data.Solutions.Count(s => string.IsNullOrWhiteSpace(s.RepositoryUrl))}");
        lines.Add(string.Empty);
        lines.Add("### Recommended Priorities");
        lines.Add(string.Empty);
        BuildRecommendedPriorities(lines, data.Analysis ?? new(), data.Solutions, projects);
        lines.Add(string.Empty);
        AddModernizationScorecardSection(lines, scorecard);
        lines.Add(string.Empty);

        AddTopFindingsSection(lines, topFindings);

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

        lines.Add("# Appendix A");
        lines.Add("## Out-of-Support Framework Projects");
        lines.Add(string.Empty);
        foreach (var project in dashboard.OutOfSupportFrameworkProjects.OrderBy(p => p.ProjectName, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"- {project.ProjectName} ({project.Framework})");
        }
        lines.Add(string.Empty);

        lines.Add("# Appendix B");
        lines.Add("## Solutions");
        lines.Add(string.Empty);
        foreach (var solution in data.Solutions.OrderBy(n => n.SolutionName, StringComparer.OrdinalIgnoreCase))
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

            lines.Add("| Project | Is SDK Style | Type | Framework | Is Legacy | Path | Dependencies | ");
            lines.Add("|---|---|---|---|---|---|---|");
            foreach (var project in solution.Projects.OrderBy(p => p.ProjectName, StringComparer.OrdinalIgnoreCase))
            {
                var isLegacy = project.IsLegacyProject ? "Yes" : "No";
                lines.Add($"| {Escape(project.ProjectName)} | {Escape(project.IsSdkStyle.ToString())} | {Escape(project.ProjectType)} | {Escape(project.Framework)} | {Escape(isLegacy)} | {Escape(project.ProjectPath)} | {project.Dependencies.Count} |");
            }
            lines.Add(string.Empty);
        }

        lines.Add("# Appendix C");
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

        lines.Add("# Appendix D");
        lines.Add("## Risk Findings Details");
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
        lines.Add(string.Empty);

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
        var topFindings = BuildTopFindings(solution.Projects, analysis);
        var scorecard = BuildModernizationScorecard([solution], solution.Projects, analysis);

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
        lines.Add("Note: Analysis findings are currently aggregated at estate level and may include findings outside this single solution.");
        lines.Add(string.Empty);
        lines.Add($"Overall Risk: {dashboard.OverallRisk}");
        lines.Add($"- Weighted Risk Score: {dashboard.WeightedRiskScore} (Weighted total is {ReportDataUtils.ComputeWeightedTotal(dashboard.Vulnerabilities)} across {ReportDataUtils.ComputeTotalFindings(dashboard.Vulnerabilities)} findings)");
        lines.Add(string.Empty);
        lines.Add($"- Critical Vulnerabilities: {dashboard.Vulnerabilities.Critical}");
        lines.Add($"- High Vulnerabilities: {dashboard.Vulnerabilities.High}");
        lines.Add($"- Medium Vulnerabilities: {dashboard.Vulnerabilities.Medium}");
        lines.Add($"- Low Vulnerabilities: {dashboard.Vulnerabilities.Low}");
        lines.Add(string.Empty);
        lines.Add($"- Deprecated Packages: {dashboard.DeprecatedPackageFindings}");
        lines.Add($"- Out-of-Support Frameworks: {dashboard.OutOfSupportFrameworkProjects.Count()} projects");
        lines.Add($"- Legacy Project Files: {dashboard.LegacyProjectFiles} projects");
        lines.Add($"- Solution In Version Control: {(string.IsNullOrWhiteSpace(solution.RepositoryUrl)? "No" : "Yes")}");
        lines.Add(string.Empty);

        lines.Add("### Recommended Priorities");
        lines.Add(string.Empty);
        BuildRecommendedPriorities(lines, analysis!, [solution], solution.Projects);
        lines.Add(string.Empty);

        AddModernizationScorecardSection(lines, scorecard);
        lines.Add(string.Empty);

        AddTopFindingsSection(lines, topFindings);

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


        lines.Add("## Appendix: Out-of-Support Framework Projects");
        lines.Add(string.Empty);
        foreach (var project in dashboard.OutOfSupportFrameworkProjects.OrderBy(p => p.ProjectName, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"- {project.ProjectName} ({project.Framework})");
        }
        lines.Add(string.Empty);

        lines.Add("## Appendix: Projects");
        lines.Add(string.Empty);
        lines.Add("| Project | Is SDK Style | Type | Framework | Is Legacy | Path | Dependencies | ");
        lines.Add("|---|---|---|---|---|---|---|");
        foreach (var project in solution.Projects.OrderBy(p => p.ProjectName, StringComparer.OrdinalIgnoreCase))
        {
            var isLegacy = project.IsLegacyProject ? "Yes" : "No";
            lines.Add($"| {Escape(project.ProjectName)} | {Escape(project.IsSdkStyle.ToString())} | {Escape(project.ProjectType)} | {Escape(project.Framework)} | {Escape(isLegacy)} | {Escape(project.ProjectPath)} | {project.Dependencies.Count} |");
        }
        lines.Add(string.Empty);


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

    private static List<string> BuildRecommendedPriorities(List<string> lines, ProjectAnalysisResult analysis, IEnumerable<SolutionInventory> solutions, List<ProjectInventory> projects)
    {
        int recommendationCount = 1;

        if (analysis?.RiskFindings.Count > 0)
        {
            var vulnerablePackages = analysis.RiskFindings
                .Where(r => string.Equals(r.Category, "Dependency.Vulnerability", StringComparison.OrdinalIgnoreCase)
                            && r.Severity.Equals(RiskSeverity.Critical))
                .Count();

            if (vulnerablePackages > 0)
            {
                lines.Add($"{recommendationCount}. Address packages with critical vulnerabilities: {vulnerablePackages}");
                recommendationCount++;
            }

            vulnerablePackages = analysis.RiskFindings
                .Where(r => string.Equals(r.Category, "Dependency.Vulnerability", StringComparison.OrdinalIgnoreCase)
                            && r.Severity.Equals(RiskSeverity.High))
                .Count();

            if (vulnerablePackages > 0)
            {
                lines.Add($"{recommendationCount}. Address packages with high vulnerabilities: {vulnerablePackages}");
                recommendationCount++;
            }

            var deprecatedPackages = analysis.RiskFindings
                .Where(r => string.Equals(r.Category, "Dependency.Deprecated", StringComparison.OrdinalIgnoreCase))
                .Count();

            if (deprecatedPackages > 0)
            {
                lines.Add($"{recommendationCount}. Upgrade or remove deprecated packages: {deprecatedPackages}");
                recommendationCount++;
            }
        }

        var outOfSupportProjects = projects
            .Where(p => ReportDataUtils.ClassifyFrameworkSupportStatus(p.Framework) == FrameworkSupportStatus.OutOfSupport)
            .Count();
        if (outOfSupportProjects > 0)
        {
            lines.Add($"{recommendationCount}. Upgrade Out-of-Support framework projects: {outOfSupportProjects}");
            recommendationCount++;
        }

        var solutionsWithoutVersionControl = solutions
            .Where(p => string.IsNullOrWhiteSpace(p.RepositoryUrl))
            .Count();
        if (solutionsWithoutVersionControl > 0)
        {
                lines.Add($"{recommendationCount}. Add solutions to version control: {solutionsWithoutVersionControl}");
                recommendationCount++;
        }

        return lines;
    }
}
