using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.ProjectAnalysis
{
    /// <summary>
    /// Analyzer that determines the project's target framework and normalizes it.
    /// </summary>
    public class FrameworkAnalyzer : IProjectAnalyzer
    {
        public Task<ProjectAnalysisResult> AnalyzeAsync(ProjectContext context)
        {
            var result = new ProjectAnalysisResult();

            if (context == null || string.IsNullOrWhiteSpace(context.ProjectPath) || !File.Exists(context.ProjectPath))
                return Task.FromResult(result);

            try
            {
                // Try MSBuild evaluation via reflection to avoid hard dependency at runtime in environments where MSBuild isn't available
                try
                {
                    var locatorType = Type.GetType("Microsoft.Build.Locator.MSBuildLocator, Microsoft.Build.Locator");
                    if (locatorType != null)
                    {
                        try
                        {
                            var register = locatorType.GetMethod("RegisterDefaults", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                            register?.Invoke(null, null);
                        }
                        catch
                        {
                            // ignore (swallowed)
                        }
                    }

                    var projectType = Type.GetType("Microsoft.Build.Evaluation.Project, Microsoft.Build");
                    if (projectType != null)
                    {
                        // Prefer centralized evaluator/normalizer from Core if available
                        try
                        {
                            var props = SharpShift.Inventory.Core.Utilities.ProjectEvaluator.EvaluateProjectProperties(context.ProjectPath);
                            if (props != null)
                            {
                                if (props.TryGetValue("TargetFramework", out var tfProp) && !string.IsNullOrWhiteSpace(tfProp))
                                {
                                    result.Framework = SharpShift.Inventory.Core.Utilities.FrameworkNormalizer.Normalize(tfProp);
                                }
                                else if (props.TryGetValue("TargetFrameworks", out var tfsProp) && !string.IsNullOrWhiteSpace(tfsProp))
                                {
                                    var first = tfsProp.Split(';').FirstOrDefault();
                                    if (!string.IsNullOrWhiteSpace(first))
                                        result.Framework = SharpShift.Inventory.Core.Utilities.FrameworkNormalizer.Normalize(first);
                                }
                                else if (props.TryGetValue("TargetFrameworkVersion", out var legacyProp) && !string.IsNullOrWhiteSpace(legacyProp))
                                {
                                    result.Framework = SharpShift.Inventory.Core.Utilities.FrameworkNormalizer.Normalize(legacyProp);
                                }

                                return Task.FromResult(result);
                            }
                        }
                        catch
                        {
                            // fall back to reflection-based approach below if evaluator/normalizer not available or failed
                        }

                        // Reflection fallback (existing behavior) - evaluate via Microsoft.Build if available
                        var projObj = Activator.CreateInstance(projectType, new object[] { context.ProjectPath });
                        var getProp = projectType.GetMethod("GetPropertyValue", new[] { typeof(string) });
                        var tf = getProp?.Invoke(projObj, new object[] { "TargetFramework" }) as string;
                        if (string.IsNullOrWhiteSpace(tf))
                        {
                            var tfs = getProp?.Invoke(projObj, new object[] { "TargetFrameworks" }) as string;
                            if (!string.IsNullOrWhiteSpace(tfs)) tf = tfs.Split(';').FirstOrDefault();
                        }

                        if (string.IsNullOrWhiteSpace(tf))
                        {
                            var legacy = getProp?.Invoke(projObj, new object[] { "TargetFrameworkVersion" }) as string;
                            if (!string.IsNullOrWhiteSpace(legacy)) result.Framework = NormalizeLegacyFramework(legacy);
                        }
                        else
                        {
                            result.Framework = NormalizeSdkFramework(tf);
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
                            // ignore
                        }

                        return Task.FromResult(result);
                    }
                }
                catch
                {
                    // MSBuild not available or reflection failed; fallback to XML parsing
                }

                var doc = XDocument.Load(context.ProjectPath);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

                var tf2 = doc.Descendants(ns + "TargetFramework").FirstOrDefault()?.Value;
                if (string.IsNullOrWhiteSpace(tf2))
                {
                    var tfs2 = doc.Descendants(ns + "TargetFrameworks").FirstOrDefault()?.Value;
                    if (!string.IsNullOrWhiteSpace(tfs2))
                        tf2 = tfs2.Split(';').FirstOrDefault();
                }

                if (string.IsNullOrWhiteSpace(tf2))
                {
                    var legacy2 = doc.Descendants(ns + "TargetFrameworkVersion").FirstOrDefault()?.Value;
                    if (!string.IsNullOrWhiteSpace(legacy2))
                        result.Framework = NormalizeLegacyFramework(legacy2);
                }
                else
                {
                    result.Framework = NormalizeSdkFramework(tf2);
                }
            }
            catch
            {
                // swallow errors and return empty result
            }

            return Task.FromResult(result);
        }

        // Deprecated: Normalization delegated to FrameworkNormalizer in Core.Utilities.
        private static string NormalizeLegacyFramework(string legacy) => SharpShift.Inventory.Core.Utilities.FrameworkNormalizer.Normalize(legacy);
        private static string NormalizeSdkFramework(string tf) => SharpShift.Inventory.Core.Utilities.FrameworkNormalizer.Normalize(tf);
    }
}
