using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using SharpShift.LoggerMessages;

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

        public static async Task<(int ExitCode, string RepoUrl, string StdErr)> TryGetRepoUrlAsync(
        string workingDirectory,
        int timeoutMs = 10000,
        CancellationToken cancellationToken = default)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",                  // e.g. "git"
                Arguments = $"-C \"{workingDirectory}\" config --get remote.origin.url",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutMs);

            await process.WaitForExitAsync(timeoutCts.Token);

            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;

            return (process.ExitCode, stdOut.Trim(), stdErr.Trim());
        }

        public static async Task<bool> TryClone(
            string repoUrl,
            string targetDir,
            int depth = UpgradeConfig.DefaultCloneDepth,
            int timeoutMs = UpgradeConfig.DefaultCloneTimeoutMs,
            ILogger? logger = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(repoUrl) || string.IsNullOrWhiteSpace(targetDir))
                {
                    logger?.LogWarning("Git clone skipped because repoUrl or targetDir is empty.");
                    return false;
                }

                if(logger is not null)
                {
                    LogMessages.StartingGitCloneOperation(logger);
                    LogMessages.GitCloneRepositoryUrl(logger, repoUrl);
                    LogMessages.GitCloneTargetDirectory(logger, targetDir);
                    LogMessages.GitCloneDepth(logger, depth);
                    LogMessages.GitCloneTimeout(logger, timeoutMs);
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

                await proc.WaitForExitAsync(CancellationToken.None);
                if (!proc.HasExited)
                {
                    try { proc.Kill(); }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to terminate timed out git clone process for {RepoUrl}", repoUrl);
                    }
                    logger?.LogWarning("Git clone timed out for {RepoUrl} after {TimeoutMs} ms", repoUrl, timeoutMs);
                    return false;
                }

                if (proc.ExitCode != 0)
                {
                    var errorOutput = await proc.StandardError.ReadToEndAsync();
                    logger?.LogWarning("Git clone failed for {RepoUrl} with exit code {ExitCode}. Error: {ErrorOutput}", repoUrl, proc.ExitCode, errorOutput);
                }

                return proc.ExitCode == 0 && Directory.Exists(targetDir);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Unexpected error while cloning repository {RepoUrl}", repoUrl);
                return false;
            }
        }

        public static string GetRepoNameFromUrl(string url)
        {
            ArgumentException.ThrowIfNullOrEmpty(url, nameof(url));

            var segments = url.TrimEnd('/').Split('/');
            var repoName = segments[^1];
            if (repoName.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                repoName = repoName[..^4];
            return repoName;
        }
    }
}
