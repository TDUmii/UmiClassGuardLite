using System.Text.Json.Serialization;

namespace UmiClassGuardLite.Core;

public sealed class AppRule
{
    public string Name { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class GuardSettings
{
    public bool AutoClose { get; set; } = false;
    public bool FocusActive { get; set; } = false;
    public string Profile { get; set; } = "Python";
    public DateTimeOffset? FocusUntil { get; set; }
    public bool StartWithWindows { get; set; } = false;
    public string[] BlockedDomains { get; set; } =
        ["youtube.com", "youtu.be", "tiktok.com", "poki.com", "crazygames.com", "friv.com"];
    public List<AppRule> Rules { get; set; } =
    [
        new() { Name = "Roblox Player", ProcessName = "RobloxPlayerBeta" },
        new() { Name = "Roblox Launcher", ProcessName = "RobloxPlayerLauncher" },
        new() { Name = "Steam", ProcessName = "steam" },
        new() { Name = "Epic Games Launcher", ProcessName = "EpicGamesLauncher" },
        new() { Name = "Discord", ProcessName = "Discord", Enabled = false },
        new() { Name = "Minecraft", ProcessName = "Minecraft.Windows", Enabled = false }
    ];
}

public sealed class ActivityEntry
{
    public string Time { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
}
