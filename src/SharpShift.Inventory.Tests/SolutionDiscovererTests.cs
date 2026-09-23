using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
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

                var disc = new SolutionDiscoverer();
                var sols = (await disc.DiscoverSolutionsAsync(temp)).ToList();

                Assert.IsTrue(sols.Contains(s1));
                Assert.IsTrue(sols.Contains(s2));
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }
    }
}
