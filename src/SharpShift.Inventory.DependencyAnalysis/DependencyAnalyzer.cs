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
        public static async Task<ProjectAnalysisResult> AnalyzeAsync(ILogger logger, ProjectInventory project, ProjectAnalysisResult result)
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
                        ArgumentNullException.ThrowIfNull(dep.Name, nameof(dep.Name));

                        LogMessages.AnalyzingDependency(logger, dep.Name, dep?.Version ?? string.Empty);
                        if (!result.Dependencies.TryGetValue(dep?.Name ?? string.Empty, out var existingDep))
                        {
                            result.Dependencies.Add(dep?.Name ?? string.Empty, new DependencySummary
                            {
                                Name = dep?.Name ?? string.Empty,
                                DependencyType = dep?.DependencyType ?? string.Empty,
                                HintPath = dep?.HintPath,
                                OtherMetadata = dep?.OtherMetadata,
                                Source = string.IsNullOrWhiteSpace(dep?.Source) ? dep?.DependencyType ?? string.Empty : dep?.Source ?? string.Empty,
                                TotalInstances = 1
                            });

                            var dependencyName = dep?.Name ?? string.Empty;
                            result.Dependencies[dependencyName].Version ??= [];
                            var versionKey = dep?.Version ?? string.Empty;
                            var version = result.Dependencies[dependencyName].Version ?? [];
                            if (version.TryGetValue(versionKey, out var count))
                            {
                                version[versionKey]++;
                            }
                            else
                            {
                                if(!version.TryAdd(versionKey, 1))
                                {
                                    LogMessages.FailedToAddVersion(logger, versionKey, dependencyName);
                                    throw new InvalidOperationException($"Failed to add version '{versionKey}' for dependency '{dependencyName}'.");
                                }
                            }
                        }
                        else
                        {
                            existingDep.TotalInstances++;
                            existingDep.Version ??= [];

                            var versionKey = dep?.Version ?? string.Empty;
                            if (existingDep.Version.TryGetValue(versionKey, out var count))
                            {
                                existingDep.Version[versionKey] = count + 1;
                            }
                            else
                            {
                                existingDep.Version.TryAdd(versionKey, 1);
                            }
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

            return result;
        }
    }
}

