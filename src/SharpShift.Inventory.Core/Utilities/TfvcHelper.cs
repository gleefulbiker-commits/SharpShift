using Microsoft.Extensions.Logging;
using SharpShift.Inventory.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SharpShift.Inventory.Core.Utilities
{
    public static class TfvcHelper
    {
        public static bool IsTfAvailable(ILogger? logger = null)
        {
            try
            {
                var psi = new ProcessStartInfo("tf", "help")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p == null)
                {
                    logger?.LogWarning("Unable to start tf help process.");
                    return false;
                }

                p.WaitForExit(5000);
                if (p.ExitCode != 0)
                {
                    logger?.LogWarning("TF availability check failed with exit code {ExitCode}", p.ExitCode);
                }
                return p.ExitCode == 0;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error while checking TF availability.");
                return false;
            }
        }

        // Walks up from the given path looking for the TFVC local-workspace marker folder ("$tf").
        public static bool IsTfvcMapped(string path, out string? mappedRoot)
        {
            mappedRoot = null;
            var dir = new DirectoryInfo(Path.GetFullPath(path));

            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "$tf")))
                {
                    mappedRoot = dir.FullName;
                    return true;
                }
                dir = dir.Parent;
            }
            return false;
        }

        public static async Task<List<SolutionDiscoveryEntry>> GetSolutionsInTfvcWorkspace(
        string workspaceDirectory,
        int timeoutMs = 10000,
        CancellationToken cancellationToken = default)
        {
            var result = new List<SolutionDiscoveryEntry>();
            var slnFiles = Directory.EnumerateFiles(workspaceDirectory, "*.sln*", SearchOption.AllDirectories).ToList();

            foreach (var slnFile in slnFiles)
            {
                result.Add(new SolutionDiscoveryEntry
                {
                    Source = "TFVC",
                    LocalPath = slnFile,
                    RepoUrl = workspaceDirectory, // In TFVC, the workspace directory is the root of the mapped repository.
                    IsArchived = false,
                    RepoName = Path.GetFileNameWithoutExtension(slnFile)
                });
            }

            return result;
        }
    }
}
