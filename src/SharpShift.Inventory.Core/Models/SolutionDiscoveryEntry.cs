using System;

namespace SharpShift.Inventory.Core.Models
{
    /// <summary>
    /// Represents a discovered solution or repository entry from a discovery source.
    /// </summary>
    public class SolutionDiscoveryEntry
    {
        /// <summary>
        /// Source of the discovery (e.g., "FileSystem", "GitHub").
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Local file system path to a .sln or .slnx file when Source == "FileSystem".
        /// </summary>
        public string? LocalPath { get; set; }

        /// <summary>
        /// Repository web URL (e.g., https://github.com/owner/repo) when Source == "GitHub".
        /// </summary>
        public string? RepoUrl { get; set; }

        /// <summary>
        /// Owner (user or organization) for remote repositories.
        /// </summary>
        public string? Owner { get; set; }

        /// <summary>
        /// Repository name for remote repositories.
        /// </summary>
        public string? RepoName { get; set; }

        /// <summary>
        /// True when the repository is archived on the remote provider.
        /// </summary>
        public bool IsArchived { get; set; }
    }
}
