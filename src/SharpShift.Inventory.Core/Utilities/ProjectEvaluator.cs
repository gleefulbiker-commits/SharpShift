using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace SharpShift.Inventory.Core.Utilities
{
    /// <summary>
    /// Provides helper methods to evaluate project files using MSBuild when available via reflection.
    /// Falls back to null when MSBuild is not available.
    /// </summary>
    public static class ProjectEvaluator
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, IDictionary<string, string>?> _cache = new();
        private static bool _msbuildRegistered = false;

        /// <summary>
        /// Clear the evaluator cache and reset MSBuild registration state.
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
            _msbuildRegistered = false;
        }

        /// <summary>
        /// Attempts to evaluate common project properties using MSBuild APIs via reflection.
        /// Returns a dictionary of property name->value when successful; otherwise null.
        /// Caches results per project path to reduce overhead.
        /// </summary>
        public static IDictionary<string, string>? EvaluateProjectProperties(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
                return null;

            if (_cache.TryGetValue(projectPath, out var cached))
                return cached;

            try
            {
                var locatorType = Type.GetType("Microsoft.Build.Locator.MSBuildLocator, Microsoft.Build.Locator");
                if (locatorType is not null && !_msbuildRegistered)
                {
                    try
                    {
                        var register = locatorType.GetMethod("RegisterDefaults", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        register?.Invoke(null, null);
                        _msbuildRegistered = true;
                    }
                    catch
                    {
                        // Suggestion: Log or handle the exception if needed, but continue to attempt project evaluation without MSBuild registration.
                    }
                }

                var projectType = Type.GetType("Microsoft.Build.Evaluation.Project, Microsoft.Build");
                if (projectType is null)
                {
                    // Microsoft.Build types not available in-process; fall back to dotnet CLI preprocessing
                    var cliResult = EvaluateProjectWithDotnetCli(projectPath);
                    _cache[projectPath] = cliResult;
                    return cliResult;
                }

                var projObj = Activator.CreateInstance(projectType, [projectPath]);
                var getProp = projectType.GetMethod("GetPropertyValue", [typeof(string)]);
                var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var keys = new[] { "TargetFramework", "TargetFrameworks", "TargetFrameworkVersion", "PackageReference" };
                foreach (var k in keys)
                {
                    if (getProp?.Invoke(projObj, [k]) is string v && !string.IsNullOrWhiteSpace(v))
                        props[k] = v;
                }

                // Also extract items for PackageReference and Reference
                try
                {
                    var projectInstanceType = projectType;
                    var itemsProp = projectInstanceType.GetProperty("Items");
                    if (itemsProp?.GetValue(projObj) is System.Collections.IEnumerable items)
                    {
                        int idx = 0;
                        foreach (var item in items)
                        {
                            if(item == null)
                                continue;

                            var itemType = item.GetType();
                            var itemTypeProp = itemType.GetProperty("ItemType");
                            var evaluatedIncludeProp = itemType.GetProperty("EvaluatedInclude");

                            var keyValue = itemType?.GetProperty("Key")?.GetValue(item)?.ToString();

                            if (keyValue?.Equals("PackageReference") ?? false)
                            {
                                var itemValue = itemType?.GetProperty("Value")?.GetValue(item);
                                foreach (var val in (itemValue as System.Collections.IEnumerable) ?? Enumerable.Empty<object>())
                                {
                                    var includeName = val.GetType().GetProperty("EvaluatedInclude")?.GetValue(val);
                                    props[$"Item.{idx}.Type"] = "PackageReference";
                                    //props[$"Item.{idx}.Include"] = includeName;
                                }
                                idx++;
                                continue;
                            }
                            else if (itemTypeProp?.GetValue(item) is string itemTypeName &&
                                evaluatedIncludeProp?.GetValue(item) is string include &&
                                !string.IsNullOrWhiteSpace(itemTypeName) &&
                                !string.IsNullOrWhiteSpace(include))
                            {
                                // store as Item.{index}.Type and Item.{index}.Include
                                props[$"Item.{idx}.Type"] = itemTypeName;
                                props[$"Item.{idx}.Include"] = include;
                                // attempt to extract metadata values for common metadata names (Version, HintPath)
                                try
                                {
                                    var metadataProp = itemType?.GetProperty("Metadata");
                                    if (metadataProp?.GetValue(item) is System.Collections.IEnumerable metadata)
                                    {
                                        foreach (var md in metadata)
                                        {
                                            var mdType = md.GetType();
                                            if (mdType.GetProperty("Name")?.GetValue(md) is string name &&
                                                mdType.GetProperty("EvaluatedValue")?.GetValue(md) is string value &&
                                                !string.IsNullOrWhiteSpace(name) &&
                                                !string.IsNullOrWhiteSpace(value))
                                            {
                                                props[$"Item.{idx}.Metadata.{name}"] = value;
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    // Suggestion: Log or handle the exception if needed, but continue to attempt project evaluation without MSBuild registration.
                                }
                            }
                            idx++;
                        }
                    }
                }
                catch
                {
                    // Suggestion: Log or handle the exception if needed, but continue to attempt project evaluation without MSBuild registration.
                }

                try
                {
                    var pcProp = projectType.GetProperty("ProjectCollection");
                    var pc = pcProp?.GetValue(projObj);
                    var unload = pc?.GetType().GetMethod("UnloadAllProjects");
                    unload?.Invoke(pc, null);
                }
                catch
                {
                    // Suggestion: Log or handle the exception if needed, but continue to attempt project evaluation without MSBuild registration.
                }

                _cache[projectPath] = props;
                return props;
            }
            catch
            {
                // Reflection-path failed unexpectedly; attempt CLI fallback before giving up
                try
                {
                    var cliResult = EvaluateProjectWithDotnetCli(projectPath);
                    _cache[projectPath] = cliResult;
                    return cliResult;
                }
                catch
                {
                    _cache[projectPath] = null;
                    return null;
                }
            }
        }

        // Attempt to evaluate project properties by invoking the dotnet CLI to produce a preprocessed project
        private static Dictionary<string, string>? EvaluateProjectWithDotnetCli(string projectPath)
        {
            try
            {
                // Verify dotnet exists
                var verInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(verInfo))
                {
                    if (proc == null)
                        return null;
                    proc.WaitForExit(5000);
                    if (proc.ExitCode != 0)
                        return null;
                }

                var preprocPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pp.xml");
                var args = $"msbuild \"{projectPath}\" /pp:\"{preprocPath}\" /verbosity:quiet /nologo";
                var start = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var p = Process.Start(start))
                {
                    if (p == null)
                        return null;
                    // wait up to 30s
                    p.WaitForExit(30000);
                    if (!File.Exists(preprocPath) || p.ExitCode != 0)
                    {
                        try { if (File.Exists(preprocPath)) File.Delete(preprocPath); } catch { }
                        return null;
                    }
                }

                var doc = XDocument.Load(preprocPath);
                var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // Extract properties of interest
                var keys = new[] { "TargetFramework", "TargetFrameworks", "TargetFrameworkVersion" };
                foreach (var k in keys)
                {
                    var el = doc.Descendants().FirstOrDefault(x => string.Equals(x.Name.LocalName, k, StringComparison.OrdinalIgnoreCase));
                    if (el != null && !string.IsNullOrWhiteSpace(el.Value))
                        props[k] = el.Value.Trim();
                }

                // Extract item groups
                int idx = 0;
                var itemGroupNodes = doc.Descendants().Where(x => string.Equals(x.Name.LocalName, "ItemGroup", StringComparison.OrdinalIgnoreCase));
                foreach (var ig in itemGroupNodes)
                {
                    foreach (var item in ig.Elements())
                    {
                        var itemTypeName = item.Name.LocalName;
                        var includeAttr = item.Attribute("Include")?.Value ?? item.Attribute("Update")?.Value;
                        if (string.IsNullOrWhiteSpace(itemTypeName) || string.IsNullOrWhiteSpace(includeAttr))
                            continue;

                        props[$"Item.{idx}.Type"] = itemTypeName;
                        props[$"Item.{idx}.Include"] = includeAttr;

                        // metadata
                        foreach (var md in item.Elements())
                        {
                            var name = md.Name.LocalName;
                            var value = md.Value;
                            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
                                props[$"Item.{idx}.Metadata.{name}"] = value.Trim();
                        }

                        idx++;
                    }
                }

                try { if (File.Exists(preprocPath)) File.Delete(preprocPath); } catch { }
                return props;
            }
            catch
            {
                return null;
            }
        }
    }
}
