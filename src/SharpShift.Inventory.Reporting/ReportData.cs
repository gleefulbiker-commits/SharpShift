using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Reporting;

public sealed class ReportData
{
    public IReadOnlyList<SolutionInventory> Solutions { get; init; } = [];

    public ProjectAnalysisResult? Analysis { get; init; }
}
