using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Discovery
{
    /// <summary>
    /// Discovers solution files under a given root path.
    /// </summary>
    public class SolutionDiscoverer : ISolutionDiscoverer
    {
        private readonly ISolutionDiscoverer _fsDiscoverer = new FileSystemSolutionDiscoverer();
        private readonly ISolutionDiscoverer _ghDiscoverer;

        public SolutionDiscoverer()
        {
            // Create GitHub discoverer with default HttpClient.
            var http = new System.Net.Http.HttpClient();
            _ghDiscoverer = new GitHubSolutionDiscoverer(http);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<SolutionDiscoveryEntry>> DiscoverSolutionsAsync(string rootPath)
        {
            var results = new List<SolutionDiscoveryEntry>();

            var fs = await _fsDiscoverer.DiscoverSolutionsAsync(rootPath);
            if (fs != null)
                results.AddRange(fs);

            var gh = await _ghDiscoverer.DiscoverSolutionsAsync(rootPath);
            if (gh != null)
                results.AddRange(gh);

            return results;
        }
    }
}
