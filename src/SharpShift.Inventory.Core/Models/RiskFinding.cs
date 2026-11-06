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

        /// <summary>
        /// Optional dependency package identifier associated with the finding.
        /// </summary>
        public string? PackageId { get; set; }

        /// <summary>
        /// Optional dependency package version associated with the finding.
        /// </summary>
        public string? PackageVersion { get; set; }

        /// <summary>
        /// Optional advisory identifier (e.g., CVE, GHSA) for vulnerability findings.
        /// </summary>
        public string? AdvisoryId { get; set; }

        /// <summary>
        /// Optional CVSS base score for vulnerability findings.
        /// </summary>
        public double? CvssScore { get; set; }

        /// <summary>
        /// Optional version hint or remediated target version.
        /// </summary>
        public string? RecommendedVersion { get; set; }
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
