using SharpShift.LoggerMessages;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;
using SharpShift.Inventory.Core.Utilities;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
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

        public static readonly JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        public static async Task<int> RunAsync(
            ILogger logger,
            string rootPath,
            string gitUrlsPath,
            string tfvcUrlsPath,
            string outputFile,
            ISolutionDiscoverer? solutionDiscoverer = null,
            int retryAttempts = 0,
            bool clean = false)
        {
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            var allSolutions = new List<SolutionInventory>();

            try
            {
                if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                {
                    logger.LogError("Path not found: {RootPath}", rootPath);
                    Console.Error.WriteLine($"Path not found: {rootPath}");
                    return 2;
                }

                if (string.IsNullOrWhiteSpace(outputFile))
                {
                    logger.LogError("Output file name is required.");
                    Console.Error.WriteLine("Output file name is required.");
                    return 2;
                }

                if (solutionDiscoverer is null)
                {
                    throw new ArgumentNullException(nameof(solutionDiscoverer), "Solution discoverer is required.");
                }

                var discoveryEntries = (await solutionDiscoverer.DiscoverSolutionsAsync(rootPath, gitUrlsPath, tfvcUrlsPath)).ToList();
                var fileSystemEntries = GetFileSystemEntries(discoveryEntries).ToList();
                var gitHubEntries = GetGitHubRepoEntries(discoveryEntries).ToList();
                var otherGitEntries = GetOtherGitRepoEntries(discoveryEntries).ToList();
                var tfvcEntries = GetTfvcRepoEntries(discoveryEntries).ToList();
                var evaluatedSolutionPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                discoveryEntries = DeDuplicateEntries(logger, discoveryEntries, fileSystemEntries, gitHubEntries, RepoSourceType.GitHub);
                
                await EvaluateAndAppendSolutions(
                    logger,
                    fileSystemEntries.Select(entry => entry.LocalPath!),
                    allSolutions,
                    evaluatedSolutionPaths);

                var cloneRoots = ResolveCloneRootsSequential(gitHubEntries.Concat(otherGitEntries), logger);
                var clonedSolutionFiles = DiscoverSolutionFilesUnderRoots(cloneRoots, logger);
                await EvaluateAndAppendSolutions(logger, clonedSolutionFiles, allSolutions, evaluatedSolutionPaths);

                if (clean)
                {
                    try
                    {
                        if (File.Exists(outputFile))
                            File.Delete(outputFile);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to delete previous output file during clean run: {OutputFile}", outputFile);
                    }

                    // clear evaluator cache
                    ProjectEvaluator.ClearCache();
                    logger?.LogInformation("Performed clean restart: removed output and cleared evaluator cache.");
                }

                var json = JsonSerializer.Serialize(allSolutions, options);
                var fullPath = Path.Combine(rootPath, outputFile);
                if (Directory.Exists(fullPath))
                {
                    logger.LogError("Output path is a directory: {FullPath}", fullPath);
                    Console.Error.WriteLine($"Output path is a directory: {fullPath}");
                    return 2;
                }

                File.WriteAllText(fullPath, json);
                LogMessages.WritingInventory(logger ?? throw new ArgumentNullException(nameof(logger)), fullPath);
                return 0;
            }
            catch (Exception ex)
            {
                (logger ?? throw new ArgumentNullException(nameof(logger))).LogError(ex, "Unhandled exception during inventory run for root path {RootPath}", rootPath);
                Console.Error.WriteLine(ex.ToString());
                return 3;
            }
        }

        private static List<SolutionDiscoveryEntry> DeDuplicateEntries(
            ILogger logger,
            List<SolutionDiscoveryEntry> allEntries,
            List<SolutionDiscoveryEntry> fileSystemEntries,
            List<SolutionDiscoveryEntry> remoteRepoEntries,
            string remoteRepoSource)
        {
            var intersection = fileSystemEntries.Select(fs => fs.RepoUrl).Intersect(remoteRepoEntries.Select(gh => gh.RepoUrl), StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var repoUrl in intersection)
            {
                LogMessages.DuplicateSolutionDiscovery(logger, repoUrl);
                remoteRepoEntries.RemoveAll(entry => string.Equals(entry.RepoUrl, repoUrl, StringComparison.OrdinalIgnoreCase) && string.Equals(entry.Source, remoteRepoSource, StringComparison.OrdinalIgnoreCase));
            }

            return allEntries;
        }

        private static IEnumerable<SolutionDiscoveryEntry> GetFileSystemEntries(IEnumerable<SolutionDiscoveryEntry> entries)
        {
            return entries.Where(entry =>
                string.Equals(entry.Source, RepoSourceType.FileSystem, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(entry.LocalPath));
        }

        private static IEnumerable<SolutionDiscoveryEntry> GetGitHubRepoEntries(IEnumerable<SolutionDiscoveryEntry> entries)
        {
            return entries.Where(entry =>
                string.Equals(entry.Source, RepoSourceType.GitHub, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(entry.RepoUrl));
        }

        private static IEnumerable<SolutionDiscoveryEntry> GetOtherGitRepoEntries(IEnumerable<SolutionDiscoveryEntry> entries)
        {
            return entries.Where(entry =>
                string.Equals(entry.Source, RepoSourceType.OtherGit, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(entry.RepoUrl));
        }

        private static IEnumerable<SolutionDiscoveryEntry> GetTfvcRepoEntries(IEnumerable<SolutionDiscoveryEntry> entries)
        {
            return entries.Where(entry =>
                string.Equals(entry.Source, RepoSourceType.Tfvc, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(entry.RepoUrl));
        }

        private static async Task EvaluateAndAppendSolutions(
            ILogger logger,
            IEnumerable<string> solutionPaths,
            List<SolutionInventory> allSolutions,
            HashSet<string> evaluatedSolutionPaths)
        {
            foreach (var solutionPath in solutionPaths)
            {
                if (string.IsNullOrWhiteSpace(solutionPath))
                    continue;

                var normalizedPath = Path.GetFullPath(solutionPath);
                if (!evaluatedSolutionPaths.Add(normalizedPath))
                {
                    LogMessages.SkippingDuplicateSolutionEvaluation(logger, normalizedPath);
                    continue;
                }

                LogMessages.EvaluatingLocalSolutionFile(logger, normalizedPath);
                var solutionInventory = await SolutionEvaluator.EvaluateSolutionProperties(normalizedPath, forceRefresh: true, logger);
                if (solutionInventory is not null)
                {
                    solutionInventory.Summary["projectCount"] = solutionInventory.Projects.Count;
                    allSolutions.Add(solutionInventory);
                }
            }
        }

        private static List<string> ResolveCloneRootsSequential(IEnumerable<SolutionDiscoveryEntry> remoteEntries, ILogger logger)
        {
            var cloneRoots = new List<string>();
            var cloneBasePath = Environment.GetEnvironmentVariable(UpgradeConfig.GithubClonePathEnv);
            var gitAvailable = false;

            foreach (var remoteEntry in remoteEntries)
            {
                LogMessages.DiscoveredRemoteGitHubRepository(logger, remoteEntry.RepoUrl!, remoteEntry.IsArchived);

                if (!string.IsNullOrWhiteSpace(remoteEntry.LocalPath) && Directory.Exists(remoteEntry.LocalPath))
                {
                    cloneRoots.Add(Path.GetFullPath(remoteEntry.LocalPath));
                    continue;
                }

                if (remoteEntry.IsArchived)
                {
                    LogMessages.SkippingArchivedRepositoryClone(logger, remoteEntry.RepoUrl!);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(cloneBasePath))
                    continue;

                var repoName = ResolveRepoFolderName(remoteEntry);
                if (string.IsNullOrWhiteSpace(repoName))
                    continue;

                var owner = string.IsNullOrWhiteSpace(remoteEntry.Owner) ? "unknown" : remoteEntry.Owner;
                var targetDir = Path.Combine(cloneBasePath, owner, repoName);

                if (Directory.Exists(targetDir))
                {
                    LogMessages.SkippingCloneTargetExists(logger, remoteEntry.RepoUrl!, targetDir);
                    remoteEntry.LocalPath = Path.GetFullPath(targetDir);
                    cloneRoots.Add(remoteEntry.LocalPath);
                    continue;
                }

                if (!gitAvailable)
                {
                    gitAvailable = GitHelper.IsGitAvailable(logger);
                }

                if (!gitAvailable)
                    continue;

                LogMessages.StartingRepositoryClone(logger, remoteEntry.RepoUrl!, targetDir);
                _ = GitHelper.TryClone(
                    remoteEntry.RepoUrl!,
                    targetDir,
                    UpgradeConfig.DefaultCloneDepth,
                    UpgradeConfig.DefaultCloneTimeoutMs,
                    logger);

                if (Directory.Exists(targetDir))
                {
                    remoteEntry.LocalPath = Path.GetFullPath(targetDir);
                    cloneRoots.Add(remoteEntry.LocalPath);
                    LogMessages.RepositoryCloneResolved(logger, remoteEntry.RepoUrl!, remoteEntry.LocalPath);
                }
            }

            return cloneRoots;
        }

        private static IEnumerable<string> DiscoverSolutionFilesUnderRoots(IEnumerable<string> cloneRoots, ILogger logger)
        {
            foreach (var cloneRoot in cloneRoots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(cloneRoot) || !Directory.Exists(cloneRoot))
                    continue;

                var slnFiles = Directory.EnumerateFiles(cloneRoot, "*.sln", SearchOption.AllDirectories);
                var slnxFiles = Directory.EnumerateFiles(cloneRoot, "*.slnx", SearchOption.AllDirectories);
                var discovered = slnFiles.Concat(slnxFiles).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                LogMessages.DiscoveredClonedRepositorySolutions(logger, cloneRoot, discovered.Count);

                foreach (var solution in discovered)
                    yield return solution;
            }
        }

        private static string ResolveRepoFolderName(SolutionDiscoveryEntry remoteEntry)
        {
            if (!string.IsNullOrWhiteSpace(remoteEntry.RepoName))
                return remoteEntry.RepoName;

            if (!Uri.TryCreate(remoteEntry.RepoUrl, UriKind.Absolute, out var uri))
                return string.Empty;

            var lastSegment = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault();

            if (string.IsNullOrWhiteSpace(lastSegment))
                return string.Empty;

            return lastSegment.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
                ? lastSegment[..^4]
                : lastSegment;
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

            if (FrameworkNormalizer.FrameworkMap.TryGetValue(tf, out var friendly))
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

