using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using FluentAvalonia.UI.Windowing;
using NetClassManage.ViewModels;
using NetClassManage.Views;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace NetClassManage
{
    public partial class App : Application
    {
        private TrayIcon? _trayIcon;
        private MainWindow? _mainWindow;
        private MainViewModel? _mainViewModel;
        private string _errorLogPath = Path.Combine(AppContext.BaseDirectory, "error.log");

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
                
                try
                {
                    _mainViewModel = new MainViewModel();
                    _mainWindow = new MainWindow
                    {
                        DataContext = _mainViewModel
                    };
                    desktop.MainWindow = _mainWindow;
                    desktop.ShutdownRequested += OnShutdownRequested;
                    SetupTrayIcon();
                    
                    var autoShowTimer = new Avalonia.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(3)
                    };
                    autoShowTimer.Tick += (s, e) =>
                    {
                        autoShowTimer.Stop();
                        ToggleWidget();
                    };
                    autoShowTimer.Start();
                }
                catch (Exception ex)
                {
                    LogError("Init", ex);
                    ShowErrorLog();
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            LogError("Unhandled", ex);
            
            if (e.IsTerminating)
            {
                ShowErrorLog();
            }
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogError("UnobservedTask", e.Exception);
            e.SetObserved();
        }

        private void LogError(string source, Exception? ex)
        {
            try
            {
                var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex}\n";
                if (ex?.InnerException != null)
                {
                    message += $"Inner: {ex.InnerException}\n";
                }
                File.AppendAllText(_errorLogPath, message + new string('-', 50) + "\n");
            }
            catch { }
        }

        private void ShowErrorLog()
        {
            try
            {
                if (File.Exists(_errorLogPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _errorLogPath,
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        private void SetupTrayIcon()
        {
            try
            {
                var iconStream = AssetLoader.Open(new Uri("avares://NetClassManage/Assets/icon.ico"));
                
                _trayIcon = new TrayIcon();
                _trayIcon.Icon = new WindowIcon(iconStream);
                _trayIcon.ToolTipText = "班级设备管理系统";
                _trayIcon.IsVisible = true;

                var showMenuItem = new NativeMenuItem("显示主窗口");
                showMenuItem.Click += (s, e) => ShowMainWindow();

                var widgetMenuItem = new NativeMenuItem("桌面小组件");
                widgetMenuItem.Click += (s, e) => ToggleWidget();

                var hideMenuItem = new NativeMenuItem("隐藏主窗口");
                hideMenuItem.Click += (s, e) => HideMainWindow();

                var exitMenuItem = new NativeMenuItem("退出");
                exitMenuItem.Click += (s, e) => ExitApp();

                var menu = new NativeMenu();
                menu.Items.Add(showMenuItem);
                menu.Items.Add(widgetMenuItem);
                menu.Items.Add(hideMenuItem);
                menu.Items.Add(new NativeMenuItemSeparator());
                menu.Items.Add(exitMenuItem);

                _trayIcon.Menu = menu;
                _trayIcon.Clicked += (s, e) => ShowMainWindow();
            }
            catch (Exception)
            {
                _trayIcon = new TrayIcon();
                _trayIcon.ToolTipText = "班级设备管理系统";
                _trayIcon.IsVisible = true;
            }
        }

        private void ShowMainWindow()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
        }

        private void HideMainWindow()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Hide();
            }
        }

        private void ToggleWidget()
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.ToggleWidget();
            }
        }

        private void ExitApp()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                _trayIcon?.Dispose();
                desktop.Shutdown();
            }
        }

        private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
        {
            _trayIcon?.Dispose();
        }
    }
}
