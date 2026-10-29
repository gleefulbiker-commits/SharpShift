using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Xml;
using Microsoft.Extensions.Logging;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Core.Utilities
{
    /// <summary>
    /// Provides helper methods to evaluate project files by parsing .csproj XML directly.
    /// </summary>
    public static class ProjectEvaluator
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ProjectInventory?> _cache = new();

        /// <summary>
        /// Clear the evaluator cache.
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Attempts to evaluate common project properties from a .csproj file using literal XML values.
        /// </summary>
        public static ProjectInventory? EvaluateProjectProperties(string? projectPath)
            => EvaluateProjectProperties(projectPath, null);

        /// <summary>
        /// Attempts to evaluate common project properties from a .csproj file using literal XML values.
        /// </summary>
        public static ProjectInventory? EvaluateProjectProperties(string? projectPath, ILogger? logger)
        {
            ProjectInventory? projectInventory = null;
            if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
            {
                logger?.LogWarning("Project file path is empty or does not exist: {ProjectPath}", projectPath);
                return projectInventory;
            }

            if (_cache.TryGetValue(projectPath, out var cached))
                return cached;

            try
            {
                projectInventory = new Models.ProjectInventory
                {
                    ProjectPath = projectPath,
                    ProjectName = Path.GetFileNameWithoutExtension(projectPath)
                };

                var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var doc = XDocument.Load(projectPath);
                var itemGroups = doc.Descendants()
                    .Where(x => string.Equals(x.Name.LocalName, "ItemGroup", StringComparison.OrdinalIgnoreCase));


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
                        var items = doc.XPathSelectElements($"./Project/ItemGroup/{k}").ToList<XElement>();
                        foreach (var item in items)
                        {
                            var includeAttr = item.Attribute("Include");
                            if (includeAttr is not null && !string.IsNullOrWhiteSpace(includeAttr.Value))
                            {
                                var dependencyInventory = new Models.DependencyInventory
                                {
                                    Name = includeAttr.Value,
                                    Version = item.Attribute("Version")?.Value ?? string.Empty,
                                    DependencyType = 
                                        k.Equals("ProjectReference", StringComparison.OrdinalIgnoreCase) ? "Project" :
                                        k.Equals("PackageReference", StringComparison.OrdinalIgnoreCase) ? "Package" :
                                        k.Equals("FrameworkReference", StringComparison.OrdinalIgnoreCase) ? "Framework" : "Unknown"
                                };
                                projectInventory.Dependencies.Add(dependencyInventory);
                            }
                        }
                        continue;
                    }

                    if(k.Equals("OutputType"))
                    {
                        var projectElement = doc.XPathSelectElement("./Project");
                        var propertyGroupElement = doc.XPathSelectElement("./Project/PropertyGroup");
                        var outputTypeElement = doc.XPathSelectElement("./Project/PropertyGroup/OutputType");
                        var outputType = doc.XPathSelectElement("./Project/PropertyGroup/OutputType");

                        props[k] = outputType?.Value ?? "Class Library";
                        projectInventory.ProjectType = outputType?.Value ?? "Class Library";
                    }

                    var value = doc
                        .Descendants()
                        .FirstOrDefault(x => string.Equals(x.Name.LocalName, k, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.Value))
                        ?.Value;

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        props[k] = value.Trim();
                        if(k.Equals("TargetFramework", StringComparison.OrdinalIgnoreCase) || k.Equals("TargetFrameworks", StringComparison.OrdinalIgnoreCase))
                        {
                            var frameworkValue = value.Trim();
                            if (k.Equals("TargetFrameworks", StringComparison.OrdinalIgnoreCase))
                                frameworkValue = frameworkValue.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? frameworkValue;

                            projectInventory.Framework = FrameworkNormalizer.Normalize(frameworkValue);
                        }

                        if (k.Equals("TargetFrameworkVersion", StringComparison.OrdinalIgnoreCase))
                        {
                            projectInventory.Framework = FrameworkNormalizer.Normalize(value.Trim());
                        }
                    }
                }

                _cache[projectPath] = projectInventory;
                return projectInventory;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to evaluate project properties for {ProjectPath}", projectPath);
                _cache[projectPath] = null;
                return null;
            }
        }
    }
}
