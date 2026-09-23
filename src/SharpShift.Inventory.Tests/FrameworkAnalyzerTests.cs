using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using SharpShift.Inventory.ProjectAnalysis;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Tests
{
    public class FrameworkAnalyzerTests
    {
        [Test]
        public async Task FrameworkAnalyzer_ParsesLegacyTargetFrameworkVersion()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(temp);
            try
            {
                var proj = Path.Combine(temp, "Legacy.csproj");
                File.WriteAllText(proj, "<Project>\n  <PropertyGroup>\n    <TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion>\n  </PropertyGroup>\n</Project>");

                var analyzer = new FrameworkAnalyzer();
                var ctx = new ProjectContext { ProjectPath = proj, ProjectName = "Legacy" };
                var res = await analyzer.AnalyzeAsync(ctx);

                Assert.AreEqual(".NET Framework 4.6.2", res.Framework);
            }
            finally { try { Directory.Delete(temp, true); } catch { } }
        }

        [Test]
        public async Task FrameworkAnalyzer_ParsesSdkTargetFramework()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(temp);
            try
            {
                var proj = Path.Combine(temp, "Sdk.csproj");
                File.WriteAllText(proj, "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net48</TargetFramework>\n  </PropertyGroup>\n</Project>");

                var analyzer = new FrameworkAnalyzer();
                var ctx = new ProjectContext { ProjectPath = proj, ProjectName = "Sdk" };
                var res = await analyzer.AnalyzeAsync(ctx);

                Assert.AreEqual(".NET Framework 4.8", res.Framework);
            }
            finally { try { Directory.Delete(temp, true); } catch { } }
        }
    }
}
