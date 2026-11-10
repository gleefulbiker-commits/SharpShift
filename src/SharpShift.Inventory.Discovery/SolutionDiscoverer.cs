using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;
using SharpShift.LoggerMessages;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SharpShift.Inventory.Discovery
{
    /// <summary>
    /// Discovers solution files under a given root path.
    /// </summary>
    public class SolutionDiscoverer : ISolutionDiscoverer
    {
        private readonly ILogger? _logger;
        private readonly FileSystemSolutionDiscoverer _fsDiscoverer;
        private readonly GitHubSolutionDiscoverer _ghDiscoverer;
        private readonly GitSolutionDiscoverer _gitDiscoverer;

        public SolutionDiscoverer()
            : this(NullLogger.Instance)
        {
        }

        public SolutionDiscoverer(ILogger? logger)
        {
            _logger = logger;
            _fsDiscoverer = new FileSystemSolutionDiscoverer(logger);
            // Create GitHub discoverer with default HttpClient.
            var http = new System.Net.Http.HttpClient();
            _ghDiscoverer = new GitHubSolutionDiscoverer(http, logger);
            _gitDiscoverer = new GitSolutionDiscoverer(logger); 
        }

        /// <inheritdoc />
        public async Task<IEnumerable<SolutionDiscoveryEntry>> DiscoverSolutionsAsync(string? rootPath, string? gitUrlsPath)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(rootPath, $"Solution discovery skipped because root path is empty or missing: {rootPath}");
            ArgumentNullException.ThrowIfNull(_logger, $"Solution discovery skipped because logger is null.");

            LogMessages.StartingSolutionDiscovery(_logger, rootPath);
            var results = new List<SolutionDiscoveryEntry>();

            var fs = await _fsDiscoverer.DiscoverSolutionsAsync(rootPath, gitUrlsPath);
            if (fs is not null)
                results.AddRange(fs);
            LogMessages.FileSystemDiscoveryCompleted(_logger, results.Count);

            var gh = await _ghDiscoverer.DiscoverSolutionsAsync(rootPath, gitUrlsPath);
            if (gh is not null)
                results.AddRange(gh);
           LogMessages.GitHubDiscoveryCompleted(_logger, results.Count);

            var git = await _gitDiscoverer.DiscoverSolutionsAsync(rootPath, gitUrlsPath);
            if (git is not null)
                results.AddRange(git);
            LogMessages.GitDiscoveryCompleted(_logger, results.Count);


            return results;
        }
    }
}
