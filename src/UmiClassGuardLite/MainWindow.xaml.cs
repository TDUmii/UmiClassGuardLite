using Microsoft.Win32;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using UmiClassGuardLite.Core;

namespace UmiClassGuardLite;

public partial class MainWindow : Window
{
    private GuardSettings _settings = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly HashSet<int> _seenPids = [];
    private readonly HashSet<int> _handledPids = [];
    private bool _ready;
    private int _detectedTotal;

    public MainWindow()
    {
        InitializeComponent();
        _settings = SettingsStore.Load();
        _settings.Rules ??= [];
        _settings.BlockedDomains ??= [];
        InitializeFromSettings();
        _ready = true;
        _timer.Tick += Timer_Tick;
        _timer.Start();
        WriteLog("Khởi chạy", "Ứng dụng chạy bằng quyền Windows hiện tại. Auto Close mặc định tắt.");
        RefreshStatus();
        ScanProcesses();
        Closing += (_, e) =>
        {
            if (_settings.FocusActive)
            {
                var answer = MessageBox.Show(
                    "Đóng ClassGuard sẽ dừng giám sát ngay lập tức. Bạn vẫn muốn thoát?",
                    "Dừng giám sát", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes) e.Cancel = true;
            }
        };
    }

    private void InitializeFromSettings()
    {
        string[] options = ["Python", "Scratch", "Roblox Studio", "Giờ kiểm tra"];
        int index = Array.FindIndex(options, s => s == _settings.Profile);
        ProfileCombo.SelectedIndex = Math.Max(0, index);
        AutoCloseCheckBox.IsChecked = _settings.AutoClose;
        StartupCheckBox.IsChecked = IsStartupEnabled();
        RulesList.ItemsSource = _settings.Rules;
        DomainTextBox.Text = string.Join(Environment.NewLine, _settings.BlockedDomains);
        TechnicalText.Text =
            "Tài khoản: " + Environment.UserName +
            Environment.NewLine + "Yêu cầu UAC: asInvoker - không tự nâng quyền; quyền thực tế theo phiên chạy" +
            Environment.NewLine + "Dữ liệu: " + SettingsStore.DataDirectory +
            Environment.NewLine + "Phiên làm việc: " + Process.GetCurrentProcess().SessionId +
            Environment.NewLine + ".NET: " + Environment.Version +
            Environment.NewLine + "Không cài service, driver hoặc firewall rule.";
        RefreshLogs();
        RefreshDiagnostics(false);
    }

