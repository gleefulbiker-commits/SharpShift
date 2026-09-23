using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using SharpShift.Inventory.Discovery;

namespace SharpShift.Inventory.Tests
{
    public class ProjectDiscovererTests
    {
        [Test]
        public async Task DiscoverProjects_FindsCsprojUnderSolutionAndFolder()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(temp);
            try
            {
                var nested = Path.Combine(temp, "area");
                Directory.CreateDirectory(nested);

                var sln = Path.Combine(temp, "Sample.sln");
                File.WriteAllText(sln, "\n");

                var p1 = Path.Combine(temp, "RootProj.csproj");
                var p2 = Path.Combine(nested, "NestedProj.csproj");
                File.WriteAllText(p1, "<Project></Project>");
                File.WriteAllText(p2, "<Project></Project>");

                var disc = new ProjectDiscoverer();
                var bySln = (await disc.DiscoverProjectsAsync(sln)).ToList();
                var byFolder = (await disc.DiscoverProjectsAsync(temp)).ToList();

                Assert.IsTrue(bySln.Any(p => p.ProjectPath == p1));
                Assert.IsTrue(bySln.Any(p => p.ProjectPath == p2));
                Assert.IsTrue(byFolder.Any(p => p.ProjectPath == p1));
                Assert.IsTrue(byFolder.Any(p => p.ProjectPath == p2));
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }
    }
}
