using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace SocialBlocker.Core;

/// <summary>
/// Reads and writes the block config to ProgramData as JSON.
/// On Windows, also locks the folder's ACL down to Administrators + SYSTEM only,
/// so a standard-user account can't just edit the block list away mid-session.
/// </summary>
public static class ConfigStore
{
    public static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SocialBlocker");

    public static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static BlockConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var fresh = BlockConfig.Default();
            Save(fresh);
            return fresh;
        }

        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<BlockConfig>(json) ?? BlockConfig.Default();
    }

    public static void Save(BlockConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);

        if (OperatingSystem.IsWindows())
        {
            TryLockDownAcl();
        }
    }

    // Best-effort hardening only. If this fails for any reason (insufficient
    // rights, unusual filesystem, etc.) the service's 10-second re-apply loop
    // in Worker.cs is the fallback layer of protection — nothing depends on
    // this succeeding.
    [SupportedOSPlatform("windows")]
    private static void TryLockDownAcl()
    {
        try
        {
            var dirInfo = new DirectoryInfo(ConfigDir);
            var security = dirInfo.GetAccessControl();
            security.SetAccessRuleProtection(true, false);

            var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

            foreach (var sid in new[] { admins, system })
            {
                security.AddAccessRule(new FileSystemAccessRule(
                    sid,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
            }

            dirInfo.SetAccessControl(security);
        }
        catch
        {
            // Ignored — see comment above.
        }
    }
}
