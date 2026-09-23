using System;

namespace SharpShift.Inventory.Core.Models
{
    /// <summary>
    /// Represents an individual risk finding discovered during analysis.
    /// </summary>
    public class RiskFinding
    {
        /// <summary>
        /// High-level category for the finding (e.g., "WCF", "COM", "Reflection").
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Severity score or level for the finding.
        /// </summary>
        public RiskSeverity Severity { get; set; } = RiskSeverity.Medium;

        /// <summary>
        /// Human-readable description of the risk.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Optional contextual evidence or location (file, line, symbol).
        /// </summary>
        public string? Evidence { get; set; }
    }

    /// <summary>
    /// Severity enumeration for risk findings.
    /// </summary>
    public enum RiskSeverity
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }
}
