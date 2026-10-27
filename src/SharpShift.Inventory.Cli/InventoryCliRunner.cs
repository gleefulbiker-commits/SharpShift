using SharpShift.Inventory.Core.Models;
using SharpShift.Inventory.Core.Utilities;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SharpShift.Inventory.Cli
{
    /// <summary>
    /// Minimal runner that discovers solutions/projects, extracts project name and framework, and writes inventory JSON.
    /// </summary>
    public static partial class InventoryCliRunner
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

        public static readonly JsonSerializerOptions options = new ()
        {
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        public static async Task<int> RunAsync(
            string rootPath,
            string outputFile,
            SharpShift.Inventory.Core.Interfaces.ISolutionDiscoverer? solutionDiscoverer = null,
            int retryAttempts = 0,
            bool clean = false,
            bool analyzeCloned = false,
            ILogger? logger = null)
        {
            var allSolutions = new List<SolutionInventory>();

            try
            {
                if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                {
                    logger?.LogError("Path not found: {RootPath}", rootPath);
                    Console.Error.WriteLine($"Path not found: {rootPath}");
                    return 2;
                }

                var slnFiles = new List<string>();
                var csprojFiles = new List<string>();

                var remoteRepos = new List<object>();
                if (solutionDiscoverer is not null)
                {
                    var sols = await solutionDiscoverer.DiscoverSolutionsAsync(rootPath);
                    // collect local solution file paths for further processing
                    slnFiles = [..sols.Where(s => s.Source == "FileSystem" && !string.IsNullOrWhiteSpace(s.LocalPath)).Select(s => s.LocalPath!)];

                    // record remote GitHub repositories for later insertion into inventory summary
                    remoteRepos = [..sols.Where(s => s.Source == "GitHub" && !string.IsNullOrWhiteSpace(s.RepoUrl)).Select(s => new { s.RepoUrl, s.IsArchived }).Cast<object>()];

                    foreach(var sol in sols)
                    {
                        if (sol.Source == "GitHub" && !string.IsNullOrWhiteSpace(sol.RepoUrl))
                        {
                            logger?.LogInformation("Discovered remote GitHub repository: {RepoUrl} (archived: {IsArchived})", sol.RepoUrl, sol.IsArchived);
                        }
                        else if (sol.Source == "FileSystem" && !string.IsNullOrWhiteSpace(sol.LocalPath))
                        {
                            logger?.LogInformation("Evaluating local solution file: {SolutionPath}", sol.LocalPath);
                            var solutionInventory = SolutionEvaluator.EvaluateSolutionProperties(sol.LocalPath, forceRefresh: true, logger);
                            if (solutionInventory is not null)
                            {
                                solutionInventory.Summary["projectCount"] = solutionInventory.Projects.Count;
                                allSolutions.Add(solutionInventory);
                            }
                        }
                    }
                }
                else
                {
                    throw new ArgumentNullException(nameof(solutionDiscoverer), "Solution discoverer is required.");
                }

                if (clean)
                {
                    try
                    {
                        if (File.Exists(outputFile))
                            File.Delete(outputFile);
                    }
                    catch
                    {
                        logger?.LogWarning("Failed to delete previous output file during clean run: {OutputFile}", outputFile);
                    }

                    // clear evaluator cache
                    ProjectEvaluator.ClearCache();
                    logger?.LogInformation("Performed clean restart: removed output and cleared evaluator cache.");
                }

                if (remoteRepos.Count > 0)
                {
                    foreach (var solutionInventory in allSolutions)
                    {
                        solutionInventory.Summary["remoteRepositories"] = remoteRepos;
                    }
                }

                var json = JsonSerializer.Serialize(allSolutions, options);
                var fullPath = Path.Combine(rootPath, outputFile);

                File.WriteAllText(fullPath, json);
                logger?.LogInformation("Wrote inventory to {OutputPath}", fullPath);
                return 0;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Unhandled exception during inventory run for root path {RootPath}", rootPath);
                Console.Error.WriteLine(ex.ToString());
                return 3;
            }
        }

        private static string NormalizeLegacyFramework(string legacy)
        {
            // legacy like v4.6.2
            if (string.IsNullOrEmpty(legacy))
            {
                return ".NET Framework";
            }

            if (legacy.Length > 1 && legacy[0] == 'v')
            {
                return $".NET Framework {legacy[1]}";
            }

            return $".NET Framework {legacy}";
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

            var m = NetRegex().Match(tf);
            if (m.Success)
            {
                var major = m.Groups[1].Value;
                var minor = m.Groups[3].Success ? m.Groups[3].Value : null;
                if (!string.IsNullOrEmpty(minor))
                    return $".NET {major}.{minor}";
                return $".NET {major}.0";
            }

            m = NetCoreAppRegex().Match(tf);
            if (m.Success)
            {
                var major = m.Groups[1].Value;
                var minor = m.Groups[3].Success ? m.Groups[3].Value : "0";
                return $".NET Core {major}.{minor}";
            }

            m = NetStandardRegex().Match(tf);
            if (m.Success)
            {
                var major = m.Groups[1].Value;
                var minor = m.Groups[3].Success ? m.Groups[3].Value : "0";
                return $".NET Standard {major}.{minor}";
            }

            return tf;
        }

        [System.Text.RegularExpressions.GeneratedRegex("^net(\\d+)(\\.(\\d+))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
        private static partial System.Text.RegularExpressions.Regex NetRegex();

        [System.Text.RegularExpressions.GeneratedRegex("^netcoreapp(\\d+)(\\.(\\d+))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
        private static partial System.Text.RegularExpressions.Regex NetCoreAppRegex();

        [System.Text.RegularExpressions.GeneratedRegex("^netstandard(\\d+)(\\.(\\d+))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
        private static partial System.Text.RegularExpressions.Regex NetStandardRegex();
    }
}

