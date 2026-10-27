using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;
using SharpShift.Inventory.DependencyAnalysis;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SharpShift.Inventory.ProjectAnalysis;

/// <summary>
/// Analyzes projects and extracts metadata relevant to inventory and dependencies.
/// </summary>
public class ProjectAnalyzer : IProjectAnalyzer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectAnalyzer"/> class.
    /// </summary>
    public ProjectAnalyzer()
    {
        // TODO: Inject project parsers and analyzers.
    }

    public async Task<ProjectAnalysisResult?> AnalyzeAsync(ILogger logger, IList<SolutionInventory> solutions)
    {
        var result = new ProjectAnalysisResult
        {
            Dependencies = new Dictionary<string, DependencySummary>(),
            RiskFindings = new List<RiskFinding>()
        };

        if (solutions is null || solutions.Count == 0)
        {
            logger.LogWarning("No solutions provided for analysis.");
            return result;
        }

        try
        {
            var dependecyAnalyzer = new DependencyAnalyzer();

            foreach (var solution in solutions)
            {
                foreach (var project in solution.Projects)
                {
                    result = await dependecyAnalyzer.AnalyzeAsync(logger, project, result);
                }
            }

            return result;
        }
        catch (Exception ex)
        {

            throw;
        }
    }
}
