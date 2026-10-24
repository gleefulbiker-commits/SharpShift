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
    /// Discovers solution files on the local file system (*.sln and *.slnx).
    /// </summary>
    public class FileSystemSolutionDiscoverer(ILogger<FileSystemSolutionDiscoverer>? logger = null) : ISolutionDiscoverer
    {
        public Task<IEnumerable<SolutionDiscoveryEntry>> DiscoverSolutionsAsync(string? rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                logger?.LogWarning("File system discovery skipped because root path is empty or missing: {RootPath}", rootPath);
                return Task.FromResult<IEnumerable<SolutionDiscoveryEntry>>([]);
            }

            var slnFiles = Directory.EnumerateFiles(rootPath, "*.sln", SearchOption.AllDirectories);
            var slnxFiles = Directory.EnumerateFiles(rootPath, "*.slnx", SearchOption.AllDirectories);
            var files = slnFiles.Concat(slnxFiles).Distinct();

            var entries = files.Select(p => new SolutionDiscoveryEntry { Source = "FileSystem", LocalPath = p });
            logger?.LogInformation("Discovered {SolutionCount} local solution files under {RootPath}", entries.Count(), rootPath);
            return Task.FromResult<IEnumerable<SolutionDiscoveryEntry>>(entries);
        }
    }
}
