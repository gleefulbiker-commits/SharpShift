using System.Collections.Generic;

namespace SharpShift.Inventory.Core.Models
{
    /// <summary>
    /// Result returned by a project analyzer containing detected attributes.
    /// </summary>
    public class ProjectAnalysisResult
    {
        /// <summary>
        /// Detected project type (e.g., "ASP.NET MVC", "WCF").
        /// </summary>
        public string ProjectType { get; set; } = "Unknown";

        /// <summary>
        /// Normalized framework string (e.g., ".NET Framework 4.6.2").
        /// </summary>
        public string? Framework { get; set; }

        /// <summary>
        /// Optional lines of code estimate.
        /// </summary>
        public int? LinesOfCode { get; set; }

        /// <summary>
        /// Discovered dependencies for the project.
        /// </summary>
        public List<DependencyInventory> Dependencies { get; set; } = new();

        /// <summary>
        /// Risk findings associated with this project.
        /// </summary>
        public List<RiskFinding> RiskFindings { get; set; } = new();
    }
}
