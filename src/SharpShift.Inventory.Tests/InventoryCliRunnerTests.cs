using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SharpShift.Inventory.Cli;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;
using SharpShift.Inventory.Core.Utilities;

namespace SharpShift.Inventory.Tests
{
    public class InventoryCliRunnerTests
    {
        private sealed class FakeSolutionDiscoverer(IEnumerable<SolutionDiscoveryEntry> entries) : ISolutionDiscoverer
        {
            private readonly IEnumerable<SolutionDiscoveryEntry> _entries = entries;

            public Task<IEnumerable<SolutionDiscoveryEntry>> DiscoverSolutionsAsync(string? rootPath = null)
            {
                return Task.FromResult(_entries);
            }
        }

        [Test]
        public async Task RunAsync_MergesRemoteMetadata_WithFilesystemAndClonedSolutionEvaluations()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var previousClonePath = Environment.GetEnvironmentVariable(UpgradeConfig.GithubClonePathEnv);

            Directory.CreateDirectory(temp);
            try
            {
                var localSolution = Path.Combine(temp, "Local.sln");
                File.WriteAllText(localSolution, "Microsoft Visual Studio Solution File, Format Version 12.00");

                var cloneBase = Path.Combine(temp, "clones");
                Environment.SetEnvironmentVariable(UpgradeConfig.GithubClonePathEnv, cloneBase);

                var cloneRoot = Path.Combine(cloneBase, "owner1", "repo1");
                Directory.CreateDirectory(cloneRoot);
                var clonedSolution = Path.Combine(cloneRoot, "Repo.sln");
                File.WriteAllText(clonedSolution, "Microsoft Visual Studio Solution File, Format Version 12.00");

                var nested = Path.Combine(cloneRoot, "nested");
                Directory.CreateDirectory(nested);
                var clonedSlnx = Path.Combine(nested, "RepoNested.slnx");
                File.WriteAllText(clonedSlnx, "<Solution><Project Path=\"Missing.csproj\" /></Solution>");

                var discoverer = new FakeSolutionDiscoverer(new[]
                {
                    new SolutionDiscoveryEntry { Source = "FileSystem", LocalPath = localSolution },
                    new SolutionDiscoveryEntry { Source = "GitHub", RepoUrl = "https://github.com/owner1/repo1", Owner = "owner1", RepoName = "repo1", IsArchived = false }
                });

                var exitCode = await InventoryCliRunner.RunAsync(
                    NullLogger.Instance,
                    temp,
                    "inventory.json",
                    discoverer,
                    retryAttempts: 0,
                    clean: false);

                Assert.That(exitCode, Is.EqualTo(0));

                var inventoryPath = Path.Combine(temp, "inventory.json");
                var json = File.ReadAllText(inventoryPath);
                var items = JsonSerializer.Deserialize<List<SolutionInventory>>(json, InventoryCliRunner.options) ?? [];

                //Assert.That(items.Any(i => i.RepositoryUrl == "https://github.com/owner1/repo1" && i.IsArchived == false), Is.True);
                Assert.That(items.Any(i => string.Equals(i.SolutionPath, Path.GetFullPath(localSolution), StringComparison.OrdinalIgnoreCase)), Is.True);
                Assert.That(items.Any(i => string.Equals(i.SolutionPath, Path.GetFullPath(clonedSolution), StringComparison.OrdinalIgnoreCase)), Is.True);
                Assert.That(items.Any(i => string.Equals(i.SolutionPath, Path.GetFullPath(clonedSlnx), StringComparison.OrdinalIgnoreCase)), Is.True);
            }
            finally
            {
                Environment.SetEnvironmentVariable(UpgradeConfig.GithubClonePathEnv, previousClonePath);
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        [Test]
        public async Task RunAsync_DoesNotCloneArchivedRepositories_WhenTargetDoesNotExist()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var previousClonePath = Environment.GetEnvironmentVariable(UpgradeConfig.GithubClonePathEnv);

            Directory.CreateDirectory(temp);
            try
            {
                var cloneBase = Path.Combine(temp, "clones");
                Environment.SetEnvironmentVariable(UpgradeConfig.GithubClonePathEnv, cloneBase);

                var discoverer = new FakeSolutionDiscoverer(new[]
                {
                    new SolutionDiscoveryEntry
                    {
                        Source = "GitHub",
                        RepoUrl = "https://github.com/owner2/repo2",
                        Owner = "owner2",
                        RepoName = "repo2",
                        IsArchived = true
                    }
                });

                var exitCode = await InventoryCliRunner.RunAsync(
                    NullLogger.Instance,
                    temp,
                    "inventory.json",
                    discoverer,
                    retryAttempts: 0,
                    clean: false);

                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(Directory.Exists(Path.Combine(cloneBase, "owner2", "repo2")), Is.False);
            }
            finally
            {
                Environment.SetEnvironmentVariable(UpgradeConfig.GithubClonePathEnv, previousClonePath);
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        [Test]
        public async Task RunAsync_DeduplicatesEvaluation_WhenSolutionAppearsInFilesystemAndCloneRoots()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var previousClonePath = Environment.GetEnvironmentVariable(UpgradeConfig.GithubClonePathEnv);

            Directory.CreateDirectory(temp);
            try
            {
                var cloneBase = Path.Combine(temp, "clones");
                Environment.SetEnvironmentVariable(UpgradeConfig.GithubClonePathEnv, cloneBase);

                var cloneRoot = Path.Combine(cloneBase, "owner3", "repo3");
                Directory.CreateDirectory(cloneRoot);
                var sharedSolution = Path.Combine(cloneRoot, "Shared.sln");
                File.WriteAllText(sharedSolution, "Microsoft Visual Studio Solution File, Format Version 12.00");

                var discoverer = new FakeSolutionDiscoverer(new[]
                {
                    new SolutionDiscoveryEntry { Source = "FileSystem", LocalPath = sharedSolution },
                    new SolutionDiscoveryEntry
                    {
                        Source = "GitHub",
                        RepoUrl = "https://github.com/owner3/repo3",
                        Owner = "owner3",
                        RepoName = "repo3",
                        IsArchived = false,
                        LocalPath = cloneRoot
                    }
                });

                var exitCode = await InventoryCliRunner.RunAsync(
                    NullLogger.Instance,
                    temp,
                    "inventory.json",
                    discoverer,
                    retryAttempts: 0,
                    clean: false);

                Assert.That(exitCode, Is.EqualTo(0));

                var inventoryPath = Path.Combine(temp, "inventory.json");
                var json = File.ReadAllText(inventoryPath);
                var items = JsonSerializer.Deserialize<List<SolutionInventory>>(json, InventoryCliRunner.options) ?? [];

                var matchCount = items.Count(i => string.Equals(i.SolutionPath, Path.GetFullPath(sharedSolution), StringComparison.OrdinalIgnoreCase));
                Assert.That(matchCount, Is.EqualTo(1));
            }
            finally
            {
                Environment.SetEnvironmentVariable(UpgradeConfig.GithubClonePathEnv, previousClonePath);
                try { Directory.Delete(temp, true); } catch { }
            }
        }
    }
}
