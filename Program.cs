using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Configuration;

namespace StartPCApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public partial class MainForm : Form
    {
        private WebView2 webView;
        
        // Windows API 声明
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [DllImport("shell32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        
        // 热键相关API
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        // 关机相关API
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool InitiateSystemShutdownEx(string lpMachineName, string lpMessage, uint dwTimeout, bool bForceAppsClosed, bool bRebootAfterShutdown, uint dwReason);
        
        private const int SW_MINIMIZE = 6;
        private const byte VK_LWIN = 0x5B;
        private const byte VK_D = 0x44;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        
        // 热键常量
        private const int HOTKEY_ID_SHUTDOWN = 1;
        private const int HOTKEY_ID_TIMER = 2;
        private const int HOTKEY_ID_SCALE = 3;
        private const int HOTKEY_ID_CUSTOM_URL = 4;
        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint VK_Q = 0x51;
        private const uint VK_W = 0x57;
        private const uint VK_E = 0x45;
        private const uint VK_R = 0x52;
        private const int WM_HOTKEY = 0x0312;
        
        // 定时器
        private System.Windows.Forms.Timer shutdownTimer;
        
        // 缩放相关
        private float currentScale = 1.0f;
        private readonly float[] scaleOptions = { 0.75f, 1.0f, 1.25f, 1.5f, 1.75f, 2.0f };
        private int currentScaleIndex = 1; // 默认100%
        
        // 设置相关
        private string customUrl = "https://www.coren.xin/winlyrics/video";
        private string settingsFilePath;
        
        public MainForm()
        {
            InitializeComponent();
            InitializeSettings();
            LoadSettings();
            CheckStartupOption();
            MinimizeAllWindows();
            SetupFullScreen();
            
            // 确保键盘事件能够被捕获
            this.KeyPreview = true;
            this.Focus();
        }
        
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            
            // 窗体加载完成后再初始化WebView和注册热键
            InitializeWebView();
            RegisterGlobalHotKeys();
        }
        
        private void InitializeComponent()
        {
            this.SuspendLayout();
            
            // MainForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Name = "MainForm";
            this.Text = "Start PC App";
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.KeyPreview = true;
            
            this.ResumeLayout(false);
        }
        
        private void MinimizeAllWindows()
        {
            try
            {
                // 使用 Win+D 快捷键显示桌面
                keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
                keybd_event(VK_D, 0, 0, UIntPtr.Zero);
                keybd_event(VK_D, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                
                // 等待一下让操作完成
                System.Threading.Thread.Sleep(500);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"最小化窗口时出错: {ex.Message}");
            }
        }
        
        private void SetupFullScreen()
        {
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
        }
        
        private async void InitializeWebView()
        {
            try
            {
                webView = new WebView2()
                {
                    Dock = DockStyle.Fill
                };
                
                this.Controls.Add(webView);
                
                // 设置用户数据目录到AppData，避免权限问题
                string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StartPCApp", "WebView2Data");
                if (!Directory.Exists(userDataFolder))
                {
                    Directory.CreateDirectory(userDataFolder);
                }
                
                // 设置WebView2环境选项
                var environmentOptions = new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = "--disable-web-security --allow-running-insecure-content --disable-features=VizDisplayCompositor --no-sandbox --disable-gpu-sandbox"
                };
                
                // 创建WebView2环境，指定用户数据目录
                var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder, environmentOptions);
                
                // 等待WebView2初始化完成
                await webView.EnsureCoreWebView2Async(environment);
                
                // 基本设置 - 保持简单避免显示问题
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.IsScriptEnabled = true;
                
                // 高DPI支持设置
                webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                
                // 设置更高的缩放因子提升清晰度
                webView.ZoomFactor = 1.5;
                
                // 导航到指定网页
                webView.CoreWebView2.Navigate(customUrl);
                
                // 页面加载完成后注入优化脚本
                webView.CoreWebView2.NavigationCompleted += async (sender, e) =>
                {
                    if (e.IsSuccess)
                    {
                        // 超高清显示优化
                        await webView.CoreWebView2.ExecuteScriptAsync(@"
                            // 超高清CSS优化
                            const style = document.createElement('style');
                            style.textContent = `
                                * {
                                    -webkit-font-smoothing: subpixel-antialiased !important;
                                    -moz-osx-font-smoothing: auto !important;
                                    text-rendering: geometricPrecision !important;
                                    image-rendering: crisp-edges !important;
                                    image-rendering: -webkit-crisp-edges !important;
                                    image-rendering: -moz-crisp-edges !important;
                                    image-rendering: pixelated !important;
                                }
                                body {
                                    margin: 0 !important;
                                    padding: 0 !important;
                                    overflow: hidden !important;
                                    zoom: 1.0 !important;
                                    transform: scale(1.0) !important;
                                    transform-origin: top left !important;
                                }
                                video {
                                    width: 100% !important;
                                    height: 100vh !important;
                                    object-fit: cover !important;
                                    image-rendering: crisp-edges !important;
                                    image-rendering: pixelated !important;
                                    filter: contrast(1.1) brightness(1.05) !important;
                                }
                                canvas {
                                    image-rendering: crisp-edges !important;
                                    image-rendering: pixelated !important;
                                    filter: contrast(1.1) brightness(1.05) !important;
                                }
                                img {
                                    image-rendering: crisp-edges !important;
                                    image-rendering: pixelated !important;
                                    filter: contrast(1.1) brightness(1.05) !important;
                                }
                            `;
                            document.head.appendChild(style);
                            
                            // 设置超高清视口
                            let viewport = document.querySelector('meta[name=viewport]');
                            if (!viewport) {
                                viewport = document.createElement('meta');
                                viewport.name = 'viewport';
                                document.head.appendChild(viewport);
                            }
                            viewport.content = 'width=device-width, initial-scale=0.67, maximum-scale=2.0, user-scalable=yes';
                            
                            // 强制超高质量渲染
                            document.documentElement.style.imageRendering = 'crisp-edges';
                            document.body.style.imageRendering = 'crisp-edges';
                            document.body.style.filter = 'contrast(1.1) brightness(1.05)';
                            
                            // 强制重新渲染所有元素
                            const allElements = document.querySelectorAll('*');
                            allElements.forEach(el => {
                                el.style.imageRendering = 'crisp-edges';
                                if (el.tagName === 'VIDEO' || el.tagName === 'IMG' || el.tagName === 'CANVAS') {
                                    el.style.filter = 'contrast(1.1) brightness(1.05)';
                                }
                            });
                        ");
                    }
                };
                
                // 添加键盘事件处理，按ESC键退出程序
                this.KeyDown += MainForm_KeyDown;
                webView.KeyDown += MainForm_KeyDown;
            }
            catch (Exception ex)
            {
                // 详细的错误处理
                string errorMessage = $"WebView2初始化失败: {ex.Message}";
                string solutions = "";
                
                if (ex.HResult == unchecked((int)0x80070005)) // E_ACCESSDENIED
                {
                    solutions = "\n\n解决方案:\n1. 以管理员身份运行程序\n2. 检查防病毒软件是否阻止程序访问\n3. 确保用户数据目录有写入权限";
                }
                else if (ex.Message.Contains("Expecting object to be local"))
                {
                    solutions = "\n\n解决方案:\n1. 重启程序\n2. 以管理员身份运行\n3. 安装最新版Microsoft Edge\n4. 重新安装WebView2运行时";
                }
                else if (ex.Message.Contains("WebView2") || ex.Message.Contains("CoreWebView2"))
                {
                    solutions = "\n\n解决方案:\n1. 安装Microsoft Edge WebView2运行时\n2. 更新Microsoft Edge浏览器\n3. 以管理员身份运行程序\n4. 检查系统是否支持WebView2";
                }
                else
                {
                    solutions = "\n\n解决方案:\n1. 重启程序\n2. 以管理员身份运行\n3. 检查系统兼容性\n4. 联系技术支持";
                }
                
                errorMessage += solutions;
                
                // 记录详细错误信息
                string logMessage = $"WebView2初始化错误详情:\n" +
                                  $"错误消息: {ex.Message}\n" +
                                  $"错误代码: 0x{ex.HResult:X8}\n" +
                                  $"堆栈跟踪: {ex.StackTrace}\n" +
                                  $"用户数据目录: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StartPCApp", "WebView2Data")}";
                
                try
                {
                    string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StartPCApp", "error.log");
                    File.WriteAllText(logPath, $"[{DateTime.Now}] {logMessage}\n\n");
                }
                catch { /* 忽略日志写入错误 */ }
                
                MessageBox.Show(errorMessage, "WebView2初始化错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                
                // 创建一个简单的标签作为回退
                Label fallbackLabel = new Label
                {
                    Text = "WebView2初始化失败\n\n" +
                           "可能原因:\n" +
                           "• 缺少WebView2运行时\n" +
                           "• 权限不足\n" +
                           "• 系统兼容性问题\n\n" +
                           "请尝试:\n" +
                           "1. 以管理员身份运行\n" +
                           "2. 安装Microsoft Edge\n" +
                           "3. 下载WebView2运行时",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Microsoft YaHei", 10, FontStyle.Regular),
                    ForeColor = Color.DarkRed,
                    BackColor = Color.LightYellow
                };
                this.Controls.Add(fallbackLabel);
            }
        }
        
        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                // 确认退出
                var result = MessageBox.Show("确定要退出程序吗？", "退出确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    Application.Exit();
                }
            }
        }
        
        private void InitializeSettings()
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appFolder = Path.Combine(appDataPath, "StartPCApp");
                if (!Directory.Exists(appFolder))
                {
                    Directory.CreateDirectory(appFolder);
                }
                settingsFilePath = Path.Combine(appFolder, "settings.txt");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置初始化失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    string[] lines = File.ReadAllLines(settingsFilePath);
                    foreach (string line in lines)
                    {
                        if (line.StartsWith("ScaleIndex="))
                        {
                            if (int.TryParse(line.Substring(11), out int scaleIndex) && scaleIndex >= 0 && scaleIndex < scaleOptions.Length)
                            {
                                currentScaleIndex = scaleIndex;
                                currentScale = scaleOptions[currentScaleIndex];
                            }
                        }
                        else if (line.StartsWith("CustomUrl="))
                        {
                            string url = line.Substring(10);
                            if (!string.IsNullOrWhiteSpace(url))
                            {
                                customUrl = url;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置加载失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void SaveSettings()
        {
            try
            {
                string[] lines = {
                    $"ScaleIndex={currentScaleIndex}",
                    $"CustomUrl={customUrl}"
                };
                File.WriteAllLines(settingsFilePath, lines);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置保存失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void RegisterGlobalHotKeys()
        {
            try
            {
                // 注册热键
                bool success = true;
                
                if (!RegisterHotKey(this.Handle, HOTKEY_ID_SHUTDOWN, MOD_ALT, VK_Q))
                {
                    success = false;
                    MessageBox.Show("注册热键 Alt+Q 失败，可能与其他程序冲突", "热键注册警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                
                if (!RegisterHotKey(this.Handle, HOTKEY_ID_TIMER, MOD_ALT, VK_W))
                {
                    success = false;
                    MessageBox.Show("注册热键 Alt+W 失败，可能与其他程序冲突", "热键注册警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                
                if (!RegisterHotKey(this.Handle, HOTKEY_ID_SCALE, MOD_ALT, VK_E))
                {
                    success = false;
                    MessageBox.Show("注册热键 Alt+E 失败，可能与其他程序冲突", "热键注册警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                
                if (!RegisterHotKey(this.Handle, HOTKEY_ID_CUSTOM_URL, MOD_ALT, VK_R))
                {
                    success = false;
                    MessageBox.Show("注册热键 Alt+R 失败，可能与其他程序冲突", "热键注册警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                
                if (success)
                {
                    // 可以选择显示成功消息，或者保持静默
                    // MessageBox.Show("所有热键注册成功！", "信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"热键注册失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int hotkeyId = m.WParam.ToInt32();
                switch (hotkeyId)
                {
                    case HOTKEY_ID_SHUTDOWN:
                        HandleShutdownHotkey();
                        break;
                    case HOTKEY_ID_TIMER:
                        HandleTimerHotkey();
                        break;
                    case HOTKEY_ID_SCALE:
                        HandleScaleHotkey();
                        break;
                    case HOTKEY_ID_CUSTOM_URL:
                        HandleCustomUrlHotkey();
                        break;
                }
            }
            base.WndProc(ref m);
        }
        
        private void HandleShutdownHotkey()
        {
            var result = MessageBox.Show("确定要关机吗？", "关机确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                ShutdownComputer();
            }
        }
        
        private void HandleTimerHotkey()
        {
            using (var dialog = new TimerShutdownDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    SetupShutdownTimer(dialog.Minutes);
                }
            }
        }
        
        private void HandleScaleHotkey()
        {
            using (var dialog = new ScaleDialog(currentScaleIndex))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    currentScaleIndex = dialog.SelectedScaleIndex;
                    currentScale = scaleOptions[currentScaleIndex];
                    ApplyScale();
                    SaveSettings();
                }
            }
        }
        
        private void HandleCustomUrlHotkey()
        {
            using (var dialog = new CustomUrlDialog(customUrl))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    customUrl = dialog.CustomUrl;
                    SaveSettings();
                    
                    // 导航到新URL
                    if (webView?.CoreWebView2 != null)
                    {
                        webView.CoreWebView2.Navigate(customUrl);
                    }
                }
            }
        }
        
        private void ShutdownComputer()
        {
            try
            {
                // 使用Windows API关机
                InitiateSystemShutdownEx(null, "系统将在10秒后关机", 10, true, false, 0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"关机失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void SetupShutdownTimer(int minutes)
        {
            if (shutdownTimer != null)
            {
                shutdownTimer.Stop();
                shutdownTimer.Dispose();
            }
            
            shutdownTimer = new System.Windows.Forms.Timer();
            shutdownTimer.Interval = minutes * 60 * 1000; // 转换为毫秒
            shutdownTimer.Tick += (sender, e) =>
            {
                shutdownTimer.Stop();
                ShutdownComputer();
            };
            shutdownTimer.Start();
            
            MessageBox.Show($"定时关机已设置，将在 {minutes} 分钟后关机", "定时关机", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void ApplyScale()
        {
            try
            {
                // 应用到WebView
                if (webView?.CoreWebView2 != null)
                {
                    webView.ZoomFactor = currentScale * 1.5; // 基础缩放 * 用户选择的缩放
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用缩放失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void CheckStartupOption()
        {
            try
            {
                // 检查是否设置了开机启动
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (key != null)
                {
                    object value = key.GetValue("StartPCApp");
                    if (value != null)
                    {
                        // 已设置开机启动，可以在这里添加相关逻辑
                    }
                    key.Close();
                }
            }
            catch (Exception ex)
            {
                // 忽略注册表访问错误
                System.Diagnostics.Debug.WriteLine($"检查启动项失败: {ex.Message}");
            }
        }
        
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                // 注销所有热键
                UnregisterHotKey(this.Handle, HOTKEY_ID_SHUTDOWN);
                UnregisterHotKey(this.Handle, HOTKEY_ID_TIMER);
                UnregisterHotKey(this.Handle, HOTKEY_ID_SCALE);
                UnregisterHotKey(this.Handle, HOTKEY_ID_CUSTOM_URL);
                
                // 停止定时器
                if (shutdownTimer != null)
                {
                    shutdownTimer.Stop();
                    shutdownTimer.Dispose();
                }
                
                // 释放WebView资源
                if (webView != null)
                {
                    webView.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理资源时出错: {ex.Message}");
            }
            
            base.OnFormClosing(e);
        }
    }
    
    // 定时关机对话框
    public class TimerShutdownDialog : Form
    {
        public int Minutes { get; private set; }
        
        private NumericUpDown numericUpDown;
        private Button okButton;
        private Button cancelButton;
        
        public TimerShutdownDialog()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = "定时关机";
            this.Size = new Size(300, 150);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            Label label = new Label
            {
                Text = "请输入关机时间（分钟）：",
                Location = new Point(20, 20),
                Size = new Size(200, 20)
            };
            
            numericUpDown = new NumericUpDown
            {
                Location = new Point(20, 50),
                Size = new Size(100, 20),
                Minimum = 1,
                Maximum = 1440, // 最大24小时
                Value = 30 // 默认30分钟
            };
            
            okButton = new Button
            {
                Text = "确定",
                Location = new Point(140, 80),
                Size = new Size(60, 25),
                DialogResult = DialogResult.OK
            };
            okButton.Click += (sender, e) => { Minutes = (int)numericUpDown.Value; };
            
            cancelButton = new Button
            {
                Text = "取消",
                Location = new Point(210, 80),
                Size = new Size(60, 25),
                DialogResult = DialogResult.Cancel
            };
            
            this.Controls.AddRange(new Control[] { label, numericUpDown, okButton, cancelButton });
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
    }
    
    // 缩放对话框
    public class ScaleDialog : Form
    {
        public int SelectedScaleIndex { get; private set; }
        
        private ComboBox comboBox;
        private Button okButton;
        private Button cancelButton;
        
        private readonly string[] scaleTexts = { "75%", "100%", "125%", "150%", "175%", "200%" };
        
        public ScaleDialog(int currentScaleIndex)
        {
            SelectedScaleIndex = currentScaleIndex;
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = "界面缩放";
            this.Size = new Size(300, 150);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            Label label = new Label
            {
                Text = "请选择缩放比例：",
                Location = new Point(20, 20),
                Size = new Size(200, 20)
            };
            
            comboBox = new ComboBox
            {
                Location = new Point(20, 50),
                Size = new Size(100, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboBox.Items.AddRange(scaleTexts);
            comboBox.SelectedIndex = SelectedScaleIndex;
            
            okButton = new Button
            {
                Text = "确定",
                Location = new Point(140, 80),
                Size = new Size(60, 25),
                DialogResult = DialogResult.OK
            };
            okButton.Click += (sender, e) => { SelectedScaleIndex = comboBox.SelectedIndex; };
            
            cancelButton = new Button
            {
                Text = "取消",
                Location = new Point(210, 80),
                Size = new Size(60, 25),
                DialogResult = DialogResult.Cancel
            };
            
            this.Controls.AddRange(new Control[] { label, comboBox, okButton, cancelButton });
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
    }
    
    // 自定义URL对话框
    public class CustomUrlDialog : Form
    {
        public string CustomUrl { get; private set; }
        
        private TextBox textBox;
        private Button okButton;
        private Button cancelButton;
        
        public CustomUrlDialog(string currentUrl)
        {
            CustomUrl = currentUrl;
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = "自定义URL";
            this.Size = new Size(400, 150);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            Label label = new Label
            {
                Text = "请输入网页URL：",
                Location = new Point(20, 20),
                Size = new Size(200, 20)
            };
            
            textBox = new TextBox
            {
                Location = new Point(20, 50),
                Size = new Size(340, 20),
                Text = CustomUrl
            };
            
            okButton = new Button
            {
                Text = "确定",
                Location = new Point(240, 80),
                Size = new Size(60, 25),
                DialogResult = DialogResult.OK
            };
            okButton.Click += (sender, e) => { CustomUrl = textBox.Text; };
            
            cancelButton = new Button
            {
                Text = "取消",
                Location = new Point(310, 80),
                Size = new Size(60, 25),
                DialogResult = DialogResult.Cancel
            };
            
            this.Controls.AddRange(new Control[] { label, textBox, okButton, cancelButton });
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
    }
}