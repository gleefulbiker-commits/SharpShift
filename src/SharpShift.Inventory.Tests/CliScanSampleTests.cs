using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Tests
{
    public class CliScanSampleTests
    {
        [Test]
        public async Task InventoryCliRunner_ScanSampleRepo_ProducesExpectedJson()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(temp);

            try
            {
                // Create a fake solution file
                var slnPath = Path.Combine(temp, "Sample.sln");
                File.WriteAllText(slnPath, "\n");

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

                var output = Path.Combine(temp, "inventory.json");

                var code = await SharpShift.Inventory.Cli.InventoryCliRunner.RunAsync(temp, output);
                Assert.AreEqual(0, code, "Runner did not return success code");

                Assert.IsTrue(File.Exists(output), "Output file not created");

                var json = File.ReadAllText(output);
                var inv = JsonSerializer.Deserialize<SolutionInventory>(json);
                Assert.IsNotNull(inv);
                Assert.AreEqual(2, inv.Projects.Count);

                // frameworks normalized
                Assert.IsTrue(inv.Projects.Exists(p => p.Framework == ".NET Framework 4.6.2"));
                Assert.IsTrue(inv.Projects.Exists(p => p.Framework == ".NET Framework 4.8"));
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }
    }
}
