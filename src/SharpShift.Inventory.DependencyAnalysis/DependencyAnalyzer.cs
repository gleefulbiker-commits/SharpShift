using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;
using SharpShift.LoggerMessages;

namespace SharpShift.Inventory.DependencyAnalysis
{
    /// <summary>
    /// Analyzer that extracts PackageReference and Reference items using MSBuild evaluation when available.
    /// </summary>
    public static class DependencyAnalyzer
    {
        public static Task<ProjectAnalysisResult> AnalyzeAsync(ILogger logger, ProjectInventory project, ProjectAnalysisResult result)
        {
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));
            ArgumentNullException.ThrowIfNull(project, nameof(project));
            ArgumentNullException.ThrowIfNull(result, nameof(result));

            if (string.IsNullOrWhiteSpace(project.ProjectPath) ||
                !File.Exists(project.ProjectPath))
                throw new ArgumentException("Invalid project path.", nameof(project));

            try
            {
                if (project.Dependencies.Count > 0)
                {
                    foreach (var dep in project.Dependencies)
                    {
                        ArgumentNullException.ThrowIfNull(dep, nameof(dep));
                        if (!result.Dependencies.TryGetValue(dep.Name, out var existingDep))
                        {
                            result.Dependencies.Add(dep.Name, new DependencySummary
                            {
                                Name = dep.Name,
                                DependencyType = dep.DependencyType,
                                HintPath = dep.HintPath,
                                OtherMetadata = dep.OtherMetadata,
                                Source = string.IsNullOrWhiteSpace(dep.Source) ? dep.DependencyType : dep.Source,
                                TotalInstances = 1
                            });
                            result.Dependencies[dep.Name].Version ??= [];
                            if(result.Dependencies[dep.Name].Version.TryGetValue(dep.Version, out var count))
                            {
                                result.Dependencies[dep.Name].Version[dep.Version] = count + 1;
                            }
                            else
                            {
                                result.Dependencies[dep.Name].Version.TryAdd(dep.Version, 1);
                            }
                        }
                        else
                        {
                            existingDep.TotalInstances++;
                        }
                    }
                }
                else
                {
                    LogMessages.NoDependenciesFound(logger, project.ProjectPath);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to aggregate dependencies for project {ProjectPath}", project.ProjectPath);
            }

            return Task.FromResult(result);
        }
    }
}

