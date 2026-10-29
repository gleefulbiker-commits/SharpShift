namespace SharpShift.Inventory.Reporting;

public sealed class ReportGenerationResult
{
    public ReportFormat Format { get; init; } = ReportFormat.Markdown;

    public List<string> GeneratedFiles { get; } = [];

    public List<string> Warnings { get; } = [];
}
