using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Discovery
{
    /// <summary>
    /// Discovers solution files under a given root path.
    /// </summary>
    public class SolutionDiscoverer : ISolutionDiscoverer
    {
        private readonly ILogger<SolutionDiscoverer>? _logger;
        private readonly FileSystemSolutionDiscoverer _fsDiscoverer;
        private readonly GitHubSolutionDiscoverer _ghDiscoverer;

        public SolutionDiscoverer()
            : this(null, null)
        {
        }

        public SolutionDiscoverer(ILogger<SolutionDiscoverer>? logger, ILoggerFactory? loggerFactory)
        {
            _logger = logger;
            _fsDiscoverer = new FileSystemSolutionDiscoverer(loggerFactory?.CreateLogger<FileSystemSolutionDiscoverer>());
            // Create GitHub discoverer with default HttpClient.
            var http = new System.Net.Http.HttpClient();
            _ghDiscoverer = new GitHubSolutionDiscoverer(http, loggerFactory?.CreateLogger<GitHubSolutionDiscoverer>());
        }

        /// <inheritdoc />
        public async Task<IEnumerable<SolutionDiscoveryEntry>> DiscoverSolutionsAsync(string? rootPath)
        {
            _logger?.LogInformation("Starting solution discovery for root path {RootPath}", rootPath);
            var results = new List<SolutionDiscoveryEntry>();

            var fs = await _fsDiscoverer.DiscoverSolutionsAsync(rootPath);
            if (fs != null)
                results.AddRange(fs);

            var gh = await _ghDiscoverer.DiscoverSolutionsAsync(rootPath);
            if (gh != null)
                results.AddRange(gh);

            _logger?.LogInformation("Completed solution discovery with {EntryCount} entries", results.Count);

            return results;
        }
    }
}
