using System.Collections.Generic;
using System.Threading.Tasks;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Core.Interfaces
{
    /// <summary>
    /// Discovers solutions under a given root path.
    /// </summary>
    public interface ISolutionDiscoverer
    {
        /// <summary>
        /// Enumerates solution file paths under the provided root path.
        /// </summary>
        /// <param name="rootPath">Root folder to search.</param>
        /// <returns>List of solution file paths.</returns>
        Task<IEnumerable<string>> DiscoverSolutionsAsync(string rootPath);
    }
}
