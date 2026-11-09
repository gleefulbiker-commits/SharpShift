namespace SharpShift.Inventory.Core.Models
{
    public sealed class ReportData
    {
        public IReadOnlyList<SolutionInventory> Solutions { get; init; } = [];

        public ProjectAnalysisResult? Analysis { get; init; }
    }
}
