using Microsoft.Extensions.Logging;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;
using SharpShift.Inventory.Core.Utilities;
using SharpShift.LoggerMessages;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharpShift.Inventory.Discovery
{
    public class TfvcSolutionDiscoverer(ILogger? logger) : ISolutionDiscoverer
    {
        public const int DefaultGitCloneTimeoutSeconds = 600000;
        public const int DefaultGitCloneDepth = 1;

        public Task<IEnumerable<SolutionDiscoveryEntry>> DiscoverSolutionsAsync(string? rootPath)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<SolutionDiscoveryEntry>> DiscoverSolutionsAsync(string? rootPath, string? gitUrlsPath, string? tfvcUrlsPath)
        {
            var results = new List<SolutionDiscoveryEntry>();

            if (string.IsNullOrWhiteSpace(tfvcUrlsPath))
            {
                logger!.LogInformation("TFVC URLs path is null or empty.");
            }
            else if (!System.IO.File.Exists(tfvcUrlsPath))
            {
                logger!.LogWarning("TFVC URLs file does not exist: {tfvcUrlsPath}", tfvcUrlsPath);
            }
            else
            {
                LogMessages.TfvcUrlsFileFound(logger!, tfvcUrlsPath);
                var tfvcFolders = File.ReadAllLines(tfvcUrlsPath);
                var tfvcPath = Path.GetDirectoryName(tfvcUrlsPath) ?? string.Empty;

                foreach (var folder in tfvcFolders)
                {
                    LogMessages.DiscoveredTfvcUrl(logger!, folder);
                    if(TfvcHelper.IsTfvcMapped(folder, out var mappedRoot))
                    {
                        var repoDetails = await TfvcHelper.GetSolutionsInTfvcWorkspace(mappedRoot!);
                        results.AddRange(repoDetails);
                    }
                }
            }

            return results;
        }
    }
}
