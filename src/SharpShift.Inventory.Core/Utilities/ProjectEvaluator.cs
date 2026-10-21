using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Linq;

namespace SharpShift.Inventory.Core.Utilities
{
    /// <summary>
    /// Provides helper methods to evaluate project files by parsing .csproj XML directly.
    /// </summary>
    public static class ProjectEvaluator
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, IDictionary<string, string>?> _cache = new();

        /// <summary>
        /// Clear the evaluator cache.
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Attempts to evaluate common project properties from a project dictionary.
        /// Returns a dictionary of property name->value when successful; otherwise null.
        /// Caches results per project path to reduce overhead.
        /// </summary>
        public static IDictionary<string, string>? EvaluateProjectProperties(IDictionary<string, string>? projectDict)
        {
            if (projectDict == null || projectDict.Count == 0)
                return null;

            var projectPath = projectDict.FirstOrDefault(kvp => kvp.Key.StartsWith("Project|", StringComparison.OrdinalIgnoreCase)).Key?.Remove(0, "Project|".Length);
            if (string.IsNullOrWhiteSpace(projectPath) && projectDict.TryGetValue("Project", out var projectValue))
                projectPath = projectValue;

            return EvaluateProjectProperties(projectPath);
        }

        /// <summary>
        /// Attempts to evaluate common project properties from a .csproj file using literal XML values.
        /// </summary>
        public static IDictionary<string, string>? EvaluateProjectProperties(string? projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
                return null;

            if (_cache.TryGetValue(projectPath, out var cached))
                return cached;

            try
            {
                var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var doc = XDocument.Load(projectPath);

                var keys = new[] { 
                    "TargetFramework", 
                    "TargetFrameworks", 
                    "TargetFrameworkVersion",
                    "ProjectReference", 
                    "PackageReference",
                    "FrameworkReference",
                    "OutputType" };

                foreach (var k in keys)
                {
                    if (k.Equals("ProjectReference", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("PackageReference", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("FrameworkReference", StringComparison.OrdinalIgnoreCase))
                    {
                        var items = doc.Descendants()
                            .Where(x => string.Equals(x.Name.LocalName, "ItemGroup", StringComparison.OrdinalIgnoreCase))
                            .SelectMany(ig => ig.Elements())
                            .Where(item => string.Equals(item.Name.LocalName, k, StringComparison.OrdinalIgnoreCase))
                            .Select(item => item.Attribute("Include")?.Value)
                            .Where(include => !string.IsNullOrWhiteSpace(include))
                            .Select(include => include!.Trim())
                            .ToList();
                        if (items.Count > 0)
                        {
                            props[k] = string.Join(";", items);
                        }
                        else
                        {
                            props[k] = string.Empty;
                        }
                        continue;
                    }

                    if(k.Equals("OutputType"))
                    {
                        var propertyGroup = doc
                        .Descendants()
                        .FirstOrDefault(x => string.Equals(x.Name.LocalName, "PropertyGroup", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.Value));
                        var outputType = propertyGroup
                            .Descendants()
                            .FirstOrDefault(x => string.Equals(x.Name.LocalName, k));

                        props[k] = "fluff";
                    }

                    var value = doc
                        .Descendants()
                        .FirstOrDefault(x => string.Equals(x.Name.LocalName, k, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.Value))
                        ?.Value;

                    if (!string.IsNullOrWhiteSpace(value))
                        props[k] = value.Trim();
                }

                _cache[projectPath] = props;
                return props;
            }
            catch
            {
                _cache[projectPath] = null;
                return null;
            }
        }
    }
}
