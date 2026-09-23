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
        /// Projects discovered in the solution or folder.
        /// </summary>
        public List<ProjectInventory> Projects { get; set; } = new();

        /// <summary>
        /// Simple summary metrics, e.g. project count.
        /// </summary>
        public Dictionary<string, object> Summary { get; set; } = new();
    }
}
