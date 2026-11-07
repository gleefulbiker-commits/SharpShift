using System.Collections.Generic;

namespace SharpShift.Inventory.Core.Models
{
    /// <summary>
    /// Represents an inventory item for a single project.
    /// </summary>
    public class ProjectInventory
    {
        private static readonly string[] _legacyFrameworkVersions = [
            ".NET Framework",
            ".NET Core 3.1",
            ".NET Standard"
        ];

        /// <summary>
        /// Detected project ID.
        /// </summary>
        public string ProjectId { get; set; } = string.Empty;

        /// <summary>
        /// Project file name or logical project name.
        /// </summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>
        /// Detected project type ID.
        /// </summary>
        public string ProjectTypeId { get; set; } = string.Empty;

        /// <summary>
        /// Detected project type (e.g., "ASP.NET MVC", "Class Library").
        /// </summary>
        public string ProjectType { get; set; } = "Class Library";

        /// <summary>
        /// Normalized framework string (e.g., ".NET Framework 4.6.2").
        /// </summary>
        public string Framework { get; set; } = string.Empty;

        public string? FrameworkVersion { get; set; }

        public bool IsSdkStyle { get; set; } = false;

        public bool IsLegacyProject
        {
            get
            {
                if (IsSdkStyle)
                {
                    return false;
                }

                if (_legacyFrameworkVersions.Contains(Framework))
                {
                    //return true;
                }

                if (string.IsNullOrEmpty(Framework))
                {
                    return true;
                }

                if (Metadata.TryGetValue("HasPackagesConfig", out var hasPackagesConfigObj) &&
                    hasPackagesConfigObj is bool hasPackagesConfig &&
                    hasPackagesConfig)
                {
                    return true;
                }

                if (Metadata.TryGetValue("HasWebConfig", out var hasWebConfigObj) &&
                    hasWebConfigObj is bool hasWebConfig &&
                    hasWebConfig)
                {
                    return true;
                }

                if (Metadata.TryGetValue("HasAppConfig", out var hasAppConfigObj) &&
                    hasAppConfigObj is bool hasAppConfig &&
                    hasAppConfig)
                {
                    return true;
                }   

                return false;
            }
        }

        /// <summary>
        /// Path to the project file discovered on disk.
        /// </summary>
        public string ProjectPath { get; set; } = string.Empty;

        /// <summary>
        /// Additional metadata for the project.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = [];

        /// <summary>
        /// Discovered dependencies for the project (if analyzers run or evaluator provided items).
        /// This is included in the serialized inventory output so dependency details (HintPath, PublicKeyToken, OtherMetadata) are persisted.
        /// </summary>
        public List<DependencyInventory> Dependencies { get; set; } = [];

        /// <summary>
        /// Local path when project was discovered via cloning (GitHub discovery clones).
        /// </summary>
        public string? LocalPath { get; set; }
    }
}
