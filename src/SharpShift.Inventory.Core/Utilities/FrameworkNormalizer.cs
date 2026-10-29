using System;
using System.Text.RegularExpressions;

namespace SharpShift.Inventory.Core.Utilities
{
    public static partial class FrameworkNormalizer
    {
        public static readonly System.Collections.Generic.Dictionary<string, string> FrameworkMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "net48", ".NET Framework 4.8" },
            { "net472", ".NET Framework 4.7.2" },
            { "net471", ".NET Framework 4.7.1" },
            { "net462", ".NET Framework 4.6.2" },
            { "net461", ".NET Framework 4.6.1" },
            { "net46", ".NET Framework 4.6" },
            { "net452", ".NET Framework 4.5.2" },
            { "net451", ".NET Framework 4.5.1" },
            { "net45", ".NET Framework 4.5" },
            { "netcoreapp3.1", ".NET Core 3.1" },
            { "netcoreapp2.1", ".NET Core 2.1" },
            { "netstandard2.0", ".NET Standard 2.0" },
            { "netstandard2.1", ".NET Standard 2.1" },
            { "net5", ".NET 5.0" },
            { "net5.0", ".NET 5.0" },
            { "net6", ".NET 6.0" },
            { "net6.0", ".NET 6.0" },
            { "net7", ".NET 7.0" },
            { "net7.0", ".NET 7.0" },
            { "net8", ".NET 8.0" },
            { "net8.0", ".NET 8.0" },
            { "net9", ".NET 9.0" },
            { "net9.0", ".NET 9.0" },
            { "net10", ".NET 10.0" },
            { "net10.0", ".NET 10.0" },
            { "net11", ".NET 11.0" },
            { "net11.0", ".NET 11.0" }
        };


        public static string Normalize(string? tf)
        {
            if (string.IsNullOrWhiteSpace(tf))
                return string.Empty;

            tf = tf.Trim();

            if (FrameworkMap.TryGetValue(tf, out var friendly))
                return friendly;

            var m = NetRegex().Match(tf);
            if (m.Success)
            {
                var major = m.Groups[1].Value;
                var minor = m.Groups[3].Success ? m.Groups[3].Value : null;
                if (!string.IsNullOrEmpty(minor))
                    return $".NET {major}.{minor}";
                return $".NET {major}.0";
            }

            m = NetCoreAppRegex().Match(tf);
            if (m.Success)
            {
                var major = m.Groups[1].Value;
                var minor = m.Groups[3].Success ? m.Groups[3].Value : "0";
                return $".NET Core {major}.{minor}";
            }

            m = NetStandardRegex().Match(tf);
            if (m.Success)
            {
                var major = m.Groups[1].Value;
                var minor = m.Groups[3].Success ? m.Groups[3].Value : "0";
                return $".NET Standard {major}.{minor}";
            }

            // legacy like v4.6.2
            if (tf.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                return $".NET Framework {tf[0]}";

            return tf;
        }

        [GeneratedRegex(@"^net(\d+)(\.(\d+))?", RegexOptions.IgnoreCase)]
        private static partial Regex NetRegex();

        [GeneratedRegex(@"^netcoreapp(\d+)(\.(\d+))?", RegexOptions.IgnoreCase)]
        private static partial Regex NetCoreAppRegex();

        [GeneratedRegex(@"^netstandard(\d+)(\.(\d+))?", RegexOptions.IgnoreCase)]
        private static partial Regex NetStandardRegex();
    }
}
