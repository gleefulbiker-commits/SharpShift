using System.Collections.Generic;
using System.Threading.Tasks;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Core.Interfaces
{
    /// <summary>
    /// Discovers solutions under a given root path or external sources (e.g., GitHub).
    /// </summary>
    public interface ISolutionDiscoverer
    {
        /// <summary>
        /// Enumerates discovered solutions and repositories under the provided root path or configured sources.
        /// </summary>
        /// <param name="rootPath">Root folder to search (may be unused by remote discoverers).</param>
        /// <returns>List of discovery entries describing local solution files or remote repositories.</returns>
        Task<IEnumerable<SolutionDiscoveryEntry>> DiscoverSolutionsAsync(string? rootPath = null);
    }
}