    private void SaveConfig()
    {
        try { SettingsStore.Save(_settings); }
        catch (Exception ex)
        {
            Diagnostics.Record("CG-1003", "Settings.Save", "Không thể lưu cấu hình.", ex);
            RefreshDiagnostics(false);
            FooterStatus.Text = "CG-1003 - Không lưu được dữ liệu";
            MessageBox.Show("Mã lỗi CG-1003: Không thể lưu cài đặt.\nLoại lỗi: " +
                ex.GetType().Name, "ClassGuard - CG-1003", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void WriteLog(string title, string detail)
    {
        var entry = new ActivityEntry
        {
            Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Title = title,
            Detail = detail
        };
        SettingsStore.AddActivity(entry);
        if (_ready && ActivityList is not null) RefreshLogs();
    }

    private void RefreshLogs() => ActivityList.ItemsSource = SettingsStore.ReadActivity();

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_settings.FocusActive && _settings.FocusUntil.HasValue &&
            DateTimeOffset.Now >= _settings.FocusUntil.Value)
        {
            _settings.FocusActive = false;
            _settings.FocusUntil = null;
            SaveConfig();
            WriteLog("Kết thúc tiết học", "Focus Mode tự tắt khi hết thời lượng.");
        }
        ScanProcesses();
        RefreshStatus();
    }

    private void ScanProcesses()
    {
        try
        {
            var matches = ProcessGuard.FindMatches(_settings.Rules);
            var currentIds = matches.Select(x => x.Pid).ToHashSet();
            _seenPids.RemoveWhere(pid => !currentIds.Contains(pid));
            _handledPids.RemoveWhere(pid => !currentIds.Contains(pid));
            foreach (var match in matches)
            {
                if (!_seenPids.Add(match.Pid)) continue;
                _detectedTotal++;
                string outcome = "Đã phát hiện, chỉ theo dõi.";
                if (_settings.FocusActive && _settings.AutoClose && _handledPids.Add(match.Pid))
                {
                    var rule = _settings.Rules.FirstOrDefault(r =>
                        r.Enabled && r.Name == match.Name);
                    if (rule is not null)
                    {
                        outcome = ProcessGuard.TryClose(match.Pid, rule.ProcessName);
                        if (outcome.StartsWith("Không") || outcome.StartsWith("Bỏ qua"))
                        {
                            Diagnostics.Record("CG-1201", "Process.Close", "Không thể đóng cửa sổ của " + match.Name);
                            RefreshDiagnostics(false);
                        }
                    }
                }
                WriteLog("Ứng dụng: " + match.Name, "PID " + match.Pid + " - " + outcome);
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Record("CG-1101", "Process.Scan", "Không thể quét tiến trình.", ex);
            RefreshDiagnostics(false);
            FooterStatus.Text = "CG-1101 - Không thể quét tiến trình";
        }
        DetectedCountText.Text = _detectedTotal.ToString();
    }

    private void RefreshStatus()
    {
        RuleCountText.Text = _settings.Rules.Count(r => r.Enabled).ToString();
        if (_settings.FocusActive)
        {
            FocusStatusText.Text = "Focus Mode đang bật";
            FocusStatusText.Foreground = new SolidColorBrush(Color.FromRgb(142, 218, 196));
            FocusDetailText.Text = _settings.AutoClose
                ? "Tự yêu cầu đóng cửa sổ game bị hạn chế (nếu có quyền)."
                : "Đang phát hiện và ghi nhận. Auto Close chưa bật.";
            TimeSpan left = (_settings.FocusUntil ?? DateTimeOffset.Now) - DateTimeOffset.Now;
            RemainingText.Text = left > TimeSpan.Zero ? left.ToString(@"hh\:mm\:ss") : "00:00:00";
            FooterStatus.Text = "Đang giám sát - " + _settings.Profile;
        }
        else
        {
            FocusStatusText.Text = "Chưa bật Focus Mode";
            FocusStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 246, 253));
            FocusDetailText.Text = "Đang theo dõi ứng dụng nhưng không tự đóng.";
            RemainingText.Text = "--:--";
            FooterStatus.Text = "Monitor Mode - không tự đóng ứng dụng";
        }
    }

    private void ShowPanel(StackPanel panel, Button nav)
    {
        foreach (var p in new[] { OverviewPanel, AppsPanel, WebPanel, LogsPanel, SettingsPanel })
            p.Visibility = p == panel ? Visibility.Visible : Visibility.Collapsed;
        foreach (var button in new[] { NavOverview, NavApps, NavWeb, NavLogs, NavSettings })
        {
            button.Background = button == nav
                ? new SolidColorBrush(Color.FromRgb(36, 59, 73)) : Brushes.Transparent;
            button.Foreground = button == nav
                ? new SolidColorBrush(Color.FromRgb(155, 225, 211))
                : new SolidColorBrush(Color.FromRgb(197, 211, 229));
        }
    }

    private void Overview_Click(object sender, RoutedEventArgs e) => ShowPanel(OverviewPanel, NavOverview);
    private void Apps_Click(object sender, RoutedEventArgs e) => ShowPanel(AppsPanel, NavApps);
    private void Web_Click(object sender, RoutedEventArgs e) => ShowPanel(WebPanel, NavWeb);
    private void Logs_Click(object sender, RoutedEventArgs e) { RefreshLogs(); ShowPanel(LogsPanel, NavLogs); }
    private void Settings_Click(object sender, RoutedEventArgs e) => ShowPanel(SettingsPanel, NavSettings);

