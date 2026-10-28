using System;
using System.Collections.Generic;
using System.Text;

namespace SharpShift.Inventory.Core.Models
{
    public class DependencySummary
    {
        /// <summary>
        /// Friendly name of the dependency (e.g., "Newtonsoft.Json").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Type of the dependency (e.g., "ProjectReference", "PackageReference", "FrameworkReference").
        /// </summary>
        public string DependencyType { get; set; } = string.Empty;
        /// <summary>
        /// Version string reported by the project file or package metadata.
        /// </summary>
        public Dictionary<string, int>? Version { get; set; }

        /// <summary>
        /// Source of the dependency (e.g., "NuGet", "GAC", "COM", "SDK").
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// HintPath for assembly references when available.
        /// </summary>
        public string? HintPath { get; set; }

        /// <summary>
        /// Gets or sets the total number of instances.
        /// </summary>
        public int TotalInstances { get; set; } = 0;

        public Dictionary<string, string>? OtherMetadata { get; set; }
    }
}
