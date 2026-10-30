using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SharpShift.Inventory.Core.Models;
using SharpShift.Inventory.Discovery;

namespace SharpShift.Inventory.Tests
{
    public class CliScanSampleTests
    {
        [Test]
        public async Task InventoryCliRunner_InvalidPath_ReturnsNotFoundCode()
        {
            var code = await SharpShift.Inventory.Cli.InventoryCliRunner.RunAsync(
                NullLogger.Instance,
                "Z:\\path-that-does-not-exist-for-tests",
                "inventory.json");

            Assert.AreEqual(2, code);
        }

        [Test]
        public async Task InventoryCliRunner_ScanSampleRepo_ProducesExpectedJson()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(temp);

            try
            {
                // Legacy-style csproj
                var legacyProj = Path.Combine(temp, "LegacyProject.csproj");
                File.WriteAllText(legacyProj, @"<Project>
  <PropertyGroup>
    <TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion>
  </PropertyGroup>
</Project>");

                // SDK-style csproj
                var sdkProj = Path.Combine(temp, "SdkProject.csproj");
                File.WriteAllText(sdkProj, "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net48</TargetFramework>\n  </PropertyGroup>\n</Project>");

                // Create a simple solution file that points to both projects
                var slnxPath = Path.Combine(temp, "Sample.slnx");
                File.WriteAllText(slnxPath, "<Solution>\n  <Project Path=\"LegacyProject.csproj\" />\n  <Project Path=\"SdkProject.csproj\" />\n</Solution>");

                var output = "inventory.json";

                var code = await SharpShift.Inventory.Cli.InventoryCliRunner.RunAsync(
                    NullLogger.Instance,
                    temp,
                    output,
                    new SolutionDiscoverer());
                Assert.AreEqual(0, code, "Runner did not return success code");

                var outputPath = Path.Combine(temp, output);
                Assert.IsTrue(File.Exists(outputPath), "Output file not created");

                var json = File.ReadAllText(outputPath);
                var inventories = JsonSerializer.Deserialize<List<SolutionInventory>>(json);
                Assert.IsNotNull(inventories);
                var inv = inventories?.FirstOrDefault(i => i.SolutionName == "Sample");
                Assert.IsNotNull(inv);
                Assert.AreEqual(2, inv?.Projects.Count);

                // frameworks normalized
                Assert.IsTrue(inv?.Projects.Exists(p => p.Framework == ".NET Framework 4.6.2"));
                Assert.IsTrue(inv?.Projects.Exists(p => p.Framework == ".NET Framework 4.8"));
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }
    }
}
