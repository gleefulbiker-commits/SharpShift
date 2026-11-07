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

        public SolutionDiscoverer()
            : this(NullLogger.Instance, null)
        {
        }

        public SolutionDiscoverer(ILogger? logger, ILoggerFactory? loggerFactory)
        {
            _logger = logger;
            _fsDiscoverer = new FileSystemSolutionDiscoverer(logger);
            // Create GitHub discoverer with default HttpClient.
            var http = new System.Net.Http.HttpClient();
            _ghDiscoverer = new GitHubSolutionDiscoverer(http, logger);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<SolutionDiscoveryEntry>> DiscoverSolutionsAsync(string? rootPath)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(rootPath, $"Solution discovery skipped because root path is empty or missing: {rootPath}");
            ArgumentNullException.ThrowIfNull(_logger, $"Solution discovery skipped because logger is null.");

            LogMessages.StartingSolutionDiscovery(_logger, rootPath);
            var results = new List<SolutionDiscoveryEntry>();

            var fs = await _fsDiscoverer.DiscoverSolutionsAsync(rootPath);
            if (fs is not null)
                results.AddRange(fs);

            var gh = await _ghDiscoverer.DiscoverSolutionsAsync(rootPath);
            if (gh is not null)
                results.AddRange(gh);

            LogMessages.GitHubDiscoveryCompleted(_logger, results.Count);

            return results;
        }
    }
}
