using System;
using System.Collections.Generic;
using System.Text;

namespace SharpShift.Inventory.Core.Models
{
    public static class ReportConstants
    {
        /// <summary>
        /// Weights assigned to risk items when calculating risk scores.
        /// </summary>
        public const int CriticalRiskWeight = 10;
        public const int HighRiskWeight = 6;
        public const int MediumRiskWeight = 3;
        public const int LowRiskWeight = 1;

        public const double FrameworkCurrencyWeight = 0.25;
        public const double PackageHealthWeight = 0.20;
        public const double SecurityPostureWeight = 0.25;
        public const double DependencyHygieneWeight = 0.15;
        public const double RepositoryModernityWeight = 0.15;

        public const int DeprecatedPackageWeight = 2;
        public const int OutOfSupportFrameworkWeight = 3;
        public const int LegacyProjectFileWeight = 2;
        public const int NotInVersionControlWeight = 1;
        public const int MultiVersionDependencyWeight = 2;
        public const int NonSdkStyleProjectWeight = 2;
        public const int ArchivedRepositoryWeight = 1;
        public const int NoRepositoryUrlWeight = 1;
        public const int NonModernProjectFileWeight = 2;
        public const int NonModernRepositoryWeight = 1;
        public const int NonModernDependencyWeight = 1;
        public const int NonModernPackageWeight = 1;
        public const int NonModernFrameworkWeight = 1;
        public const int NonModernSecurityWeight = 1;


        /// <summary>
        /// These are the risk scores at or above which a condition is considered critical, high, or medium.
        /// </summary>
        public const double CriticalRiskThreshold = 8.0;
        public const double HighRiskThreshold = 5.0;
        public const double MediumRiskThreshold = 2.0;

        public const int RiskSeverityCriticalPenalty = 15;
        public const int RiskSeverityHighPenalty = 10;
        public const int RiskSeverityMediumPenalty = 6;
        public const int RiskSeverityLowPenalty = 3;
    }
}
