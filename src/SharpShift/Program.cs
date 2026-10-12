using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
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
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: inventory scan <path> [--output <file>]");
                return 1;
            }

            if (args.Length >= 2 && args[0].Equals("scan", StringComparison.OrdinalIgnoreCase))
            {
                // Consolidate argument parsing into ParseArguments to avoid duplication
                var (path, output, requireMsBuild, retryAttempts, clean, analyzeCloned) = ParseArguments(args);

                // Configure DI
                var services = new ServiceCollection();
                services.AddSingleton<ISolutionDiscoverer, SolutionDiscoverer>();
                services.AddSingleton<IProjectDiscoverer, ProjectDiscoverer>();

                var provider = services.BuildServiceProvider();

                var sol = provider.GetRequiredService<ISolutionDiscoverer>();
                var proj = provider.GetRequiredService<IProjectDiscoverer>();

                return await InventoryCliRunner.RunAsync(path, output, sol, proj, requireMsBuild, retryAttempts, clean, analyzeCloned);
            }

            Console.WriteLine("Unknown command. Usage: inventory scan <path> [--output <file>]");
            return 1;
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
