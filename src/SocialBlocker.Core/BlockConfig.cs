namespace SocialBlocker.Core;

/// <summary>
/// The full state of a block session: whether one is active, when it ends,
/// and what's on the block list. Persisted as JSON in ProgramData.
/// </summary>
public class BlockConfig
{
    public bool Active { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public List<string> BlockedDomains { get; set; } = new();
    public List<string> BlockedProcessNames { get; set; } = new();

    public static BlockConfig Default() => new()
    {
        Active = false,
        EndTimeUtc = DateTime.MinValue,
        BlockedDomains = new List<string>
        {
            "facebook.com", "www.facebook.com", "m.facebook.com",
            "instagram.com", "www.instagram.com",
            "twitter.com", "www.twitter.com", "x.com", "www.x.com",
            "tiktok.com", "www.tiktok.com",
            "reddit.com", "www.reddit.com",
            "snapchat.com", "www.snapchat.com",
            "pinterest.com", "www.pinterest.com",
            "linkedin.com", "www.linkedin.com"
        },
        // Enforced in Phase 4 (firewall + process watchdog) — stored here now
        // so the config schema and the UI's app-picker don't need to change later.
        BlockedProcessNames = new List<string>
        {
            "Discord", "Telegram", "Slack", "WhatsApp", "Steam"
        }
    };
}
