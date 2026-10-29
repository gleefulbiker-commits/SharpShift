namespace SharpShift.Inventory.Reporting;

internal sealed class PdfReportWriter : IReportWriter
{
    public ReportFormat Format => ReportFormat.Pdf;

    public Task<IReadOnlyList<string>> WriteCombinedAsync(ReportData data, ReportOptions options, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("PDF writer is not implemented yet; use Markdown format for now.");
    }

    public Task<IReadOnlyList<string>> WritePerSolutionAsync(ReportData data, ReportOptions options, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("PDF writer is not implemented yet; use Markdown format for now.");
    }
}
