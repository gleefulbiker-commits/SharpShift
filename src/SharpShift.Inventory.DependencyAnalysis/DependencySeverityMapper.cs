using SharpShift.Inventory.Core.Models;
using System;

namespace SharpShift.Inventory.DependencyAnalysis;

public static class DependencySeverityMapper
{
    public static RiskSeverity FromCvss(double? score)
    {
        if (!score.HasValue)
            return RiskSeverity.Medium;

        return score.Value switch
        {
            >= 9.0 => RiskSeverity.Critical,
            >= 7.0 => RiskSeverity.High,
            >= 4.0 => RiskSeverity.Medium,
            _ => RiskSeverity.Low
        };
    }

    public static RiskSeverity FromString(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity))
            return RiskSeverity.Medium;

        return severity.Trim().ToUpperInvariant() switch
        {
            "CRITICAL" => RiskSeverity.Critical,
            "HIGH" => RiskSeverity.High,
            "MODERATE" => RiskSeverity.Medium,
            "MEDIUM" => RiskSeverity.Medium,
            "LOW" => RiskSeverity.Low,
            _ => RiskSeverity.Medium
        };
    }
}
