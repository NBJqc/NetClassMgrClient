using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using NetClassManage.Views;
using OpenCvSharp;
using ReactiveUI;

namespace NetClassManage.ViewModels
{
    public class MainViewModel : ReactiveObject
    {
        private string _serverUrl = "https://ncmgr.nbjqc.cn";
        private string _token = "";
        private string _deviceName = Environment.MachineName;
        private bool _autoConnect = true;
        private int _heartbeatInterval = 30;
        private bool _autoUpdate = true;
        private int _checkInterval = 24;
        private bool _isConnected;
        private string _statusText = "状态: 未连接";
        private string _updateInfo = "当前版本: 1.0.0\n\n最后检查: 从未\n\n更新状态: 就绪";
        private string _logText = "";
        private string _currentPage = "Connect";
        private bool _isCheckingUpdate;
        private bool _hasUpdate;
        private string _newVersionInfo = "";
        private string _deviceId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
        private string _deviceStatus = "离线";
        private string _lastOnlineTime = "-";
        private CancellationTokenSource? _heartbeatCts;
        private CancellationTokenSource? _shutdownCts;
        private string _notificationHistory = "";
        private ObservableCollection<NotificationHistoryItem> _notificationHistoryItems = new ObservableCollection<NotificationHistoryItem>();
        private string _className = "班级名称";
        
        private ClientUpdateInfo? _latestUpdateInfo;
        private bool _isUpdating;
        private double _updateProgress;

        private readonly string _configPath = "client_config.json";
        private readonly string _notificationHistoryPath = "notification_history.json";
        private static readonly HttpClient _sharedClient;

        // 固定的更新服务器地址
        private const string UPDATE_SERVER_URL = "https://ncmgr.nbjqc.cn";

        static MainViewModel()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (msg, cert, chain, err) => true,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            _sharedClient = new HttpClient(handler);
            _sharedClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public string ServerUrl
        {
            get => _serverUrl;
            set => this.RaiseAndSetIfChanged(ref _serverUrl, value);
        }

        public string Token
        {
            get => _token;
            set => this.RaiseAndSetIfChanged(ref _token, value);
        }

        public string DeviceName
        {
            get => _deviceName;
            set => this.RaiseAndSetIfChanged(ref _deviceName, value);
        }

        public bool AutoConnect
        {
            get => _autoConnect;
            set => this.RaiseAndSetIfChanged(ref _autoConnect, value);
        }

        public int HeartbeatInterval
        {
            get => _heartbeatInterval;
            set => this.RaiseAndSetIfChanged(ref _heartbeatInterval, value);
        }

        public bool AutoUpdate
        {
            get => _autoUpdate;
            set => this.RaiseAndSetIfChanged(ref _autoUpdate, value);
        }

        public int CheckInterval
        {
            get => _checkInterval;
            set => this.RaiseAndSetIfChanged(ref _checkInterval, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            set => this.RaiseAndSetIfChanged(ref _isConnected, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => this.RaiseAndSetIfChanged(ref _statusText, value);
        }

        public string UpdateInfo
        {
            get => _updateInfo;
            set => this.RaiseAndSetIfChanged(ref _updateInfo, value);
        }

        public string LogText
        {
            get => _logText;
            set => this.RaiseAndSetIfChanged(ref _logText, value);
        }

        public bool IsCheckingUpdate
        {
            get => _isCheckingUpdate;
            set => this.RaiseAndSetIfChanged(ref _isCheckingUpdate, value);
        }

        public bool HasUpdate
        {
            get => _hasUpdate;
            set => this.RaiseAndSetIfChanged(ref _hasUpdate, value);
        }

        public string NewVersionInfo
        {
            get => _newVersionInfo;
            set => this.RaiseAndSetIfChanged(ref _newVersionInfo, value);
        }

        public bool IsUpdating
        {
            get => _isUpdating;
            set => this.RaiseAndSetIfChanged(ref _isUpdating, value);
        }

        public double UpdateProgress
        {
            get => _updateProgress;
            set => this.RaiseAndSetIfChanged(ref _updateProgress, value);
        }

        public string DeviceId
        {
            get => _deviceId;
            set => this.RaiseAndSetIfChanged(ref _deviceId, value);
        }

        public string DeviceStatus
        {
            get => _deviceStatus;
            set => this.RaiseAndSetIfChanged(ref _deviceStatus, value);
        }

        public string LastOnlineTime
        {
            get => _lastOnlineTime;
            set => this.RaiseAndSetIfChanged(ref _lastOnlineTime, value);
        }

        public string NotificationHistory
        {
            get => _notificationHistory;
            set => this.RaiseAndSetIfChanged(ref _notificationHistory, value);
        }

        public ObservableCollection<NotificationHistoryItem> NotificationHistoryItems
        {
            get => _notificationHistoryItems;
            set => this.RaiseAndSetIfChanged(ref _notificationHistoryItems, value);
        }

        private FluentAvalonia.UI.Controls.NavigationViewItem? _selectedNavItem;
        public FluentAvalonia.UI.Controls.NavigationViewItem? SelectedNavItem
        {
            get => _selectedNavItem;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedNavItem, value);
                if (value?.Tag != null)
                {
                    CurrentPage = value.Tag.ToString() ?? "Connect";
                }
            }
        }

