using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SharpShift.Inventory.Core.Utilities
{
    /// <summary>
    /// Provides helper methods to evaluate solution (.sln) files by enumerating contained projects
    /// and delegating per-project evaluation to ProjectEvaluator. Results are cached per solution path.
    /// </summary>
    public static partial class SolutionEvaluator
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Dictionary<string, IDictionary<string, string>?>> _cache = new(StringComparer.OrdinalIgnoreCase);

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
        /// Parse the provided .sln file and return absolute paths to referenced project files.
        /// Returns an empty list when the solution path is invalid or contains no projects.
        /// </summary>
        public static Dictionary<string, IDictionary<string, string>?> ParseSolutionProjectPaths(string solutionDictionary)
        {
            //if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
            //    return solutionDictionary;

            //var raw = File.ReadAllText(projectPath);
            //var projects = new Dictionary<string, IDictionary<string, string>?>(StringComparer.OrdinalIgnoreCase);
            //var projectDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            //projectDict.Add("Project", projectPath);
            //projects.Add("Project", projectDict);

            //XElement root;
            //try
            //{
            //    root = XElement.Parse(raw);
            //    foreach (var child in root.Elements())
            //        ProcessElement(child, child.Name.LocalName, projectDict);
            //}
            //catch
            //{
            //    // invalid XML fragment — return empty map
            //    return projects;
            //}


            //foreach (var child in root.Elements())
            //    ProcessElement(child, child.Name.LocalName, slnxProps);

            //try
            //{
            //    foreach (var line in raw)
            //    {
            //        var sdk = ProjectRegex().Match(line);
            //        if (sdk.Success)

            //        {
            //            projects.Add("Sdk", sdk.Groups["Sdk"].Value);
            //            continue;
            //        }
            //        var propertyGroup
            //    }
            //}
            //catch (Exception ex)
            //{
            //    // ignore invalid path resolutions
            //}

            return new Dictionary<string, IDictionary<string, string>?>();
        }

        /// <summary>
        /// Evaluate all projects in the solution. Returns a mapping of absolute project path -> project property dictionary (or null if evaluation failed for a project).
        /// Returns null if the solution path is invalid.
        /// Results are cached per solution path.
        /// </summary>
        public static Dictionary<string, IDictionary<string, string>?> EvaluateSolutionProjects(Dictionary<string, IDictionary<string, string>?> solutionDictionary)
        {
            //if (_cache.TryGetValue(solutionPath, out var cached))
            //    return cached;

            // Evaluate the solution for its properties.

            // Parse the solution file to get project paths
            //var projects = ParseSolutionProjectPaths(solutionDictionary);
            //var result = new Dictionary<string, IDictionary<string, string>?>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in solutionDictionary.Where(kvp => kvp.Value.Equals("Project")))
            {
                try
                {
                    var projProps = kvp.Value;
                    ProjectEvaluator.EvaluateProjectProperties(projProps);
                }
                catch
                {
                    // ensure failures are represented as null so callers can see which projects failed
                }
            }

            //_cache[solutionPath] = result;
            return solutionDictionary;
        }

        public static Dictionary<string, IDictionary<string, string>?> EvaluateSolutionProperties(string solutionPath, bool forceRefresh = false)
        {
            var solutionProperties = new Dictionary<string, IDictionary<string, string>?>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(solutionPath) || !File.Exists(solutionPath))
                return solutionProperties;

            var solutionDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "SolutionPath", solutionPath } };
            solutionProperties.Add("Solution", solutionDict);

            if (_cache.TryGetValue(solutionPath, out var cached))
                return cached;

            if (forceRefresh)
                _cache.TryRemove(solutionPath, out _);

            try
            {
                if(Path.GetExtension(solutionPath).Equals(".slnx", StringComparison.CurrentCultureIgnoreCase))
                {
                    _cache[solutionPath] = SolutionEvaluator.EvaluateSlnxProperties(solutionProperties);
                    return solutionProperties;
                }

                return EvaluateSolutionProjects(solutionProperties);
            }
            catch (Exception ex)
            {
                // Add logging
            }
            return new Dictionary<string, IDictionary<string, string>?>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Evaluate properties of a .slnx solution file using ProjectEvaluator. Returns a mapping
        /// of absolute solution path -> solution property dictionary (or null if evaluation failed for a project).
        /// </summary>
        /// <param name="solutionProperties">A dictionary of solution properties.</param>
        /// <returns>A dictionary mapping solution paths to their properties, or null if evaluation failed.</returns>
        private static Dictionary<string, IDictionary<string, string>?> EvaluateSlnxProperties(Dictionary<string, IDictionary<string, string>?> solutionProperties)
        {
            try
            {
                //var slnxProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                //if (string.IsNullOrWhiteSpace(solutionPath) || !File.Exists(solutionPath))
                //    return slnxProps;
                
                var raw = File.ReadAllText(solutionProperties["Solution"]["SolutionPath"]);
                if (string.IsNullOrWhiteSpace(raw))
                    return solutionProperties;

                XElement root;
                try
                {
                    root = XElement.Parse(raw);
                    var solutionDict = solutionProperties["Solution"];
                    ProcessElement(
                        solutionProperties["Solution"]["SolutionPath"],
                        root,
                        root.Name.LocalName,
                        solutionDict as Dictionary<string, string> ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                }
                catch (Exception ex)
                {
                    // invalid XML fragment — return empty map
                    throw new InvalidOperationException($"The solution file '{solutionProperties["Solution"]["SolutionPath"]}' is not a valid XML document.", ex);
                }

                return solutionProperties;
            }
            catch
            {
                return solutionProperties;
            }
        }

        static void ProcessElement(string solutionPath, XElement el, string path, Dictionary<string, string> propsDict)
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
                    propsDict[key] = val;
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
                                propsDict[$"Project|{projectPath}"] = JsonSerializer.Serialize(propsDict, _jsonOptions);
                                propsDict[$"Project|{projectPath}"] = JsonSerializer.Serialize(ProjectEvaluator.EvaluateProjectProperties(propsDict), _jsonOptions);
                            }
                            break;
                        default:
                            break;
                    }
                    var childPath = string.IsNullOrEmpty(path) ? child.Name.LocalName : $"{path}.{child.Name.LocalName}";
                    ProcessElement(solutionPath, child, childPath, propsDict);
                }
            }
        }
    }
}
