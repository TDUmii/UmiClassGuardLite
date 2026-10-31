using System.Diagnostics;

namespace UmiClassGuardLite.Core;

public static class ProcessGuard
{
    // Never kill Windows shell, tooling, OS security processes or ClassGuard itself.
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "idle", "registry", "smss", "csrss", "wininit", "services", "lsass", "svchost",
        "winlogon", "dwm", "explorer", "taskmgr", "sihost", "fontdrvhost", "conhost",
        "securityhealthservice", "msmpeng", "shellexperiencehost", "startmenuexperiencehost",
        "searchhost", "runtimebroker", "spoolsv", "audiodg", "umiclassguardlite",
        "powershell", "pwsh", "cmd", "dotnet", "devenv", "code", "pycharm64"
    };
    public static bool IsSafeTarget(string processName) =>
        !string.IsNullOrWhiteSpace(processName)
        && processName.Length <= 100
        && processName.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.')
        && !Protected.Contains(processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName[..^4] : processName);

    public static List<(int Pid, string Name)> FindMatches(IEnumerable<AppRule> rules)
    {
        int session = Process.GetCurrentProcess().SessionId;
        List<(int, string)> matches = [];
        foreach (var rule in rules.Where(r => r.Enabled && IsSafeTarget(r.ProcessName)))
        {
            string name = rule.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? rule.ProcessName[..^4] : rule.ProcessName;
            try
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    using (p)
                    {
                        try
                        {
                            if (p.Id != Environment.ProcessId && p.SessionId == session)
                                matches.Add((p.Id, rule.Name));
                        }
                        catch (Exception) { }
                    }
                }
            }
            catch (Exception) { }
        }
        return matches.DistinctBy(x => x.Item1).ToList();
    }

    public static string TryClose(int pid, string expectedProcessName)
    {
        if (!IsSafeTarget(expectedProcessName) || pid == Environment.ProcessId)
            return "Bỏ qua tiến trình được bảo vệ";
        try
        {
            using var target = Process.GetProcessById(pid);
            using var own = Process.GetCurrentProcess();
            var expected = expectedProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? expectedProcessName[..^4] : expectedProcessName;
            if (target.SessionId != own.SessionId ||
                !target.ProcessName.Equals(expected, StringComparison.OrdinalIgnoreCase))
                return "Không khớp phiên làm việc hoặc tên tiến trình";

            if (target.HasExited) return "Tiến trình đã kết thúc";
            if (target.CloseMainWindow()) return "Đã yêu cầu ứng dụng đóng cửa sổ";
            return "Không có cửa sổ cho phép đóng; chỉ ghi nhận (không cưỡng bức)";
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            return "Không đủ quyền hoặc tiến trình đã thoát";
        }
    }
}
