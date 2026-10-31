using UmiClassGuardLite.Core;
using System.Text.Json;
using System.Diagnostics;

int pass = 0;
void Assert(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    pass++;
    Console.WriteLine("PASS: " + name);
}

Assert(!ProcessGuard.IsSafeTarget("explorer.exe"), "Windows shell protected");
Assert(!ProcessGuard.IsSafeTarget("lsass"), "Windows security process protected");
Assert(!ProcessGuard.IsSafeTarget("UmiClassGuardLite.exe"), "Own process protected");
Assert(!ProcessGuard.IsSafeTarget("bad/name.exe"), "Malformed process name rejected");
Assert(ProcessGuard.IsSafeTarget("RobloxPlayerBeta.exe"), "Expected game allowed as detection target");
Assert(ProcessGuard.FindMatches([]).Count == 0, "Empty rules yield no matches");

var valid = BrowserPolicyManager.ValidateDomains([
    "https://youtube.com/watch?v=1", "youtube.com", "tiktok.com",
    "bad..example.com", "not a domain", "https://www.poki.com/"
]);
Assert(valid.SequenceEqual(["youtube.com", "tiktok.com", "poki.com"]), "Domain validation, deduplication and normalization");
Assert(BrowserPolicyManager.ValidateDomains(["--bad.com", "bad-.com"]).Length == 0,
    "Invalid domain labels rejected");

var defaults = new GuardSettings();
Assert(!defaults.AutoClose && !defaults.FocusActive, "Safe defaults - no automatic enforcement");
Assert(defaults.Rules.Any(x => x.ProcessName == "RobloxPlayerBeta"), "Roblox Player rule available");
Assert(defaults.Rules.All(x => x.ProcessName != "RobloxStudioBeta"), "Roblox Studio not targeted");

var json = JsonSerializer.Serialize(defaults);
var restored = JsonSerializer.Deserialize<GuardSettings>(json);
Assert(restored?.Rules.Count == defaults.Rules.Count && !restored.AutoClose,
    "Config serialization round trip");
Assert(Diagnostics.Catalog.ContainsKey("CG-1001"), "Data permission error code exists");
Assert(Diagnostics.Catalog.ContainsKey("CG-2002"), "Browser policy permission code exists");
Assert(Diagnostics.Catalog.ContainsKey("CG-9001"), "Unhandled exception code exists");
string diagnostic = Diagnostics.BuildReport(false);
Assert(diagnostic.Contains("Version: 1.0.1") && diagnostic.Contains("RECENT ERRORS"),
    "Diagnostic report includes version and error details");
Assert(diagnostic.Contains("CG-9003") && diagnostic.Contains("CG-2001"),
    "Diagnostic report includes offline startup guidance and browser verification warning");
Console.WriteLine("RESULT: " + pass + " / " + pass + " PASS");
