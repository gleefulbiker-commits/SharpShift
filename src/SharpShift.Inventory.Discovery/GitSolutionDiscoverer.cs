using Microsoft.Extensions.Logging;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;
using SharpShift.Inventory.Core.Utilities;
using SharpShift.LoggerMessages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpShift.Inventory.Discovery
{
    public class GitSolutionDiscoverer(ILogger? logger) : ISolutionDiscoverer
    {
        public const int DefaultGitCloneTimeoutSeconds = 600000;
        public const int DefaultGitCloneDepth = 1;

        public Task<IEnumerable<SolutionDiscoveryEntry>> DiscoverSolutionsAsync(string? rootPath)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<SolutionDiscoveryEntry>> DiscoverSolutionsAsync(string? rootPath, string? gitUrlsPath)
        {
            var results = new List<SolutionDiscoveryEntry>();

            if (string.IsNullOrWhiteSpace(gitUrlsPath))
            {
                logger!.LogInformation("Git URLs path is null or empty.");
            }
            else if (!System.IO.File.Exists(gitUrlsPath))
            {
                logger!.LogWarning("Git URLs file does not exist: {gitUrlsPath}", gitUrlsPath);
            }
            else
            {
                LogMessages.GitUrlsFileFound(logger!, gitUrlsPath);
                var urls = File.ReadAllLines(gitUrlsPath);
                var clonePath = Path.GetDirectoryName(gitUrlsPath) ?? string.Empty;

                foreach (var url in urls)
                {
                    LogMessages.DiscoveredGitUrl(logger!, url);
                    var repoName = GitHelper.GetRepoNameFromUrl(url);
                    var isSuccess = await GitHelper.TryClone(url, Path.Combine(clonePath, repoName), DefaultGitCloneDepth, DefaultGitCloneTimeoutSeconds, logger);
                    if (isSuccess)
                    {
                        LogMessages.RepositoryCloneResolved(logger!, url, Path.Combine(clonePath, repoName));
                        results.Add(new SolutionDiscoveryEntry { Source = "Git", LocalPath = Path.Combine(clonePath, repoName), RepoUrl = url });
                    }
                    else
                    {
                        LogMessages.FailedToCloneRepository(logger!, url, Path.Combine(clonePath, repoName));
                    }
                }
            }

            return results;
        }
    }
}