        public string CurrentPage
        {
            get => _currentPage;
            set
            {
                this.RaiseAndSetIfChanged(ref _currentPage, value);
                this.RaisePropertyChanged(nameof(IsConnectPage));
                this.RaisePropertyChanged(nameof(IsDevicePage));
                this.RaisePropertyChanged(nameof(IsNotificationPage));
                this.RaisePropertyChanged(nameof(IsUpdatePage));
                this.RaisePropertyChanged(nameof(IsLogPage));
                this.RaisePropertyChanged(nameof(IsAboutPage));
            }
        }

        public bool IsConnectPage => CurrentPage == "Connect";
        public bool IsDevicePage => CurrentPage == "Device";
        public bool IsNotificationPage => CurrentPage == "Notification";
        public bool IsUpdatePage => CurrentPage == "Update";
        public bool IsLogPage => CurrentPage == "Log";
        public bool IsAboutPage => CurrentPage == "About";

        public event Action? OnConnect;
        public event Action? OnDisconnect;
        public event Action? OnSave;
        public event Action? OnTest;
        public event Action? OnCheckUpdate;
        public event Action? OnClearLog;
        public event Action? OnClearNotification;
        public event Action? OnDoUpdate;
        public event Action? OnRefreshDevice;
        public event Action? OnRestartDevice;

        public void ToggleWidget()
        {
            if (DesktopWidget.Instance == null || !DesktopWidget.Instance.IsVisible)
            {
                DesktopWidget.ShowWidget(_className);
            }
            else
            {
                DesktopWidget.HideWidget();
            }
        }

