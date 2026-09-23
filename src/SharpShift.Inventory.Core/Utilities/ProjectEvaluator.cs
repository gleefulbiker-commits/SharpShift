using System;
using System.Collections.Generic;
using System.IO;

namespace SharpShift.Inventory.Core.Utilities
{
    /// <summary>
    /// Provides helper methods to evaluate project files using MSBuild when available via reflection.
    /// Falls back to null when MSBuild is not available.
    /// </summary>
    public static class ProjectEvaluator
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, IReadOnlyDictionary<string, string>?> _cache = new();
        private static bool _msbuildRegistered = false;

        /// <summary>
        /// Attempts to evaluate common project properties using MSBuild APIs via reflection.
        /// Returns a dictionary of property name->value when successful; otherwise null.
        /// Caches results per project path to reduce overhead.
        /// </summary>
        public static IReadOnlyDictionary<string, string>? EvaluateProjectProperties(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
                return null;

            if (_cache.TryGetValue(projectPath, out var cached))
                return cached;

            try
            {
                var locatorType = Type.GetType("Microsoft.Build.Locator.MSBuildLocator, Microsoft.Build.Locator");
                if (locatorType != null && !_msbuildRegistered)
                {
                    try
                    {
                        var register = locatorType.GetMethod("RegisterDefaults", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        register?.Invoke(null, null);
                        _msbuildRegistered = true;
                    }
                    catch { }
                }

                var projectType = Type.GetType("Microsoft.Build.Evaluation.Project, Microsoft.Build");
                if (projectType == null)
                    return null;

                var projObj = Activator.CreateInstance(projectType, new object[] { projectPath });
                var getProp = projectType.GetMethod("GetPropertyValue", new[] { typeof(string) });
                var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                string[] keys = new[] { "TargetFramework", "TargetFrameworks", "TargetFrameworkVersion" };
                foreach (var k in keys)
                {
                    var v = getProp?.Invoke(projObj, new object[] { k }) as string;
                    if (!string.IsNullOrWhiteSpace(v))
                        props[k] = v;
                }

                // Also extract items for PackageReference and Reference
                try
                {
                    var projectInstanceType = projectType;
                    var itemsProp = projectInstanceType.GetProperty("Items");
                    var items = itemsProp?.GetValue(projObj) as System.Collections.IEnumerable;
                    if (items != null)
                    {
                        int idx = 0;
                        foreach (var item in items)
                        {
                            var itemType = item.GetType();
                            var itemTypeName = itemType.GetProperty("ItemType")?.GetValue(item) as string;
                            var include = itemType.GetProperty("EvaluatedInclude")?.GetValue(item) as string;
                            if (!string.IsNullOrWhiteSpace(itemTypeName) && !string.IsNullOrWhiteSpace(include))
                            {
                                // store as Item.{index}.Type and Item.{index}.Include
                                props[$"Item.{idx}.Type"] = itemTypeName;
                                props[$"Item.{idx}.Include"] = include;
                                // attempt to extract metadata values for common metadata names (Version, HintPath)
                                try
                                {
                                    var metadataProp = itemType.GetProperty("Metadata");
                                    var metadata = metadataProp?.GetValue(item) as System.Collections.IEnumerable;
                                    if (metadata != null)
                                    {
                                        foreach (var md in metadata)
                                        {
                                            var mdType = md.GetType();
                                            var name = mdType.GetProperty("Name")?.GetValue(md) as string;
                                            var value = mdType.GetProperty("EvaluatedValue")?.GetValue(md) as string;
                                            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
                                            {
                                                props[$"Item.{idx}.Metadata.{name}"] = value;
                                            }
                                        }
                                    }
                                }
                                catch { }
                                idx++;
                            }
                        }
                    }
                }
                catch { }

                try
                {
                    var pcProp = projectType.GetProperty("ProjectCollection");
                    var pc = pcProp?.GetValue(projObj);
                    var unload = pc?.GetType().GetMethod("UnloadAllProjects");
                    unload?.Invoke(pc, null);
                }
                catch { }

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
