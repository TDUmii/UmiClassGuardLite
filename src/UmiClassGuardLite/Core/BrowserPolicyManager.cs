using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;

namespace UmiClassGuardLite.Core;

public static class BrowserPolicyManager
{
    // These HKCU policies are conditional on actual browser management context.
    // A successful registry write is NOT evidence of browser enforcement.
    private static readonly (string Name, string Path)[] Targets =
    [
        ("Edge", @"Software\Policies\Microsoft\Edge\URLBlocklist"),
        ("Chrome", @"Software\Policies\Google\Chrome\URLBlocklist")
    ];
    private const string MarkerRoot = @"Software\UmiClassGuardLite\ManagedBrowserPolicies";

    public static string[] ValidateDomains(IEnumerable<string> input)
    {
        List<string> domains = [];
        foreach (string text in input)
        {
            string candidate = text.Trim().ToLowerInvariant();
            if (candidate.StartsWith("https://")) candidate = candidate[8..];
            if (candidate.StartsWith("http://")) candidate = candidate[7..];
            candidate = candidate.TrimEnd('/').Split('/')[0];
            if (candidate.StartsWith("www.")) candidate = candidate[4..];
            string[] labels = candidate.Split('.');
            if (candidate.Length < 4 || candidate.Length > 253 || labels.Length < 2 ||
                !Regex.IsMatch(labels[^1], @"^[a-z]{2,63}$") ||
                labels.Any(label => label.Length is < 1 or > 63 ||
                    !Regex.IsMatch(label, @"^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$")))
                continue;
            if (!domains.Contains(candidate)) domains.Add(candidate);
        }
        return domains.ToArray();
    }

    public static string Apply(string[] domains)
    {
        var valid = ValidateDomains(domains);
        if (valid.Length == 0) return "Không có tên miền hợp lệ.";
        List<string> report = [];
        foreach (var (name, path) in Targets)
        {
            try
            {
                string markerPath = MarkerRoot + "\\" + name;
                using var marker = Registry.CurrentUser.OpenSubKey(markerPath);
                using var existing = Registry.CurrentUser.OpenSubKey(path);
                if (existing is not null && marker?.GetValue("Owned")?.ToString() != "1")
                {
                    Diagnostics.Record("CG-2003", "WebPolicy." + name, "Đã có chính sách trình duyệt khác; không thay đổi.");
                    report.Add(name + ": đã có chính sách khác; giữ nguyên. CG-2003");
                    continue;
                }
                // Refuse to overwrite if a previously created key has changed externally.
                if (existing is not null && marker is not null)
                {
                    var oldValues = existing.GetValueNames().Select(n => existing.GetValue(n)?.ToString() ?? "").OrderBy(s => s).ToArray();
                    var tracked = (marker.GetValue("Values")?.ToString() ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries).OrderBy(s => s).ToArray();
                    if (!oldValues.SequenceEqual(tracked))
                    {
                        Diagnostics.Record("CG-2003", "WebPolicy." + name, "Chính sách trình duyệt đã thay đổi bên ngoài; không ghi đè.");
                        report.Add(name + ": chính sách đã thay đổi bên ngoài; bỏ qua. CG-2003");
                        continue;
                    }
                }

                // Replace only our own dedicated URLBlocklist keys.
                if (marker is not null) Registry.CurrentUser.DeleteSubKeyTree(path, false);
                using var key = Registry.CurrentUser.CreateSubKey(path, true);
                if (key is null) { report.Add(name + ": không có quyền ghi."); continue; }
                for (int i = 0; i < valid.Length; i++) key.SetValue((i + 1).ToString(), valid[i], RegistryValueKind.String);
                using var owned = Registry.CurrentUser.CreateSubKey(markerPath, true);
                owned?.SetValue("Owned", "1", RegistryValueKind.String);
                owned?.SetValue("Values", string.Join("|", valid), RegistryValueKind.String);
                report.Add(name + ": đã ghi HKCU - CHƯA xác minh hiệu lực; khởi động lại trình duyệt.");
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException or ArgumentException)
            {
                Diagnostics.Record("CG-2002", "WebPolicy." + name, "Không thể ghi URLBlocklist trong HKCU.", ex);
                report.Add(name + ": CG-2002 - thất bại (" + ex.GetType().Name + ").");
            }
        }
        return string.Join(Environment.NewLine, report);
    }

    public static string Restore()
    {
        List<string> report = [];
        foreach (var (name, path) in Targets)
        {
            try
            {
                string markerPath = MarkerRoot + "\\" + name;
                using var marker = Registry.CurrentUser.OpenSubKey(markerPath);
                if (marker?.GetValue("Owned")?.ToString() != "1") { report.Add(name + ": không có chính sách do app quản lý."); continue; }
                using var key = Registry.CurrentUser.OpenSubKey(path);
                var current = key?.GetValueNames().Select(n => key.GetValue(n)?.ToString() ?? "").OrderBy(s => s).ToArray() ?? [];
                var tracked = (marker.GetValue("Values")?.ToString() ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries).OrderBy(s => s).ToArray();
                if (!current.SequenceEqual(tracked))
                {
                    Diagnostics.Record("CG-2003", "WebRestore." + name, "Chính sách đã thay đổi bên ngoài; không tự xóa.");
                    report.Add(name + ": chính sách đã bị thay đổi; không tự xóa. CG-2003");
                    continue;
                }
                Registry.CurrentUser.DeleteSubKeyTree(path, false);
                Registry.CurrentUser.DeleteSubKeyTree(markerPath, false);
                report.Add(name + ": đã xóa chính sách do ClassGuard tạo.");
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException or ArgumentException)
            {
                Diagnostics.Record("CG-2004", "WebRestore." + name, "Không thể khôi phục URLBlocklist do ứng dụng tạo.", ex);
                report.Add(name + ": CG-2004 - không khôi phục được (" + ex.GetType().Name + ").");
            }
        }
        return string.Join(Environment.NewLine, report);
    }
}
