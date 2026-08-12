using SocialBlocker.Core;

if (args.Length == 0)
{
    PrintUsage();
    return;
}

switch (args[0].ToLowerInvariant())
{
    case "start":
        Start(args);
        break;
    case "stop":
        Stop();
        break;
    case "status":
        Status();
        break;
    case "list":
        List();
        break;
    case "selftest":
        SelfTest();
        break;
    default:
        PrintUsage();
        break;
}

void Start(string[] a)
{
    if (a.Length < 2 || !int.TryParse(a[1], out var minutes) || minutes <= 0)
    {
        Console.WriteLine("Usage: socialblocker start <minutes>");
        return;
    }

    var config = ConfigStore.Load();
    config.Active = true;
    config.EndTimeUtc = DateTime.UtcNow.AddMinutes(minutes);
    ConfigStore.Save(config);

    Console.WriteLine($"Block session started. Ends at {config.EndTimeUtc.ToLocalTime():t} local time.");
    Console.WriteLine("The background service picks this up within 10 seconds.");
}

void Stop()
{
    var config = ConfigStore.Load();

    if (!config.Active)
    {
        Console.WriteLine("No active session.");
        return;
    }

    if (DateTime.UtcNow < config.EndTimeUtc)
    {
        Console.WriteLine("A session is still active. This build has no early-exit override yet");
        Console.WriteLine("(that's the Phase 6 tamper-resistance feature) — wait it out, or stop");
        Console.WriteLine("the SocialBlockerService manually as Administrator.");
        return;
    }

    config.Active = false;
    ConfigStore.Save(config);
    Console.WriteLine("Session cleared.");
}

void Status()
{
    var config = ConfigStore.Load();

    if (!config.Active)
    {
        Console.WriteLine("Inactive.");
        return;
    }

    var remaining = config.EndTimeUtc - DateTime.UtcNow;
    Console.WriteLine(remaining > TimeSpan.Zero
        ? $"Active — {remaining:hh\\:mm\\:ss} remaining."
        : "Active but expired — the service will lift it on its next tick.");
}

void List()
{
    var config = ConfigStore.Load();

    Console.WriteLine("Blocked domains:");
    foreach (var domain in config.BlockedDomains) Console.WriteLine($"  {domain}");

    Console.WriteLine("Blocked apps (enforced starting Phase 4):");
    foreach (var app in config.BlockedProcessNames) Console.WriteLine($"  {app}");
}

void SelfTest()
{
    var passed = 0;
    var failed = 0;

    void Check(string name, bool condition)
    {
        if (condition) { passed++; Console.WriteLine($"  PASS  {name}"); }
        else { failed++; Console.WriteLine($"  FAIL  {name}"); }
    }

    Console.WriteLine("Running hosts-file logic self-test (no files are touched)...");

    var original = new List<string>
    {
        "127.0.0.1 localhost",
        "# a user comment"
    };

    var blocked = HostsFileManager.BuildBlockedLines(original, new[] { "facebook.com", "instagram.com" });
    Check("keeps pre-existing lines", blocked.Contains("127.0.0.1 localhost") && blocked.Contains("# a user comment"));
    Check("adds first domain", blocked.Contains("0.0.0.0 facebook.com"));
    Check("adds second domain", blocked.Contains("0.0.0.0 instagram.com"));
    Check("wraps the block in markers",
        blocked.Any(l => l.Contains("SocialBlocker START")) && blocked.Any(l => l.Contains("SocialBlocker END")));

    var reapplied = HostsFileManager.BuildBlockedLines(blocked, new[] { "facebook.com", "instagram.com" });
    Check("re-applying doesn't duplicate the block", reapplied.Count(l => l.Contains("SocialBlocker START")) == 1);

    var stripped = HostsFileManager.StripManagedBlock(blocked);
    Check("removing blocks restores the original lines exactly", stripped.SequenceEqual(original));

    Console.WriteLine($"{passed} passed, {failed} failed.");
}

void PrintUsage()
{
    Console.WriteLine("SocialBlocker CLI");
    Console.WriteLine("  socialblocker start <minutes>   Start a block session");
    Console.WriteLine("  socialblocker stop               Clear an expired session");
    Console.WriteLine("  socialblocker status             Show remaining time");
    Console.WriteLine("  socialblocker list                Show the configured block list");
    Console.WriteLine("  socialblocker selftest            Verify the hosts-file logic in-memory");
}
