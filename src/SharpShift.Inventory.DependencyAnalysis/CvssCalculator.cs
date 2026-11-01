using SharpShift.Inventory.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharpShift.Inventory.DependencyAnalysis
{
    public sealed class CvssCalculator
    {
        public double CalculateBaseScore(
            AttackVector av,
            AttackComplexity ac,
            PrivilegesRequired pr,
            UserInteraction ui,
            Scope scope,
            Impact c,
            Impact i,
            Impact a)
        {
            double avValue = av switch
            {
                AttackVector.Network => 0.85,
                AttackVector.Adjacent => 0.62,
                AttackVector.Local => 0.55,
                AttackVector.Physical => 0.20,
                _ => throw new ArgumentOutOfRangeException()
            };

            double acValue = ac switch
            {
                AttackComplexity.Low => 0.77,
                AttackComplexity.High => 0.44,
                _ => throw new ArgumentOutOfRangeException()
            };

            double uiValue = ui switch
            {
                UserInteraction.None => 0.85,
                UserInteraction.Required => 0.62,
                _ => throw new ArgumentOutOfRangeException()
            };

            double prValue = (scope, pr) switch
            {
                (_, PrivilegesRequired.None) => 0.85,

                (Scope.Unchanged, PrivilegesRequired.Low) => 0.62,
                (Scope.Unchanged, PrivilegesRequired.High) => 0.27,

                (Scope.Changed, PrivilegesRequired.Low) => 0.68,
                (Scope.Changed, PrivilegesRequired.High) => 0.50,

                _ => throw new ArgumentOutOfRangeException()
            };

            double Confidentiality(Impact x) => x switch
            {
                Impact.None => 0.00,
                Impact.Low => 0.22,
                Impact.High => 0.56,
                _ => throw new ArgumentOutOfRangeException()
            };

            double cValue = Confidentiality(c);
            double iValue = Confidentiality(i);
            double aValue = Confidentiality(a);

            var iss = 1 -
                      ((1 - cValue) *
                       (1 - iValue) *
                       (1 - aValue));

            double impact = scope switch
            {
                Scope.Unchanged =>
                    6.42 * iss,

                Scope.Changed =>
                    7.52 * (iss - 0.029)
                    - 3.25 * Math.Pow(iss - 0.02, 15),

                _ => throw new ArgumentOutOfRangeException()
            };

            if (impact <= 0)
                return 0;

            var exploitability =
                8.22 *
                avValue *
                acValue *
                prValue *
                uiValue;

            double score = scope switch
            {
                Scope.Unchanged =>
                    Math.Min(impact + exploitability, 10),

                Scope.Changed =>
                    Math.Min(1.08 * (impact + exploitability), 10),

                _ => throw new ArgumentOutOfRangeException()
            };

            return RoundUp(score);
        }

        private static double RoundUp(double value)
            => Math.Ceiling(value * 10) / 10.0;
    }
}
