namespace Shared.Helpers;

/// <summary>
/// Minimal SemVer-style comparison for mobile version strings
/// (e.g. 1.0.2, 1.0.2-rc.1, mobile-v1.0.2-rc.1).
/// </summary>
public static class SemanticVersion
{
    public const string StableChannel = "stable";
    public const string BetaChannel = "beta";

    /// <summary>
    /// Normalize channel input. Unknown or empty → stable.
    /// </summary>
    public static string NormalizeChannel(string? channel)
    {
        if (string.Equals(channel, BetaChannel, StringComparison.OrdinalIgnoreCase))
            return BetaChannel;

        return StableChannel;
    }

    public static bool IsBetaChannel(string? channel)
        => NormalizeChannel(channel) == BetaChannel;

    /// <summary>
    /// Extract version from a mobile release tag (mobile-v1.0.2-rc.1 → 1.0.2-rc.1).
    /// </summary>
    public static string FromMobileTag(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return string.Empty;

        var version = tagName.Trim();
        if (version.StartsWith("mobile-v", StringComparison.OrdinalIgnoreCase))
            version = version["mobile-v".Length..];
        else if (version.StartsWith('v') || version.StartsWith('V'))
            version = version[1..];

        return version;
    }

    /// <summary>
    /// Compare two version strings.
    /// Returns -1 if left &lt; right, 0 if equal, 1 if left &gt; right.
    /// Pre-release versions are lower than the same core version without a pre-release
    /// (1.0.2-rc.1 &lt; 1.0.2). Invalid inputs sort as empty.
    /// </summary>
    public static int Compare(string? left, string? right)
    {
        var a = Parse(left);
        var b = Parse(right);

        var coreCompare = CompareNumericParts(a.Core, b.Core);
        if (coreCompare != 0)
            return coreCompare;

        // No pre-release is greater than any pre-release
        if (!a.HasPreRelease && !b.HasPreRelease)
            return 0;
        if (!a.HasPreRelease)
            return 1;
        if (!b.HasPreRelease)
            return -1;

        return ComparePreRelease(a.PreReleaseParts, b.PreReleaseParts);
    }

    private static (int[] Core, string[] PreReleaseParts, bool HasPreRelease) Parse(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return (Array.Empty<int>(), Array.Empty<string>(), false);

        var value = version.Trim();
        if (value.StartsWith("mobile-v", StringComparison.OrdinalIgnoreCase))
            value = value["mobile-v".Length..];
        else if (value.StartsWith('v') || value.StartsWith('V'))
            value = value[1..];

        // Drop build metadata (+...)
        var plus = value.IndexOf('+');
        if (plus >= 0)
            value = value[..plus];

        string core;
        string? pre = null;
        var dash = value.IndexOf('-');
        if (dash >= 0)
        {
            core = value[..dash];
            pre = value[(dash + 1)..];
        }
        else
        {
            core = value;
        }

        var coreParts = core
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var n) ? n : 0)
            .ToArray();

        if (string.IsNullOrWhiteSpace(pre))
            return (coreParts, Array.Empty<string>(), false);

        var preParts = pre
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        return (coreParts, preParts, true);
    }

    private static int CompareNumericParts(int[] a, int[] b)
    {
        var len = Math.Max(a.Length, b.Length);
        for (var i = 0; i < len; i++)
        {
            var left = i < a.Length ? a[i] : 0;
            var right = i < b.Length ? b[i] : 0;
            if (left < right) return -1;
            if (left > right) return 1;
        }

        return 0;
    }

    private static int ComparePreRelease(string[] a, string[] b)
    {
        var len = Math.Max(a.Length, b.Length);
        for (var i = 0; i < len; i++)
        {
            if (i >= a.Length) return -1;
            if (i >= b.Length) return 1;

            var left = a[i];
            var right = b[i];
            var leftIsNum = int.TryParse(left, out var leftNum);
            var rightIsNum = int.TryParse(right, out var rightNum);

            if (leftIsNum && rightIsNum)
            {
                if (leftNum < rightNum) return -1;
                if (leftNum > rightNum) return 1;
                continue;
            }

            // Numeric identifiers have lower precedence than non-numeric
            if (leftIsNum && !rightIsNum) return -1;
            if (!leftIsNum && rightIsNum) return 1;

            var cmp = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0) return cmp < 0 ? -1 : 1;
        }

        return 0;
    }
}
