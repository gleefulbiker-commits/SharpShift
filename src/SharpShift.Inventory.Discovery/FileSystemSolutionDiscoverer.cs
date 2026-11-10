using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;
using SharpShift.LoggerMessages;

namespace SharpShift.Inventory.Discovery
{
    /// <summary>
    /// Discovers solution files on the local file system (*.sln and *.slnx).
    /// </summary>
    public class FileSystemSolutionDiscoverer(ILogger? logger) : ISolutionDiscoverer
    {
        public Task<IEnumerable<SolutionDiscoveryEntry>> DiscoverSolutionsAsync(string? rootPath, string? gitUrlsPath)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(rootPath, $"File system discovery skipped because root path is empty or missing: {rootPath}");
            ArgumentNullException.ThrowIfNull(logger, $"File system discovery skipped because logger is null.");

            var slnFiles = Directory.EnumerateFiles(rootPath, "*.sln", SearchOption.AllDirectories);
            var slnxFiles = Directory.EnumerateFiles(rootPath, "*.slnx", SearchOption.AllDirectories);
            var files = slnFiles.Concat(slnxFiles).Distinct();

            var entries = files.Select(p => new SolutionDiscoveryEntry { Source = "FileSystem", LocalPath = p });
            var count = entries.Count();
            LogMessages.DiscoveredLocalSolutionFiles(logger, count, rootPath);
            return Task.FromResult<IEnumerable<SolutionDiscoveryEntry>>(entries);
        }
    }
}
