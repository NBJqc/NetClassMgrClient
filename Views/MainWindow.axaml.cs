using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using NetClassManage.ViewModels;
using System;

namespace NetClassManage.Views
{
    public partial class MainWindow : AppWindow
    {
        public static MainWindow? Instance { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
            Closing += MainWindow_Closing;
        }

        public void AddNotificationCard(string time, string title, string message, int priority)
        {
            var card = new NotificationCard(time, title, message, priority);
            NotificationsPanel.Children.Insert(0, card);
        }

        public void ClearNotificationCards()
        {
            NotificationsPanel.Children.Clear();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void NavView_SelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                if (e.SelectedItem is NavigationViewItem item && item.Tag != null)
                {
                    var tag = item.Tag.ToString() ?? "Connect";
                    if (tag == "Widget")
                    {
                        viewModel.ToggleWidget();
                        NavView.SelectedItem = null;
                    }
                    else
                    {
                        viewModel.CurrentPage = tag;
                    }
                }
            }
        }

        private void OnConnectClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.Connect();
            }
        }

        private void OnDisconnectClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.Disconnect_();
            }
        }

        private void OnSaveClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.Save();
            }
        }

        private void OnTestClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.Test();
            }
        }

        private void OnCheckUpdateClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.CheckUpdate();
            }
        }

        private void OnDoUpdateClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.DoUpdate_();
            }
        }

        private void OnClearLogClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ClearLog_();
            }
        }

        private void OnClearNotificationClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ClearNotification_();
            }
        }

        private void OnRefreshDeviceClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.RefreshDevice_();
            }
        }

        private void OnRestartDeviceClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.RestartDevice_();
            }
        }
    }
}
