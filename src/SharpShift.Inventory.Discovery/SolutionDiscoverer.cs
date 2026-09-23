using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpShift.Inventory.Core.Interfaces;

namespace SharpShift.Inventory.Discovery
{
    /// <summary>
    /// Discovers solution files under a given root path.
    /// </summary>
    public class SolutionDiscoverer : ISolutionDiscoverer
    {
        /// <inheritdoc />
        public Task<IEnumerable<string>> DiscoverSolutionsAsync(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                return Task.FromResult(Enumerable.Empty<string>());

            var files = Directory.EnumerateFiles(rootPath, "*.sln", SearchOption.AllDirectories);
            return Task.FromResult(files);
        }
    }
}
