using System.Threading;
using System.Windows;
using System.Windows.Threading;
using UmiClassGuardLite.Core;

namespace UmiClassGuardLite;

public partial class App : Application
{
    private Mutex? _singleton;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUiException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Diagnostics.Record("CG-9001", "AppDomain", "Ứng dụng gặp lỗi ngoài dự kiến.", args.ExceptionObject as Exception);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Diagnostics.Record("CG-9001", "Background", "Tác vụ nền gặp lỗi ngoài dự kiến.", args.Exception);
            args.SetObserved();
        };
        try
        {
            _singleton = new Mutex(true, @"Local\UmiClassGuardLite_" + Environment.UserName, out bool first);
            if (!first)
            {
                MessageBox.Show("ClassGuard đang mở trong phiên User hiện tại.", "Umi ClassGuard Lite",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            Diagnostics.Record("CG-9001", "Startup", "Ứng dụng không khởi động được.", ex);
            MessageBox.Show("CG-9001: Ứng dụng không khởi động được.\n" +
                "Hãy gửi mã lỗi cùng file diagnostics.jsonl trong %LOCALAPPDATA%\\UmiClassGuardLite.\n" +
                "Chi tiết: " + ex.GetType().Name, "ClassGuard - Lỗi khởi động",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnUiException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Diagnostics.Record("CG-9001", "Interface", "Lỗi giao diện chưa được xử lý.", e.Exception);
        MessageBox.Show("Mã lỗi CG-9001. Ứng dụng gặp lỗi giao diện.\n" +
            "Hãy mở Cài đặt > Chẩn đoán để xuất báo cáo nếu ứng dụng còn hoạt động.\n" +
            "Loại lỗi: " + e.Exception.GetType().Name,
            "ClassGuard - CG-9001", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _singleton?.ReleaseMutex(); } catch (ApplicationException) { }
        _singleton?.Dispose();
        base.OnExit(e);
    }
}
