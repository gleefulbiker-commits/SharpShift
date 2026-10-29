using SharpShift.Inventory.Core.Models;
using SharpShift.LoggerMessages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace SharpShift.Inventory.Core.Utilities
{
    /// <summary>
    /// Provides helper methods to evaluate solution (.sln) files by enumerating contained projects
    /// and delegating per-project evaluation to ProjectEvaluator. Results are cached per solution path.
    /// </summary>
    public static partial class SolutionEvaluator
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SolutionInventory?> _cache = new(StringComparer.OrdinalIgnoreCase);
        
        [GeneratedRegex("Format Version\\s+(?<version>\\d+(?:\\.\\d+)+)", RegexOptions.Compiled)]
        public static partial Regex SlnVersionRegex();

        [GeneratedRegex("VisualStudioVersion\\s*=\\s*(?<version>\\d+(?:\\.\\d+)+)", RegexOptions.Compiled)]
        public static partial Regex VisualStudioVersionRegex();

        [GeneratedRegex("Project\\(\"(?<projecttypeid>\\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\\})\"\\) = \"(?<projectname>[A-Za-z0-9]+)\", \"(?<projectlocalpath>[^\"]*)\", \"(?<projectid>\\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\\})\"", RegexOptions.Compiled)]
        public static partial Regex LegacyProjectRegex();

        [GeneratedRegex("<\\s*Project\\b(?:\\s+Sdk\\s*=\\s*(?:\"(?<sdk>[^\"]*)\"|'(?<sdk>[^']*)'))?[^>]*>", RegexOptions.Compiled)]
        public static partial Regex ProjectRegex();

        internal static partial class Log
        {
            [LoggerMessage(EventId = 1001, Level = LogLevel.Warning,
                Message = "Project evaluation returned null for {ProjectPath}")]
            public static partial void ProjectEvaluationNull(ILogger logger, string projectPath);
        }
        /// <summary>
        /// Clear the solution evaluator cache and reset ProjectEvaluator cache as well.
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
            ProjectEvaluator.ClearCache();
        }

        /// <summary>
        /// Evaluate all projects in the solution. Returns a mapping of absolute project path -> project property dictionary (or null if evaluation failed for a project).
        /// Returns null if the solution path is invalid.
        /// Results are cached per solution path.
        /// </summary>
        public static SolutionInventory? EvaluateSolutionProjects(SolutionInventory solutionInventory, ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(solutionInventory, nameof(solutionInventory));

            if (_cache.TryGetValue(solutionInventory.SolutionPath, out var cached))
                return cached;

            // Evaluate the solution for its properties.
            var slnFileText = File.ReadAllText(solutionInventory.SolutionPath);
            var slnFormatVersion = SlnVersionRegex().Match(slnFileText).Groups["version"].Value;
            solutionInventory.Summary["FormatVersion"] = slnFormatVersion;
            var visualStudioVersion = VisualStudioVersionRegex().Match(slnFileText).Groups["version"].Value;
            solutionInventory.Summary["VisualStudioVersion"] = visualStudioVersion;
            var projectMatches = LegacyProjectRegex().Matches(slnFileText);

            if (projectMatches.Count == 0)
            {
                projectMatches = ProjectRegex().Matches(slnFileText);
                if (projectMatches.Count == 0)
                {
                    // No projects found in the solution file
                    Log.ProjectEvaluationNull(logger!, solutionInventory.SolutionPath);
                    return solutionInventory;
                }
            }

            foreach (Match match in projectMatches)
            {
                // Now scan for the project local path in the match groups. If found, add it to the solution inventory.
                var projectTypeId = match.Groups["projecttypeid"].Value;
                var projectName = match.Groups["projectname"].Value;
                var projectLocalPath = match.Groups["projectlocalpath"].Value;
                var projectId = match.Groups["projectid"].Value;
                if (!string.IsNullOrEmpty(projectLocalPath))
                {
                    var absoluteProjectPath = Path.Combine(Path.GetDirectoryName(solutionInventory.SolutionPath) ?? string.Empty, projectLocalPath);
                    solutionInventory.Projects.Add(new ProjectInventory
                    {
                        ProjectName = Path.GetFileNameWithoutExtension(absoluteProjectPath),
                        ProjectPath = absoluteProjectPath,
                        ProjectTypeId = projectTypeId,
                        ProjectType = DotNetProjectTypeGuids.Map.ContainsKey(projectTypeId) ? DotNetProjectTypeGuids.Map[projectTypeId] : "Unknown",
                        ProjectId = projectId
                    });
                }
            }

            solutionInventory.Projects.ForEach(project =>
            {
                var evaluatedProject = ProjectEvaluator.EvaluateProjectProperties(project.ProjectPath, logger);
                if (evaluatedProject is null)
                {
                    logger?.LogWarning("Project evaluation returned null for {ProjectPath}", project.ProjectPath);
                    return;
                }

                project.Framework = evaluatedProject.Framework;
                project.Metadata = evaluatedProject.Metadata;
                project.Dependencies = evaluatedProject.Dependencies;
                project.LocalPath = evaluatedProject.LocalPath;
            });

            _cache[solutionInventory.SolutionPath] = solutionInventory;
            return solutionInventory;
        }

        public static SolutionInventory? EvaluateSolutionProperties(string solutionPath, bool forceRefresh = false, ILogger? logger = null)
        {
            var solutionInventory = new SolutionInventory
            {
                SolutionName = Path.GetFileNameWithoutExtension(solutionPath),
                SolutionPath = solutionPath,
                Projects = [],
                Summary = []
            };  

            if (string.IsNullOrWhiteSpace(solutionPath) || !File.Exists(solutionPath))
            {
                logger?.LogWarning("Solution file path is empty or missing: {SolutionPath}", solutionPath);
                return solutionInventory;
            }

            if (_cache.TryGetValue(solutionPath, out var cached))
                return cached;

            if (forceRefresh)
                _cache.TryRemove(solutionPath, out _);

            try
            {
                if(Path.GetExtension(solutionPath).Equals(".slnx", StringComparison.CurrentCultureIgnoreCase))
                {
                    _cache[solutionPath] = SolutionEvaluator.EvaluateSlnxProperties(logger, solutionInventory);
                    return solutionInventory;
                }

                return EvaluateSolutionProjects(solutionInventory, logger);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to evaluate solution properties for {SolutionPath}", solutionPath);
            }
            return solutionInventory;
        }

        /// <summary>
        /// Evaluate properties of a .slnx solution file using ProjectEvaluator. Returns a mapping
        /// of absolute solution path -> solution property dictionary (or null if evaluation failed for a project).
        /// </summary>
        /// <param name="solutionInventory">A SolutionInventory object.</param>
        /// <returns>A SolutionInventory object with evaluated properties.</returns>
        private static SolutionInventory EvaluateSlnxProperties(ILogger? logger, SolutionInventory solutionInventory)
        {
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));
            ArgumentNullException.ThrowIfNull(solutionInventory, nameof(solutionInventory));

            try
            {
                if(string.IsNullOrWhiteSpace(solutionInventory.SolutionPath) ||
                   !File.Exists(solutionInventory.SolutionPath))
                {
                    logger?.LogWarning("Solution file path is empty or missing: {SolutionPath}", solutionInventory.SolutionPath);
                    return solutionInventory;
                }
                else
                {
                    var doc = XDocument.Load(solutionInventory.SolutionPath);

                    LogMessages.EvaluatingSolutionProperties(logger, solutionInventory.SolutionPath);
                    if (doc.Root is null)
                        return solutionInventory;

                    try
                    {
                        var projects = doc.XPathSelectElements("/Solution/Project").ToList();
                        projects.ForEach(el =>
                        {
                            var projectPath = Path.Combine(Path.GetDirectoryName(solutionInventory.SolutionPath) ?? string.Empty, el.Attribute("Path")?.Value ?? string.Empty);
                            if (!string.IsNullOrEmpty(projectPath))
                            {
                                var project = ProjectEvaluator.EvaluateProjectProperties(projectPath, logger);
                                if (project is not null)
                                    solutionInventory.Projects.Add(project);
                                else
                                    logger?.LogWarning("Project evaluation returned null for {ProjectPath}", projectPath);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        // invalid XML fragment — return empty map
                        throw new InvalidOperationException($"The solution file '{solutionInventory.SolutionPath}' is not a valid XML document.", ex);
                    }

                    return solutionInventory;
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to evaluate .slnx solution properties for {SolutionPath}", solutionInventory.SolutionPath);
                return solutionInventory;
            }
        }

        static void ProcessElement(string solutionPath, XElement el, string path, SolutionInventory solutionInventory, ILogger? logger = null)
        {
            var children = el.Elements().ToList();

            if (children.Count == 0)
            {
                var val = (el.Value ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(val))
                {
                    var key = string.IsNullOrEmpty(path) ? el.Name.LocalName : path;
                    solutionInventory.Summary[key] = val;
                }
            }
            else
            {

                foreach (var child in children)
                {
                    switch (child.Name.LocalName)
                    {
                        case "Project":
                            var projectPath = Path.Combine(Path.GetDirectoryName(solutionPath) ?? string.Empty, child.Attribute("Path")?.Value ?? string.Empty);
                            if (!string.IsNullOrEmpty(projectPath))
                            {
                                var project = ProjectEvaluator.EvaluateProjectProperties(projectPath, logger);
                                if (project is not null)
                                    solutionInventory.Projects.Add(project);
                                else
                                    logger?.LogWarning("Project evaluation returned null for {ProjectPath}", projectPath);
                            }
                            break;
                        default:
                            break;
                    }
                    var childPath = string.IsNullOrEmpty(path) ? child.Name.LocalName : $"{path}.{child.Name.LocalName}";
                    ProcessElement(solutionPath ?? string.Empty, child, childPath, solutionInventory, logger);
                }
            }
        }
    }
}
