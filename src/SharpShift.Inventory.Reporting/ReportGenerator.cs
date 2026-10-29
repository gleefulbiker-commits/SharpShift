using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Reporting;

/// <summary>
/// Generates reports from inventory and analysis results.
/// </summary>
public class ReportGenerator
{
    private readonly Dictionary<ReportFormat, IReportWriter> _writers;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

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

    /// <summary>
    /// Loads inventory and analysis data from JSON files.
    /// </summary>
    /// <param name="options">Report options containing input file details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Loaded report data.</returns>
    public async Task<ReportData> LoadDataFromJsonAsync(ReportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var inputDirectory = string.IsNullOrWhiteSpace(options.InputDirectory)
            ? Directory.GetCurrentDirectory()
            : options.InputDirectory;

        if (!Directory.Exists(inputDirectory))
            throw new DirectoryNotFoundException($"Input directory not found: {inputDirectory}");

        var inventoryFile = Path.Combine(inputDirectory, options.InventoryFileName);
        if (!File.Exists(inventoryFile))
            throw new FileNotFoundException("Inventory file not found.", inventoryFile);

        await using var inventoryStream = File.OpenRead(inventoryFile);
        var solutions = await JsonSerializer.DeserializeAsync<List<SolutionInventory>>(inventoryStream, JsonOptions, cancellationToken)
            ?? [];

        var analysisFile = Path.Combine(inputDirectory, options.AnalysisFileName);
        ProjectAnalysisResult? analysis = null;
        if (File.Exists(analysisFile))
        {
            await using var analysisStream = File.OpenRead(analysisFile);
            analysis = await JsonSerializer.DeserializeAsync<ProjectAnalysisResult>(analysisStream, JsonOptions, cancellationToken);
        }

        return new ReportData
        {
            Solutions = solutions,
            Analysis = analysis
        };
    }

    public async Task<ReportGenerationResult> GenerateAsync(ReportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var reportData = await LoadDataFromJsonAsync(options, cancellationToken);
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
