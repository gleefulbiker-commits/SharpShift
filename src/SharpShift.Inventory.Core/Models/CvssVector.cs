using System;
using System.Collections.Generic;
using System.Text;

namespace SharpShift.Inventory.Core.Models
{
    public sealed record CvssVector(
        string Version,
        AttackVector AttackVector,
        AttackComplexity AttackComplexity,
        PrivilegesRequired PrivilegesRequired,
        UserInteraction UserInteraction,
        Scope Scope,
        Impact Confidentiality,
        Impact Integrity,
        Impact Availability);
}
