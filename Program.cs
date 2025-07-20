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
                
                // 设置WebView2环境选项
                var options = CoreWebView2Environment.CreateAsync(null, null, new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = "--disable-web-security --allow-running-insecure-content --disable-features=VizDisplayCompositor"
                });
                
                // 等待WebView2初始化完成
                await webView.EnsureCoreWebView2Async(await options);
                
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
                string errorMessage = $"初始化WebView时出错: {ex.Message}";
                if (ex.HResult == unchecked((int)0x80070005)) // E_ACCESSDENIED
                {
                    errorMessage += "\n\n可能的解决方案:\n1. 以管理员身份运行程序\n2. 确保已安装Microsoft Edge WebView2运行时\n3. 检查防病毒软件是否阻止了程序";
                }
                
                MessageBox.Show(errorMessage, "WebView初始化错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                
                // 创建一个简单的标签作为回退
                Label fallbackLabel = new Label
                {
                    Text = "WebView初始化失败\n\n请以管理员身份运行程序\n或安装Microsoft Edge WebView2运行时",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Microsoft YaHei", 12, FontStyle.Bold),
                    ForeColor = Color.Red,
                    BackColor = Color.White
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
                string[] settings = {
                    $"ScaleIndex={currentScaleIndex}",
                    $"CustomUrl={customUrl}"
                };
                File.WriteAllLines(settingsFilePath, settings);
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
                // 注册 Alt+Q 一键关机
                bool result1 = RegisterHotKey(this.Handle, HOTKEY_ID_SHUTDOWN, MOD_ALT, VK_Q);
                // 注册 Alt+W 定时关机
                bool result2 = RegisterHotKey(this.Handle, HOTKEY_ID_TIMER, MOD_ALT, VK_W);
                // 注册 Alt+E 缩放调整
                bool result3 = RegisterHotKey(this.Handle, HOTKEY_ID_SCALE, MOD_ALT, VK_E);
                // 注册 Alt+R 自定义网页
                bool result4 = RegisterHotKey(this.Handle, HOTKEY_ID_CUSTOM_URL, MOD_ALT, VK_R);
                
                if (!result1 || !result2 || !result3 || !result4)
                {
                    MessageBox.Show("热键注册失败，可能与其他程序冲突。\n\nAlt+Q: 一键关机\nAlt+W: 定时关机\nAlt+E: 缩放调整\nAlt+R: 自定义网页", "热键注册", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"热键注册错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            var result = MessageBox.Show("确定要立即关机吗？", "一键关机 (Alt+Q)", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                ShutdownComputer(0); // 立即关机
            }
        }
        
        private void HandleTimerHotkey()
        {
            ShowTimerShutdownDialog();
        }
        
        private void ShowTimerShutdownDialog()
        {
            using (var dialog = new TimerShutdownDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    int minutes = dialog.Minutes;
                    if (minutes > 0)
                    {
                        StartShutdownTimer(minutes);
                    }
                }
            }
        }
        
        private void StartShutdownTimer(int minutes)
        {
            // 停止之前的定时器
            shutdownTimer?.Stop();
            shutdownTimer?.Dispose();
            
            // 创建新的定时器
            shutdownTimer = new System.Windows.Forms.Timer();
            shutdownTimer.Interval = minutes * 60 * 1000; // 转换为毫秒
            shutdownTimer.Tick += (sender, e) =>
            {
                shutdownTimer.Stop();
                ShutdownComputer(0);
            };
            
            shutdownTimer.Start();
            
            MessageBox.Show($"定时关机已设置，将在 {minutes} 分钟后自动关机。\n\n再次按 Alt+W 可以重新设置时间。", "定时关机", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void ShutdownComputer(uint delay)
        {
            try
            {
                // 使用系统命令关机
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = $"/s /t {delay}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"关机失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void HandleScaleHotkey()
        {
            ShowScaleDialog();
        }
        
        private void HandleCustomUrlHotkey()
        {
            ShowCustomUrlDialog();
        }
        
        private void ShowScaleDialog()
        {
            using (var dialog = new ScaleDialog(currentScaleIndex))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    currentScaleIndex = dialog.SelectedScaleIndex;
                    currentScale = scaleOptions[currentScaleIndex];
                    ApplyScale();
                }
            }
        }
        
        private void ApplyScale()
        {
            try
            {
                // 应用缩放到窗体
                this.Scale(new SizeF(currentScale, currentScale));
                
                // 如果有WebView，也需要调整其缩放
                if (webView?.CoreWebView2 != null)
                {
                    webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                    // 设置WebView的缩放因子
                    webView.ZoomFactor = currentScale;
                }
                
                // 保存设置
                SaveSettings();
                
                MessageBox.Show($"缩放已设置为 {(int)(currentScale * 100)}%", "缩放设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"缩放设置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void ShowCustomUrlDialog()
        {
            using (var dialog = new CustomUrlDialog(customUrl))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    customUrl = dialog.CustomUrl;
                    SaveSettings();
                    
                    // 导航到新的URL
                    if (webView?.CoreWebView2 != null)
                    {
                        webView.CoreWebView2.Navigate(customUrl);
                    }
                    
                    MessageBox.Show("自定义网页已设置并加载！", "设置成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        
        private void CheckStartupOption()
        {
            try
            {
                string appName = "StartPCApp";
                string exePath = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
                
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    bool isInStartup = key?.GetValue(appName) != null;
                    
                    if (!isInStartup)
                    {
                        var result = MessageBox.Show(
                            "是否将此程序添加到开机启动项？\n\n这样可以在系统启动时自动运行程序。",
                            "添加到启动项",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question
                        );
                        
                        if (result == DialogResult.Yes)
                        {
                            AddToStartup(appName, exePath);
                            MessageBox.Show("已成功添加到开机启动项！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 静默处理启动项检查错误
                System.Diagnostics.Debug.WriteLine($"启动项检查错误: {ex.Message}");
            }
        }
        
        private void AddToStartup(string appName, string exePath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    key?.SetValue(appName, exePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加启动项失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 取消注册热键
            UnregisterHotKey(this.Handle, HOTKEY_ID_SHUTDOWN);
            UnregisterHotKey(this.Handle, HOTKEY_ID_TIMER);
            UnregisterHotKey(this.Handle, HOTKEY_ID_SCALE);
            UnregisterHotKey(this.Handle, HOTKEY_ID_CUSTOM_URL);
            
            // 停止定时器
            shutdownTimer?.Stop();
            shutdownTimer?.Dispose();
            
            webView?.Dispose();
            base.OnFormClosing(e);
        }
    }
    
    // 定时关机对话框
    public partial class TimerShutdownDialog : Form
    {
        private NumericUpDown numericUpDown;
        private Button btnOK;
        private Button btnCancel;
        private Label lblMinutes;
        
        public int Minutes { get; private set; }
        
        public TimerShutdownDialog()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.numericUpDown = new NumericUpDown();
            this.btnOK = new Button();
            this.btnCancel = new Button();
            this.lblMinutes = new Label();
            this.SuspendLayout();
            
            // lblMinutes
            this.lblMinutes.AutoSize = true;
            this.lblMinutes.Location = new System.Drawing.Point(12, 15);
            this.lblMinutes.Name = "lblMinutes";
            this.lblMinutes.Size = new System.Drawing.Size(200, 13);
            this.lblMinutes.TabIndex = 0;
            this.lblMinutes.Text = "请设置关机时间（分钟）：";
            
            // numericUpDown
            this.numericUpDown.Location = new System.Drawing.Point(12, 35);
            this.numericUpDown.Maximum = new decimal(new int[] { 1440, 0, 0, 0 }); // 最大24小时
            this.numericUpDown.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numericUpDown.Name = "numericUpDown";
            this.numericUpDown.Size = new System.Drawing.Size(200, 20);
            this.numericUpDown.TabIndex = 1;
            this.numericUpDown.Value = new decimal(new int[] { 30, 0, 0, 0 }); // 默认30分钟
            
            // btnOK
            this.btnOK.DialogResult = DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(56, 70);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 2;
            this.btnOK.Text = "确定";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            
            // btnCancel
            this.btnCancel.DialogResult = DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(137, 70);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            
            // TimerShutdownDialog
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(224, 105);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.numericUpDown);
            this.Controls.Add(this.lblMinutes);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TimerShutdownDialog";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "定时关机设置";
            this.TopMost = true;
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        
        private void btnOK_Click(object sender, EventArgs e)
        {
            Minutes = (int)numericUpDown.Value;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
    
    public partial class ScaleDialog : Form
    {
        private ComboBox comboBoxScale;
        private Button btnOK;
        private Button btnCancel;
        private Label lblScale;
        
        public int SelectedScaleIndex { get; private set; }
        
        public ScaleDialog(int currentScaleIndex)
        {
            SelectedScaleIndex = currentScaleIndex;
            InitializeComponent();
            comboBoxScale.SelectedIndex = currentScaleIndex;
        }
        
        private void InitializeComponent()
        {
            this.comboBoxScale = new ComboBox();
            this.btnOK = new Button();
            this.btnCancel = new Button();
            this.lblScale = new Label();
            this.SuspendLayout();
            
            // ScaleDialog
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(280, 120);
            this.Controls.Add(this.lblScale);
            this.Controls.Add(this.comboBoxScale);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ScaleDialog";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "缩放设置";
            this.TopMost = true;
            
            // lblScale
            this.lblScale.AutoSize = true;
            this.lblScale.Location = new Point(12, 15);
            this.lblScale.Name = "lblScale";
            this.lblScale.Size = new Size(150, 13);
            this.lblScale.TabIndex = 0;
            this.lblScale.Text = "请选择程序缩放比例：";
            
            // comboBoxScale
            this.comboBoxScale.DropDownStyle = ComboBoxStyle.DropDownList;
            this.comboBoxScale.FormattingEnabled = true;
            this.comboBoxScale.Items.AddRange(new object[] {
                "75% (小)",
                "100% (正常)",
                "125% (中等)",
                "150% (大)",
                "175% (很大)",
                "200% (超大)"
            });
            this.comboBoxScale.Location = new Point(12, 35);
            this.comboBoxScale.Name = "comboBoxScale";
            this.comboBoxScale.Size = new Size(250, 21);
            this.comboBoxScale.TabIndex = 1;
            
            // btnOK
            this.btnOK.DialogResult = DialogResult.OK;
            this.btnOK.Location = new Point(106, 70);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new Size(75, 23);
            this.btnOK.TabIndex = 2;
            this.btnOK.Text = "确定";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new EventHandler(this.btnOK_Click);
            
            // btnCancel
            this.btnCancel.DialogResult = DialogResult.Cancel;
            this.btnCancel.Location = new Point(187, 70);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new Size(75, 23);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        
        private void btnOK_Click(object sender, EventArgs e)
        {
            SelectedScaleIndex = comboBoxScale.SelectedIndex;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
    
    public partial class CustomUrlDialog : Form
    {
        public string CustomUrl { get; private set; }
        
        private TextBox urlTextBox;
        private Button okButton;
        private Button cancelButton;
        
        public CustomUrlDialog(string currentUrl)
        {
            InitializeComponent();
            urlTextBox.Text = currentUrl;
        }
        
        private void InitializeComponent()
        {
            this.Text = "设置自定义网页";
            this.Size = new Size(450, 150);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            // 创建标签
            Label label = new Label
            {
                Text = "请输入网页URL:",
                Location = new Point(20, 20),
                Size = new Size(200, 20)
            };
            
            // 创建文本框
            urlTextBox = new TextBox
            {
                Location = new Point(20, 50),
                Size = new Size(390, 25)
            };
            
            // 创建按钮
            okButton = new Button
            {
                Text = "确定",
                Location = new Point(255, 85),
                Size = new Size(75, 25),
                DialogResult = DialogResult.OK
            };
            okButton.Click += OkButton_Click;
            
            cancelButton = new Button
            {
                Text = "取消",
                Location = new Point(335, 85),
                Size = new Size(75, 25),
                DialogResult = DialogResult.Cancel
            };
            
            // 添加控件到窗体
            this.Controls.AddRange(new Control[] { label, urlTextBox, okButton, cancelButton });
            
            // 设置默认按钮
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
        
        private void OkButton_Click(object sender, EventArgs e)
        {
            string url = urlTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(url))
            {
                // 简单的URL验证
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }
                CustomUrl = url;
            }
            else
            {
                MessageBox.Show("请输入有效的URL", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
            }
        }
    }
}