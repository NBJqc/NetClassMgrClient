using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Windows.Forms;

namespace NetClassManage.Updater
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string targetDir;
            int mainPid;

            if (args.Length < 2)
            {
                var currentDir = AppDomain.CurrentDomain.BaseDirectory;
                var updateZipPath = Path.Combine(currentDir, "update.zip");

                if (File.Exists(updateZipPath))
                {
                    targetDir = currentDir;
                    mainPid = -1;
                }
                else
                {
                    MessageBox.Show(
                        "此程序由主程序自动调用。\n\n如需手动更新，请将 update.zip 放置在本程序同目录下。",
                        "NetClassMgr 更新程序",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
            }
            else
            {
                targetDir = args[0];
                mainPid = int.Parse(args[1]);
            }

            var updateZipFullPath = Path.Combine(targetDir, "update.zip");

            if (!File.Exists(updateZipFullPath))
            {
                MessageBox.Show(
                    "未找到 update.zip 文件，将启动客户端。",
                    "NetClassMgr 更新程序",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                StartMainApplication(targetDir);
                return;
            }

            Application.Run(new UpdateForm(targetDir, mainPid, updateZipFullPath));
        }

        private static void StartMainApplication(string targetDir)
        {
            try
            {
                var mainExePath = Path.Combine(targetDir, "NetClassManage.exe");
                if (File.Exists(mainExePath))
                {
                    Process.Start(new ProcessStartInfo(mainExePath)
                    {
                        WorkingDirectory = targetDir,
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }
    }

    public class UpdateForm : Form
    {
        private readonly string _targetDir;
        private readonly int _mainPid;
        private readonly string _updateZipPath;
        
        private Label _iconLabel;
        private Label _titleLabel;
        private Label _statusLabel;
        private ProgressBar _progressBar;
        private Label _progressLabel;
        private Panel _dividerPanel;

        private readonly Color _accentColor = Color.FromArgb(0, 120, 212);
        private readonly Color _backgroundColor = Color.FromArgb(32, 32, 35);
        private readonly Color _borderColor = Color.FromArgb(0, 120, 212);
        private readonly Color _textColor = Color.White;
        private readonly Color _secondaryTextColor = Color.FromArgb(180, 180, 180);

        public UpdateForm(string targetDir, int mainPid, string updateZipPath)
        {
            _targetDir = targetDir;
            _mainPid = mainPid;
            _updateZipPath = updateZipPath;

            InitializeForm();
            InitializeControls();

            Shown += async (s, e) => await RunUpdateAsync();
        }

        private void InitializeForm()
        {
            Text = "NetClassMgr 客户端更新";
            Size = new Size(450, 240);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = _backgroundColor;
            DoubleBuffered = true;

            var screen = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(
                screen.X + (screen.Width - Width) / 2,
                screen.Y + (screen.Height - Height) / 2
            );

            Region = CreateRoundedRegion(ClientRectangle, 8);
        }

        private Region CreateRoundedRegion(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return new Region(path);
        }

        private void InitializeControls()
        {
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 20),
                BackColor = Color.Transparent
            };

            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.Transparent
            };

            _iconLabel = new Label
            {
                Text = "⬇",
                Font = new Font("Segoe UI", 20),
                ForeColor = _accentColor,
                AutoSize = true,
                Location = new Point(0, 5),
                BackColor = Color.Transparent
            };

            _titleLabel = new Label
            {
                Text = "NetClassMgr 客户端更新",
                Font = new Font("Microsoft YaHei UI", 14, FontStyle.Bold),
                ForeColor = _textColor,
                AutoSize = true,
                Location = new Point(35, 8),
                BackColor = Color.Transparent
            };

            var badgePanel = new Panel
            {
                Size = new Size(50, 24),
                Location = new Point(_titleLabel.Right + 10, 10),
                BackColor = _accentColor
            };
            badgePanel.Region = CreateRoundedRegion(new Rectangle(0, 0, 50, 24), 4);

            var badgeLabel = new Label
            {
                Text = "更新",
                Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            badgePanel.Controls.Add(badgeLabel);

            headerPanel.Controls.Add(_iconLabel);
            headerPanel.Controls.Add(_titleLabel);
            headerPanel.Controls.Add(badgePanel);

            _dividerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(80, 80, 85),
                Margin = new Padding(0, 0, 0, 16)
            };

            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 16, 0, 0)
            };

            _statusLabel = new Label
            {
                Text = "正在准备更新...",
                Font = new Font("Microsoft YaHei UI", 11),
                ForeColor = _secondaryTextColor,
                AutoSize = true,
                Location = new Point(0, 0),
                BackColor = Color.Transparent
            };

            _progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Width = ClientSize.Width - 48,
                Height = 6,
                Location = new Point(24, 35),
                Style = ProgressBarStyle.Continuous
            };

            _progressLabel = new Label
            {
                Text = "0%",
                Font = new Font("Microsoft YaHei UI", 10),
                ForeColor = Color.FromArgb(120, 120, 120),
                AutoSize = true,
                Location = new Point(0, 50),
                BackColor = Color.Transparent
            };
            _progressLabel.Left = (ClientSize.Width - _progressLabel.Width) / 2;

            contentPanel.Controls.Add(_statusLabel);
            contentPanel.Controls.Add(_progressBar);
            contentPanel.Controls.Add(_progressLabel);

            mainPanel.Controls.Add(contentPanel);
            mainPanel.Controls.Add(_dividerPanel);
            mainPanel.Controls.Add(headerPanel);

            Controls.Add(mainPanel);

            var dragHelper = new DragHelper(this, headerPanel);
            dragHelper.Enable();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using (var pen = new Pen(_borderColor, 1))
            {
                var rect = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, CreateRoundedPath(rect, 8));
            }
        }

        private GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        private async Task RunUpdateAsync()
        {
            try
            {
                UpdateStatus("正在停止相关进程...");
                await StopRelatedProcesses();

                UpdateStatus("正在解压更新文件...");
                await ExtractZipAsync(_updateZipPath, _targetDir);

                UpdateStatus("正在清理临时文件...");
                try
                {
                    if (File.Exists(_updateZipPath))
                    {
                        File.Delete(_updateZipPath);
                    }
                }
                catch { }

                UpdateStatus("更新完成！");
                UpdateProgress(100);
                await Task.Delay(1500);

                UpdateStatus("正在启动客户端...");
                StartMainApplication(_targetDir);

                await Task.Delay(500);
                Close();
            }
            catch (Exception ex)
            {
                UpdateStatus("更新失败: " + ex.Message);
                await Task.Delay(3000);
                StartMainApplication(_targetDir);
                Close();
            }
        }

        private void UpdateStatus(string status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatus(status)));
                return;
            }
            _statusLabel.Text = status;
        }

        private void UpdateProgress(int value)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateProgress(value)));
                return;
            }
            _progressBar.Value = value;
            _progressLabel.Text = $"{value}%";
            _progressLabel.Left = (ClientSize.Width - _progressLabel.Width) / 2;
        }

        private async Task StopRelatedProcesses()
        {
            try
            {
                if (_mainPid > 0)
                {
                    UpdateStatus("正在关闭主程序...");
                    try
                    {
                        var process = Process.GetProcessById(_mainPid);
                        if (process != null && !process.HasExited)
                        {
                            process.Kill();
                            await Task.Run(() => process.WaitForExit(5000));
                        }
                    }
                    catch { }
                }
            }
            catch { }

            UpdateStatus("正在检查其他占用进程...");
            await Task.Delay(1000);

            try
            {
                var processes = Process.GetProcessesByName("NetClassManage");
                foreach (var process in processes)
                {
                    if (process.Id != Process.GetCurrentProcess().Id)
                    {
                        try
                        {
                            process.Kill();
                            process.WaitForExit(3000);
                        }
                        catch { }
                    }
                }
            }
            catch { }

            await Task.Delay(2000);
        }

        private async Task ExtractZipAsync(string zipPath, string targetDir)
        {
            await Task.Run(() =>
            {
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    var totalEntries = archive.Entries.Count;
                    var completedEntries = 0;

                    foreach (var entry in archive.Entries)
                    {
                        completedEntries++;
                        var progress = (int)((double)completedEntries / totalEntries * 100);
                        UpdateProgress(progress);

                        var entryTargetPath = Path.Combine(targetDir, entry.FullName);

                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(entryTargetPath);
                            continue;
                        }

                        var directoryPath = Path.GetDirectoryName(entryTargetPath);
                        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                        {
                            Directory.CreateDirectory(directoryPath);
                        }

                        int retries = 0;
                        const int maxRetries = 5;
                        bool extracted = false;

                        while (retries < maxRetries && !extracted)
                        {
                            try
                            {
                                if (File.Exists(entryTargetPath))
                                {
                                    File.SetAttributes(entryTargetPath, FileAttributes.Normal);
                                    File.Delete(entryTargetPath);
                                }
                                entry.ExtractToFile(entryTargetPath, true);
                                extracted = true;
                            }
                            catch (IOException ex)
                            {
                                retries++;
                                if (retries >= maxRetries)
                                {
                                    throw new Exception($"无法提取文件 '{entry.FullName}': {ex.Message}");
                                }
                                Thread.Sleep(2000);
                            }
                        }
                    }
                }
            });
        }

        private void StartMainApplication(string targetDir)
        {
            try
            {
                var mainExePath = Path.Combine(targetDir, "NetClassManage.exe");
                if (File.Exists(mainExePath))
                {
                    Process.Start(new ProcessStartInfo(mainExePath)
                    {
                        WorkingDirectory = targetDir,
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }
    }

    internal class DragHelper
    {
        private readonly Form _form;
        private readonly Control _dragControl;
        private bool _dragging;
        private Point _dragCursorPoint;
        private Point _dragFormPoint;

        public DragHelper(Form form, Control dragControl)
        {
            _form = form;
            _dragControl = dragControl;
        }

        public void Enable()
        {
            _dragControl.MouseDown += OnMouseDown;
            _dragControl.MouseMove += OnMouseMove;
            _dragControl.MouseUp += OnMouseUp;
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            _dragging = true;
            _dragCursorPoint = Cursor.Position;
            _dragFormPoint = _form.Location;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;

            var diff = Point.Subtract(Cursor.Position, new Size(_dragCursorPoint));
            _form.Location = Point.Add(_dragFormPoint, new Size(diff));
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            _dragging = false;
        }
    }
}
