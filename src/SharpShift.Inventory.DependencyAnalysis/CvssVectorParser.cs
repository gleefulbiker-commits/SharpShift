using System;
using System.Collections.Generic;
using System.Text;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.DependencyAnalysis
{
    public static class CvssVectorParser
    {
        private static readonly HashSet<string> RequiredMetrics =
        [
            "AV",
        "AC",
        "PR",
        "UI",
        "S",
        "C",
        "I",
        "A"
        ];

        public static CvssRating Parse(string vector)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(vector);

            var segments = vector.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

            if (segments.Length < 9)
            {
                throw new FormatException(
                    "Invalid CVSS vector. Missing metrics.");
            }

            var versionSegment = segments[0];

            if (!versionSegment.StartsWith("CVSS:",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException(
                    "CVSS vector must begin with 'CVSS:'.");
            }

            var version = versionSegment["CVSS:".Length..];

            if (version != "3.1")
            {
                throw new NotSupportedException(
                    $"CVSS version '{version}' is not supported.");
            }

            var metrics = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var segment in segments.Skip(1))
            {
                var parts = segment.Split(':');

                if (parts.Length != 2)
                {
                    throw new FormatException(
                        $"Invalid metric segment '{segment}'.");
                }

                metrics[parts[0]] = parts[1];
            }

            var missing = RequiredMetrics
                .Where(x => !metrics.ContainsKey(x))
                .ToArray();

            if (missing.Length > 0)
            {
                throw new FormatException(
                    $"Missing required metrics: {string.Join(", ", missing)}");
            }

            var cvssVector = new CvssVector(
                version,
                ParseAttackVector(metrics["AV"]),
                ParseAttackComplexity(metrics["AC"]),
                ParsePrivilegesRequired(metrics["PR"]),
                ParseUserInteraction(metrics["UI"]),
                ParseScope(metrics["S"]),
                ParseImpact(metrics["C"]),
                ParseImpact(metrics["I"]),
                ParseImpact(metrics["A"]));

            var baseScore = new CvssCalculator().CalculateBaseScore(
                ParseAttackVector(metrics["AV"]),
                ParseAttackComplexity(metrics["AC"]),
                ParsePrivilegesRequired(metrics["PR"]),
                ParseUserInteraction(metrics["UI"]),
                ParseScope(metrics["S"]),
                ParseImpact(metrics["C"]),
                ParseImpact(metrics["I"]),
                ParseImpact(metrics["A"]));
            return new CvssRating(vector, baseScore, vector, cvssVector);
        }

        private static AttackVector ParseAttackVector(string value)
            => value switch
            {
                "N" => AttackVector.Network,
                "A" => AttackVector.Adjacent,
                "L" => AttackVector.Local,
                "P" => AttackVector.Physical,
                _ => throw InvalidMetric("AV", value)
            };

        private static AttackComplexity ParseAttackComplexity(string value)
            => value switch
            {
                "L" => AttackComplexity.Low,
                "H" => AttackComplexity.High,
                _ => throw InvalidMetric("AC", value)
            };

        private static PrivilegesRequired ParsePrivilegesRequired(string value)
            => value switch
            {
                "N" => PrivilegesRequired.None,
                "L" => PrivilegesRequired.Low,
                "H" => PrivilegesRequired.High,
                _ => throw InvalidMetric("PR", value)
            };

        private static UserInteraction ParseUserInteraction(string value)
            => value switch
            {
                "N" => UserInteraction.None,
                "R" => UserInteraction.Required,
                _ => throw InvalidMetric("UI", value)
            };

        private static Scope ParseScope(string value)
            => value switch
            {
                "U" => Scope.Unchanged,
                "C" => Scope.Changed,
                _ => throw InvalidMetric("S", value)
            };

        private static Impact ParseImpact(string value)
            => value switch
            {
                "N" => Impact.None,
                "L" => Impact.Low,
                "H" => Impact.High,
                _ => throw InvalidMetric("CIA", value)
            };

        private static FormatException InvalidMetric(
            string metric,
            string value)
            =>
                new(
                    $"Metric '{metric}' contains invalid value '{value}'.");
    }
}
