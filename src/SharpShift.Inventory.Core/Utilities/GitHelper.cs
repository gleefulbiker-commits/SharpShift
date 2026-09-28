using System;
using System.Diagnostics;
using System.IO;

namespace SharpShift.Inventory.Core.Utilities
{
    public static class GitHelper
    {
        public static bool IsGitAvailable()
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
                if (p == null) return false;
                p.WaitForExit(5000);
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryClone(string repoUrl, string targetDir, int depth = UpgradeConfig.DefaultCloneDepth, int timeoutMs = UpgradeConfig.DefaultCloneTimeoutMs)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(repoUrl) || string.IsNullOrWhiteSpace(targetDir))
                    return false;

                var parent = Path.GetDirectoryName(targetDir);
                if (!string.IsNullOrWhiteSpace(parent) && !Directory.Exists(parent))
                    Directory.CreateDirectory(parent);

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
                    return false;

                var completed = proc.WaitForExit(timeoutMs);
                if (!completed)
                {
                    try { proc.Kill(); } catch { }
                    return false;
                }

                return proc.ExitCode == 0 && Directory.Exists(targetDir);
            }
            catch
            {
                return false;
            }
        }
    }
}
