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
    public partial class NotificationWindow : Window
    {
        private const int MarginFromRight = 128;
        private const int MarginFromBottom = 24;
        private const int WindowHeight = 240;
        private static int _notificationOffset = 0;
        private int _countdownSeconds = 5;
        private DispatcherTimer? _countdownTimer;
        private DispatcherTimer? _animationTimer;
        private DispatcherTimer? _topmostTimer;
        private double _animationProgress = 0;
        private bool _isShutdownNotification = false;
        
        public event EventHandler? ShutdownCancelled;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_TOP = new IntPtr(0);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        public NotificationWindow()
        {
            InitializeComponent();
            Opacity = 0;
            PositionInCorner();
            StartCountdown();
            StartTopmostTimer();
        }

        public NotificationWindow(string title, string message, int priority = 0) : this()
        {
            TitleText.Text = string.IsNullOrEmpty(title) ? "服务器通知" : title;
            MessageText.Text = message;
            SetPriority(priority);
        }

        public void SetMessage(string title, string message, int priority = 0)
        {
            TitleText.Text = string.IsNullOrEmpty(title) ? "服务器通知" : title;
            MessageText.Text = message;
            SetPriority(priority);
        }

        public void SetShutdownMode(bool isShutdown)
        {
            _isShutdownNotification = isShutdown;
            if (CancelButton != null)
            {
                CancelButton.IsVisible = isShutdown;
            }
        }

        private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_isShutdownNotification)
            {
                ShutdownCancelled?.Invoke(this, EventArgs.Empty);
            }
            _countdownTimer?.Stop();
            _animationTimer?.Stop();
            _topmostTimer?.Stop();
            _notificationOffset = Math.Max(0, _notificationOffset - WindowHeight - 8);
            Close();
        }

        private void SetPriority(int priority)
        {
            string priorityText;
            FluentAvalonia.UI.Controls.Symbol icon;
            Color badgeColor;

            switch (priority)
            {
                case 0:
                    priorityText = "一般";
                    icon = FluentAvalonia.UI.Controls.Symbol.Alert;
                    badgeColor = Color.Parse("#0078D4");
                    break;
                case 1:
                    priorityText = "重要";
                    icon = FluentAvalonia.UI.Controls.Symbol.Alert;
                    badgeColor = Color.Parse("#FF8C00");
                    break;
                case 2:
                    priorityText = "紧急";
                    icon = FluentAvalonia.UI.Controls.Symbol.Important;
                    badgeColor = Color.Parse("#E81123");
                    break;
                default:
                    priorityText = "一般";
                    icon = FluentAvalonia.UI.Controls.Symbol.Alert;
                    badgeColor = Color.Parse("#0078D4");
                    break;
            }

            if (IconSymbol != null)
            {
                IconSymbol.Symbol = icon;
                IconSymbol.Foreground = new SolidColorBrush(badgeColor);
            }

            if (PriorityText != null)
            {
                PriorityText.Text = priorityText;
            }

            if (PriorityBadge != null)
            {
                PriorityBadge.Background = new SolidColorBrush(badgeColor);
            }
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            PositionInCorner();
            StartAnimation();
        }

        private void StartAnimation()
        {
            _animationProgress = 0;
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _animationTimer.Tick += AnimationTimer_Tick;
            _animationTimer.Start();
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

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            _animationProgress += 0.08;
            if (_animationProgress >= 1)
            {
                _animationProgress = 1;
                _animationTimer?.Stop();
                _animationTimer = null;
            }

            Opacity = _animationProgress;
            
            if (MainBorder != null && MainBorder.RenderTransform is TranslateTransform transform)
            {
                transform.Y = 30 * (1 - _animationProgress);
            }
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
                ConfirmButton.IsEnabled = true;
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

        private void PositionInCorner()
        {
            try
            {
                var screen = Screens.Primary;
                if (screen != null)
                {
                    var workingArea = screen.WorkingArea;
                    
                    const double fixedWidth = 450;
                    const double fixedHeight = 240;
                    
                    double x = workingArea.X + workingArea.Width - fixedWidth - MarginFromRight;
                    double y = workingArea.Y + workingArea.Height - fixedHeight - MarginFromBottom - _notificationOffset;
                    
                    x = Math.Max(workingArea.X, Math.Min(x, workingArea.X + workingArea.Width - fixedWidth));
                    y = Math.Max(workingArea.Y, Math.Min(y, workingArea.Y + workingArea.Height - fixedHeight));
                    
                    Position = new PixelPoint((int)x, (int)y);
                }
            }
            catch
            {
            }
        }

        private void OnConfirmClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _countdownTimer?.Stop();
            _animationTimer?.Stop();
            _topmostTimer?.Stop();
            _notificationOffset = Math.Max(0, _notificationOffset - WindowHeight - 8);
            Close();
        }

        public static async Task ShowNotification(string title, string message, int priority = 0)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var window = new NotificationWindow(title, message, priority);
                _notificationOffset += WindowHeight + 8;
                window.Show();
                window.Topmost = true;
                
                window.Closed += (s, e) =>
                {
                    _notificationOffset = Math.Max(0, _notificationOffset - WindowHeight - 8);
                };
            });
        }
    }
}
