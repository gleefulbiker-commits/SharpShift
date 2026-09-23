using System;
using System.Collections.Generic;

namespace SharpShift.Inventory.Core.Models
{
    /// <summary>
    /// Represents a dependency referenced by a project (NuGet, GAC, COM, SDK, etc.).
    /// </summary>
    public class DependencyInventory
    {
        /// <summary>
        /// Friendly name of the dependency (e.g., "Newtonsoft.Json").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Version string reported by the project file or package metadata.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Source of the dependency (e.g., "NuGet", "GAC", "COM", "SDK").
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Additional information or metadata (public key token, hint path, etc.).
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// HintPath for assembly references when available.
        /// </summary>
        public string? HintPath { get; set; }

        /// <summary>
        /// Public key token when available (from reference include or metadata).
        /// </summary>
        public string? PublicKeyToken { get; set; }

        /// <summary>
        /// Remaining metadata entries captured as key/value pairs.
        /// </summary>
        public Dictionary<string, string>? OtherMetadata { get; set; }
    }
}
