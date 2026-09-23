using System;

namespace SharpShift.Inventory.Core.Utilities
{
    public static class FrameworkNormalizer
    {
        public static string Normalize(string? tf)
        {
            if (string.IsNullOrWhiteSpace(tf))
                return string.Empty;

            tf = tf.Trim();

            var map = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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
                { "netstandard2.1", ".NET Standard 2.1" }
            };

            if (map.TryGetValue(tf, out var friendly))
                return friendly;

            var m = System.Text.RegularExpressions.Regex.Match(tf, "^net(\\d+)(\\.(\\d+))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var major = m.Groups[1].Value;
                var minor = m.Groups[3].Success ? m.Groups[3].Value : null;
                if (!string.IsNullOrEmpty(minor))
                    return $".NET {major}.{minor}";
                return $".NET {major}.0";
            }

            m = System.Text.RegularExpressions.Regex.Match(tf, "^netcoreapp(\\d+)(\\.(\\d+))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var major = m.Groups[1].Value;
                var minor = m.Groups[3].Success ? m.Groups[3].Value : "0";
                return $".NET Core {major}.{minor}";
            }

            m = System.Text.RegularExpressions.Regex.Match(tf, "^netstandard(\\d+)(\\.(\\d+))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var major = m.Groups[1].Value;
                var minor = m.Groups[3].Success ? m.Groups[3].Value : "0";
                return $".NET Standard {major}.{minor}";
            }

            // legacy like v4.6.2
            if (tf.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                return $".NET Framework {tf.Substring(1)}";

            return tf;
        }
    }
}
