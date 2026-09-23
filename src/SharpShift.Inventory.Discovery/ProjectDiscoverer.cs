using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Discovery
{
    /// <summary>
    /// Discovers project files (.csproj) for a given solution or folder.
    /// </summary>
    public class ProjectDiscoverer : IProjectDiscoverer
    {
        /// <inheritdoc />
        public Task<IEnumerable<ProjectContext>> DiscoverProjectsAsync(string solutionOrFolderPath)
        {
            if (string.IsNullOrWhiteSpace(solutionOrFolderPath))
                return Task.FromResult(Enumerable.Empty<ProjectContext>());

            var result = new List<ProjectContext>();

            string root = solutionOrFolderPath;
            string? solutionPath = null;

            if (File.Exists(solutionOrFolderPath) && solutionOrFolderPath.EndsWith(".sln", System.StringComparison.OrdinalIgnoreCase))
            {
                solutionPath = solutionOrFolderPath;
                root = Path.GetDirectoryName(solutionOrFolderPath) ?? solutionOrFolderPath;
            }

            if (!Directory.Exists(root) && !File.Exists(root))
                return Task.FromResult((IEnumerable<ProjectContext>)result);

            var csprojFiles = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories);
            foreach (var f in csprojFiles)
            {
                result.Add(new ProjectContext
                {
                    ProjectPath = f,
                    ProjectName = Path.GetFileNameWithoutExtension(f),
                    SolutionPath = solutionPath
                });
            }

            return Task.FromResult((IEnumerable<ProjectContext>)result);
        }
    }
}
