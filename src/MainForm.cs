using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ZkTimeSync
{
    /// <summary>
    /// 主窗体（外壳 + 运行日志与实时监控页）。
    ///
    /// 界面骨架：标准 Windows 标题栏 + 左侧 Fluent 导航栏（品牌区 / 导航项 / 版本信息）、
    /// 右侧顶部标题栏、中央页面宿主、底部状态栏。
    /// 采用 Windows 原生窗口边框（Sizable），支持系统 Snap、双击标题栏最大化、
    /// 标准最小化/最大化/关闭按钮，避免自绘无边框窗口的各类兼容风险。
    /// 所有颜色/字号/间距均取自 Theme 设计令牌，不出现硬编码色值。
    /// </summary>
    public partial class MainForm : Form
    {
        // ==================== 窗口布局 ====================
        /// <summary>侧栏宽度（与 BuildShell 的列宽保持一致）</summary>
        private const int SidebarWidth = 212;
        /// <summary>顶部标题栏高度（与 BuildContent 的行高保持一致）</summary>
        private const int HeaderHeight = 66;
        /// <summary>底部状态栏高度（与 BuildContent 的行高保持一致）</summary>
        private const int StatusHeight = 34;

        // ==================== 页面索引 ====================
        private const int PageLogs = 0;
        private const int PageDevices = 1;
        private const int PageScan = 2;
        private const int PageSchedule = 3;
        private const int PageNotify = 4;

        // ==================== 指标卡片索引 ====================
        private const int StatTotal = 0;
        private const int StatOk = 1;
        private const int StatFail = 2;
        private const int StatLatest = 3;
        private const int DevStatTotal = 4;
        private const int DevStatTz = 5;
        private const int DevStatOk = 6;
        private const int DevStatFail = 7;

        private static readonly string[] PageTitles = new string[]
        {
            "运行日志与实时监控",
            "记忆设备管理",
            "局域网扫描 / 数据库导入",
            "自动同步与定时任务",
            "企业微信通知设置"
        };

        private static readonly string[] PageSubtitles = new string[]
        {
            "查看后台调度与设备对时的完整输出，并可随时手动触发一次全员同步",
            "维护记忆设备库：端口、通讯密码与设备所在地时区（IANA）",
            "从网段扫描或 SQL Server 设备表批量发现考勤机并导入记忆库",
            "配置 CRON 计划、固定间隔，以及断电恢复后的启动自动同步",
            "同步结束后自动推送成功 / 失败设备清单到企业微信"
        };

        private static readonly string[] NavLabels = new string[] { "运行监控", "记忆设备", "扫描与导入", "自动同步", "微信通知" };
        // Segoe MDL2 Assets 图标字形（Fluent 官方图标），不使用 emoji
        private static readonly string[] NavGlyphs = new string[]
        {
            Theme.IconList, Theme.IconClock, Theme.IconSearch, Theme.IconCalendar, Theme.IconChat
        };
        private static readonly string[] WeekdayNames = new string[] { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };

        // ==================== 运行时状态 ====================
        private AppConfig _config;
        private TaskSchedulerService _scheduler;
        private System.Windows.Forms.Timer _uiRefreshTimer;
        private Action<string> _logSink;

        /// <summary>配置加载期间抑制控件事件回写</summary>
        private bool _loading = true;
        /// <summary>记忆设备表存在未保存修改（暂停自动刷新）</summary>
        private bool _gridDirty;
        /// <summary>正在后台计算下次执行时间</summary>
        private bool _previewBusy;
        private bool _scanning;

        private readonly List<string> _pendingLogs = new List<string>();
        private readonly object _pendingLogLock = new object();
        private readonly List<StatCard> _statCards = new List<StatCard>();
        private readonly List<NavItem> _navItems = new List<NavItem>();

        private Control[] _pages;
        private int _activePage = -1;

        private LogLevel _statusLevel = LogLevel.Info;
        private Panel _pnlStatus;
        private Label _lblStatusText;
        private Label _lblStatusRight;
        private Label _lblPageTitle;
        private Label _lblPageSubtitle;
        private Label _lblClock;
        private Label _lblServerTz;
        private Panel _pageHost;

        private UiLogConsole _txtLogs;
        private UiButton _btnSyncNow;
        private Label _lblLogHint;

        public MainForm()
        {
            BuildShell();
            BuildPages();

            _logSink = new Action<string>(AppendLogLine);
            Logger.OnLog += _logSink;

            LoadConfigAndInitialize();
        }

        // ==================== 布局辅助 ====================

        private static TableLayoutPanel NewGrid(int columns, int rows, Color backColor)
        {
            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.Margin = new Padding(0);
            grid.Padding = new Padding(0);
            grid.BackColor = backColor;
            grid.ColumnCount = columns;
            grid.RowCount = rows;
            return grid;
        }

        /// <summary>正数表示固定像素（Absolute），0 表示按比例自适应（Percent 100）</summary>
        private static void AddColumns(TableLayoutPanel grid, float[] widths)
        {
            for (int i = 0; i < widths.Length; i++)
            {
                grid.ColumnStyles.Add(widths[i] > 0f
                    ? new ColumnStyle(SizeType.Absolute, widths[i])
                    : new ColumnStyle(SizeType.Percent, 100f));
            }
            grid.ColumnCount = widths.Length;
        }

        /// <summary>正数表示固定像素（Absolute），0 表示按比例自适应（Percent 100）</summary>
        private static void AddRows(TableLayoutPanel grid, float[] heights)
        {
            for (int i = 0; i < heights.Length; i++)
            {
                grid.RowStyles.Add(heights[i] > 0f
                    ? new RowStyle(SizeType.Absolute, heights[i])
                    : new RowStyle(SizeType.Percent, 100f));
            }
            grid.RowCount = heights.Length;
        }

        private static UiButton MakeButton(string text, UiButtonKind kind, Padding margin)
        {
            UiButton button = new UiButton();
            button.Text = text;
            button.Kind = kind;
            button.Dock = DockStyle.Fill;
            button.Margin = margin;
            return button;
        }

        private static DeviceInfo FindDevice(List<DeviceInfo> devices, string ip)
        {
            if (devices == null || string.IsNullOrEmpty(ip)) return null;
            for (int i = 0; i < devices.Count; i++)
            {
                DeviceInfo device = devices[i];
                if (device != null && string.Equals(device.Ip, ip, StringComparison.OrdinalIgnoreCase)) return device;
            }
            return null;
        }

        private static string CellText(DataGridViewRow row, int index)
        {
            if (row == null || index < 0 || index >= row.Cells.Count) return "";
            object value = row.Cells[index].Value;
            return value == null ? "" : Convert.ToString(value).Trim();
        }

        private static bool TryParseCell(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text)) return false;
            return int.TryParse(text.Trim(), out value);
        }

        private static void FillRect(Graphics g, int x, int y, int width, int height, Color color)
        {
            if (width <= 0 || height <= 0) return;
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, x, y, width, height);
            }
        }

        /// <summary>在侧栏分区右侧补一条竖向发丝线，避免与内容区糊在一起</summary>
        private static void AttachSidebarHairline(Control control)
        {
            control.Paint += delegate(object s, PaintEventArgs e)
            {
                Control c = (Control)s;
                using (Pen pen = new Pen(Theme.SidebarDivider))
                {
                    e.Graphics.DrawLine(pen, c.Width - 1, 0, c.Width - 1, c.Height);
                }
            };
        }

        // ==================== 外壳 ====================

        private void BuildShell()
        {
            Text = "中控考勤/门禁时间全自动同步管理系统";
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            Font = Theme.FontBody;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);

            // 标准 Windows 窗口边框：原生标题栏与窗口按钮，支持 Snap / 双击最大化 /
            // 系统右键菜单，规避自绘无边框窗口的焦点、缩放与任务栏遮挡问题。
            FormBorderStyle = FormBorderStyle.Sizable;
            Size = new Size(1280, 800);
            MinimumSize = new Size(1024, 700);
            ShowIcon = false;

            TableLayoutPanel root = NewGrid(2, 1, Theme.Bg);
            AddColumns(root, new float[] { SidebarWidth, 0f });
            AddRows(root, new float[] { 0f });

            root.Controls.Add(BuildSidebar(), 0, 0);
            root.Controls.Add(BuildContent(), 1, 0);

            Controls.Add(root);
        }

        // ==================== 侧栏 / 内容 ====================

        private Control BuildSidebar()
        {
            TableLayoutPanel side = NewGrid(1, 3, Theme.Sidebar);
            AddColumns(side, new float[] { 0f });
            AddRows(side, new float[] { 84f, 0f, 56f });

            // 第一行：品牌区（Fluent 应用图标 + 产品名）
            BrandPanel brand = new BrandPanel();
            brand.Dock = DockStyle.Fill;
            brand.Margin = new Padding(0);
            AttachSidebarHairline(brand);
            side.Controls.Add(brand, 0, 0);

            // 第二行：导航项
            FlowLayoutPanel nav = new FlowLayoutPanel();
            nav.Dock = DockStyle.Fill;
            nav.Margin = new Padding(0);
            nav.FlowDirection = FlowDirection.TopDown;
            nav.WrapContents = false;
            nav.AutoScroll = false;
            nav.BackColor = Theme.Sidebar;
            nav.Padding = new Padding(Theme.Sp12, Theme.Sp12, Theme.Sp12, 0);
            AttachSidebarHairline(nav);

            for (int i = 0; i < NavLabels.Length; i++)
            {
                NavItem item = new NavItem();
                item.Glyph = NavGlyphs[i];
                item.Text = NavLabels[i];
                item.Width = SidebarWidth - Theme.Sp24;
                item.Height = 36;
                item.Margin = new Padding(0, 0, 0, Theme.Sp4);

                int index = i;
                item.Click += delegate { NavigateTo(index); };

                nav.Controls.Add(item);
                _navItems.Add(item);
            }
            side.Controls.Add(nav, 0, 1);

            // 第三行：版本信息
            Panel footer = new Panel();
            footer.Dock = DockStyle.Fill;
            footer.Margin = new Padding(0);
            footer.BackColor = Theme.Sidebar;
            AttachSidebarHairline(footer);

            Label line1 = UiStyle.CreateLabel("ZKTeco 考勤 / 门禁终端", Theme.FontTiny, Theme.SidebarMuted, Theme.Sidebar);
            line1.SetBounds(Theme.Sp16, 12, SidebarWidth - Theme.Sp24, 16);
            footer.Controls.Add(line1);

            Label line2 = UiStyle.CreateLabel("v2.1 · Windows 服务版", Theme.FontTiny, Theme.SidebarMuted, Theme.Sidebar);
            line2.SetBounds(Theme.Sp16, 28, SidebarWidth - Theme.Sp24, 16);
            footer.Controls.Add(line2);

            side.Controls.Add(footer, 0, 2);
            return side;
        }

        private Control BuildContent()
        {
            TableLayoutPanel content = NewGrid(1, 3, Theme.Bg);
            AddColumns(content, new float[] { 0f });
            AddRows(content, new float[] { HeaderHeight, 0f, StatusHeight });

            content.Controls.Add(BuildHeader(), 0, 0);
            content.Controls.Add(BuildPageHost(), 0, 1);
            content.Controls.Add(BuildStatusBar(), 0, 2);

            return content;
        }

        private Control BuildHeader()
        {
            Panel head = new Panel();
            head.Dock = DockStyle.Fill;
            head.Margin = new Padding(0);
            head.BackColor = Theme.Surface;
            head.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(pen, 0, head.Height - 1, head.Width, head.Height - 1);
                }
            };

            TableLayoutPanel grid = NewGrid(2, 2, Theme.Surface);
            AddColumns(grid, new float[] { 0f, 380f });
            AddRows(grid, new float[] { 34f, 32f });

            _lblPageTitle = UiStyle.CreateLabel(PageTitles[PageLogs], Theme.FontTitle, Theme.TextPrimary, Theme.Surface);
            _lblPageTitle.Dock = DockStyle.Fill;
            _lblPageTitle.Margin = new Padding(Theme.Sp24, 0, 0, 0);
            grid.Controls.Add(_lblPageTitle, 0, 0);

            _lblClock = UiStyle.CreateLabel("", Theme.FontStrong, Theme.TextSecondary, Theme.Surface);
            _lblClock.Dock = DockStyle.Fill;
            _lblClock.Margin = new Padding(0, 0, Theme.Sp24, 0);
            _lblClock.TextAlign = ContentAlignment.MiddleRight;
            grid.Controls.Add(_lblClock, 1, 0);

            _lblPageSubtitle = UiStyle.CreateLabel(PageSubtitles[PageLogs], Theme.FontSmall, Theme.TextMuted, Theme.Surface);
            _lblPageSubtitle.Dock = DockStyle.Fill;
            _lblPageSubtitle.Margin = new Padding(Theme.Sp24, 0, 0, 0);
            grid.Controls.Add(_lblPageSubtitle, 0, 1);

            _lblServerTz = UiStyle.CreateLabel("", Theme.FontTiny, Theme.TextMuted, Theme.Surface);
            _lblServerTz.Dock = DockStyle.Fill;
            _lblServerTz.Margin = new Padding(0, 0, Theme.Sp24, 0);
            _lblServerTz.TextAlign = ContentAlignment.MiddleRight;
            grid.Controls.Add(_lblServerTz, 1, 1);

            head.Controls.Add(grid);
            return head;
        }

        private Control BuildStatusBar()
        {
            _pnlStatus = new Panel();
            _pnlStatus.Dock = DockStyle.Fill;
            _pnlStatus.Margin = new Padding(0);
            _pnlStatus.BackColor = Theme.Surface;
            _pnlStatus.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(pen, 0, 0, _pnlStatus.Width, 0);
                }
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Theme.FillRound(e.Graphics, new Rectangle(18, 13, 8, 8), 4, StatusColor());
            };

            TableLayoutPanel grid = NewGrid(2, 1, Theme.Surface);
            AddColumns(grid, new float[] { 0f, 480f });
            AddRows(grid, new float[] { 0f });

            _lblStatusText = UiStyle.CreateLabel("系统初始化中…", Theme.FontBody, Theme.TextSecondary, Theme.Surface);
            _lblStatusText.Dock = DockStyle.Fill;
            _lblStatusText.Margin = new Padding(34, 0, Theme.Sp8, 0);
            grid.Controls.Add(_lblStatusText, 0, 0);

            _lblStatusRight = UiStyle.CreateLabel("", Theme.FontSmall, Theme.TextMuted, Theme.Surface);
            _lblStatusRight.Dock = DockStyle.Fill;
            _lblStatusRight.Margin = new Padding(0, 0, Theme.Sp24, 0);
            _lblStatusRight.TextAlign = ContentAlignment.MiddleRight;
            grid.Controls.Add(_lblStatusRight, 1, 0);

            _pnlStatus.Controls.Add(grid);
            return _pnlStatus;
        }

        private Control BuildPageHost()
        {
            _pageHost = new Panel();
            _pageHost.Dock = DockStyle.Fill;
            _pageHost.Margin = new Padding(0);
            _pageHost.BackColor = Theme.Bg;
            _pageHost.Padding = new Padding(Theme.Sp16, Theme.Sp16, Theme.Sp16, Theme.Sp12);
            return _pageHost;
        }

        private void BuildPages()
        {
            _pages = new Control[5];
            _pages[PageLogs] = BuildLogsPage();
            _pages[PageDevices] = BuildDevicesPage();
            _pages[PageScan] = BuildScanPage();
            _pages[PageSchedule] = BuildSchedulePage();
            _pages[PageNotify] = BuildNotifyPage();

            for (int i = 0; i < _pages.Length; i++)
            {
                _pages[i].Dock = DockStyle.Fill;
                _pages[i].Margin = new Padding(0);
                _pages[i].Visible = false;
                _pageHost.Controls.Add(_pages[i]);
            }

            NavigateTo(PageLogs);
        }

        private void NavigateTo(int index)
        {
            if (_pages == null || index < 0 || index >= _pages.Length) return;
            if (_activePage == index) return;

            _activePage = index;
            for (int i = 0; i < _pages.Length; i++)
            {
                bool active = (i == index);
                if (_pages[i].Visible != active) _pages[i].Visible = active;
                if (i < _navItems.Count) _navItems[i].Active = active;
            }

            _lblPageTitle.Text = PageTitles[index];
            _lblPageSubtitle.Text = PageSubtitles[index];

            if (index == PageSchedule)
            {
                RefreshSchedulePreview();
                RefreshServiceState();
            }
            if (index == PageDevices) UpdateDeviceHint();
        }

        // ==================== 运行日志页 ====================

        private Control BuildLogsPage()
        {
            TableLayoutPanel page = NewGrid(1, 2, Theme.Bg);
            AddRows(page, new float[] { 104f, 0f });

            page.Controls.Add(BuildStatsRow(
                new string[] { "设备总数", "同步成功", "同步失败", "最近同步" },
                new string[] { "记忆库中的设备台数", "最近一次对时成功台数", "需要排查的设备台数", "最近一次对时时间" },
                new Color[] { Theme.Accent, Theme.Success, Theme.Danger, Theme.Primary }), 0, 0);

            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0);
            card.Title = "实时运行日志";
            card.Subtitle = "后台调度、设备对时与企业微信推送的完整输出";

            TableLayoutPanel inner = NewGrid(1, 2, Theme.Surface);
            AddRows(inner, new float[] { 46f, 0f });

            TableLayoutPanel tools = NewGrid(4, 1, Theme.Surface);
            AddColumns(tools, new float[] { 232f, 154f, 0f, 172f });
            AddRows(tools, new float[] { 0f });

            _btnSyncNow = MakeButton("立即执行全员时间同步", UiButtonKind.Primary, new Padding(0, 7, 10, 7));
            _btnSyncNow.Click += btnSyncNow_Click;
            tools.Controls.Add(_btnSyncNow, 0, 0);

            UiButton btnClear = MakeButton("清空控制台", UiButtonKind.Secondary, new Padding(0, 7, 10, 7));
            btnClear.Click += btnClearLog_Click;
            tools.Controls.Add(btnClear, 1, 0);

            _lblLogHint = UiStyle.CreateLabel("自动同步任务在后台守护线程中运行，日志实时刷新。", Theme.FontSmall, Theme.TextMuted, Theme.Surface);
            _lblLogHint.Dock = DockStyle.Fill;
            _lblLogHint.Margin = new Padding(Theme.Sp4, 0, Theme.Sp8, 0);
            tools.Controls.Add(_lblLogHint, 2, 0);

            UiButton btnOpenLog = MakeButton("打开日志目录", UiButtonKind.Ghost, new Padding(0, 7, 0, 7));
            btnOpenLog.Click += btnOpenLogDir_Click;
            tools.Controls.Add(btnOpenLog, 3, 0);

            inner.Controls.Add(tools, 0, 0);

            _txtLogs = new UiLogConsole();
            _txtLogs.Dock = DockStyle.Fill;
            _txtLogs.Margin = new Padding(0);
            inner.Controls.Add(_txtLogs, 0, 1);

            card.Controls.Add(inner);
            page.Controls.Add(card, 0, 1);

            return page;
        }

        private TableLayoutPanel BuildStatsRow(string[] titles, string[] captions, Color[] accents)
        {
            TableLayoutPanel row = NewGrid(titles.Length, 1, Theme.Bg);
            AddColumns(row, new float[titles.Length]);
            AddRows(row, new float[] { 0f });

            for (int i = 0; i < titles.Length; i++)
            {
                StatCard card = new StatCard();
                card.Title = titles[i];
                card.Caption = captions[i];
                card.Accent = accents[i];
                card.Dock = DockStyle.Fill;
                card.Margin = new Padding(0, Theme.Sp8, i == titles.Length - 1 ? 0 : Theme.Sp12, Theme.Sp8);

                row.Controls.Add(card, i, 0);
                _statCards.Add(card);
            }
            return row;
        }

        // ==================== 状态与刷新 ====================

        private void OnRefreshTick(object sender, EventArgs e)
        {
            UpdateClock();
            RefreshDeviceGrid();
            UpdateOverviewStats();
        }

        private void UpdateClock()
        {
            if (_lblClock == null) return;
            DateTime now = DateTime.Now;
            _lblClock.Text = string.Format("{0:yyyy-MM-dd HH:mm:ss}   {1}", now, WeekdayNames[(int)now.DayOfWeek]);
        }

        private void UpdateHeaderTimezone()
        {
            if (_lblServerTz == null) return;
            TimeZoneInfo local = TimeZoneInfo.Local;
            _lblServerTz.Text = string.Format("服务器本地时区 {0}  ·  {1}", TimeZoneHelper.FormatOffset(local), local.Id);
        }

        private void UpdateOverviewStats()
        {
            if (_config == null || _config.Devices == null) return;

            int total = 0, timezone = 0, ok = 0, fail = 0;
            DateTime latest = DateTime.MinValue;

            for (int i = 0; i < _config.Devices.Count; i++)
            {
                DeviceInfo device = _config.Devices[i];
                total++;
                if (device == null) continue;

                if (!string.IsNullOrEmpty(device.Iana) && device.Iana.Trim().Length > 0) timezone++;

                string status = device.LastStatus == null ? "" : device.LastStatus;
                if (status.IndexOf("成功", StringComparison.Ordinal) >= 0) ok++;
                else if (status.IndexOf("失败", StringComparison.Ordinal) >= 0 ||
                         status.IndexOf("超时", StringComparison.Ordinal) >= 0) fail++;

                DateTime parsed;
                if (!string.IsNullOrEmpty(device.LastSyncTime) &&
                    DateTime.TryParse(device.LastSyncTime, out parsed) && parsed > latest)
                {
                    latest = parsed;
                }
            }

            SetStatValue(StatTotal, total.ToString());
            SetStatValue(StatOk, ok.ToString());
            SetStatValue(StatFail, fail.ToString());
            SetStatValue(StatLatest, latest == DateTime.MinValue ? "--" : latest.ToString("MM-dd HH:mm"));

            SetStatValue(DevStatTotal, total.ToString());
            SetStatValue(DevStatTz, timezone.ToString());
            SetStatValue(DevStatOk, ok.ToString());
            SetStatValue(DevStatFail, fail.ToString());
        }

        private void SetStatValue(int index, string value)
        {
            if (index < 0 || index >= _statCards.Count) return;
            StatCard card = _statCards[index];
            if (card.Value != value) card.Value = value;
        }

        private void SetStatus(string message, LogLevel level)
        {
            _statusLevel = level;
            UiBeginInvoke(delegate
            {
                if (_lblStatusText != null) _lblStatusText.Text = message;
                if (_pnlStatus != null) _pnlStatus.Invalidate();
            });
        }

        private Color StatusColor()
        {
            switch (_statusLevel)
            {
                case LogLevel.Success: return Theme.Success;
                case LogLevel.Warn: return Theme.Warning;
                case LogLevel.Error: return Theme.Danger;
                case LogLevel.Muted: return Theme.TextMuted;
                default: return Theme.Accent;
            }
        }

        /// <summary>
        /// 跨线程安全地回写界面：句柄尚未创建时直接执行（此时本就在 UI 线程），
        /// 避免构造期间 BeginInvoke 静默失败导致界面永远停在初始状态。
        /// </summary>
        private void UiBeginInvoke(Action action)
        {
            if (action == null) return;
            try
            {
                if (IsDisposed || Disposing) return;
                if (IsHandleCreated)
                {
                    if (InvokeRequired) BeginInvoke(action);
                    else action();
                }
                else
                {
                    action();
                }
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        // ==================== 日志输出 ====================

        private void AppendLogLine(string line)
        {
            if (line == null) return;

            if (_txtLogs == null || !_txtLogs.IsHandleCreated)
            {
                lock (_pendingLogLock) { _pendingLogs.Add(line); }
                return;
            }

            UiBeginInvoke(delegate { WriteLogLine(line); });
        }

        private void WriteLogLine(string line)
        {
            if (_txtLogs == null) return;
            _txtLogs.AppendText(line + Environment.NewLine, UiLogConsole.DetectLevel(line));
        }

        private void FlushPendingLogs()
        {
            List<string> buffered;
            lock (_pendingLogLock)
            {
                buffered = new List<string>(_pendingLogs);
                _pendingLogs.Clear();
            }

            for (int i = 0; i < buffered.Count; i++) WriteLogLine(buffered[i]);
        }

        // ==================== 初始化与关闭 ====================

        private void LoadConfigAndInitialize()
        {
            _config = ConfigManager.Load();

            BindDeviceGrid();
            BindScanPage();
            BindSchedulePage();
            BindNotifyPage();

            RefreshDeviceGrid();
            UpdateOverviewStats();
            UpdateClock();
            UpdateHeaderTimezone();

            _lblStatusRight.Text = "配置源 config.json · 界面每 5 秒自动刷新";

            _scheduler = new TaskSchedulerService(_config);
            _scheduler.Start();

            _uiRefreshTimer = new System.Windows.Forms.Timer();
            _uiRefreshTimer.Interval = 5000;
            _uiRefreshTimer.Tick += OnRefreshTick;
            _uiRefreshTimer.Start();

            _loading = false;

            if (_config.General != null && _config.General.UseCron)
            {
                Logger.Info("自动同步计划已启动：CRON 模式 " + _config.General.CronExpression);
            }
            else
            {
                Logger.Info(string.Format("自动同步计划已启动：固定间隔模式，每 {0} 分钟执行一次。",
                    _config.General == null ? 60 : _config.General.SyncIntervalMinutes));
            }

            Logger.Info("界面初始化完成，配置来源 config.json。");
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            FlushPendingLogs();
            RefreshSchedulePreview();
            UpdateOverviewStats();
            SetStatus("系统就绪，自动同步计划已启动。", LogLevel.Success);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_uiRefreshTimer != null)
            {
                _uiRefreshTimer.Stop();
                _uiRefreshTimer.Dispose();
                _uiRefreshTimer = null;
            }

            if (_logSink != null)
            {
                Logger.OnLog -= _logSink;
                _logSink = null;
            }

            if (_scheduler != null)
            {
                _scheduler.Stop();
                _scheduler = null;
            }

            base.OnFormClosing(e);
        }

        // ==================== 日志页事件 ====================

        private void btnSyncNow_Click(object sender, EventArgs e)
        {
            if (_config == null) return;

            if (SyncEngine.IsRunning)
            {
                SetStatus("同步任务正在执行中，请稍候…", LogLevel.Warn);
                return;
            }

            _btnSyncNow.Enabled = false;
            SetStatus("正在执行全员时间同步…", LogLevel.Info);
            Logger.Info("用户手动触发：立即执行全员时间同步。");

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    SyncEngine.ExecuteSync(_config, delegate(string msg) { SetStatus(msg, LogLevel.Info); });
                }
                catch (Exception ex)
                {
                    Logger.Error("手动同步出现异常: " + ex.Message);
                }
                finally
                {
                    UiBeginInvoke(delegate
                    {
                        if (_btnSyncNow != null) _btnSyncNow.Enabled = true;
                        UpdateOverviewStats();
                        RefreshDeviceGrid();
                    });
                }
            });
        }

        private void btnClearLog_Click(object sender, EventArgs e)
        {
            if (_txtLogs != null) _txtLogs.Clear();
            SetStatus("控制台已清空。", LogLevel.Muted);
        }

        private void btnOpenLogDir_Click(object sender, EventArgs e)
        {
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start(dir);
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法打开日志目录：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
