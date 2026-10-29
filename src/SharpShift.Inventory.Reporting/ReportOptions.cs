namespace SharpShift.Inventory.Reporting;

public sealed class ReportOptions
{
    public string InputDirectory { get; set; } = string.Empty;

    public string InventoryFileName { get; set; } = "inventory.json";

    public string AnalysisFileName { get; set; } = "analysis.json";

    public string OutputDirectory { get; set; } = string.Empty;

    public string BaseFileName { get; set; } = "report";

    public bool GenerateCombined { get; set; } = true;

    public bool GeneratePerSolution { get; set; } = true;

    public ReportFormat Format { get; set; } = ReportFormat.Markdown;
}
