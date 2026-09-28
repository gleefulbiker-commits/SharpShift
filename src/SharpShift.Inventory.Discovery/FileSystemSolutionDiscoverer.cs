using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Discovery
{
    /// <summary>
    /// Discovers solution files on the local file system (*.sln and *.slnx).
    /// </summary>
    public class FileSystemSolutionDiscoverer : ISolutionDiscoverer
    {
        public Task<IEnumerable<SolutionDiscoveryEntry>> DiscoverSolutionsAsync(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                return Task.FromResult<IEnumerable<SolutionDiscoveryEntry>>(Enumerable.Empty<SolutionDiscoveryEntry>());

            var slnFiles = Directory.EnumerateFiles(rootPath, "*.sln", SearchOption.AllDirectories);
            var slnxFiles = Directory.EnumerateFiles(rootPath, "*.slnx", SearchOption.AllDirectories);
            var files = slnFiles.Concat(slnxFiles).Distinct();

            var entries = files.Select(p => new SolutionDiscoveryEntry { Source = "FileSystem", LocalPath = p });
            return Task.FromResult<IEnumerable<SolutionDiscoveryEntry>>(entries);
        }
    }
}
