using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using SharpShift.Inventory.Core.Models;
using SharpShift.Inventory.Core.Utilities;

namespace SharpShift.Inventory.Reporting;

/// <summary>
/// Generates reports from inventory and analysis results.
/// </summary>
public class ReportGenerator
{
    private readonly Dictionary<ReportFormat, IReportWriter> _writers;

    public ReportGenerator(IEnumerable<IReportWriter>? writers = null)
    {
        var registeredWriters = writers?.ToArray()
            ??
            [
                new MarkdownReportWriter(),
                new PdfReportWriter()
            ];

        _writers = registeredWriters.ToDictionary(w => w.Format, w => w);
    }

    public async Task<ReportGenerationResult> GenerateAsync(ReportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var reportData = await ReportDataUtils.CreateReportData(options, cancellationToken);
        var outputDirectory = string.IsNullOrWhiteSpace(options.OutputDirectory)
            ? (string.IsNullOrWhiteSpace(options.InputDirectory) ? Directory.GetCurrentDirectory() : options.InputDirectory)
            : options.OutputDirectory;

        Directory.CreateDirectory(outputDirectory);
        options.OutputDirectory = outputDirectory;

        if (!_writers.TryGetValue(options.Format, out var writer))
            throw new NotSupportedException($"Unsupported report format: {options.Format}");

        var result = new ReportGenerationResult
        {
            Format = options.Format
        };

        if (reportData.Analysis is null)
            result.Warnings.Add($"Analysis file '{options.AnalysisFileName}' was not found or could not be loaded.");

        if (options.GenerateCombined)
        {
            var files = await writer.WriteCombinedAsync(reportData, options, cancellationToken);
            result.GeneratedFiles.AddRange(files);
        }

        if (options.GeneratePerSolution)
        {
            var files = await writer.WritePerSolutionAsync(reportData, options, cancellationToken);
            result.GeneratedFiles.AddRange(files);
        }

        return result;
    }
}
