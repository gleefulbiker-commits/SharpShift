using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Cli
{
    /// <summary>
    /// Minimal runner that discovers solutions/projects, extracts project name and framework, and writes inventory JSON.
    /// </summary>
    public static class InventoryCliRunner
    {
        public static async Task<int> RunAsync(string rootPath, string outputFile, SharpShift.Inventory.Core.Interfaces.ISolutionDiscoverer? solutionDiscoverer = null, SharpShift.Inventory.Core.Interfaces.IProjectDiscoverer? projectDiscoverer = null, bool requireMsBuild = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                {
                    Console.Error.WriteLine($"Path not found: {rootPath}");
                    return 2;
                }

                var slnFiles = new List<string>();
                var csprojFiles = new List<string>();

                if (solutionDiscoverer != null)
                {
                    var sols = await solutionDiscoverer.DiscoverSolutionsAsync(rootPath);
                    slnFiles = sols.ToList();
                }
                else
                {
                    slnFiles = Directory.EnumerateFiles(rootPath, "*.sln", SearchOption.AllDirectories).ToList();
                }

                if (projectDiscoverer != null)
                {
                    var projects = await projectDiscoverer.DiscoverProjectsAsync(rootPath);
                    csprojFiles = projects is null ? new List<string>() : projects.Select(p => p.ProjectPath).ToList();
                }
                else
                {
                    csprojFiles = Directory.EnumerateFiles(rootPath, "*.csproj", SearchOption.AllDirectories).ToList();
                }

                var solutionName = slnFiles.FirstOrDefault() is string s ? Path.GetFileNameWithoutExtension(s) : new DirectoryInfo(rootPath).Name;

                var inventory = new SolutionInventory
                {
                    SolutionName = solutionName
                };

                foreach (var proj in csprojFiles)
                {
                    var projInv = new ProjectInventory
                    {
                        ProjectPath = proj,
                        ProjectName = Path.GetFileNameWithoutExtension(proj)
                    };

                    try
                    {
                        // Try MSBuild evaluation first
                        try
                        {
                        var props = SharpShift.Inventory.Core.Utilities.ProjectEvaluator.EvaluateProjectProperties(proj);
                        if (props != null)
                        {
                            if (props.TryGetValue("TargetFramework", out var tf) && !string.IsNullOrWhiteSpace(tf))
                            {
                                projInv.Framework = SharpShift.Inventory.Core.Utilities.FrameworkNormalizer.Normalize(tf);
                            }
                            else if (props.TryGetValue("TargetFrameworks", out var tfs) && !string.IsNullOrWhiteSpace(tfs))
                            {
                                var first = tfs.Split(';').FirstOrDefault();
                                projInv.Framework = SharpShift.Inventory.Core.Utilities.FrameworkNormalizer.Normalize(first);
                            }
                            else if (props.TryGetValue("TargetFrameworkVersion", out var legacy) && !string.IsNullOrWhiteSpace(legacy))
                            {
                                projInv.Framework = SharpShift.Inventory.Core.Utilities.FrameworkNormalizer.Normalize(legacy);
                            }
                            else
                            {
                                // leave blank
                            }
                        }
                        else
                        {
                            if (requireMsBuild)
                            {
                                Console.Error.WriteLine($"MSBuild evaluation required but not available for project: {proj}");
                                return 4;
                            }
                            // fallback to XML parsing
                            var doc = XDocument.Load(proj);
                            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

                            var tf = doc.Descendants(ns + "TargetFramework").FirstOrDefault()?.Value;
                            if (string.IsNullOrWhiteSpace(tf))
                            {
                                var tfs = doc.Descendants(ns + "TargetFrameworks").FirstOrDefault()?.Value;
                                if (!string.IsNullOrWhiteSpace(tfs))
                                    tf = tfs.Split(';').FirstOrDefault();
                            }

                            if (string.IsNullOrWhiteSpace(tf))
                            {
                                var legacy = doc.Descendants(ns + "TargetFrameworkVersion").FirstOrDefault()?.Value;
                                if (!string.IsNullOrWhiteSpace(legacy)) projInv.Framework = NormalizeLegacyFramework(legacy);
                            }
                            else
                            {
                                projInv.Framework = SharpShift.Inventory.Core.Utilities.FrameworkNormalizer.Normalize(tf);
                            }
                        }
                        }
                        catch
                        {
                            // fallback to XML parsing
                            var doc = XDocument.Load(proj);
                            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

                            var tf = doc.Descendants(ns + "TargetFramework").FirstOrDefault()?.Value;
                            if (string.IsNullOrWhiteSpace(tf))
                            {
                                var tfs = doc.Descendants(ns + "TargetFrameworks").FirstOrDefault()?.Value;
                                if (!string.IsNullOrWhiteSpace(tfs))
                                    tf = tfs.Split(';').FirstOrDefault();
                            }

                            if (string.IsNullOrWhiteSpace(tf))
                            {
                                var legacy = doc.Descendants(ns + "TargetFrameworkVersion").FirstOrDefault()?.Value;
                                if (!string.IsNullOrWhiteSpace(legacy)) projInv.Framework = NormalizeLegacyFramework(legacy);
                            }
                            else
                            {
                                projInv.Framework = NormalizeSdkFramework(tf);
                            }
                        }
                    }
                    catch
                    {
                        // ignore parsing errors for now
                    }

                    inventory.Projects.Add(projInv);
                }

                inventory.Summary["projectCount"] = inventory.Projects.Count;

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(inventory, options);

                File.WriteAllText(outputFile, json);
                Console.WriteLine($"Wrote inventory to {outputFile}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 3;
            }
        }

        private static string NormalizeLegacyFramework(string legacy)
        {
            // legacy like v4.6.2
            if (legacy.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                return $".NET Framework {legacy.Substring(1)}";
            }

            return legacy;
        }

        private static string NormalizeSdkFramework(string tf)
        {
            if (string.IsNullOrWhiteSpace(tf))
                return tf ?? string.Empty;

            tf = tf.Trim();

            var map = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "net48", ".NET Framework 4.8" },
                { "net472", ".NET Framework 4.7.2" },
                { "net471", ".NET Framework 4.7.1" },
                { "net462", ".NET Framework 4.6.2" },
                { "net461", ".NET Framework 4.6.1" },
                { "netcoreapp3.1", ".NET Core 3.1" },
                { "netcoreapp2.1", ".NET Core 2.1" },
                { "netstandard2.0", ".NET Standard 2.0" },
                { "netstandard2.1", ".NET Standard 2.1" }
            };

            if (map.TryGetValue(tf, out var friendly))
                return friendly;

            var m = System.Text.RegularExpressions.Regex.Match(tf, "^net(\\d+)(\\.(\\d+))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var major = m.Groups[1].Value;
                var minor = m.Groups[3].Success ? m.Groups[3].Value : null;
                if (!string.IsNullOrEmpty(minor))
                    return $".NET {major}.{minor}";
                return $".NET {major}.0";
            }

            m = System.Text.RegularExpressions.Regex.Match(tf, "^netcoreapp(\\d+)(\\.(\\d+))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var major = m.Groups[1].Value;
                var minor = m.Groups[3].Success ? m.Groups[3].Value : "0";
                return $".NET Core {major}.{minor}";
            }

            m = System.Text.RegularExpressions.Regex.Match(tf, "^netstandard(\\d+)(\\.(\\d+))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var major = m.Groups[1].Value;
                var minor = m.Groups[3].Success ? m.Groups[3].Value : "0";
                return $".NET Standard {major}.{minor}";
            }

            return tf;
        }
    }
}
