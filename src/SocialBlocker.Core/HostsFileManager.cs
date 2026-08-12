using System.Diagnostics;

namespace SocialBlocker.Core;

/// <summary>
/// Manages a clearly-marked block of entries inside the Windows hosts file.
/// The two methods that matter for correctness — BuildBlockedLines and
/// StripManagedBlock — are pure functions with no file I/O, so they can be
/// exercised directly (see SocialBlocker.Cli's "selftest" command) without
/// touching any real file.
/// </summary>
public static class HostsFileManager
{
    private const string BeginMarker = "# === SocialBlocker START ===";
    private const string EndMarker = "# === SocialBlocker END ===";

    public static string HostsPath =>
        OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts")
            : "/etc/hosts"; // only reached in non-Windows dev/testing scenarios

    public static void ApplyBlocks(IEnumerable<string> domains)
    {
        var existing = ReadLines();
        var updated = BuildBlockedLines(existing, domains);
        File.WriteAllLines(HostsPath, updated);
        FlushDnsCache();
    }

    public static void RemoveBlocks()
    {
        var existing = ReadLines();
        var updated = StripManagedBlock(existing);
        File.WriteAllLines(HostsPath, updated);
        FlushDnsCache();
    }

    /// <summary>
    /// Takes the current hosts-file lines and a domain list, and returns the
    /// new full set of lines with a single, de-duplicated SocialBlocker block
    /// appended at the end. Calling this repeatedly with the same input is
    /// idempotent — it replaces the old managed block rather than stacking a
    /// new one, which is what makes the service's re-apply-every-10-seconds
    /// loop safe to run continuously.
    /// </summary>
    public static List<string> BuildBlockedLines(List<string> existingLines, IEnumerable<string> domains)
    {
        var lines = StripManagedBlock(existingLines);

        lines.Add(BeginMarker);
        foreach (var domain in domains.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"0.0.0.0 {domain}");
        }
        lines.Add(EndMarker);

        return lines;
    }

    /// <summary>
    /// Returns the lines with any existing SocialBlocker-managed block removed,
    /// leaving everything the user or other software put in the file untouched.
    /// </summary>
    public static List<string> StripManagedBlock(List<string> lines)
    {
        var result = new List<string>();
        var inManagedBlock = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed == BeginMarker) { inManagedBlock = true; continue; }
            if (trimmed == EndMarker) { inManagedBlock = false; continue; }
            if (!inManagedBlock) result.Add(line);
        }

        return result;
    }

    private static List<string> ReadLines() =>
        File.Exists(HostsPath) ? File.ReadAllLines(HostsPath).ToList() : new List<string>();

    private static void FlushDnsCache()
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/flushdns",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            proc?.WaitForExit(5000);
        }
        catch
        {
            // Best-effort — a stale DNS cache entry expires on its own shortly anyway.
        }
    }
}
