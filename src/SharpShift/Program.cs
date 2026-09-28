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
                var path = args[1];
                var output = "inventory.json";

                for (int i = 2; i < args.Length; i++)
                {
                    if (args[i] == "--output" && i + 1 < args.Length)
                    {
                        output = args[i + 1];
                        i++;
                    }
                }

                // Configure DI
                var services = new ServiceCollection();
                services.AddSingleton<ISolutionDiscoverer, SolutionDiscoverer>();
                services.AddSingleton<IProjectDiscoverer, ProjectDiscoverer>();

                var provider = services.BuildServiceProvider();

                var sol = provider.GetRequiredService<ISolutionDiscoverer>();
                var proj = provider.GetRequiredService<IProjectDiscoverer>();

                // Allow optional flag to require MSBuild-based evaluation
                var requireMsBuild = false;
                int retryAttempts = 0;
                bool clean = false;
                bool analyzeCloned = false;
                for (int i = 2; i < args.Length; i++)
                {
                    if (args[i] == "--require-msbuild")
                    {
                        requireMsBuild = true;
                    }
                    else if (args[i] == "--retry" && i + 1 < args.Length && int.TryParse(args[i + 1], out var r))
                    {
                        retryAttempts = r;
                        i++;
                    }
                    else if (args[i] == "--clean")
                    {
                        clean = true;
                    }
                    else if (args[i] == "--analyze-cloned")
                    {
                        analyzeCloned = true;
                    }
                }

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
                if (args[i] == "--output" && i + 1 < args.Length)
                {
                    output = args[i + 1];
                    i++;
                }
                else if (args[i] == "--require-msbuild")
                {
                    requireMsBuild = true;
                }
                else if (args[i] == "--retry" && i + 1 < args.Length && int.TryParse(args[i + 1], out var r))
                {
                    retryAttempts = r;
                    i++;
                }
                else if (args[i] == "--clean")
                {
                    clean = true;
                }
                else if (args[i] == "--analyze-cloned")
                {
                    analyzeCloned = true;
                }
            }

            return (path, output, requireMsBuild, retryAttempts, clean, analyzeCloned);
        }
    }
}
