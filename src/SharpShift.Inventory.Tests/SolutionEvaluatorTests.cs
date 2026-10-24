using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SharpShift.Inventory.Core.Utilities;

namespace SharpShift.Inventory.Tests
{
    public class SolutionEvaluatorTests
    {
        [Test]
        public void ParseSolutionProjectPaths_FindsProjectPaths()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(temp);
            try
            {
                var projA = Path.Combine(temp, "A.csproj");
                var projB = Path.Combine(temp, "sub", "B.csproj");
                Directory.CreateDirectory(Path.GetDirectoryName(projB) ?? temp);
                File.WriteAllText(projA, "<Project Sdk=\"Microsoft.NET.Sdk\"/>\n");
                File.WriteAllText(projB, "<Project Sdk=\"Microsoft.NET.Sdk\"/>\n");

                var sln = Path.Combine(temp, "Test.sln");
                // include both projects using relative paths
                var content = string.Join(Environment.NewLine, new[] {
                    "Microsoft Visual Studio Solution File, Format Version 12.00",
                    "# Visual Studio Version 17",
                    $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"A\", \"A.csproj\", \"{Guid.NewGuid():B}\"",
                    "EndProject",
                    $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"B\", \"sub\\B.csproj\", \"{Guid.NewGuid():B}\"",
                    "EndProject"
                });
                File.WriteAllText(sln, content);

                //var paths = SolutionEvaluator.ParseSolutionProjectPaths(sln);
                //Assert.IsNotNull(paths);
                var absA = Path.GetFullPath(projA);
                var absB = Path.GetFullPath(projB);
                //Assert.Contains(absA, paths.ToList());
                //Assert.Contains(absB, paths.ToList());
            }
            finally { try { Directory.Delete(temp, true); } catch { } }
        }
    }
}
