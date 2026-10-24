using System;
using System.Linq;
using NUnit.Framework;
using SharpShift.Inventory.Core.Utilities;

namespace SharpShift.Inventory.Tests
{
    public class DotNetProjectTypeGuidsTests
    {
        [Test]
        public void Map_AllValuesAreValidGuids()
        {
            foreach (var key in DotNetProjectTypeGuids.Map.Keys)
            {
                var trimmed = key.Trim('{', '}');
                Assert.That(Guid.TryParse(trimmed, out _), Is.True, $"Invalid GUID key: {key}");
            }
        }

        [Test]
        public void Map_ContainsCanonicalAnchorEntries()
        {
            Assert.That(DotNetProjectTypeGuids.Map["{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"], Is.EqualTo("C# (Legacy)"));
            Assert.That(DotNetProjectTypeGuids.Map["{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"], Is.EqualTo("C# (SDK-Style)"));
            Assert.That(DotNetProjectTypeGuids.Map["{F184B08F-C81C-45F6-A57F-5ABD9991F28F}"], Is.EqualTo("VB.NET (Legacy)"));
            Assert.That(DotNetProjectTypeGuids.Map["{F2A71F9B-5D33-465A-A702-920D77279786}"], Is.EqualTo("F#"));
            Assert.That(DotNetProjectTypeGuids.Map["{349C5851-65DF-11DA-9384-00065B846F21}"], Is.EqualTo("ASP.NET Web Application"));
            Assert.That(DotNetProjectTypeGuids.Map["{66A26720-8FB5-11D2-AA7E-00C04F688DDE}"], Is.EqualTo("Solution Folder"));
        }

        [Test]
        public void Map_KeysAreNonEmptyAndUniqueIgnoringCase()
        {
            var keys = DotNetProjectTypeGuids.Map.Keys.ToList();

            Assert.That(keys.All(k => !string.IsNullOrWhiteSpace(k)), Is.True);
            Assert.That(keys.Count, Is.EqualTo(keys.Distinct(StringComparer.OrdinalIgnoreCase).Count()));
        }
    }
}