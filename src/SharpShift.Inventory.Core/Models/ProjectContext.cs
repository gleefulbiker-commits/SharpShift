using System;

namespace SharpShift.Inventory.Core.Models
{
    /// <summary>
    /// Context information about a discovered project used by analyzers.
    /// </summary>
    public class ProjectContext
    {
        /// <summary>
        /// Full path to the project file (.csproj).
        /// </summary>
        public string ProjectPath { get; set; } = string.Empty;

        /// <summary>
        /// Optional path to the parent solution file (.sln) if discovered in the context of a solution.
        /// </summary>
        public string? SolutionPath { get; set; }

        /// <summary>
        /// Project name (file name without extension) as a convenience.
        /// </summary>
        public string ProjectName { get; set; } = string.Empty;
    }
}
