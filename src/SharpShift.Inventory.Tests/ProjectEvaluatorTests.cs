using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SharpShift.Inventory.Core.Utilities;

namespace SharpShift.Inventory.Tests
{
    public class ProjectEvaluatorTests
    {
        [SetUp]
        public void SetUp()
        {
            ProjectEvaluator.ClearCache();
        }

        [Test]
        public void EvaluateProjectProperties_ReturnsNull_WhenProjectPathMissing()
        {
            var missing = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csproj");

            var result = ProjectEvaluator.EvaluateProjectProperties(missing);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void EvaluateProjectProperties_ParsesFrameworkTypeAndDependencies()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(temp);
            try
            {
                var referencedProject = Path.Combine(temp, "OtherProject.csproj");
                File.WriteAllText(referencedProject, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

                var projectPath = Path.Combine(temp, "App.csproj");
                File.WriteAllText(projectPath,
                    "<Project Sdk=\"Microsoft.NET.Sdk\">" +
                    "<PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup>" +
                    "<ItemGroup>" +
                    "<ProjectReference Include=\"OtherProject.csproj\" />" +
                    "<PackageReference Include=\"Newtonsoft.Json\" Version=\"13.0.3\" />" +
                    "<FrameworkReference Include=\"Microsoft.AspNetCore.App\" />" +
                    "</ItemGroup>" +
                    "</Project>");

                var result = ProjectEvaluator.EvaluateProjectProperties(projectPath);

                Assert.That(result, Is.Not.Null);
                Assert.That(result!.ProjectName, Is.EqualTo("App"));
                Assert.That(result.ProjectType, Is.EqualTo("Exe"));
                Assert.That(result.Framework, Is.EqualTo(".NET 10.0"));
                Assert.That(result.Dependencies.Count, Is.EqualTo(3));

                var projectDependency = result.Dependencies.Single(d => d.DependencyType == "Project");
                var packageDependency = result.Dependencies.Single(d => d.DependencyType == "Package");
                var frameworkDependency = result.Dependencies.Single(d => d.DependencyType == "Framework");

                Assert.That(projectDependency.Name, Is.EqualTo("OtherProject.csproj"));
                Assert.That(packageDependency.Name, Is.EqualTo("Newtonsoft.Json"));
                Assert.That(packageDependency.Version, Is.EqualTo("13.0.3"));
                Assert.That(frameworkDependency.Name, Is.EqualTo("Microsoft.AspNetCore.App"));
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        [Test]
        public void EvaluateProjectProperties_UsesFirstTargetFramework_FromTargetFrameworks()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(temp);
            try
            {
                var projectPath = Path.Combine(temp, "MultiTarget.csproj");
                File.WriteAllText(projectPath,
                    "<Project Sdk=\"Microsoft.NET.Sdk\">" +
                    "<PropertyGroup><TargetFrameworks>net8.0;net10.0</TargetFrameworks></PropertyGroup>" +
                    "</Project>");

                var result = ProjectEvaluator.EvaluateProjectProperties(projectPath);

                Assert.That(result, Is.Not.Null);
                Assert.That(result!.Framework, Is.EqualTo(".NET 8.0"));
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        [Test]
        public void EvaluateProjectProperties_ReturnsCachedResult_UntilCacheCleared()
        {
            var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(temp);
            try
            {
                var projectPath = Path.Combine(temp, "Cached.csproj");
                File.WriteAllText(projectPath,
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");

                var first = ProjectEvaluator.EvaluateProjectProperties(projectPath);
                Assert.That(first, Is.Not.Null);
                Assert.That(first!.Framework, Is.EqualTo(".NET 8.0"));

                File.WriteAllText(projectPath,
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

                var cached = ProjectEvaluator.EvaluateProjectProperties(projectPath);
                Assert.That(cached, Is.Not.Null);
                Assert.That(cached!.Framework, Is.EqualTo(".NET 8.0"));

                ProjectEvaluator.ClearCache();

                var refreshed = ProjectEvaluator.EvaluateProjectProperties(projectPath);
                Assert.That(refreshed, Is.Not.Null);
                Assert.That(refreshed!.Framework, Is.EqualTo(".NET 10.0"));
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

    }
}
