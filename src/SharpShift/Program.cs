using Microsoft.Build.Framework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpShift.Inventory.Cli;
using SharpShift.Inventory.Core;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Core.Models;
using SharpShift.Inventory.Discovery;
using SharpShift.Inventory.ProjectAnalysis;
using SharpShift.LoggerMessages;
using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace SharpShift
{
    public static class Program
    {
        // Minimal CLI entrypoint: inventory scan <path> [--output <file>]
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
                if (args.Length == 0)
                {
                    logger.LogWarning("No arguments provided.");
                    Console.WriteLine("Usage: inventory scan <path> [--output <file>]");
                    return 1;
                }

                if (args.Length >= 2)
                {
                    var configuration = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json", optional: true)
                        .Build();

                    var defaultInventoryFilename = configuration["OutputFilename"] ?? "inventory.json";
                    var defaultAnalysisFilename = configuration["AnalysisOutputFilename"] ?? "analysis.json";
                    var (path, output, analysisOutput, retryAttempts, clean, analyzeCloned) = ParseArguments(args, defaultInventoryFilename, defaultAnalysisFilename);

                    var result = args[0].ToLower() switch
                    {
                        "scan" => await ScanRepositories(path, output, retryAttempts, clean, analyzeCloned, logger, provider, loggerFactory),
                        "analyze" => await AnalyzeEstate(path, output, analysisOutput, retryAttempts, clean, analyzeCloned, logger, provider, loggerFactory),
                        _ => throw new InvalidOperationException($"Invalid command. '{args[0]}' is not a valid subcommand.")
                    };

                    return result;
                }
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Unhandled exception in program entrypoint.");
                return 3;
            }

            return 1;
        }

        /// <summary>
        /// Parses the command-line arguments and returns a tuple of 
        /// (string path, string output, bool requireMsBuild, int retryAttempts, bool clean, bool analyzeCloned)
        /// </summary>
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

                // Use a switch expression to select a local handler that performs side-effects
                // and returns how many extra positions the loop should advance (0 or 1).
                Func<int> handler = token switch
                {
                    "--output" when i + 1 < args.Length => () => { output = args[i + 1]; return 1; }
                    ,
                    "--retry" when i + 1 < args.Length => () => { if (int.TryParse(args[i + 1], out var rr)) retryAttempts = rr; return 1; }
                    ,
                    "--clean" => () => { clean = true; return 0; }
                    ,
                    "--analyze-cloned" => () => { analyzeCloned = true; return 0; }
                    ,
                    _ => () => 0,
                };

                i += handler();
            }

            return (path, output, analysisOutput, retryAttempts, clean, analyzeCloned);
        }

        public static async Task<int> ScanRepositories(
            string path,
            string output,
            int retryAttempts,
            bool clean,
            bool analyzeCloned,
            Microsoft.Extensions.Logging.ILogger logger,
            IServiceProvider provider,
            Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
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
                    Microsoft.Extensions.Logging.ILogger logger,
                    IServiceProvider provider,
                    Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
        {

            var resolver = new DefaultJsonTypeInfoResolver();
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                TypeInfoResolver = resolver
            };

            // Add logic for analyzing solutions, projects, dependencies, then risks

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
    }
}
