using SharpShift.Inventory.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace SharpShift.Inventory.Core.Utilities
{
    public static class ReportDataUtils
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

        public static int ClampScore(int score)
        {
            if (score < 0) return 0;
            if (score > 100) return 100;
            return score;
        }

        public static string FormatScore(int score)
        {
            return $"{ClampScore(score)}%";
        }

        public static async Task<ReportData> CreateReportData(ReportOptions options, CancellationToken cancellationToken = default)
        {
            return await LoadDataFromJsonAsync(options, cancellationToken);
        }


        /// <summary>
        /// Loads inventory and analysis data from JSON files.
        /// </summary>
        /// <param name="options">Report options containing input file details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Loaded report data.</returns>
        public static async Task<ReportData> LoadDataFromJsonAsync(ReportOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            var inputDirectory = string.IsNullOrWhiteSpace(options.InputDirectory)
                ? Directory.GetCurrentDirectory()
                : options.InputDirectory;

            if (!Directory.Exists(inputDirectory))
                throw new DirectoryNotFoundException($"Input directory not found: {inputDirectory}");

            var inventoryFile = Path.Combine(inputDirectory, options.InventoryFileName);
            if (!File.Exists(inventoryFile))
                throw new FileNotFoundException("Inventory file not found.", inventoryFile);

            var inventoryJson = await File.ReadAllTextAsync(inventoryFile, cancellationToken);
            var solutions = InventoryOutputReader.Deserialize(inventoryJson, _jsonOptions);

            var analysisFile = Path.Combine(inputDirectory, options.AnalysisFileName);
            ProjectAnalysisResult? analysis = null;
            if (File.Exists(analysisFile))
            {
                await using var analysisStream = File.OpenRead(analysisFile);
                analysis = await JsonSerializer.DeserializeAsync<ProjectAnalysisResult>(analysisStream, _jsonOptions, cancellationToken);
            }

            return new ReportData
            {
                Solutions = solutions,
                Analysis = analysis
            };
        }

        public static IEnumerable<ProjectInventory> GetOutOfSupportFrameworkProjects(IReadOnlyList<SharpShift.Inventory.Core.Models.ProjectInventory> projects) =>
            projects.Where(project => ClassifyFrameworkSupportStatus(project.Framework) == FrameworkSupportStatus.OutOfSupport);

        public static int CountLegacyProjectFiles(IReadOnlyList<SharpShift.Inventory.Core.Models.ProjectInventory> projects)
        {
            var legacyCount = 0;

            foreach (var project in projects)
            {
                if (project.IsLegacyProject)
                    legacyCount++;
            }

            return legacyCount;
        }

        public static FrameworkSupportStatus ClassifyFrameworkSupportStatus(string? framework)
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

        public static Dictionary<RiskSeverity, int> InitializeSeverityCounts() =>
            new ()
            {
                [RiskSeverity.Critical] = 0,
                [RiskSeverity.High] = 0,
                [RiskSeverity.Medium] = 0,
                [RiskSeverity.Low] = 0
            };

        public static double ComputeTotalFindings(VulnerabilitySeverityCounts counts) =>
            counts.Critical + counts.High + counts.Medium + counts.Low;

        public static double ComputeWeightedTotal(VulnerabilitySeverityCounts counts) =>
            (counts.Critical * ReportConstants.CriticalRiskWeight) +
            (counts.High * ReportConstants.HighRiskWeight) +
            (counts.Medium * ReportConstants.MediumRiskWeight) +
            (counts.Low * ReportConstants.LowRiskWeight);

        public static double ComputeWeightedRiskScore(VulnerabilitySeverityCounts counts) =>
            ComputeTotalFindings(counts) == 0 ? 0 : Math.Round(ComputeWeightedTotal(counts) / ComputeTotalFindings(counts), 2, MidpointRounding.AwayFromZero);

        public static string GetOverallRiskLabel(double weightedScore) =>
            weightedScore >= ReportConstants.CriticalRiskThreshold ? "Critical" :
            weightedScore >= ReportConstants.HighRiskThreshold ? "High" :
            weightedScore >= ReportConstants.MediumRiskThreshold ? "Medium" : "Low";


        public static int ComputeSecurityPostureScore(ProjectAnalysisResult? analysis)
        {
            if (analysis is null)
                return 100;

            var penalty = analysis.RiskFindings
                .Where(r => string.Equals(r.Category, "Dependency.Vulnerability", StringComparison.OrdinalIgnoreCase))
                .Sum(r => r.Severity switch
                {
                    RiskSeverity.Critical => ReportConstants.RiskSeverityCriticalPenalty,
                    RiskSeverity.High => ReportConstants.RiskSeverityHighPenalty,
                    RiskSeverity.Medium => ReportConstants.RiskSeverityMediumPenalty,
                    _ => ReportConstants.RiskSeverityLowPenalty 
                });

            return ClampScore(100 - penalty);
        }

        public static ModernizationScorecardData ComputeModernizationScorecard(IReadOnlyList<SolutionInventory> solutions, IReadOnlyList<ProjectInventory> projects, ProjectAnalysisResult? analysis)
        {
            var frameworkCurrency = ClampScore(ComputeFrameworkCurrencyScore(projects));
            var packageHealth = ClampScore(ComputePackageHealthScore(analysis));
            var securityPosture = ClampScore(ReportDataUtils.ComputeSecurityPostureScore(analysis));
            var dependencyHygiene = ClampScore(ComputeDependencyHygieneScore(projects, analysis));
            var repositoryModernity = ClampScore(ComputeRepositoryModernityScore(solutions, projects));
            var overallScore = ClampScore(ComputeOverallModernizationScore(frameworkCurrency, packageHealth, securityPosture, dependencyHygiene, repositoryModernity));

            return new ModernizationScorecardData(
                frameworkCurrency,
                packageHealth,
                securityPosture,
                dependencyHygiene,
                repositoryModernity,
                overallScore);
        }

        public static int ComputeOverallModernizationScore(int frameworkCurrency, int packageHealth, int securityPosture, int dependencyHygiene, int repositoryModernity)
        {
            return (int)Math.Round(
                (frameworkCurrency * ReportConstants.FrameworkCurrencyWeight) +
                (packageHealth * ReportConstants.PackageHealthWeight) +
                (securityPosture * ReportConstants.SecurityPostureWeight) +
                (dependencyHygiene * ReportConstants.DependencyHygieneWeight) +
                (repositoryModernity * ReportConstants.RepositoryModernityWeight),
                MidpointRounding.AwayFromZero);
        }

        public static int ComputeFrameworkCurrencyScore(IReadOnlyList<ProjectInventory> projects)
        {
            if (projects.Count == 0)
                return 100;

            var weighted = projects.Sum(project => ReportDataUtils.ClassifyFrameworkSupportStatus(project.Framework) switch
            {
                FrameworkSupportStatus.Supported => 1.0,
                FrameworkSupportStatus.NearEndOfSupport => 0.6,
                FrameworkSupportStatus.OutOfSupport => 0.0,
                _ => 0.5
            });

            return (int)Math.Round((weighted / projects.Count) * 100, MidpointRounding.AwayFromZero);
        }

        public static int ComputePackageHealthScore(ProjectAnalysisResult? analysis)
        {
            if (analysis is null)
                return 100;

            var deprecatedCount = analysis.RiskFindings.Count(r => string.Equals(r.Category, "Dependency.Deprecated", StringComparison.OrdinalIgnoreCase));
            var vulnerabilities = analysis.RiskFindings.Count(r => string.Equals(r.Category, "Dependency.Vulnerability", StringComparison.OrdinalIgnoreCase));

            return ClampScore(100 - (deprecatedCount * 5) - (vulnerabilities * 3));
        }

        public static int ComputeDependencyHygieneScore(IReadOnlyList<ProjectInventory> projects, ProjectAnalysisResult? analysis)
        {
            var consistencyScore = 100;
            if (analysis?.Dependencies.Count > 0)
            {
                var multiVersion = analysis.Dependencies.Values.Count(d => d.Version is { Count: > 1 });
                consistencyScore = ClampScore(100 - (multiVersion * 4));
            }

            if (projects.Count == 0)
                return consistencyScore;

            var sdkStyleProjects = projects.Count(p => !p.IsLegacyProject);
            var sdkStyleScore = (int)Math.Round((double)sdkStyleProjects / projects.Count * 100, MidpointRounding.AwayFromZero);

            return ClampScore((consistencyScore + sdkStyleScore) / 2);
        }

        public static int ComputeRepositoryModernityScore(IReadOnlyList<SolutionInventory> solutions, IReadOnlyList<ProjectInventory> projects)
        {
            if (solutions.Count == 0)
                return 100;

            var sourceControlScore = (int)Math.Round(
                (double)solutions.Count(s => !string.IsNullOrWhiteSpace(s.RepositoryUrl) && !s.IsArchived) / solutions.Count * 100,
                MidpointRounding.AwayFromZero);

            if (projects.Count == 0)
                return sourceControlScore;

            var modernProjectFiles = projects.Count(p => !p.IsLegacyProject);
            var projectFileScore = (int)Math.Round((double)modernProjectFiles / projects.Count * 100, MidpointRounding.AwayFromZero);

            return ClampScore((sourceControlScore + projectFileScore) / 2);
        }


        public readonly record struct ExecutiveRiskAssessmentData(
            string OverallRisk,
            VulnerabilitySeverityCounts Vulnerabilities,
            int DeprecatedPackageFindings,
            double WeightedRiskScore,
            IEnumerable<ProjectInventory> OutOfSupportFrameworkProjects,
            int LegacyProjectFiles);

        public readonly record struct TopFinding(
            string PackageId,
            int AffectedProjects,
            IReadOnlyList<string> AffectedVersions,
            IReadOnlyList<ReportDataUtils.AdvisoryReference> Advisories,
            IReadOnlyList<ReportDataUtils.CategorySummary> Categories,
            RiskSeverity HighestSeverity,
            string Recommendation,
            int RankScore);

        public readonly record struct AdvisoryReference(string AdvisoryId, double? CvssScore);

        public readonly record struct CategorySummary(string Category, int Count);

        public readonly record struct ModernizationScorecardData(
            int FrameworkCurrency,
            int PackageHealth,
            int SecurityPosture,
            int DependencyHygiene,
            int RepositoryModernity,
            int OverallScore);
    }
}
