using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using SharpShift.Inventory.Core.Models;
using SharpShift.Inventory.Discovery;

namespace SharpShift.Inventory.Tests
{
    public class SolutionDiscovererTests
    {
        [Test]
        public async Task DiscoverSolutions_FindsAllSlnFiles()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(temp);
            try
            {
                var nested = Path.Combine(temp, "sub");
                Directory.CreateDirectory(nested);

                var s1 = Path.Combine(temp, "A.sln");
                var s2 = Path.Combine(nested, "B.sln");
                File.WriteAllText(s1, "\n");
                File.WriteAllText(s2, "\n");

                var disc = new FileSystemSolutionDiscoverer();
                var sols = (await disc.DiscoverSolutionsAsync(temp)).ToList();

                // Ensure entries are structured (not plain strings) and have LocalPath populated
                Assert.IsTrue(sols.All(s => s.LocalPath != null));
                Assert.IsTrue(sols.All(s => s is SolutionDiscoveryEntry));

                Assert.IsTrue(sols.Any(s => s.LocalPath == s1));
                Assert.IsTrue(sols.Any(s => s.LocalPath == s2));
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }
    }
}
