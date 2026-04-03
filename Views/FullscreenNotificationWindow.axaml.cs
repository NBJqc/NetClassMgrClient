using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NetClassManage.Views
{
    public partial class FullscreenNotificationWindow : Window
    {
        private int _countdownSeconds = 5;
        private DispatcherTimer? _countdownTimer;
        private DispatcherTimer? _topmostTimer;
        private double _animationProgress = 0;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        public FullscreenNotificationWindow()
        {
            InitializeComponent();
            Opacity = 0;
            StartCountdown();
            StartTopmostTimer();
        }

        public FullscreenNotificationWindow(string title, string message) : this()
        {
            TitleText.Text = string.IsNullOrEmpty(title) ? "霸屏通知" : title;
            MessageText.Text = message;
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            MaximizeWindow();
            StartAnimation();
        }

        private void MaximizeWindow()
        {
            try
            {
                var screen = Screens.Primary;
                if (screen != null)
                {
                    var workingArea = screen.WorkingArea;
                    Width = workingArea.Width;
                    Height = workingArea.Height;
                    Position = new PixelPoint(workingArea.X, workingArea.Y);
                }
            }
            catch
            {
            }
        }

        private void StartAnimation()
        {
            var animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            animationTimer.Tick += (s, e) =>
            {
                _animationProgress += 0.05;
                if (_animationProgress >= 1)
                {
                    _animationProgress = 1;
                    animationTimer.Stop();
                }
                Opacity = _animationProgress;
            };
            animationTimer.Start();
        }

        private void StartTopmostTimer()
        {
            _topmostTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _topmostTimer.Tick += (s, e) =>
            {
                Topmost = true;
                if (TryGetPlatformHandle() is { } handle)
                {
                    SetWindowPos(handle.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            };
            _topmostTimer.Start();
        }

        private void StartCountdown()
        {
            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += CountdownTimer_Tick;
            _countdownTimer.Start();
            UpdateCountdownText();
        }

        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            _countdownSeconds--;
            if (_countdownSeconds <= 0)
            {
                _countdownTimer?.Stop();
                CloseButton.IsEnabled = true;
                CountdownText.Text = "";
            }
            else
            {
                UpdateCountdownText();
            }
        }

        private void UpdateCountdownText()
        {
            CountdownText.Text = $"{_countdownSeconds}秒后可关闭";
        }

        private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _countdownTimer?.Stop();
            _topmostTimer?.Stop();
            Close();
        }

        public static async Task ShowFullscreenNotification(string title, string message)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var window = new FullscreenNotificationWindow(title, message);
                window.Show();
                window.Topmost = true;
            });
        }
    }
}