namespace SharpShift.Inventory.Reporting;

public interface IReportWriter
{
    ReportFormat Format { get; }

    Task<IReadOnlyList<string>> WriteCombinedAsync(ReportData data, ReportOptions options, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> WritePerSolutionAsync(ReportData data, ReportOptions options, CancellationToken cancellationToken = default);
}
