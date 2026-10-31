using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace UmiClassGuardLite.Core;

public static class Diagnostics
{
    private static readonly object Sync = new();
    public const string Version = "1.0.1";
    public static string DiagnosticPath => Path.Combine(SettingsStore.DataDirectory, "diagnostics.jsonl");
    public static string LastCode { get; private set; } = "CG-0000";
    public static string LastMessage { get; private set; } = "Chưa có lỗi được ghi nhận.";

    public static readonly IReadOnlyDictionary<string, string> Catalog = new Dictionary<string, string>
    {
        ["CG-0000"] = "Không phát hiện lỗi được ghi nhận.",
        ["CG-1001"] = "Không có quyền ghi thư mục dữ liệu của tài khoản.",
        ["CG-1002"] = "Không đọc được cấu hình hoặc cấu hình bị lỗi.",
        ["CG-1003"] = "Không thể lưu cấu hình.",
        ["CG-1004"] = "Không ghi được nhật ký sự kiện.",
        ["CG-1101"] = "Không thể truy vấn danh sách ứng dụng đang chạy.",
        ["CG-1201"] = "Không thể đóng ứng dụng được yêu cầu vì giới hạn quyền hoặc tiến trình.",
        ["CG-1202"] = "Tên tiến trình trong quy tắc không hợp lệ hoặc được bảo vệ.",
        ["CG-2001"] = "Trình duyệt chưa xác nhận áp dụng chính sách website.",
        ["CG-2002"] = "Không thể ghi chính sách trình duyệt bằng quyền hiện tại.",
        ["CG-2003"] = "Đã tồn tại chính sách trình duyệt khác hoặc bị thay đổi.",
        ["CG-2004"] = "Không thể khôi phục chính sách trình duyệt do ứng dụng tạo.",
        ["CG-3001"] = "Không thể đọc hoặc chỉnh tùy chọn khởi động cùng Windows.",
        ["CG-9001"] = "Lỗi ứng dụng chưa xử lý.",
        ["CG-9002"] = "Môi trường Windows không đáp ứng điều kiện chạy.",
        ["CG-9003"] = "Ứng dụng bị chặn trước khi khởi chạy; không thể tự ghi nhật ký."
    };

    public static void Record(string code, string area, string friendlyMessage, Exception? ex = null)
    {
        if (!Catalog.ContainsKey(code)) code = "CG-9001";
        var entry = new DiagnosticEntry
        {
            Time = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            Code = code,
            Area = area,
            Message = friendlyMessage,
            ExceptionType = ex?.GetType().Name ?? ""
        };
        lock (Sync)
        {
            LastCode = code;
            LastMessage = friendlyMessage;
            try
            {
                Directory.CreateDirectory(SettingsStore.DataDirectory);
                File.AppendAllText(DiagnosticPath, JsonSerializer.Serialize(entry) + Environment.NewLine);
            }
            catch { /* Keep error available in memory when disk is locked. */ }
        }
    }

    public static string BuildReport(bool runSelfTest = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("UMI CLASSGUARD LITE - DIAGNOSTIC REPORT");
        sb.AppendLine("Generated: " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        sb.AppendLine("Version: " + Version);
        sb.AppendLine("OS: " + RuntimeInformation.OSDescription);
        sb.AppendLine("Architecture: " + RuntimeInformation.OSArchitecture);
        sb.AppendLine("Process architecture: " + RuntimeInformation.ProcessArchitecture);
        sb.AppendLine("Runtime: " + RuntimeInformation.FrameworkDescription);
        sb.AppendLine("Windows: " + OperatingSystem.IsWindows() + " | Windows 10+: " + OperatingSystem.IsWindowsVersionAtLeast(10));
        sb.AppendLine("Elevation requested: asInvoker (standard user compatible)");
        sb.AppendLine("Note: This report omits passwords, visited URLs, window titles and user identity.");
        sb.AppendLine();
        if (runSelfTest)
        {
            sb.AppendLine("SELF-TEST");
            try
            {
                Directory.CreateDirectory(SettingsStore.DataDirectory);
                string temp = Path.Combine(SettingsStore.DataDirectory, "classguard_write_test_" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(temp, "test");
                File.Delete(temp);
                sb.AppendLine("DATA_WRITE: PASS");
            }
            catch (Exception e)
            {
                Record("CG-1001", "SelfTest.Data", "Không có quyền ghi dữ liệu.", e);
                sb.AppendLine("DATA_WRITE: FAIL - CG-1001 - " + e.GetType().Name);
            }
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                sb.AppendLine("HKCU_RUN_READ: PASS" + (key is null ? " (empty)" : ""));
            }
            catch (Exception e)
            {
                Record("CG-3001", "SelfTest.Startup", "Không có quyền đọc tùy chọn khởi động.", e);
                sb.AppendLine("HKCU_RUN_READ: FAIL - CG-3001 - " + e.GetType().Name);
            }
            try
            {
                using var p = Process.GetCurrentProcess();
                sb.AppendLine("PROCESS_SESSION: PASS (session available)");
                _ = p.SessionId;
            }
            catch (Exception e)
            {
                Record("CG-1101", "SelfTest.Process", "Không truy vấn được tiến trình hiện tại.", e);
                sb.AppendLine("PROCESS_SESSION: FAIL - CG-1101 - " + e.GetType().Name);
            }
            sb.AppendLine("WEB_ENFORCEMENT: UNVERIFIED - CG-2001 (manual browser test required)");
            sb.AppendLine();
        }

        sb.AppendLine("RECENT ERRORS");
        string[] rows = [];
        try { if (File.Exists(DiagnosticPath)) rows = File.ReadLines(DiagnosticPath).TakeLast(30).ToArray(); }
        catch (Exception e) { sb.AppendLine("Could not read diagnostic file: " + e.GetType().Name); }
        if (rows.Length == 0)
        {
            sb.AppendLine(LastCode + " - " + LastMessage);
        }
        foreach (string row in rows)
        {
            try
            {
                var entry = JsonSerializer.Deserialize<DiagnosticEntry>(row);
                if (entry is not null)
                    sb.AppendLine(entry.Time + " | " + entry.Code + " | " + entry.Area + " | " +
                                  entry.Message + (entry.ExceptionType.Length > 0 ? " | " + entry.ExceptionType : ""));
            }
            catch { sb.AppendLine("CG-1002 | Dòng chẩn đoán không đọc được."); }
        }
        sb.AppendLine();
        sb.AppendLine("ERROR CODES");
        foreach (var item in Catalog) sb.AppendLine(item.Key + " - " + item.Value);
        return sb.ToString();
    }

    public sealed class DiagnosticEntry
    {
        public string Time { get; set; } = "";
        public string Code { get; set; } = "";
        public string Area { get; set; } = "";
        public string Message { get; set; } = "";
        public string ExceptionType { get; set; } = "";
    }
}