        public MainViewModel()
        {
            LoadConfig();
            LoadNotificationHistory();

            OnConnect += async () => await ConnectAsync();
            OnDisconnect += Disconnect;
            OnSave += async () => await SaveAsync();
            OnTest += async () => await TestAsync();
            OnCheckUpdate += async () => await CheckUpdateAsync();
            OnClearLog += ClearLog;
            OnClearNotification += ClearNotification;
            OnDoUpdate += DoUpdate;
            OnRefreshDevice += RefreshDevice;
            OnRestartDevice += RestartDevice;

            AddLog("程序启动 - 版本: 1.0.0");
            AddLog("配置文件加载: " + _configPath);

            if (AutoConnect)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(500);
                    await Dispatcher.UIThread.InvokeAsync(() => Connect());
                });
            }
        }

        public void Connect() => OnConnect?.Invoke();
        public void Disconnect_() => OnDisconnect?.Invoke();
        public void Save() => OnSave?.Invoke();
        public void Test() => OnTest?.Invoke();
        public void CheckUpdate() => OnCheckUpdate?.Invoke();
        public void ClearLog_() => OnClearLog?.Invoke();
        public void ClearNotification_() => OnClearNotification?.Invoke();
        public void DoUpdate_() => OnDoUpdate?.Invoke();
        public void RefreshDevice_() => OnRefreshDevice?.Invoke();
        public void RestartDevice_() => OnRestartDevice?.Invoke();

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var config = JsonSerializer.Deserialize<ClientConfig>(json);
                    if (config != null)
                    {
                        ServerUrl = config.ServerUrl ?? "https://ncmgr.nbjqc.cn";
                        Token = config.Token ?? "";
                        DeviceName = string.IsNullOrEmpty(config.DeviceName) ? Environment.MachineName : config.DeviceName;
                        AutoConnect = config.AutoConnect;
                        HeartbeatInterval = config.HeartbeatInterval;
                        AutoUpdate = config.AutoUpdate;
                        CheckInterval = config.CheckInterval;
                        if (!string.IsNullOrEmpty(config.DeviceId))
                        {
                            DeviceId = config.DeviceId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog("加载配置失败: " + ex.Message);
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                var config = new ClientConfig
                {
                    ServerUrl = ServerUrl,
                    Token = Token,
                    DeviceName = DeviceName,
                    AutoConnect = AutoConnect,
                    HeartbeatInterval = HeartbeatInterval,
                    AutoUpdate = AutoUpdate,
                    CheckInterval = CheckInterval,
                    DeviceId = DeviceId
                };

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_configPath, json);
                AddLog("配置保存成功");
            }
            catch (Exception ex)
            {
                AddLog("保存配置失败: " + ex.Message);
            }
        }

        private async Task ConnectAsync()
        {
            if (string.IsNullOrEmpty(ServerUrl))
            {
                AddLog("错误: 请输入服务器地址");
                return;
            }

            AddLog("正在连接服务器: " + ServerUrl);
            StatusText = "状态: 正在连接...";

            try
            {
                var registered = await RegisterDeviceAsync();
                if (!registered)
                {
                    AddLog("设备注册失败");
                    StatusText = "状态: 注册失败";
                    return;
                }

                StatusText = "状态: 已连接\n服务器: " + ServerUrl;
                IsConnected = true;
                DeviceStatus = "在线";
                LastOnlineTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                AddLog("连接成功，设备已注册");

                StartHeartbeat();
                
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    await CheckUpdateAsync();
                });
            }
            catch (Exception ex)
            {
                AddLog("连接失败: " + ex.Message);
                StatusText = "状态: 连接失败\n错误: " + ex.Message;
                DeviceStatus = "离线";
            }
        }

        private async Task<bool> RegisterDeviceAsync()
        {
            try
            {
                var clientInfo = new Dictionary<string, string>
                {
                    ["device_id"] = DeviceId,
                    ["device_name"] = DeviceName,
                    ["os_version"] = Environment.OSVersion.ToString(),
                    ["machine_name"] = Environment.MachineName,
                    ["user_name"] = Environment.UserName,
                    ["register_time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                var clientInfoJson = JsonSerializer.Serialize(clientInfo);
                
                var formData = new Dictionary<string, string>
                {
                    ["action"] = "register_client",
                    ["client_info"] = clientInfoJson
                };

                if (!string.IsNullOrEmpty(Token))
                {
                    formData["token"] = Token;
                }

                var content = new FormUrlEncodedContent(formData);
                var url = ServerUrl.TrimEnd('/') + "/api.php";
                var response = await _sharedClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var resultStr = await response.Content.ReadAsStringAsync();
                    AddLog("设备注册响应: " + resultStr);
                    
                    try
                    {
                        var result = JsonSerializer.Deserialize<ApiResponse>(resultStr, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        
                        if (result != null && result.Success)
                        {
                            if (result.Data != null)
                            {
                                try
                                {
                                    var dataJson = JsonSerializer.Serialize(result.Data);
                                    var dataDict = JsonSerializer.Deserialize<Dictionary<string, object>>(dataJson);
                                    if (dataDict != null && dataDict.ContainsKey("class_name"))
                                    {
                                        _className = dataDict["class_name"]?.ToString() ?? "班级名称";
                                        await Dispatcher.UIThread.InvokeAsync(() =>
                                        {
                                            if (DesktopWidget.Instance != null)
                                            {
                                                DesktopWidget.Instance.SetClassName(_className);
                                            }
                                        });
                                    }
                                }
                                catch
                                {
                                }
                            }
                            AddLog("设备注册成功");
                            return true;
                        }
                        else
                        {
                            AddLog("注册失败: " + (result?.Message ?? "未知错误"));
                            return false;
                        }
                    }
                    catch (JsonException ex)
                    {
                        AddLog("解析注册响应失败: " + ex.Message);
                        return false;
                    }
                }
                AddLog("HTTP错误: " + response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                AddLog("注册异常: " + ex.Message);
                return false;
            }
        }

        private void StartHeartbeat()
        {
            _heartbeatCts?.Cancel();
            _heartbeatCts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                while (!_heartbeatCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(HeartbeatInterval * 1000, _heartbeatCts.Token);
                        await HeartbeatAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => AddLog("心跳异常: " + ex.Message));
                    }
                }
            });
        }

        private void StopHeartbeat()
        {
            _heartbeatCts?.Cancel();
            _heartbeatCts = null;
        }

        private async Task HeartbeatAsync()
        {
            try
            {
                var formData = new Dictionary<string, string>
                {
                    ["action"] = "heartbeat"
                };

                if (!string.IsNullOrEmpty(Token))
                {
                    formData["token"] = Token;
                }

                var content = new FormUrlEncodedContent(formData);
                var url = ServerUrl.TrimEnd('/') + "/api.php";
                var response = await _sharedClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var resultStr = await response.Content.ReadAsStringAsync();
                    
                    await Dispatcher.UIThread.InvokeAsync(() => AddLog("心跳响应: " + resultStr.Substring(0, Math.Min(500, resultStr.Length))));
                    
                    try
                    {
                        var result = JsonSerializer.Deserialize<HeartbeatResponse>(resultStr, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (result != null && result.Success)
                        {
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                LastOnlineTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            });

                            if (result.Data != null)
                            {
                                if (!string.IsNullOrEmpty(result.Data.ClassName))
                                {
                                    _className = result.Data.ClassName;
                                    await Dispatcher.UIThread.InvokeAsync(() =>
                                    {
                                        if (DesktopWidget.Instance != null)
                                        {
                                            DesktopWidget.Instance.SetClassName(_className);
                                        }
                                    });
                                }

                                if (result.Data.Notifications != null)
                                {
                                    foreach (var notif in result.Data.Notifications)
                                    {
                                        await ProcessNotificationAsync(notif);
                                    }
                                }

                                if (result.Data.Commands != null)
                                {
                                    foreach (var cmd in result.Data.Commands)
                                    {
                                        await ProcessServerCommandAsync(cmd);
                                    }
                                }
                            }
                        }
                        else
                        {
                            await Dispatcher.UIThread.InvokeAsync(() => 
                                AddLog("心跳失败: " + (result?.Message ?? "未知错误")));
                        }
                    }
                    catch (JsonException ex)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => 
                            AddLog("解析心跳响应失败: " + ex.Message + " | 响应: " + resultStr.Substring(0, Math.Min(200, resultStr.Length))));
                    }
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() => AddLog("心跳请求异常: " + ex.Message));
            }
        }

        private async Task ProcessNotificationAsync(ServerNotification notif)
        {
            var title = notif.Title ?? "服务器通知";
            var content = notif.Content ?? notif.Message ?? "";
            await ShowNotificationAsync(title, content, notif.Priority);
            
            if (DesktopWidget.Instance != null)
            {
                DesktopWidget.Instance.AddNotification(title, content, DateTime.Now, notif.Priority);
            }
        }

        private async Task ProcessServerCommandAsync(ServerCommand cmd)
        {
            var cmdType = cmd.CommandType ?? cmd.Type ?? "unknown";
            var cmdMessage = cmd.Message ?? "";
            
            await Dispatcher.UIThread.InvokeAsync(() => 
            {
                AddLog("收到指令: " + cmdType + " - " + cmdMessage);
                AddLog("  原始数据 - CommandType: " + (cmd.CommandType ?? "null") + ", Type: " + (cmd.Type ?? "null"));
                AddLog("  CommandData: " + (cmd.CommandData ?? "null"));
                AddLog("  Url: " + (cmd.Url ?? "null"));
            });

            switch (cmdType.ToLower())
            {
                case "notification":
                case "notify":
                    await ShowNotificationAsync(cmdMessage);
                    break;

                case "shutdown":
                case "poweroff":
                    await ExecuteShutdownAsync();
                    break;

                case "photo":
                case "camera":
                case "take_photo":
                case "capture_photo":
                    await CaptureAndUploadPhotoAsync();
                    break;

                case "restart":
                    await ExecuteRestartAsync();
                    break;

                case "play_audio":
                    await ExecutePlayAudioAsync(cmd);
                    break;

                case "open_url":
                    await ExecuteOpenUrlAsync(cmd);
                    break;

                default:
                    await Dispatcher.UIThread.InvokeAsync(() => AddLog("未知指令类型: " + cmdType));
                    break;
            }

            if (cmd.Id > 0)
            {
                await CommandExecutedAsync(cmd.Id);
            }
        }

        private async Task CommandExecutedAsync(int commandId)
        {
            try
            {
                var formData = new Dictionary<string, string>
                {
                    ["action"] = "command_executed",
                    ["command_id"] = commandId.ToString()
                };

                if (!string.IsNullOrEmpty(Token))
                {
                    formData["token"] = Token;
                }

                var content = new FormUrlEncodedContent(formData);
                var url = ServerUrl.TrimEnd('/') + "/api.php";
                await _sharedClient.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() => AddLog("确认指令执行失败: " + ex.Message));
            }
        }

        private async Task ShowNotificationAsync(string title, string message, int priority = 0)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                AddNotification(title, message, priority);
                
                // Check if this is a fullscreen notification (priority 3 or higher)
                if (priority >= 3)
                {
                    await FullscreenNotificationWindow.ShowFullscreenNotification(title, message);
                }
                else
                {
                    await NotificationWindow.ShowNotification(title, message, priority);
                }
            });
        }

        private async Task ShowNotificationAsync(string message)
        {
            await ShowNotificationAsync("服务器通知", message, 0);
        }

        private void AddNotification(string notification)
        {
            AddNotification("服务器通知", notification, 0);
        }

        private void AddNotification(string title, string message, int priority)
        {
            var now = DateTime.Now;
            var timeStr = now.ToString("yyyy-MM-dd HH:mm:ss");
            NotificationHistory += "[" + timeStr + "] " + title + ": " + message + "\n";
            
            var item = new NotificationHistoryItem
            {
                Time = timeStr,
                Title = title,
                Message = message,
                Priority = priority
            };
            
            _notificationHistoryItems.Insert(0, item);
            this.RaisePropertyChanged(nameof(NotificationHistoryItems));
            
            if (MainWindow.Instance != null)
            {
                Dispatcher.UIThread.Post(() => 
                {
                    MainWindow.Instance.AddNotificationCard(timeStr, title, message, priority);
                });
            }
            
            SaveNotificationHistory();
            AddLog("显示通知: [" + timeStr + "] " + title + ": " + message);
        }

        private async Task ExecuteShutdownAsync()
        {
            _shutdownCts?.Cancel();
            _shutdownCts = new CancellationTokenSource();
            
            await ShowShutdownNotificationAsync();
            
            try
            {
                await Task.Delay(10000, _shutdownCts.Token);
                
                if (!_shutdownCts.Token.IsCancellationRequested)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        Process.Start(new ProcessStartInfo("shutdown", "/s /t 0") { CreateNoWindow = true });
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        Process.Start(new ProcessStartInfo("shutdown", "-h now") { CreateNoWindow = true });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AddLog("关机已取消");
            }
        }

        private async Task ShowShutdownNotificationAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var window = new Views.NotificationWindow("关机提醒", "服务器发送了关机指令，系统将在10秒后关机", 2);
                window.SetShutdownMode(true);
                window.ShutdownCancelled += (s, e) =>
                {
                    _shutdownCts?.Cancel();
                };
                window.Show();
                window.Topmost = true;
            });
            
            AddNotification("关机提醒", "服务器发送了关机指令，系统将在10秒后关机", 2);
        }

        private async Task ExecuteRestartAsync()
        {
            await ShowNotificationAsync("服务器发送了重启指令，系统将在30秒后重启");
            await Task.Delay(30000);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("shutdown", "/r /t 0") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start(new ProcessStartInfo("reboot") { CreateNoWindow = true });
            }
        }

        private async Task ExecutePlayAudioAsync(ServerCommand cmd)
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() => AddLog("正在下载音频..."));
                
                string audioUrl;
                if (!string.IsNullOrEmpty(cmd.Url))
                {
                    audioUrl = cmd.Url;
                }
                else if (!string.IsNullOrEmpty(cmd.CommandData))
                {
                    try
                    {
                        var data = JsonSerializer.Deserialize<JsonElement>(cmd.CommandData);
                        if (data.TryGetProperty("url", out var urlProp))
                        {
                            audioUrl = urlProp.GetString() ?? "";
                        }
                        else
                        {
                            audioUrl = "";
                        }
                    }
                    catch
                    {
                        audioUrl = cmd.CommandData;
                    }
                }
                else
                {
                    audioUrl = "";
                }

                if (!string.IsNullOrEmpty(audioUrl))
                {
                    var fullUrl = audioUrl.StartsWith("http") ? audioUrl : ServerUrl.TrimEnd('/') + "/" + audioUrl.TrimStart('/');
                    var audioData = await _sharedClient.GetByteArrayAsync(fullUrl);
                    
                    var tempPath = Path.Combine(Path.GetTempPath(), "audio_" + Guid.NewGuid().ToString("N") + ".mp3");
                    await File.WriteAllBytesAsync(tempPath, audioData);

                    await Dispatcher.UIThread.InvokeAsync(() => AddLog("音频已下载，正在播放..."));

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = tempPath,
                            UseShellExecute = true,
                            CreateNoWindow = false
                        };
                        Process.Start(psi);
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        Process.Start(new ProcessStartInfo("aplay", tempPath) { CreateNoWindow = true });
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        Process.Start(new ProcessStartInfo("afplay", tempPath) { CreateNoWindow = true });
                    }

                    await ShowNotificationAsync("正在播放音频");
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() => AddLog("播放音频失败: " + ex.Message));
            }
        }

        private async Task ExecuteOpenUrlAsync(ServerCommand cmd)
        {
            try
            {
                var url = cmd.Url ?? cmd.Data?.Url;
                if (!string.IsNullOrEmpty(url))
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    }
                    await ShowNotificationAsync("正在打开URL: " + url);
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() => AddLog("打开URL失败: " + ex.Message));
            }
        }

        private async Task CaptureAndUploadPhotoAsync()
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() => AddLog("正在尝试拍照..."));

                var cameras = new List<int>();
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        using var cap = new VideoCapture(i);
                        if (cap.IsOpened())
                        {
                            cameras.Add(i);
                            cap.Release();
                        }
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => AddLog($"检测摄像头 {i} 失败: {ex.Message}"));
                        continue;
                    }
                }

                if (cameras.Count == 0)
                {
                    await ShowNotificationAsync("未找到可用摄像头");
                    return;
                }

                int successCount = 0;
                foreach (int camIdx in cameras)
                {
                    try
                    {
                        var photoPath = Path.Combine(Path.GetTempPath(), $"photo_cam{camIdx}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                        
                        using var cap = new VideoCapture(camIdx);
                        if (cap.IsOpened())
                        {
                            using var frame = new Mat();
                            cap.Read(frame);
                            if (!frame.Empty())
                            {
                                Cv2.ImWrite(photoPath, frame);
                                
                                if (File.Exists(photoPath))
                                {
                                    await UploadPhotoAsync(photoPath, camIdx);
                                    successCount++;
                                }
                            }
                            cap.Release();
                        }
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => AddLog($"摄像头 {camIdx} 拍照失败: {ex.Message}"));
                        continue;
                    }
                }

                await ShowNotificationAsync($"已拍摄 {successCount} 个摄像头的照片");
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() => AddLog("拍照异常: " + ex.Message));
                await ShowNotificationAsync("拍照失败: " + ex.Message);
            }
        }

        private async Task UploadPhotoAsync(string photoPath, int cameraIndex = 0)
        {
            try
            {
                var photoBytes = await File.ReadAllBytesAsync(photoPath);
                var fileName = Path.GetFileName(photoPath);

                using var multipartContent = new MultipartFormDataContent();
                multipartContent.Add(new StringContent("upload_photo"), "action");
                multipartContent.Add(new StringContent(cameraIndex.ToString()), "camera_index");
                multipartContent.Add(new ByteArrayContent(photoBytes), "photo", fileName);
                
                if (!string.IsNullOrEmpty(Token))
                {
                    multipartContent.Add(new StringContent(Token), "token");
                }

                var url = ServerUrl.TrimEnd('/') + "/api.php";
                var response = await _sharedClient.PostAsync(url, multipartContent);

                if (response.IsSuccessStatusCode)
                {
                    var resultStr = await response.Content.ReadAsStringAsync();
                    await Dispatcher.UIThread.InvokeAsync(() => AddLog("照片上传响应: " + resultStr));
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(() => AddLog("照片上传失败: " + response.StatusCode));
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() => AddLog("上传异常: " + ex.Message));
            }
            finally
            {
                try
                {
                    if (File.Exists(photoPath))
                        File.Delete(photoPath);
                }
                catch { }
            }
        }

        private void Disconnect()
        {
            StopHeartbeat();
            AddLog("正在断开连接...");
            StatusText = "状态: 未连接\n设备: " + Environment.MachineName;
            IsConnected = false;
            DeviceStatus = "离线";
            AddLog("已断开连接");
        }

        private async Task TestAsync()
        {
            if (string.IsNullOrEmpty(ServerUrl))
            {
                AddLog("错误: 请输入服务器地址");
                return;
            }

            AddLog("正在测试连接: " + ServerUrl);

            try
            {
                var formData = new Dictionary<string, string>
                {
                    ["action"] = "verify_connection"
                };

                var content = new FormUrlEncodedContent(formData);
                var url = ServerUrl.TrimEnd('/') + "/api.php";
                var response = await _sharedClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var resultStr = await response.Content.ReadAsStringAsync();
                    await Dispatcher.UIThread.InvokeAsync(() => AddLog("连接测试响应: " + resultStr));
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(() => AddLog("连接测试失败: HTTP " + response.StatusCode));
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() => AddLog("连接测试失败: " + ex.Message));
            }
        }

        private async Task CheckUpdateAsync()
        {
            IsCheckingUpdate = true;
            AddLog("正在检查更新...");

            try
            {
                var url = UPDATE_SERVER_URL.TrimEnd('/') + "/api.php?action=check_update&version_code=100";
                var response = await _sharedClient.GetStringAsync(url).ConfigureAwait(false);

                var result = JsonSerializer.Deserialize<ApiResponse<ClientUpdateResponse>>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateInfo = "当前版本: 1.0.0\n\n最后检查: " + 
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    if (result?.Success == true && result.Data?.HasUpdate == true)
                    {
                        _latestUpdateInfo = new ClientUpdateInfo
                        {
                            Version = result.Data.Version,
                            VersionCode = result.Data.VersionCode,
                            UpdateNotes = result.Data.UpdateNotes,
                            IsMandatory = result.Data.IsMandatory,
                            FileSize = result.Data.FileSize,
                            FileHash = result.Data.FileHash,
                            DownloadUrl = result.Data.DownloadUrl,
                            PublishedAt = result.Data.PublishedAt
                        };

                        HasUpdate = true;
                        NewVersionInfo = $"发现新版本: {_latestUpdateInfo.Version}\n\n" +
                            $"版本代码: {_latestUpdateInfo.VersionCode}\n" +
                            $"发布时间: {_latestUpdateInfo.PublishedAt}\n\n" +
                            $"更新说明:\n{_latestUpdateInfo.UpdateNotes}";
                        AddLog($"发现新版本: {_latestUpdateInfo.Version}");
                        
                        AddLog("开始自动下载更新...");
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(2000);
                            await Dispatcher.UIThread.InvokeAsync(() => DoUpdate());
                        });
                    }
                    else
                    {
                        HasUpdate = false;
                        _latestUpdateInfo = null;
                        AddLog("当前已是最新版本");
                    }
                    IsCheckingUpdate = false;
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AddLog("更新检查失败: " + ex.Message);
                    IsCheckingUpdate = false;
                });
            }
        }

        private async void DoUpdate()
        {
            if (_latestUpdateInfo == null)
            {
                AddLog("没有可用的更新");
                return;
            }

            IsUpdating = true;
            UpdateProgress = 0;
            AddLog("开始更新...");

            try
            {
                var updateZipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update.zip");
                var updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NetClassManage.Updater.exe");

                if (!File.Exists(updaterPath))
                {
                    AddLog("错误: 更新程序不存在");
                    IsUpdating = false;
                    return;
                }

                AddLog("正在下载更新包...");
                using (var response = await _sharedClient.GetAsync(_latestUpdateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? -1;
                    var downloadedBytes = 0L;

                    using (var fileStream = new FileStream(updateZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    {
                        var buffer = new byte[8192];
                        int bytesRead;
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            downloadedBytes += bytesRead;
                            if (totalBytes > 0)
                            {
                                UpdateProgress = (double)downloadedBytes / totalBytes * 100;
                            }
                        }
                    }
                }

                UpdateProgress = 100;
                AddLog("下载完成，验证文件...");

                var fileHash = ComputeFileHash(updateZipPath);
                if (!string.Equals(fileHash, _latestUpdateInfo.FileHash, StringComparison.OrdinalIgnoreCase))
                {
                    AddLog("错误: 文件校验失败");
                    File.Delete(updateZipPath);
                    IsUpdating = false;
                    return;
                }

                AddLog("文件验证通过，准备更新...");

                var currentProcess = Process.GetCurrentProcess();
                var targetDir = AppDomain.CurrentDomain.BaseDirectory;

                AddLog("正在启动更新程序...");
                var startInfo = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = $"\"{targetDir}\" {currentProcess.Id}",
                    UseShellExecute = true,
                    WorkingDirectory = targetDir
                };

                Process.Start(startInfo);

                AddLog("更新程序已启动，正在关闭客户端...");
                await Task.Delay(1000);

                currentProcess.Kill();
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AddLog("更新失败: " + ex.Message);
                    IsUpdating = false;
                });
            }
        }

        private string ComputeFileHash(string filePath)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private void RefreshDevice()
        {
            AddLog("正在刷新设备状态...");
            if (IsConnected)
            {
                DeviceStatus = "在线";
                LastOnlineTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                DeviceStatus = "离线";
            }
            AddLog("设备状态已刷新");
        }

        private void RestartDevice()
        {
            AddLog("重启设备命令已发送...");
            AddLog("此功能需要服务器支持");
        }

        private void ClearLog()
        {
            LogText = "";
        }

        private void ClearNotification()
        {
            NotificationHistory = "";
            _notificationHistoryItems.Clear();
            this.RaisePropertyChanged(nameof(NotificationHistoryItems));
            
            if (MainWindow.Instance != null)
            {
                Dispatcher.UIThread.Post(() => 
                {
                    MainWindow.Instance.ClearNotificationCards();
                });
            }
            
            if (File.Exists(_notificationHistoryPath))
            {
                try
                {
                    File.Delete(_notificationHistoryPath);
                }
                catch (Exception ex)
                {
                    AddLog("删除通知历史文件失败: " + ex.Message);
                }
            }
        }

        private void SaveNotificationHistory()
        {
            try
            {
                var items = new List<NotificationHistoryItem>(_notificationHistoryItems);
                var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_notificationHistoryPath, json);
            }
            catch (Exception ex)
            {
                AddLog("保存通知历史失败: " + ex.Message);
            }
        }

        private void LoadNotificationHistory()
        {
            try
            {
                if (File.Exists(_notificationHistoryPath))
                {
                    var json = File.ReadAllText(_notificationHistoryPath);
                    var items = JsonSerializer.Deserialize<List<NotificationHistoryItem>>(json);
                    if (items != null)
                    {
                        _notificationHistoryItems.Clear();
                        foreach (var item in items)
                        {
                            _notificationHistoryItems.Add(item);
                            NotificationHistory += "[" + item.Time + "] " + item.Title + ": " + item.Message + "\n";
                        }
                        
                        if (MainWindow.Instance != null)
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                MainWindow.Instance.ClearNotificationCards();
                                foreach (var item in items)
                                {
                                    MainWindow.Instance.AddNotificationCard(item.Time, item.Title, item.Message, item.Priority);
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog("加载通知历史失败: " + ex.Message);
            }
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            LogText += "[" + timestamp + "] " + message + "\n";
        }
    }

    public class ApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public object? Data { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }

    public class ClientUpdateResponse
    {
        [JsonPropertyName("has_update")]
        public bool HasUpdate { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("version_code")]
        public int VersionCode { get; set; }

        [JsonPropertyName("update_notes")]
        public string? UpdateNotes { get; set; }

        [JsonPropertyName("is_mandatory")]
        public bool IsMandatory { get; set; }

        [JsonPropertyName("file_size")]
        public long FileSize { get; set; }

        [JsonPropertyName("file_hash")]
        public string? FileHash { get; set; }

        [JsonPropertyName("download_url")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("published_at")]
        public string? PublishedAt { get; set; }
    }

    public class ClientUpdateInfo
    {
        public string? Version { get; set; }
        public int VersionCode { get; set; }
        public string? UpdateNotes { get; set; }
        public bool IsMandatory { get; set; }
        public long FileSize { get; set; }
        public string? FileHash { get; set; }
        public string? DownloadUrl { get; set; }
        public string? PublishedAt { get; set; }
    }

    public class NotificationHistoryItem
    {
        public string Time { get; set; } = "";
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public int Priority { get; set; }
    }

    public class HeartbeatResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        
        [JsonPropertyName("data")]
        public HeartbeatData? Data { get; set; }
    }

    public class HeartbeatData
    {
        [JsonPropertyName("notifications")]
        public List<ServerNotification>? Notifications { get; set; }
        
        [JsonPropertyName("commands")]
        public List<ServerCommand>? Commands { get; set; }
        
        [JsonPropertyName("class_name")]
        public string? ClassName { get; set; }
    }

    public class ServerNotification
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("class_id")]
        public int ClassId { get; set; }
        
        [JsonPropertyName("title")]
        public string? Title { get; set; }
        
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        
        [JsonPropertyName("content")]
        public string? Content { get; set; }
        
        [JsonPropertyName("type")]
        public string? Type { get; set; }
        
        [JsonPropertyName("notification_type")]
        public string? NotificationType { get; set; }
        
        [JsonPropertyName("priority")]
        public int Priority { get; set; }
    }

    public class ServerCommand
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("type")]
        public string? Type { get; set; }
        
        [JsonPropertyName("command_type")]
        public string? CommandType { get; set; }
        
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        
        [JsonPropertyName("url")]
        public string? Url { get; set; }
        
        [JsonPropertyName("command_data")]
        public string? CommandData { get; set; }
        
        [JsonPropertyName("data")]
        public CommandData? Data { get; set; }
    }

    public class CommandData
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
        
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    public class ClientConfig
    {
        public string ServerUrl { get; set; } = "http://ncmgr.nbjqc.cn/manager";
        public string Token { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public bool AutoConnect { get; set; } = true;
        public int HeartbeatInterval { get; set; } = 30;
        public bool AutoUpdate { get; set; } = true;
        public int CheckInterval { get; set; } = 24;
    }
}
