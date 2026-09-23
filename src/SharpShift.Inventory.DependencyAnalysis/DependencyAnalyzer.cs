using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.DependencyAnalysis
{
    /// <summary>
    /// Analyzer that extracts PackageReference and Reference items using MSBuild evaluation when available.
    /// </summary>
    public class DependencyAnalyzer : IProjectAnalyzer
    {
        public Task<ProjectAnalysisResult> AnalyzeAsync(ProjectContext context)
        {
            var result = new ProjectAnalysisResult();

            if (context == null || string.IsNullOrWhiteSpace(context.ProjectPath) || !File.Exists(context.ProjectPath))
                return Task.FromResult(result);

            try
            {
                var props = SharpShift.Inventory.Core.Utilities.ProjectEvaluator.EvaluateProjectProperties(context.ProjectPath);
                if (props != null)
                {
                    // props contains Item.{i}.Type and Item.{i}.Include entries from evaluator
                    var items = props.Where(kv => kv.Key.StartsWith("Item.", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (items.Count > 0)
                    {
                        var groups = new Dictionary<int, Dictionary<string, string>>();
                        foreach (var kv in items)
                        {
                            // Item.{idx}.Type or Item.{idx}.Include or Item.{idx}.Metadata.{name}
                            var parts = kv.Key.Split('.');
                            if (parts.Length >= 3 && int.TryParse(parts[1], out var idx))
                            {
                                if (!groups.TryGetValue(idx, out var dict))
                                {
                                    dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                    groups[idx] = dict;
                                }

                                if (parts.Length == 3)
                                {
                                    dict[parts[2]] = kv.Value;
                                }
                                else if (parts.Length >= 4)
                                {
                                    // preserve metadata key name: Metadata.{name}
                                    var key = parts[2] + "." + string.Join('.', parts.Skip(3));
                                    dict[key] = kv.Value;
                                }
                            }
                        }

                    foreach (var g in groups.Values)
                    {
                        if (g.TryGetValue("Type", out var type) && g.TryGetValue("Include", out var include))
                        {
                            // Attempt to pick up metadata captured by ProjectEvaluator
                            string? version = null;
                            string? hintPath = null;
                            string? metadata = null;

                            // keys like Item.{idx}.Metadata.{Name}
                            var metaKeys = g.Keys.Where(k => k.StartsWith("Metadata.", StringComparison.OrdinalIgnoreCase)).ToList();
                            var other = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var mk in metaKeys)
                            {
                                var mdName = mk.Substring("Metadata.".Length);
                                var mdVal = g[mk];
                                if (string.Equals(mdName, "Version", StringComparison.OrdinalIgnoreCase))
                                    version = mdVal;
                                else if (string.Equals(mdName, "HintPath", StringComparison.OrdinalIgnoreCase))
                                    hintPath = mdVal;
                                else
                                {
                                    other[mdName] = mdVal;
                                    metadata = string.IsNullOrEmpty(metadata) ? $"{mdName}={mdVal}" : metadata + ";" + $"{mdName}={mdVal}";
                                }
                            }

                            if (type.Equals("PackageReference", StringComparison.OrdinalIgnoreCase))
                            {
                                var dep = new DependencyInventory { Name = include, Version = string.IsNullOrWhiteSpace(version) ? null : version, Metadata = metadata, HintPath = hintPath, OtherMetadata = other.Count > 0 ? other : null, Source = "NuGet" };
                                result.Dependencies.Add(dep);
                            }
                            else if (type.Equals("Reference", StringComparison.OrdinalIgnoreCase))
                            {
                                // Try to parse public key token from include (e.g., System.Xml, PublicKeyToken=abcdef)
                                string? pkt = null;
                                try
                                {
                                    var parts = include.Split(',').Select(p => p.Trim()).ToArray();
                                    foreach (var p in parts)
                                    {
                                        if (p.StartsWith("PublicKeyToken=", StringComparison.OrdinalIgnoreCase))
                                        {
                                            pkt = p.Substring("PublicKeyToken=".Length);
                                            break;
                                        }
                                    }
                                }
                                catch { }

                                var dep = new DependencyInventory { Name = include, Metadata = string.IsNullOrWhiteSpace(hintPath) ? metadata : hintPath, HintPath = hintPath, PublicKeyToken = pkt, OtherMetadata = other.Count > 0 ? other : null, Source = "Reference" };
                                result.Dependencies.Add(dep);
                            }
                        }
                    }
                    }
                    else
                    {
                        // No item entries from evaluator - fall back to XML parsing
                        props = null;
                    }
                }

                if (props == null)
                {
                    // Fallback: parse XML to discover PackageReference and Reference items
                    try
                    {
                        var doc = System.Xml.Linq.XDocument.Load(context.ProjectPath);
                        var ns = doc.Root?.Name.Namespace ?? System.Xml.Linq.XNamespace.None;
                        var pkgRefs = doc.Descendants(ns + "PackageReference").Select(x => x.Attribute("Include")?.Value).Where(x => !string.IsNullOrWhiteSpace(x));
                        foreach (var p in pkgRefs)
                        {
                            var version = "";
                            var other = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            try
                            {
                                var el = doc.Descendants(ns + "PackageReference").FirstOrDefault(x => x.Attribute("Include")?.Value == p);
                                version = el?.Attribute("Version")?.Value ?? el?.Element(ns + "Version")?.Value ?? string.Empty;
                                // capture any child elements as other metadata
                                if (el != null)
                                {
                                    foreach (var child in el.Elements())
                                    {
                                        var name = child.Name.LocalName;
                                        var val = child.Value;
                                        if (string.Equals(name, "Version", StringComparison.OrdinalIgnoreCase))
                                            continue;
                                        other[name] = val;
                                    }
                                }
                            }
                            catch { }

                            result.Dependencies.Add(new DependencyInventory { Name = p!, Version = string.IsNullOrWhiteSpace(version) ? null : version, Source = "NuGet", OtherMetadata = other.Count > 0 ? other : null });
                        }

                        var refs = doc.Descendants(ns + "Reference").Select(x => x.Attribute("Include")?.Value).Where(x => !string.IsNullOrWhiteSpace(x));
                        foreach (var r in refs)
                        {
                            var hint = "";
                            var other = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            try
                            {
                                var el = doc.Descendants(ns + "Reference").FirstOrDefault(x => x.Attribute("Include")?.Value == r);
                                hint = el?.Element(ns + "HintPath")?.Value ?? string.Empty;
                                if (el != null)
                                {
                                    foreach (var child in el.Elements())
                                    {
                                        var name = child.Name.LocalName;
                                        var val = child.Value;
                                        if (string.Equals(name, "HintPath", StringComparison.OrdinalIgnoreCase))
                                            continue;
                                        other[name] = val;
                                    }
                                }
                            }
                            catch { }

                            result.Dependencies.Add(new DependencyInventory { Name = r!, Metadata = string.IsNullOrWhiteSpace(hint) ? null : hint, Source = "Reference", OtherMetadata = other.Count > 0 ? other : null });
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
            catch
            {
                // swallow errors
            }

            return Task.FromResult(result);
        }
    }
}

