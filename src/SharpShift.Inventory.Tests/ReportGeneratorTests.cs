using NUnit.Framework;
using SharpShift.Inventory.Core.Models;
using SharpShift.Inventory.Reporting;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace SharpShift.Inventory.Tests;

public class ReportGeneratorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    [Test]
    public async Task ReportGenerator_MarkdownCombined_WritesFile()
    {
        var root = CreateTempDirectory();
        try
        {
            var inventoryPath = Path.Combine(root, "inventory.json");
            var analysisPath = Path.Combine(root, "analysis.json");
            await File.WriteAllTextAsync(inventoryPath, JsonSerializer.Serialize(CreateSolutions(), JsonOptions));
            await File.WriteAllTextAsync(analysisPath, JsonSerializer.Serialize(CreateAnalysis(), JsonOptions));

            var generator = new ReportGenerator();
            var result = await generator.GenerateAsync(new ReportOptions
            {
                InputDirectory = root,
                OutputDirectory = root,
                InventoryFileName = "inventory.json",
                AnalysisFileName = "analysis.json",
                Format = ReportFormat.Markdown,
                GenerateCombined = true,
                GeneratePerSolution = false,
                BaseFileName = "customer-report"
            });

            var file = Path.Combine(root, "customer-report.md");
            Assert.That(result.GeneratedFiles, Has.Member(file));
            Assert.That(File.Exists(file), Is.True);

            var text = await File.ReadAllTextAsync(file);
            Assert.That(text, Does.Contain("# SharpShift Modernization Report"));
            Assert.That(text, Does.Contain("## Executive Summary"));
            Assert.That(text, Does.Contain("## Executive Risk Assessment"));
            Assert.That(text, Does.Contain("Overall Risk:"));
            Assert.That(text, Does.Contain("Recommended Priority:"));
            Assert.That(text, Does.Contain("## Dependency Summary"));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Test]
    public async Task ReportGenerator_MarkdownPerSolution_WritesExpectedFiles()
    {
        var root = CreateTempDirectory();
        try
        {
            var inventoryPath = Path.Combine(root, "inventory.json");
            var analysisPath = Path.Combine(root, "analysis.json");
            var solutions = CreateSolutions();
            solutions.Add(new SolutionInventory
            {
                SolutionName = "Beta.Solution",
                SolutionPath = @"C:\repos\beta\Beta.Solution.sln",
                Projects =
                [
                    new ProjectInventory
                    {
                        ProjectName = "Beta.Library",
                        ProjectType = "Class Library",
                        Framework = ".NET 10.0",
                        ProjectPath = @"C:\repos\beta\Beta.Library\Beta.Library.csproj"
                    }
                ]
            });

            await File.WriteAllTextAsync(inventoryPath, JsonSerializer.Serialize(solutions, JsonOptions));
            await File.WriteAllTextAsync(analysisPath, JsonSerializer.Serialize(CreateAnalysis(), JsonOptions));

            var generator = new ReportGenerator();
            var result = await generator.GenerateAsync(new ReportOptions
            {
                InputDirectory = root,
                OutputDirectory = root,
                InventoryFileName = "inventory.json",
                AnalysisFileName = "analysis.json",
                Format = ReportFormat.Markdown,
                GenerateCombined = false,
                GeneratePerSolution = true,
                BaseFileName = "customer-report"
            });

            var alphaFile = Path.Combine(root, "customer-report.AlphaSolution.md");
            var betaFile = Path.Combine(root, "customer-report.Beta.Solution.md");
            Assert.That(result.GeneratedFiles, Has.Member(alphaFile));
            Assert.That(result.GeneratedFiles, Has.Member(betaFile));
            Assert.That(File.Exists(alphaFile), Is.True);
            Assert.That(File.Exists(betaFile), Is.True);

            var alphaText = await File.ReadAllTextAsync(alphaFile);
            Assert.That(alphaText, Does.Contain("## Executive Risk Assessment"));
            Assert.That(alphaText, Does.Contain("Overall Risk:"));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Test]
    public void ReportGenerator_PdfFormat_ThrowsNotSupported()
    {
        var root = CreateTempDirectory();
        try
        {
            var inventoryPath = Path.Combine(root, "inventory.json");
            var analysisPath = Path.Combine(root, "analysis.json");
            File.WriteAllText(inventoryPath, JsonSerializer.Serialize(CreateSolutions(), JsonOptions));
            File.WriteAllText(analysisPath, JsonSerializer.Serialize(CreateAnalysis(), JsonOptions));

            var generator = new ReportGenerator();
            Assert.ThrowsAsync<NotSupportedException>(async () => await generator.GenerateAsync(new ReportOptions
            {
                InputDirectory = root,
                OutputDirectory = root,
                InventoryFileName = "inventory.json",
                AnalysisFileName = "analysis.json",
                Format = ReportFormat.Pdf,
                GenerateCombined = true,
                GeneratePerSolution = false
            }));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Test]
    public async Task ReportGenerator_MissingAnalysis_StillGeneratesWithWarnings()
    {
        var root = CreateTempDirectory();
        try
        {
            var inventoryPath = Path.Combine(root, "inventory.json");
            await File.WriteAllTextAsync(inventoryPath, JsonSerializer.Serialize(CreateSolutions(), JsonOptions));

            var generator = new ReportGenerator();
            var result = await generator.GenerateAsync(new ReportOptions
            {
                InputDirectory = root,
                OutputDirectory = root,
                InventoryFileName = "inventory.json",
                Format = ReportFormat.Markdown,
                GenerateCombined = true,
                GeneratePerSolution = false,
                AnalysisFileName = "missing-analysis.json"
            });

            Assert.That(result.Warnings.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(result.GeneratedFiles.Count, Is.EqualTo(1));
            Assert.That(File.Exists(result.GeneratedFiles[0]), Is.True);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Test]
    public async Task ReportGenerator_ExecutiveRiskDashboard_ComputesWeightedRiskFrameworkAndLegacyCounts()
    {
        var root = CreateTempDirectory();
        try
        {
            var sdkProjectPath = Path.Combine(root, "SdkProject.csproj");
            var legacyProjectPath = Path.Combine(root, "LegacyProject.csproj");

            await File.WriteAllTextAsync(sdkProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            await File.WriteAllTextAsync(legacyProjectPath, "<Project><PropertyGroup><TargetFrameworkVersion>v4.8</TargetFrameworkVersion></PropertyGroup></Project>");

            var solutions = new List<SolutionInventory>
            {
                new()
                {
                    SolutionName = "RiskSolution",
                    SolutionPath = Path.Combine(root, "RiskSolution.sln"),
                    Projects =
                    [
                        new ProjectInventory
                        {
                            ProjectName = "SdkProject",
                            Framework = ".NET 7.0",
                            ProjectPath = sdkProjectPath
                        },
                        new ProjectInventory
                        {
                            ProjectName = "LegacyProject",
                            Framework = ".NET 6.0",
                            ProjectPath = legacyProjectPath
                        }
                    ]
                }
            };

            var analysis = new ProjectAnalysisResult
            {
                RiskFindings =
                [
                    new RiskFinding
                    {
                        Category = "Dependency.Vulnerability",
                        Severity = RiskSeverity.High,
                        Description = "High vulnerability",
                        Evidence = "CVE-1"
                    },
                    new RiskFinding
                    {
                        Category = "Dependency.Vulnerability",
                        Severity = RiskSeverity.Low,
                        Description = "Low vulnerability",
                        Evidence = "CVE-2"
                    },
                    new RiskFinding
                    {
                        Category = "Dependency.Deprecated",
                        Severity = RiskSeverity.Medium,
                        Description = "Deprecated package",
                        Evidence = "Pkg"
                    }
                ]
            };

            var inventoryPath = Path.Combine(root, "inventory.json");
            var analysisPath = Path.Combine(root, "analysis.json");
            await File.WriteAllTextAsync(inventoryPath, JsonSerializer.Serialize(solutions, JsonOptions));
            await File.WriteAllTextAsync(analysisPath, JsonSerializer.Serialize(analysis, JsonOptions));

            var generator = new ReportGenerator();
            var result = await generator.GenerateAsync(new ReportOptions
            {
                InputDirectory = root,
                OutputDirectory = root,
                InventoryFileName = "inventory.json",
                AnalysisFileName = "analysis.json",
                Format = ReportFormat.Markdown,
                GenerateCombined = true,
                GeneratePerSolution = false,
                BaseFileName = "risk-dashboard"
            });

            var file = Path.Combine(root, "risk-dashboard.md");
            Assert.That(result.GeneratedFiles, Has.Member(file));

            var text = await File.ReadAllTextAsync(file);
            Assert.That(text, Does.Contain("Overall Risk: Medium"));
            Assert.That(text, Does.Contain("- High Vulnerabilities: 1"));
            Assert.That(text, Does.Contain("- Low Vulnerabilities: 1"));
            Assert.That(text, Does.Contain("- Deprecated Packages: 1"));
            Assert.That(text, Does.Contain("- Out-of-Support Frameworks: 1 projects"));
            Assert.That(text, Does.Contain("- Legacy Project Files: 1 projects"));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Test]
    public async Task ReportGenerator_ExecutiveRiskDashboard_HandlesUnknownFrameworkAndMissingProjectPath()
    {
        var root = CreateTempDirectory();
        try
        {
            var solutions = new List<SolutionInventory>
            {
                new()
                {
                    SolutionName = "UnknownSolution",
                    SolutionPath = Path.Combine(root, "UnknownSolution.sln"),
                    Projects =
                    [
                        new ProjectInventory
                        {
                            ProjectName = "UnknownProject",
                            Framework = "CustomFramework",
                            ProjectPath = Path.Combine(root, "missing.csproj")
                        }
                    ]
                }
            };

            var analysis = new ProjectAnalysisResult
            {
                RiskFindings =
                [
                    new RiskFinding
                    {
                        Category = "Dependency.Vulnerability",
                        Severity = RiskSeverity.Critical,
                        Description = "Critical vulnerability",
                        Evidence = "CVE-10"
                    }
                ]
            };

            var inventoryPath = Path.Combine(root, "inventory.json");
            var analysisPath = Path.Combine(root, "analysis.json");
            await File.WriteAllTextAsync(inventoryPath, JsonSerializer.Serialize(solutions, JsonOptions));
            await File.WriteAllTextAsync(analysisPath, JsonSerializer.Serialize(analysis, JsonOptions));

            var generator = new ReportGenerator();
            await generator.GenerateAsync(new ReportOptions
            {
                InputDirectory = root,
                OutputDirectory = root,
                InventoryFileName = "inventory.json",
                AnalysisFileName = "analysis.json",
                Format = ReportFormat.Markdown,
                GenerateCombined = true,
                GeneratePerSolution = false,
                BaseFileName = "unknown-dashboard"
            });

            var text = await File.ReadAllTextAsync(Path.Combine(root, "unknown-dashboard.md"));
            Assert.That(text, Does.Contain("Overall Risk: Critical"));
            Assert.That(text, Does.Contain("- Out-of-Support Frameworks: 0 projects"));
            Assert.That(text, Does.Contain("- Legacy Project Files: 0 projects"));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static List<SolutionInventory> CreateSolutions()
    {
        return
        [
            new SolutionInventory
            {
                SolutionName = "AlphaSolution",
                SolutionPath = @"C:\repos\alpha\AlphaSolution.sln",
                Summary = new Dictionary<string, object>
                {
                    ["projectCount"] = 1
                },
                Projects =
                [
                    new ProjectInventory
                    {
                        ProjectName = "Alpha.Web",
                        ProjectType = "Exe",
                        Framework = ".NET 10.0",
                        ProjectPath = @"C:\repos\alpha\Alpha.Web\Alpha.Web.csproj",
                        Dependencies =
                        [
                            new DependencyInventory
                            {
                                Name = "Newtonsoft.Json",
                                DependencyType = "Package",
                                Version = "13.0.3",
                                Source = "NuGet"
                            }
                        ]
                    }
                ]
            }
        ];
    }

    private static ProjectAnalysisResult CreateAnalysis()
    {
        return new ProjectAnalysisResult
        {
            Dependencies = new Dictionary<string, DependencySummary>
            {
                ["Newtonsoft.Json"] = new DependencySummary
                {
                    Name = "Newtonsoft.Json",
                    DependencyType = "Package",
                    Source = "NuGet",
                    TotalInstances = 1,
                    Version = new Dictionary<string, int>
                    {
                        ["13.0.3"] = 1
                    }
                }
            },
            RiskFindings =
            [
                new RiskFinding
                {
                    Category = "Reflection",
                    Severity = RiskSeverity.Medium,
                    Description = "Uses runtime activation.",
                    Evidence = "Alpha.Web/Program.cs"
                }
            ]
        };
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "SharpShiftTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
