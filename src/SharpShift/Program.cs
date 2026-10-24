using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpShift.Inventory.Cli;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Discovery;

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
            services.AddSingleton<IProjectDiscoverer, ProjectDiscoverer>();

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

                if (args.Length >= 2 && args[0].Equals("scan", StringComparison.OrdinalIgnoreCase))
                {
                    // Consolidate argument parsing into ParseArguments to avoid duplication
                    var (path, output, requireMsBuild, retryAttempts, clean, analyzeCloned) = ParseArguments(args);

                    logger.LogInformation("Starting scan for {Path} with output {OutputFile}", path, output);

                    var sol = provider.GetRequiredService<ISolutionDiscoverer>();
                    var proj = provider.GetRequiredService<IProjectDiscoverer>();
                    var runnerLogger = loggerFactory.CreateLogger("SharpShift.Inventory.Cli.InventoryCliRunner");

                    return await InventoryCliRunner.RunAsync(path, output, sol, proj, requireMsBuild, retryAttempts, clean, analyzeCloned, runnerLogger);
                }

                logger.LogWarning("Unknown command. Args: {Args}", string.Join(' ', args));
                Console.WriteLine("Unknown command. Usage: inventory scan <path> [--output <file>]");
                return 1;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Unhandled exception in program entrypoint.");
                return 3;
            }
        }

        /// <summary>
        /// Parses the command-line arguments and returns a tuple of 
        /// (string path, string output, bool requireMsBuild, int retryAttempts, bool clean, bool analyzeCloned)
        /// </summary>
        public static (
            string path,
            string output,
            bool requireMsBuild,
            int retryAttempts,
            bool clean,
            bool analyzeCloned) ParseArguments(string[] args)
        {
            var path = args[1];
            var output = "inventory.json";
            var requireMsBuild = false;
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
                    "--output" when i + 1 < args.Length => () => { output = args[i + 1]; return 1; },
                    "--require-msbuild" => () => { requireMsBuild = true; return 0; },
                    "--retry" when i + 1 < args.Length => () => { if (int.TryParse(args[i + 1], out var rr)) retryAttempts = rr; return 1; },
                    "--clean" => () => { clean = true; return 0; },
                    "--analyze-cloned" => () => { analyzeCloned = true; return 0; },
                    _ => () => 0,
                };

                i += handler();
            }

            return (path, output, requireMsBuild, retryAttempts, clean, analyzeCloned);
        }
    }
}
