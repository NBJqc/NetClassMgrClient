using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NetClassManage.Views
{
    public partial class DesktopWidget : Window
    {
        private string _className = "班级名称";
        private List<WidgetNotification> _notifications = new List<WidgetNotification>();
        private static DesktopWidget? _instance;
        private bool _isDragging;
        private PixelPoint _dragStartPosition;
        private PixelPoint _dragStartWindowPosition;
        private readonly string _widgetConfigPath = Path.Combine(AppContext.BaseDirectory, "widget_config.json");

        public static DesktopWidget? Instance => _instance;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        public DesktopWidget()
        {
            InitializeComponent();
            _instance = this;
            LoadWidgetPosition();
            PositionWidget();
        }

        private void LoadWidgetPosition()
        {
            try
            {
                if (File.Exists(_widgetConfigPath))
                {
                    var json = File.ReadAllText(_widgetConfigPath);
                    var config = JsonSerializer.Deserialize<WidgetConfig>(json);
                    if (config != null)
                    {
                        Position = new PixelPoint(config.X, config.Y);
                    }
                }
            }
            catch
            {
            }
        }

        private void SaveWidgetPosition()
        {
            try
            {
                var config = new WidgetConfig
                {
                    X = Position.X,
                    Y = Position.Y
                };
                var json = JsonSerializer.Serialize(config);
                File.WriteAllText(_widgetConfigPath, json);
            }
            catch
            {
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                var pos = e.GetPosition(this);
                _dragStartPosition = new PixelPoint((int)pos.X, (int)pos.Y);
                _dragStartWindowPosition = Position;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isDragging)
            {
                var pos = e.GetPosition(this);
                var currentPosition = new PixelPoint((int)pos.X, (int)pos.Y);
                var deltaX = currentPosition.X - _dragStartPosition.X;
                var deltaY = currentPosition.Y - _dragStartPosition.Y;
                Position = new PixelPoint(_dragStartWindowPosition.X + deltaX, _dragStartWindowPosition.Y + deltaY);
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                SaveWidgetPosition();
                SendToBottom();
            }
        }

        private void SendToBottom()
        {
            if (!IsVisible) return;
            
            if (TryGetPlatformHandle() is { } handle)
            {
                SetWindowPos(handle.Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }

        public void SetClassName(string className)
        {
            _className = className;
            if (ClassNameText != null)
            {
                ClassNameText.Text = className;
            }
        }

        public void AddNotification(string title, string message, DateTime time, int priority = 0)
        {
            var notification = new WidgetNotification
            {
                Title = title,
                Message = message,
                Time = time,
                Priority = priority
            };

            _notifications.Insert(0, notification);
            
            if (_notifications.Count > 20)
            {
                _notifications.RemoveAt(_notifications.Count - 1);
            }

            UpdateNotificationsUI();
        }

        public void ClearNotifications()
        {
            _notifications.Clear();
            UpdateNotificationsUI();
        }

        private void UpdateNotificationsUI()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (NotificationsPanel == null) return;

                NotificationsPanel.Children.Clear();

                foreach (var notif in _notifications)
                {
                    var card = CreateNotificationCard(notif);
                    NotificationsPanel.Children.Add(card);
                }
            });
        }

        private Border CreateNotificationCard(WidgetNotification notif)
        {
            Color badgeColor;
            string priorityText;

            switch (notif.Priority)
            {
                case 0:
                    badgeColor = Color.Parse("#0078D4");
                    priorityText = "一般";
                    break;
                case 1:
                    badgeColor = Color.Parse("#FF8C00");
                    priorityText = "重要";
                    break;
                case 2:
                    badgeColor = Color.Parse("#E81123");
                    priorityText = "紧急";
                    break;
                default:
                    badgeColor = Color.Parse("#0078D4");
                    priorityText = "一般";
                    break;
            }

            var card = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#33FFFFFF")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0)
            };

            var grid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
                ColumnDefinitions = new ColumnDefinitions("*,Auto")
            };

            var titleText = new TextBlock
            {
                Text = notif.Title,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 4)
            };
            Grid.SetRow(titleText, 0);
            Grid.SetColumn(titleText, 0);

            var priorityBadge = new Border
            {
                Background = new SolidColorBrush(badgeColor),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(8, 0, 0, 0)
            };

            var priorityTextBlock = new TextBlock
            {
                Text = priorityText,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White
            };
            priorityBadge.Child = priorityTextBlock;
            Grid.SetRow(priorityBadge, 0);
            Grid.SetColumn(priorityBadge, 1);

            var messageText = new TextBlock
            {
                Text = notif.Message,
                FontSize = 13,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 4)
            };
            Grid.SetRow(messageText, 1);
            Grid.SetColumnSpan(messageText, 2);

            var timeText = new TextBlock
            {
                Text = notif.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#80FFFFFF")),
                Margin = new Thickness(0, 4, 0, 0)
            };
            Grid.SetRow(timeText, 2);
            Grid.SetColumnSpan(timeText, 2);

            grid.Children.Add(titleText);
            grid.Children.Add(priorityBadge);
            grid.Children.Add(messageText);
            grid.Children.Add(timeText);

            card.Child = grid;

            return card;
        }

        private void PositionWidget()
        {
            try
            {
                if (!File.Exists(_widgetConfigPath))
                {
                    var screen = Screens.Primary;
                    if (screen != null)
                    {
                        var workingArea = screen.WorkingArea;
                        const double marginFromLeft = 24;
                        const double marginFromTop = 24;

                        double x = workingArea.X + marginFromLeft;
                        double y = workingArea.Y + marginFromTop;

                        Position = new PixelPoint((int)x, (int)y);
                    }
                }
            }
            catch
            {
            }
        }

        private void OnRefreshClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            UpdateNotificationsUI();
        }

        private void OnSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.Show();
                MainWindow.Instance.Activate();
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
            base.OnClosing(e);
        }

        public override void Show()
        {
            base.Show();
            SendToBottom();
        }

        public static void ShowWidget(string? className = null)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_instance == null)
                {
                    _instance = new DesktopWidget();
                }
                _instance.Show();
                _instance.SendToBottom();
                if (!string.IsNullOrEmpty(className))
                {
                    _instance.SetClassName(className);
                }
            });
        }

        public static void HideWidget()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _instance?.Hide();
            });
        }
    }

    public class WidgetNotification
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Time { get; set; }
        public int Priority { get; set; }
    }

    public class WidgetConfig
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}
