using System.Collections.Generic;
using System.Threading.Tasks;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Core.Interfaces
{
    /// <summary>
    /// Discovers projects for a given solution or folder.
    /// </summary>
    public interface IProjectDiscoverer
    {
        /// <summary>
        /// Enumerates discovered project contexts under the provided path.
        /// </summary>
        Task<IEnumerable<ProjectContext>> DiscoverProjectsAsync(string solutionOrFolderPath);
    }
}