    private void ProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || ProfileCombo.SelectedItem is not ComboBoxItem item) return;
        _settings.Profile = item.Content.ToString() ?? "Python";
        SaveConfig();
        WriteLog("Đổi profile", _settings.Profile);
        RefreshStatus();
    }

    private void StartFocus_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MinutesTextBox.Text, out int minutes) || minutes < 1 || minutes > 360)
        {
            MessageBox.Show("Thời lượng hợp lệ từ 1 đến 360 phút.", "ClassGuard");
            return;
        }
        _settings.FocusActive = true;
        _settings.FocusUntil = DateTimeOffset.Now.AddMinutes(minutes);
        _seenPids.Clear();
        _handledPids.Clear();
        SaveConfig();
        WriteLog("Bắt đầu Focus", _settings.Profile + " - " + minutes + " phút; Auto Close: " + _settings.AutoClose);
        ScanProcesses();
        RefreshStatus();
    }

    private void StopFocus_Click(object sender, RoutedEventArgs e)
    {
        _settings.FocusActive = false;
        _settings.FocusUntil = null;
        SaveConfig();
        WriteLog("Dừng Focus", "Giáo viên đã dừng chế độ tập trung.");
        RefreshStatus();
    }

    private void AutoCloseChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        bool enabled = AutoCloseCheckBox.IsChecked == true;
        if (enabled && !_settings.AutoClose)
        {
            if (MessageBox.Show(
                "Khi Focus Mode bật, ClassGuard sẽ thử đóng các ứng dụng trong danh sách quy tắc. " +
                "Ứng dụng có thể mất dữ liệu chưa lưu. Bạn muốn bật Auto Close?",
                "Xác nhận Auto Close", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                AutoCloseCheckBox.IsChecked = false;
                return;
            }
        }
        _settings.AutoClose = enabled;
        _seenPids.Clear();
        _handledPids.Clear();
        SaveConfig();
        WriteLog("Auto Close", enabled ? "Đã bật (chỉ khi Focus Active)" : "Đã tắt");
        RefreshStatus();
    }

    private void RuleChanged_Click(object sender, RoutedEventArgs e)
    {
        _seenPids.Clear();
        _handledPids.Clear();
        SaveConfig();
        RefreshStatus();
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        string display = RuleNameInput.Text.Trim();
        string process = ProcessNameInput.Text.Trim();
        if (display.Length == 0 || !ProcessGuard.IsSafeTarget(process))
        {
            Diagnostics.Record("CG-1202", "Rules.Add", "Tên tiến trình không hợp lệ hoặc thuộc danh sách được bảo vệ.");
            RefreshDiagnostics(false);
            MessageBox.Show("CG-1202: Tên tiến trình không hợp lệ hoặc thuộc danh sách hệ thống được bảo vệ.", "ClassGuard");
            return;
        }
        if (_settings.Rules.Any(r => r.ProcessName.Equals(process, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Tên tiến trình này đã có trong danh sách.", "ClassGuard");
            return;
        }
        _settings.Rules.Add(new AppRule { Name = display, ProcessName = process, Enabled = true });
        RulesList.ItemsSource = null;
        RulesList.ItemsSource = _settings.Rules;
        RuleNameInput.Clear();
        ProcessNameInput.Clear();
        SaveConfig();
        RefreshStatus();
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AppRule rule }) return;
        _settings.Rules.Remove(rule);
        RulesList.ItemsSource = null;
        RulesList.ItemsSource = _settings.Rules;
        SaveConfig();
        RefreshStatus();
    }

    private void ApplyWeb_Click(object sender, RoutedEventArgs e)
    {
        string[] lines = DomainTextBox.Text.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
        string[] domains = BrowserPolicyManager.ValidateDomains(lines);
        if (domains.Length == 0)
        {
            MessageBox.Show("Hãy nhập ít nhất một tên miền hợp lệ.", "ClassGuard");
            return;
        }
        if (MessageBox.Show(
            "Chỉ thử ghi chính sách URLBlocklist ở HKCU cho Chrome và Edge. " +
            "Có thể không hoạt động trên máy trường hoặc profile không được quản lý. " +
            "Ứng dụng sẽ không thay đổi chính sách đã tồn tại của trường. Tiếp tục?",
            "Thử nghiệm Website Guard", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _settings.BlockedDomains = domains;
        SaveConfig();
        string result = BrowserPolicyManager.Apply(domains);
        WebResultText.Text = result + Environment.NewLine +
            "CG-2001: Chưa xác minh việc chặn URL. Kiểm tra edge://policy hoặc chrome://policy và mở website thử sau khi khởi động lại trình duyệt." +
            Environment.NewLine + "Quy tắc sẽ tồn tại cho đến khi bạn bấm Khôi phục, kể cả khi Focus Mode kết thúc.";
        if (!result.Contains("CG-2002") && !result.Contains("CG-2003"))
            Diagnostics.Record("CG-2001", "WebPolicy.Verify", "Đã thử ghi URLBlocklist nhưng chưa xác minh khả năng chặn.");
        RefreshDiagnostics(false);
        WriteLog("Website Policy", result.Replace(Environment.NewLine, " | "));
    }

    private void RestoreWeb_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Chỉ gỡ những khóa URLBlocklist do ClassGuard tự tạo và chưa bị chỉnh sửa. Tiếp tục?",
            "Khôi phục", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var result = BrowserPolicyManager.Restore();
        WebResultText.Text = result + Environment.NewLine + "Khởi động lại Chrome / Edge nếu cần.";
        WriteLog("Khôi phục website policy", result.Replace(Environment.NewLine, " | "));
    }

    private void RefreshLog_Click(object sender, RoutedEventArgs e) => RefreshLogs();

    private const string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupKey);
            return key?.GetValue("UmiClassGuardLite") is string path &&
                path.Contains(Environment.ProcessPath ?? "<invalid>", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception) { return false; }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(StartupKey, true);
            if (key is null) throw new UnauthorizedAccessException("Windows không cho phép sửa HKCU Run.");
            if (StartupCheckBox.IsChecked == true)
            {
                string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Không thể xác định file EXE.");
                key.SetValue("UmiClassGuardLite", "\"" + executable + "\"", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue("UmiClassGuardLite", false);
            }
            _settings.StartWithWindows = StartupCheckBox.IsChecked == true;
            SaveConfig();
            SettingsResultText.Text = "Đã lưu. Tự khởi chạy: " + (_settings.StartWithWindows ? "bật" : "tắt") + ".";
            WriteLog("Cài đặt", SettingsResultText.Text);
        }
        catch (Exception ex)
        {
            Diagnostics.Record("CG-3001", "Startup.RunKey", "Không thay đổi được tự khởi động Windows.", ex);
            SettingsResultText.Text = "CG-3001: Không thể áp dụng cài đặt (" + ex.GetType().Name + ").";
            StartupCheckBox.IsChecked = IsStartupEnabled();
            RefreshDiagnostics(false);
        }
    }

    private void RefreshDiagnostics(bool test)
    {
        if (DiagnosticReportBox is null) return;
        DiagnosticReportBox.Text = Diagnostics.BuildReport(test);
        LastErrorCodeText.Text = Diagnostics.LastCode;
        LastErrorDetailText.Text = Diagnostics.LastMessage;
    }

    private void RunDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        RefreshDiagnostics(true);
        WriteLog("Chẩn đoán", "Đã kiểm tra quyền ghi dữ liệu, HKCU Run và truy vấn tiến trình.");
        MessageBox.Show("Kiểm tra hoàn tất. Xem bảng kết quả; trạng thái Web Guard vẫn phải kiểm tra thực tế trong trình duyệt.",
            "ClassGuard - Chẩn đoán", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RefreshDiagnostics(false);
            Clipboard.SetText(DiagnosticReportBox.Text);
            MessageBox.Show("Đã sao chép báo cáo chẩn đoán. Dán vào cuộc trò chuyện để kiểm tra.",
                "ClassGuard", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Diagnostics.Record("CG-9001", "Diagnostics.Clipboard", "Không thể sao chép báo cáo vào Clipboard.", ex);
            RefreshDiagnostics(false);
            MessageBox.Show("CG-9001: Không thể sao chép Clipboard. Hãy thử nút Xuất file báo cáo.",
                "ClassGuard", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RefreshDiagnostics(true);
            var dialog = new SaveFileDialog
            {
                Title = "Lưu báo cáo lỗi Umi ClassGuard",
                FileName = "UmiClassGuard-Diagnostic-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".txt",
                Filter = "Text file (*.txt)|*.txt",
                AddExtension = true,
                DefaultExt = ".txt"
            };
            if (dialog.ShowDialog(this) == true)
            {
                File.WriteAllText(dialog.FileName, DiagnosticReportBox.Text);
                MessageBox.Show("Đã xuất báo cáo. Bạn có thể gửi file này để kiểm tra lỗi.",
                    "ClassGuard", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Record("CG-1001", "Diagnostics.Export", "Không thể ghi file báo cáo ở vị trí được chọn.", ex);
            RefreshDiagnostics(false);
            MessageBox.Show("CG-1001: Không thể lưu báo cáo. Hãy chọn thư mục khác hoặc sao chép nội dung.",
                "ClassGuard", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.DataDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + SettingsStore.DataDirectory + "\"") { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "ClassGuard"); }
    }
}
