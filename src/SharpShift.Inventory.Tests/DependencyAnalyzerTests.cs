using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using SharpShift.Inventory.DependencyAnalysis;
using SharpShift.Inventory.Core.Models;

namespace SharpShift.Inventory.Tests
{
    public class DependencyAnalyzerTests
    {
        [Test]
        public async Task DependencyAnalyzer_ExtractsPackageAndReference()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(temp);
            try
            {
                var proj = Path.Combine(temp, "Deps.csproj");
                File.WriteAllText(proj, "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup>\n    <PackageReference Include=\"Newtonsoft.Json\" Version=\"12.0.3\" />\n    <Reference Include=\"System.Data\" />\n  </ItemGroup>\n</Project>");

                var analyzer = new DependencyAnalyzer();
                var ctx = new ProjectContext { ProjectPath = proj, ProjectName = "Deps" };
                var res = await analyzer.AnalyzeAsync(ctx);

                Assert.IsTrue(res.Dependencies.Any(d => d.Name.Contains("Newtonsoft.Json")));
                Assert.IsTrue(res.Dependencies.Any(d => d.Name.Contains("System.Data")));
            }
            finally { try { Directory.Delete(temp, true); } catch { } }
        }
    }
}
