using SharpShift.Inventory.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        [GeneratedRegex("Project\\(\"\\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\\}\"\\) = \"[A-Za-z0-9]+\", \"[^\"]*\", \"\\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\\}\"", RegexOptions.Compiled)]
        public static partial Regex LegacyProjectRegex();

        [GeneratedRegex("<\\s*Project\\b(?:\\s+Sdk\\s*=\\s*(?:\"(?<sdk>[^\"]*)\"|'(?<sdk>[^']*)'))?[^>]*>", RegexOptions.Compiled)]
        public static partial Regex ProjectRegex();

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
        public static SolutionInventory? EvaluateSolutionProjects(SolutionInventory solutionInventory)
        {
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
                    return solutionInventory;
                }
            }

            foreach (Match match in projectMatches)
            {
                // Now scan for the project local path in the match groups. If found, add it to the solution inventory.
                var projectLocalPath = match.Groups["projectlocalpath"].Value;
                if (!string.IsNullOrEmpty(projectLocalPath))
                {
                    var absoluteProjectPath = Path.Combine(Path.GetDirectoryName(solutionInventory.SolutionPath), projectLocalPath);
                    solutionInventory.Projects.Add(new ProjectInventory
                    {
                        ProjectName = Path.GetFileNameWithoutExtension(absoluteProjectPath),
                        ProjectPath = absoluteProjectPath
                    });
                }
            }

            foreach (var project in solutionInventory.Projects)
            {
                try
                {
                    var projectInventory = ProjectEvaluator.EvaluateProjectProperties(project);
                }
                catch
                {
                    // ensure failures are represented as null so callers can see which projects failed
                }
            }

            _cache[solutionInventory.SolutionPath] = solutionInventory;
            return solutionInventory;
        }

        public static SolutionInventory? EvaluateSolutionProperties(string solutionPath, bool forceRefresh = false)
        {
            var solutionInventory = new SolutionInventory
            {
                SolutionName = Path.GetFileNameWithoutExtension(solutionPath),
                SolutionPath = solutionPath,
                Projects = [],
                Summary = []
            };  

            if (string.IsNullOrWhiteSpace(solutionPath) || !File.Exists(solutionPath))
                return solutionInventory;

            if (_cache.TryGetValue(solutionPath, out var cached))
                return cached;

            if (forceRefresh)
                _cache.TryRemove(solutionPath, out _);

            try
            {
                if(Path.GetExtension(solutionPath).Equals(".slnx", StringComparison.CurrentCultureIgnoreCase))
                {
                    _cache[solutionPath] = SolutionEvaluator.EvaluateSlnxProperties(solutionInventory);
                    return solutionInventory;
                }

                return EvaluateSolutionProjects(solutionInventory);
            }
            catch (Exception ex)
            {
                // Add logging
            }
            return solutionInventory;
        }

        /// <summary>
        /// Evaluate properties of a .slnx solution file using ProjectEvaluator. Returns a mapping
        /// of absolute solution path -> solution property dictionary (or null if evaluation failed for a project).
        /// </summary>
        /// <param name="solutionInventory">A SolutionInventory object.</param>
        /// <returns>A SolutionInventory object with evaluated properties.</returns>
        private static SolutionInventory EvaluateSlnxProperties(SolutionInventory solutionInventory)
        {
            try
            {
                var doc = XDocument.Load(solutionInventory.SolutionPath);
                if (doc.Root is null)
                    return solutionInventory;

                try
                {
                    var projects = doc.XPathSelectElements("/Solution/Project").ToList();
                    projects.ForEach(el =>
                    {
                        var projectPath = Path.Combine(Path.GetDirectoryName(solutionInventory.SolutionPath), el.Attribute("Path")?.Value ?? string.Empty);
                        if (!string.IsNullOrEmpty(projectPath))
                        {
                            solutionInventory.Projects.Add(ProjectEvaluator.EvaluateProjectProperties(projectPath));
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
            catch
            {
                return solutionInventory;
            }
        }

        static void ProcessElement(string solutionPath, XElement el, string path, SolutionInventory solutionInventory)
        {
            var elementName = el.Name.LocalName;
            var firstAttribute = el.FirstAttribute?.Value ?? string.Empty;

            var children = el.Elements().ToList();
            if (!children.Any())
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
                            var projectPath = Path.Combine(Path.GetDirectoryName(solutionPath), child.Attribute("Path")?.Value ?? string.Empty);
                            if (!string.IsNullOrEmpty(projectPath))
                            {
                                solutionInventory.Projects.Add(ProjectEvaluator.EvaluateProjectProperties(projectPath));
                            }
                            break;
                        default:
                            break;
                    }
                    var childPath = string.IsNullOrEmpty(path) ? child.Name.LocalName : $"{path}.{child.Name.LocalName}";
                    ProcessElement(solutionPath, child, childPath, solutionInventory);
                }
            }
        }
    }
}
