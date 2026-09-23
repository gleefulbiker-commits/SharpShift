using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SharpShift.Inventory.Core.Interfaces;
using SharpShift.Inventory.Discovery;

namespace SharpShift.Inventory.Cli
{
    internal static class Program
    {
        // Minimal CLI entrypoint: inventory scan <path> [--output <file>]
        private static async Task<int> Main(string[] args)
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
                for (int i = 2; i < args.Length; i++)
                {
                    if (args[i] == "--require-msbuild")
                    {
                        requireMsBuild = true;
                        break;
                    }
                }
                // No-op change to update timestamp: Program already passes requireMsBuild.
                return await InventoryCliRunner.RunAsync(path, output, sol, proj, requireMsBuild);
            }

            Console.WriteLine("Unknown command. Usage: inventory scan <path> [--output <file>]");
            return 1;
        }
    }
}
