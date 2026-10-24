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
                var evaluated = SharpShift.Inventory.Core.Utilities.ProjectEvaluator.EvaluateProjectProperties(context.ProjectPath);
                if (evaluated is not null && !string.IsNullOrWhiteSpace(evaluated.Framework))
                {
                    result.Framework = SharpShift.Inventory.Core.Utilities.FrameworkNormalizer.Normalize(evaluated.Framework);
                    return Task.FromResult(result);
                }

                var doc = XDocument.Load(context.ProjectPath);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

                var tf = doc.Descendants(ns + "TargetFramework").FirstOrDefault()?.Value;
                if (string.IsNullOrWhiteSpace(tf))
                {
                    var tfs = doc.Descendants(ns + "TargetFrameworks").FirstOrDefault()?.Value;
                    if (!string.IsNullOrWhiteSpace(tfs))
                        tf = tfs.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                }

                if (!string.IsNullOrWhiteSpace(tf))
                {
                    result.Framework = NormalizeSdkFramework(tf);
                    return Task.FromResult(result);
                }

                var legacy = doc.Descendants(ns + "TargetFrameworkVersion").FirstOrDefault()?.Value;
                if (!string.IsNullOrWhiteSpace(legacy))
                    result.Framework = NormalizeLegacyFramework(legacy);
            }
            catch
            {
            }

            return Task.FromResult(result);
        }

        // Deprecated: Normalization delegated to FrameworkNormalizer in Core.Utilities.
        private static string NormalizeLegacyFramework(string legacy) => SharpShift.Inventory.Core.Utilities.FrameworkNormalizer.Normalize(legacy);
        private static string NormalizeSdkFramework(string tf) => SharpShift.Inventory.Core.Utilities.FrameworkNormalizer.Normalize(tf);
    }
}
