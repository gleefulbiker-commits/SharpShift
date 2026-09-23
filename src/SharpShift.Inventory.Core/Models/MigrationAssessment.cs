using System;

namespace SharpShift.Inventory.Core.Models
{
    /// <summary>
    /// Represents an overall migration assessment for a project.
    /// </summary>
    public class MigrationAssessment
    {
        /// <summary>
        /// Relative migration complexity: Low, Medium, High.
        /// </summary>
        public MigrationComplexity Complexity { get; set; } = MigrationComplexity.Unknown;

        /// <summary>
        /// Numeric risk score used to derive the Complexity value.
        /// </summary>
        public int? RiskScore { get; set; }

        /// <summary>
        /// Estimated effort in weeks to modernize the project (rough estimate).
        /// </summary>
        public int? EstimatedEffortWeeks { get; set; }

        /// <summary>
        /// Short summary or recommendations for migration.
        /// </summary>
        public string? Summary { get; set; }
    }

    /// <summary>
    /// Enumeration of migration complexity levels.
    /// </summary>
    public enum MigrationComplexity
    {
        Unknown = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }
}
