using System.Collections.Generic;

namespace SharpShift.Inventory.Core.Models
{
    /// <summary>
    /// Represents the inventory for a discovered solution.
    /// </summary>
    public class SolutionInventory
    {
        /// <summary>
        /// Logical solution name (file name without extension) or root folder name.
        /// </summary>
        public string SolutionName { get; set; } = string.Empty;

        /// <summary>
        /// This contains the fully qualified path for the solution file.
        /// </summary>
        public string SolutionPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URL of the repository associated with this item.
        /// </summary>
        public string RepositoryUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the repository is archived.
        /// </summary>
        public bool IsArchived { get; set; } = false;

        /// <summary>
        /// Projects discovered in the solution or folder.
        /// </summary>
        public List<ProjectInventory> Projects { get; set; } = [];

        /// <summary>
        /// Simple summary metrics, e.g. project count.
        /// </summary>
        public Dictionary<string, object> Summary { get; set; } = [];
    }
}
