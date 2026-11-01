using System;
using System.Collections.Generic;
using System.Text;

namespace SharpShift.Inventory.Core.Models
{
    public sealed record CvssRating(
        string Vector,
        double BaseScore,
        string Severity,
        CvssVector ParsedVector);
}
