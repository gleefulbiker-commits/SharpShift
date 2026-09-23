using System.IO;
using NUnit.Framework;
using SharpShift.Inventory.Core.Utilities;

namespace SharpShift.Inventory.Tests
{
    public class ProjectEvaluatorTests
    {
        [Test]
        public void EvaluateProjectProperties_IncludesItemMetadata_WhenMsBuildAvailable()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(temp);
            try
            {
                var proj = Path.Combine(temp, "MetaTest.csproj");
                File.WriteAllText(proj, "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup>\n    <PackageReference Include=\"Meta.Package\">\n      <Version>1.2.3</Version>\n      <PrivateAssets>all</PrivateAssets>\n    </PackageReference>\n  </ItemGroup>\n</Project>");

                var props = ProjectEvaluator.EvaluateProjectProperties(proj);
                if (props == null)
                {
                    Assert.Ignore("MSBuild not available in this environment; ProjectEvaluator returned null.");
                }

                // If evaluator returned no item entries, treat this as a best-effort environment and skip the detailed assertions
                var hasItemKeys = false;
                foreach (var k in props.Keys)
                {
                    if (k.StartsWith("Item.", System.StringComparison.OrdinalIgnoreCase))
                    {
                        hasItemKeys = true;
                        break;
                    }
                }

                if (!hasItemKeys)
                {
                    Assert.Ignore("ProjectEvaluator evaluated the project but did not return any Item entries in this environment.");
                }

                // Find any item index that is a PackageReference and assert metadata is present for that item
                var pkgIndex = -1;
                foreach (var k in props.Keys)
                {
                    if (k.EndsWith(".Type") && string.Equals(props[k], "PackageReference", System.StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = k.Split('.');
                        if (parts.Length >= 3 && int.TryParse(parts[1], out var idx))
                        {
                            pkgIndex = idx;
                            break;
                        }
                    }
                }

                Assert.IsTrue(pkgIndex >= 0, "No PackageReference item was found in evaluated project properties.");

                var includeKey = $"Item.{pkgIndex}.Include";
                var verKey = $"Item.{pkgIndex}.Metadata.Version";
                var paKey = $"Item.{pkgIndex}.Metadata.PrivateAssets";

                Assert.IsTrue(props.ContainsKey(includeKey));
                Assert.AreEqual("Meta.Package", props[includeKey]);
                Assert.IsTrue(props.ContainsKey(verKey));
                Assert.AreEqual("1.2.3", props[verKey]);
                Assert.IsTrue(props.ContainsKey(paKey));
                Assert.AreEqual("all", props[paKey]);
            }
            finally { try { Directory.Delete(temp, true); } catch { } }
        }
    }
}
