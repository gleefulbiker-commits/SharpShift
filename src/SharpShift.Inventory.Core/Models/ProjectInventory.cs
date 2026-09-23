using System.Collections.Generic;

namespace SharpShift.Inventory.Core.Models
{
    /// <summary>
    /// Represents an inventory item for a single project.
    /// </summary>
    public class ProjectInventory
    {
        /// <summary>
        /// Project file name or logical project name.
        /// </summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>
        /// Detected project type (e.g., "ASP.NET MVC", "Class Library").
        /// </summary>
        public string ProjectType { get; set; } = "Unknown";

        /// <summary>
        /// Normalized framework string (e.g., ".NET Framework 4.6.2").
        /// </summary>
        public string Framework { get; set; } = string.Empty;

        /// <summary>
        /// Path to the project file discovered on disk.
        /// </summary>
        public string ProjectPath { get; set; } = string.Empty;

        /// <summary>
        /// Additional metadata for the project.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Discovered dependencies for the project (if analyzers run or evaluator provided items).
        /// This is included in the serialized inventory output so dependency details (HintPath, PublicKeyToken, OtherMetadata) are persisted.
        /// </summary>
        public List<DependencyInventory> Dependencies { get; set; } = new();
    }
}
