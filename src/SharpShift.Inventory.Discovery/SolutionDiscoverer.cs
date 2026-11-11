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
        private readonly TfvcSolutionDiscoverer _tfvcDiscoverer;

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
            _tfvcDiscoverer = new TfvcSolutionDiscoverer(logger);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<SolutionDiscoveryEntry>> DiscoverSolutionsAsync(string? rootPath, string? gitUrlsPath, string? tfvcUrlsPath)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(rootPath, $"Solution discovery skipped because root path is empty or missing: {rootPath}");
            ArgumentNullException.ThrowIfNull(_logger, $"Solution discovery skipped because logger is null.");

            LogMessages.StartingSolutionDiscovery(_logger, rootPath);
            var results = new List<SolutionDiscoveryEntry>();

            var fs = await _fsDiscoverer.DiscoverSolutionsAsync(rootPath, gitUrlsPath, tfvcUrlsPath);
            if (fs is not null)
                results.AddRange(fs);
            LogMessages.FileSystemDiscoveryCompleted(_logger, results.Count);

            var gh = await _ghDiscoverer.DiscoverSolutionsAsync(rootPath, gitUrlsPath, tfvcUrlsPath);
            if (gh is not null)
                results.AddRange(gh);
           LogMessages.GitHubDiscoveryCompleted(_logger, results.Count);

            if(!string.IsNullOrWhiteSpace(gitUrlsPath))
            {
                LogMessages.GitUrlsFileFound(_logger, gitUrlsPath);
                var git = await _gitDiscoverer.DiscoverSolutionsAsync(rootPath, gitUrlsPath, tfvcUrlsPath);
                if (git is not null)
                    results.AddRange(git);
                LogMessages.GitDiscoveryCompleted(_logger, results.Count);
            }
            else
            {
                LogMessages.GitUrlsFileNotFound(_logger, gitUrlsPath!);
            }

            if(!string.IsNullOrWhiteSpace(tfvcUrlsPath))
            {
                LogMessages.TfvcUrlsFileFound(_logger, tfvcUrlsPath);
                var tfvc = await _tfvcDiscoverer.DiscoverSolutionsAsync(rootPath, gitUrlsPath, tfvcUrlsPath);
                if (tfvc is not null)
                    results.AddRange(tfvc);
                LogMessages.TfvcDiscoveryCompleted(_logger, results.Count);
            }
            else
            {
                LogMessages.TfvcUrlsFileNotFound(_logger, tfvcUrlsPath!);
            }

            return results;
        }
    }
}
