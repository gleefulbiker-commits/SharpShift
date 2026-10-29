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
public static class ProjectAnalyzer
{
    public static async Task<ProjectAnalysisResult?> AnalyzeAsync(ILogger logger, IList<SolutionInventory> solutions)
    {
        var result = new ProjectAnalysisResult
        {
            Dependencies = [],
            RiskFindings = []
        };

        if (solutions is null || solutions.Count == 0)
        {
            logger.LogWarning("No solutions provided for analysis.");
            return result;
        }

        try
        {
            foreach (var solution in solutions)
            {
                foreach (var project in solution.Projects)
                {
                    result = await DependencyAnalyzer.AnalyzeAsync(logger, project, result);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Project analysis failed while processing discovered solutions.");
            throw;
        }
    }
}
