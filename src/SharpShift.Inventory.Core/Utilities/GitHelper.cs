using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace SharpShift.Inventory.Core.Utilities
{
    public static class GitHelper
    {
        public static bool IsGitAvailable(ILogger? logger = null)
        {
            try
            {
                var psi = new ProcessStartInfo("git", "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p == null)
                {
                    logger?.LogWarning("Unable to start git --version process.");
                    return false;
                }

                p.WaitForExit(5000);
                if (p.ExitCode != 0)
                {
                    logger?.LogWarning("Git availability check failed with exit code {ExitCode}", p.ExitCode);
                }
                return p.ExitCode == 0;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error while checking git availability.");
                return false;
            }
        }

        public static bool TryClone(string repoUrl, string targetDir, int depth = UpgradeConfig.DefaultCloneDepth, int timeoutMs = UpgradeConfig.DefaultCloneTimeoutMs, ILogger? logger = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(repoUrl) || string.IsNullOrWhiteSpace(targetDir))
                {
                    logger?.LogWarning("Git clone skipped because repoUrl or targetDir is empty.");
                    return false;
                }

                var parent = Path.GetDirectoryName(targetDir);
                if (!string.IsNullOrWhiteSpace(parent) && !Directory.Exists(parent))
                    Directory.CreateDirectory(parent);

                if(logger is not null)
                {
                    logger.LogInformation("Starting git clone operation.");
                    logger.LogInformation("Repository URL: {RepoUrl}", repoUrl);
                    logger.LogInformation("Target Directory: {TargetDir}", targetDir);
                    logger.LogInformation("Clone Depth: {Depth}", depth);
                    logger.LogInformation("Timeout (ms): {TimeoutMs}", timeoutMs);
                }
                var args = $"clone --depth {depth} {repoUrl} \"{targetDir}\"";
                var psi = new ProcessStartInfo("git", args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    logger?.LogWarning("Unable to start git clone process for {RepoUrl}", repoUrl);
                    return false;
                }

                var completed = proc.WaitForExit(timeoutMs);
                if (!completed)
                {
                    try { proc.Kill(); } catch { }
                    logger?.LogWarning("Git clone timed out for {RepoUrl} after {TimeoutMs} ms", repoUrl, timeoutMs);
                    return false;
                }

                if (proc.ExitCode != 0)
                {
                    logger?.LogWarning("Git clone failed for {RepoUrl} with exit code {ExitCode}", repoUrl, proc.ExitCode);
                }

                return proc.ExitCode == 0 && Directory.Exists(targetDir);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Unexpected error while cloning repository {RepoUrl}", repoUrl);
                return false;
            }
        }
    }
}
