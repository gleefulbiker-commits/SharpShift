using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpShift.Inventory.Cli;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;
using SharpShift.Inventory.Discovery;
using SharpShift.Inventory.ProjectAnalysis;
using SharpShift.Inventory.Reporting;
using SharpShift.LoggerMessages;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace SharpShift
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                });
            });
            services.AddSingleton<ISolutionDiscoverer, SolutionDiscoverer>();

            using var provider = services.BuildServiceProvider();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("SharpShift.Program");

            try
            {
                if (args.Length < 2)
                {
                    logger.LogWarning("Insufficient arguments provided.");
                    Console.WriteLine("Usage: scan|analyze|report <path> [options]");
                    return 1;
                }

                var configuration = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true)
                    .Build();

                var defaultInventoryFilename = configuration["OutputFilename"] ?? "inventory.json";
                var defaultAnalysisFilename = configuration["AnalysisOutputFilename"] ?? "analysis.json";
                var command = args[0].ToLowerInvariant();

                var result = command switch
                {
                    "scan" => await RunScanOrAnalyze(args, defaultInventoryFilename, defaultAnalysisFilename, true, logger, provider, loggerFactory),
                    "analyze" => await RunScanOrAnalyze(args, defaultInventoryFilename, defaultAnalysisFilename, false, logger, provider, loggerFactory),
                    "report" => await GenerateReport(args, defaultInventoryFilename, defaultAnalysisFilename, logger),
                    _ => throw new InvalidOperationException($"Invalid command. '{args[0]}' is not a valid subcommand.")
                };

                return result;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Unhandled exception in program entrypoint.");
                return 3;
            }
        }

        private static async Task<int> RunScanOrAnalyze(
            string[] args,
            string defaultOutputFilename,
            string defaultAnalysisFilename,
            bool isScan,
            ILogger logger,
            IServiceProvider provider,
            ILoggerFactory loggerFactory)
        {
            var (path, output, analysisOutput, retryAttempts, clean, analyzeCloned) = ParseArguments(args, defaultOutputFilename, defaultAnalysisFilename);
            if (isScan)
                return await ScanRepositories(path, output, retryAttempts, clean, analyzeCloned, logger, provider, loggerFactory);

            return await AnalyzeEstate(path, output, analysisOutput, retryAttempts, clean, analyzeCloned, logger, provider, loggerFactory);
        }

        public static (
            string path,
            string output,
            string analysisOutput,
            int retryAttempts,
            bool clean,
            bool analyzeCloned) ParseArguments(string[] args, string defaultOutputFilename, string defaultAnalysisFilename)
        {
            var path = args[1];
            var output = defaultOutputFilename;
            var analysisOutput = defaultAnalysisFilename;
            var retryAttempts = 0;
            var clean = false;
            var analyzeCloned = false;

            for (int i = 2; i < args.Length; i++)
            {
                var token = args[i];

                Func<int> handler = token switch
                {
                    "--output" when i + 1 < args.Length => () => { output = args[i + 1]; return 1; },
                    "--retry" when i + 1 < args.Length => () => { if (int.TryParse(args[i + 1], out var rr)) retryAttempts = rr; return 1; },
                    "--clean" => () => { clean = true; return 0; },
                    "--analyze-cloned" => () => { analyzeCloned = true; return 0; },
                    _ => () => 0,
                };

                i += handler();
            }

            return (path, output, analysisOutput, retryAttempts, clean, analyzeCloned);
        }

        public static ReportOptions ParseReportArguments(string[] args, string defaultInventoryFilename, string defaultAnalysisFilename)
        {
            var options = new ReportOptions
            {
                InventoryFileName = defaultInventoryFilename,
                AnalysisFileName = defaultAnalysisFilename,
                BaseFileName = "report",
                Format = ReportFormat.Markdown,
                GenerateCombined = true,
                GeneratePerSolution = true
            };

            var combinedSpecified = false;
            var perSolutionSpecified = false;

            for (int i = 2; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--inventory" when i + 1 < args.Length:
                        options.InventoryFileName = args[++i];
                        break;
                    case "--analysis" when i + 1 < args.Length:
                        options.AnalysisFileName = args[++i];
                        break;
                    case "--format" when i + 1 < args.Length:
                        var formatValue = args[++i];
                        options.Format = formatValue.Equals("pdf", StringComparison.OrdinalIgnoreCase)
                            ? ReportFormat.Pdf
                            : ReportFormat.Markdown;
                        break;
                    case "--outdir" when i + 1 < args.Length:
                        options.OutputDirectory = args[++i];
                        break;
                    case "--basename" when i + 1 < args.Length:
                        options.BaseFileName = args[++i];
                        break;
                    case "--combined":
                        combinedSpecified = true;
                        options.GenerateCombined = true;
                        if (!perSolutionSpecified)
                            options.GeneratePerSolution = false;
                        break;
                    case "--per-solution":
                        perSolutionSpecified = true;
                        options.GeneratePerSolution = true;
                        if (!combinedSpecified)
                            options.GenerateCombined = false;
                        break;
                }
            }

            if (!combinedSpecified && !perSolutionSpecified)
            {
                options.GenerateCombined = true;
                options.GeneratePerSolution = true;
            }

            return options;
        }

        public static async Task<int> ScanRepositories(
            string path,
            string output,
            int retryAttempts,
            bool clean,
            bool analyzeCloned,
            ILogger logger,
            IServiceProvider provider,
            ILoggerFactory loggerFactory)
        {
            LogMessages.StartingSolutionScan(logger, path, output);

            var sol = provider.GetRequiredService<ISolutionDiscoverer>();
            var runnerLogger = loggerFactory.CreateLogger("SharpShift.Inventory.Cli.InventoryCliRunner");

            return await InventoryCliRunner.RunAsync(path, output, sol, retryAttempts, clean, analyzeCloned, runnerLogger);
        }

        public static async Task<int> AnalyzeEstate(
            string path,
            string output,
            string analysisOutput,
            int retryAttempts,
            bool clean,
            bool analyzeCloned,
            ILogger logger,
            IServiceProvider provider,
            ILoggerFactory loggerFactory)
        {
            var resolver = new DefaultJsonTypeInfoResolver();
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                TypeInfoResolver = resolver
            };

            LogMessages.StartingAnalysis(logger, path, output);
            var fullyqualifiedOutputPath = Path.Combine(path, output);
            var solutions = JsonSerializer.Deserialize<List<SolutionInventory>>(File.ReadAllText(fullyqualifiedOutputPath), options) ?? [];
            logger.LogInformation(LogMessages.StartingSolutionAnalysis);

            logger.LogInformation(LogMessages.StartingProjectAnalysis);
            var result = await ProjectAnalyzer.AnalyzeAsync(logger, solutions);

            logger.LogInformation(LogMessages.StartingDependencyAnalysis);

            var json = JsonSerializer.Serialize(result, options);
            var fullPath = Path.Combine(path, analysisOutput);

            logger.LogInformation(LogMessages.StartingRiskAnalysis);

            File.WriteAllText(fullPath, json);
            LogMessages.WritingInventory(logger, fullPath);

            return 0;
        }

        public static async Task<int> GenerateReport(string[] args, string defaultInventoryFilename, string defaultAnalysisFilename, ILogger logger)
        {
            var path = args[1];
            var reportOptions = ParseReportArguments(args, defaultInventoryFilename, defaultAnalysisFilename);
            reportOptions.InputDirectory = path;

            if (string.IsNullOrWhiteSpace(reportOptions.OutputDirectory))
                reportOptions.OutputDirectory = path;

            try
            {
                var generator = new ReportGenerator();
                var result = await generator.GenerateAsync(reportOptions);

                foreach (var warning in result.Warnings)
                    logger.LogWarning("{Warning}", warning);

                foreach (var file in result.GeneratedFiles)
                    logger.LogInformation("Generated report file: {ReportFile}", file);

                return 0;
            }
            catch (NotSupportedException ex)
            {
                logger.LogError(ex, "Report format not supported.");
                return 2;
            }
        }
    }
}
