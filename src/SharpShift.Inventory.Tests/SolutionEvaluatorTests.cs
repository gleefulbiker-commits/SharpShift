using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SharpShift.Inventory.Core.Utilities;

namespace SharpShift.Inventory.Tests
{
    public class SolutionEvaluatorTests
    {
        [SetUp]
        public void SetUp()
        {
            SolutionEvaluator.ClearCache();
            ProjectEvaluator.ClearCache();
        }

        [Test]
        public void EvaluateSolutionProperties_ReturnsEmptyInventory_WhenPathMissing()
        {
            var missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sln");

            var result = SolutionEvaluator.EvaluateSolutionProperties(missingPath, forceRefresh: true, NullLogger.Instance);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.SolutionPath, Is.EqualTo(missingPath));
            Assert.That(result.Projects, Is.Empty);
        }

        [Test]
        public void EvaluateSolutionProperties_ParsesSlnProjects()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(temp);
            try
            {
                var projA = Path.Combine(temp, "ProjA.csproj");
                var projBDir = Path.Combine(temp, "sub");
                var projB = Path.Combine(projBDir, "ProjB.csproj");
                Directory.CreateDirectory(projBDir);

                File.WriteAllText(projA, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
                File.WriteAllText(projB, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");

                var slnPath = Path.Combine(temp, "Sample.sln");
                var slnContent = string.Join(Environment.NewLine, new[]
                {
                    "Microsoft Visual Studio Solution File, Format Version 12.00",
                    "VisualStudioVersion = 18.0.0",
                    $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"ProjA\", \"ProjA.csproj\", \"{Guid.NewGuid():B}\"",
                    "EndProject",
                    $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"ProjB\", \"sub\\ProjB.csproj\", \"{Guid.NewGuid():B}\"",
                    "EndProject"
                });

                File.WriteAllText(slnPath, slnContent);

                var result = SolutionEvaluator.EvaluateSolutionProperties(slnPath, forceRefresh: true, NullLogger.Instance);

                Assert.That(result, Is.Not.Null);
                Assert.That(result!.Projects.Count, Is.EqualTo(2));
                Assert.That(result.Summary["FormatVersion"], Is.EqualTo("12.00"));
                Assert.That(result.Summary["VisualStudioVersion"], Is.EqualTo("18.0.0"));
                Assert.That(result.Projects.Any(p => p.ProjectName == "ProjA"), Is.True);
                Assert.That(result.Projects.Any(p => p.ProjectName == "ProjB"), Is.True);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        [Test]
        public void EvaluateSolutionProperties_ParsesSlnxProjects()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(temp);
            try
            {
                var projPath = Path.Combine(temp, "App.csproj");
                File.WriteAllText(projPath, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

                var slnxPath = Path.Combine(temp, "Sample.slnx");
                File.WriteAllText(slnxPath, "<Solution><Project Path=\"App.csproj\" /></Solution>");

                var result = SolutionEvaluator.EvaluateSolutionProperties(slnxPath, forceRefresh: true, NullLogger.Instance);

                Assert.That(result, Is.Not.Null);
                Assert.That(result!.Projects.Count, Is.EqualTo(1));
                Assert.That(result.Projects[0].ProjectName, Is.EqualTo("App"));
                Assert.That(result.Projects[0].Framework, Is.EqualTo(".NET 10.0"));
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }
    }
}
