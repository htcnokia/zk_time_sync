using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ZkTimeSync
{
    public class MainForm : Form
    {
        private AppConfig _config;
        private TaskSchedulerService _scheduler;
        private System.Windows.Forms.Timer _uiRefreshTimer;

        // UI 控件
        private TabControl tabControl;
        private TabPage tabDevice;
        private TabPage tabScan;
        private TabPage tabSchedule;
        private TabPage tabNotify;
        private TabPage tabLogs;

        // 状态栏
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus;

        // 日志与监控页
        private TextBox txtLogs;
        private Button btnSyncNow;

        // 设备管理页
        private DataGridView dgvDevices;
        private Button btnSaveDevices;
        private Button btnAddDevice;
        private Button btnDeleteDevice;

        // 自动扫描页
        private TextBox txtCidr;
        private CheckBox chkMssql;
        private Button btnImportFromMssql;
        private CheckBox chkMssqlAutoImport;
        private TextBox txtMssqlConn;
        private TextBox txtMssqlQuery;
        private Button btnStartScan;
        private ProgressBar prgScan;
        private Label lblScanProgress;
        private ListBox lstScanResults;
        private Button btnImportSelected;

        // 定时设置页
        private CheckBox chkUseCron;
        private TextBox txtCronExpr;
        private Label lblCronHint;
        private TextBox txtIntervalMin;
        private CheckBox chkAutoStart;
        private Button btnSaveSchedule;

        // 消息通知页
        private CheckBox chkWeComEnabled;
        private ComboBox cmbWeComMode;
        private Label lblWebhookUrl;
        private TextBox txtWebhookUrl;
        private Label lblCorpId;
        private TextBox txtCorpId;
        private Label lblCorpSecret;
        private TextBox txtCorpSecret;
        private Label lblAgentId;
        private TextBox txtAgentId;
        private Label lblToUser;
        private TextBox txtToUser;
        private TextBox txtCustomHeader;
        private TextBox txtCustomFooter;
        private Button btnSaveWeCom;
        private Button btnTestWeCom;

        public MainForm()
        {
            InitializeComponent();
            LoadConfigAndInitialize();
        }

        private void InitializeComponent()
        {
            this.Text = "中控考勤/门禁时间全自动同步管理系统";
            this.Size = new Size(880, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Microsoft YaHei", 9F, FontStyle.Regular, GraphicsUnit.Point);

            // 主菜单/选项卡
            tabControl = new TabControl { Dock = DockStyle.Fill };
            tabDevice = new TabPage { Text = " 🕒 记忆设备管理 " };
            tabScan = new TabPage { Text = " 🔍 局域网扫描/数据库导入 " };
            tabSchedule = new TabPage { Text = " 📅 自动同步与定时任务 " };
            tabNotify = new TabPage { Text = " 💬 企业微信通知设置 " };
            tabLogs = new TabPage { Text = " 📋 运行日志与实时监控 " };

            tabControl.TabPages.AddRange(new TabPage[] { tabLogs, tabDevice, tabScan, tabSchedule, tabNotify });
            this.Controls.Add(tabControl);

            // 状态栏
            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel { Text = "系统就绪" };
            statusStrip.Items.Add(lblStatus);
            this.Controls.Add(statusStrip);

            // --- 1. 运行日志与实时监控页 ---
            var pnlLogTop = new Panel { Dock = DockStyle.Top, Height = 50 };
            btnSyncNow = new Button { Text = "🚀 立即执行全员时间同步任务", Left = 15, Top = 10, Width = 220, Height = 30, BackColor = Color.LightSkyBlue };
            btnSyncNow.Click += btnSyncNow_Click;
            pnlLogTop.Controls.Add(btnSyncNow);

            txtLogs = new TextBox { Multiline = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 9.5F) };
            tabLogs.Controls.Add(txtLogs);
            tabLogs.Controls.Add(pnlLogTop);

            // --- 2. 记忆设备管理页 ---
            var pnlDevTop = new Panel { Dock = DockStyle.Top, Height = 45 };
            btnAddDevice = new Button { Text = "➕ 手工添加", Left = 15, Top = 8, Width = 100, Height = 28 };
            btnDeleteDevice = new Button { Text = "❌ 删除选中", Left = 125, Top = 8, Width = 100, Height = 28 };
            btnSaveDevices = new Button { Text = "💾 保存修改并重新应用", Left = 235, Top = 8, Width = 180, Height = 28, BackColor = Color.LightGreen };
            
            btnAddDevice.Click += btnAddDevice_Click;
            btnDeleteDevice.Click += btnDeleteDevice_Click;
            btnSaveDevices.Click += btnSaveDevices_Click;

            pnlDevTop.Controls.AddRange(new Control[] { btnAddDevice, btnDeleteDevice, btnSaveDevices });

            dgvDevices = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White
            };
            
            // 设定列
            dgvDevices.Columns.Add("Ip", "设备 IP 地址");
            dgvDevices.Columns.Add("Port", "端口");
            dgvDevices.Columns.Add("CommKey", "通讯密码/CommKey");
            dgvDevices.Columns.Add("Name", "设备名称");
            dgvDevices.Columns.Add("LastSyncTime", "上次同步时间");
            dgvDevices.Columns.Add("LastStatus", "上次同步结果");

            tabDevice.Controls.Add(dgvDevices);
            tabDevice.Controls.Add(pnlDevTop);

            // --- 3. 局域网扫描/数据库导入页 ---
            var pnlScanLeft = new GroupBox { Text = "第一步：配置扫描源 (IP范围 & 数据库)", Left = 15, Top = 15, Width = 450, Height = 510 };
            
            pnlScanLeft.Controls.Add(new Label { Text = "CIDR / IP范围设置 (支持逗号、短横线，如: 192.168.1.0/24, 192.168.2.1-100):", Left = 15, Top = 25, Width = 420, Height = 20 });
            txtCidr = new TextBox { Left = 15, Top = 48, Width = 410, Height = 50, Multiline = true, ScrollBars = ScrollBars.Both };

            chkMssql = new CheckBox { Text = "从 SQL Server 数据库加载设备", Left = 15, Top = 110, Width = 240 };
            chkMssql.CheckedChanged += chkMssql_CheckedChanged;
            btnImportFromMssql = new Button { Text = "📥 立即导入", Left = 260, Top = 106, Width = 110, Height = 28, BackColor = Color.LightGreen };
            btnImportFromMssql.Click += btnImportFromMssql_Click;
            chkMssqlAutoImport = new CheckBox { Text = "自动导入：每次自动同步前先同步SQL设备信息 (按IP去重)", Left = 15, Top = 138, Width = 420 };
            chkMssqlAutoImport.CheckedChanged += chkMssqlAutoImport_CheckedChanged;
            pnlScanLeft.Controls.AddRange(new Control[] { chkMssql, btnImportFromMssql, chkMssqlAutoImport });

            pnlScanLeft.Controls.Add(new Label { Text = "数据库连接字符串 (SQL Server):", Left = 15, Top = 166, Width = 300, Height = 20 });
            txtMssqlConn = new TextBox { Left = 15, Top = 186, Width = 410 };

            pnlScanLeft.Controls.Add(new Label { Text = "查询 SQL 语句 (列名须返回或别名为: Ip, [Port], [CommKey], [Name]):", Left = 15, Top = 221, Width = 410, Height = 20 });
            txtMssqlQuery = new TextBox { Left = 15, Top = 241, Width = 410, Height = 100, Multiline = true, ScrollBars = ScrollBars.Both };

            btnStartScan = new Button { Text = "🔍 开始扫描并检测中控设备", Left = 15, Top = 356, Width = 410, Height = 35, BackColor = Color.LightYellow };
            btnStartScan.Click += btnStartScan_Click;
            pnlScanLeft.Controls.Add(btnStartScan);

            prgScan = new ProgressBar { Left = 15, Top = 401, Width = 410, Height = 20 };
            lblScanProgress = new Label { Text = "扫描进度: 未开始", Left = 15, Top = 426, Width = 410 };
            pnlScanLeft.Controls.AddRange(new Control[] { prgScan, lblScanProgress });

            var pnlScanRight = new GroupBox { Text = "第二步：检测在线结果与记忆导入", Left = 480, Top = 15, Width = 360, Height = 510 };
            lstScanResults = new ListBox { Left = 15, Top = 25, Width = 330, Height = 400, SelectionMode = SelectionMode.MultiSimple };
            btnImportSelected = new Button { Text = "📥 将选中的在线设备导入记忆库", Left = 15, Top = 440, Width = 330, Height = 40, BackColor = Color.LightGreen };
            btnImportSelected.Click += btnImportSelected_Click;
            pnlScanRight.Controls.AddRange(new Control[] { lstScanResults, btnImportSelected });

            tabScan.Controls.AddRange(new Control[] { pnlScanLeft, pnlScanRight });

            // --- 4. 定时任务与系统设置页 ---
            var pnlSchedule = new GroupBox { Text = "自动同步定时任务配置", Left = 20, Top = 20, Width = 800, Height = 400 };
            
            chkAutoStart = new CheckBox { Text = "允许该软件开机自动启动并在系统托盘静默运行 (Windows 启动组/注册表)", Left = 30, Top = 40, Width = 600 };
            pnlSchedule.Controls.Add(chkAutoStart);

            chkUseCron = new CheckBox { Text = "使用更强大的 CRON 表达式定时 (推荐：如每天凌晨2点整执行)", Left = 30, Top = 90, Width = 600 };
            chkUseCron.CheckedChanged += chkUseCron_CheckedChanged;
            pnlSchedule.Controls.Add(chkUseCron);

            pnlSchedule.Controls.Add(new Label { Text = "CRON 表达式 (支持 秒 分 时 日 月 周 6段式格式):", Left = 55, Top = 120, Width = 500 });
            txtCronExpr = new TextBox { Left = 55, Top = 145, Width = 250 };
            lblCronHint = new Label { Text = "提示: '0 0 2 * * ?' 代表每天凌晨2点。'0 */30 * * * ?' 代表每30分钟一期。", Left = 315, Top = 148, Width = 450, ForeColor = Color.Gray };
            pnlSchedule.Controls.AddRange(new Control[] { txtCronExpr, lblCronHint });

            pnlSchedule.Controls.Add(new Label { Text = "或者指定简单的间隔执行时间 (分钟):", Left = 55, Top = 195, Width = 500 });
            txtIntervalMin = new TextBox { Left = 55, Top = 220, Width = 150 };
            pnlSchedule.Controls.Add(txtIntervalMin);

            btnSaveSchedule = new Button { Text = "💾 保存自动计划任务并立即生效", Left = 55, Top = 280, Width = 300, Height = 40, BackColor = Color.LightGreen };
            btnSaveSchedule.Click += btnSaveSchedule_Click;
            pnlSchedule.Controls.Add(btnSaveSchedule);

            tabSchedule.Controls.Add(pnlSchedule);

            // --- 5. 企业微信通知设置页 ---
            var pnlWeCom = new GroupBox { Text = "企业微信（通知机器人 / 自建应用）参数配置", Left = 20, Top = 20, Width = 810, Height = 510 };
            
            chkWeComEnabled = new CheckBox { Text = "开启企业微信实时同步报告通知", Left = 30, Top = 35, Width = 300 };
            chkWeComEnabled.CheckedChanged += chkWeComEnabled_CheckedChanged;
            pnlWeCom.Controls.Add(chkWeComEnabled);

            pnlWeCom.Controls.Add(new Label { Text = "推送模式:", Left = 30, Top = 70, Width = 100 });
            cmbWeComMode = new ComboBox { Left = 130, Top = 67, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbWeComMode.Items.AddRange(new string[] { "Webhook", "App" });
            cmbWeComMode.SelectedIndexChanged += cmbWeComMode_SelectedIndexChanged;
            pnlWeCom.Controls.Add(cmbWeComMode);

            lblWebhookUrl = new Label { Text = "机器人 Webhook 链接:", Left = 30, Top = 110, Width = 150 };
            txtWebhookUrl = new TextBox { Left = 180, Top = 107, Width = 550 };
            pnlWeCom.Controls.AddRange(new Control[] { lblWebhookUrl, txtWebhookUrl });

            lblCorpId = new Label { Text = "企业 ID (CorpID):", Left = 30, Top = 145, Width = 150 };
            txtCorpId = new TextBox { Left = 180, Top = 142, Width = 200 };
            lblCorpSecret = new Label { Text = "应用 Secret:", Left = 400, Top = 145, Width = 100 };
            txtCorpSecret = new TextBox { Left = 500, Top = 142, Width = 230 };
            pnlWeCom.Controls.AddRange(new Control[] { lblCorpId, txtCorpId, lblCorpSecret, txtCorpSecret });

            lblAgentId = new Label { Text = "应用 AgentID:", Left = 30, Top = 180, Width = 150 };
            txtAgentId = new TextBox { Left = 180, Top = 177, Width = 200 };
            lblToUser = new Label { Text = "指定接收人 (@all 或 成员账号):", Left = 400, Top = 180, Width = 200 };
            txtToUser = new TextBox { Left = 600, Top = 177, Width = 130 };
            pnlWeCom.Controls.AddRange(new Control[] { lblAgentId, txtAgentId, lblToUser, txtToUser });

            pnlWeCom.Controls.Add(new Label { Text = "消息页眉（首部自定义样式及字符）:", Left = 30, Top = 220, Width = 400 });
            txtCustomHeader = new TextBox { Left = 30, Top = 242, Width = 740, Height = 75, Multiline = true, ScrollBars = ScrollBars.Both };

            pnlWeCom.Controls.Add(new Label { Text = "消息页脚（尾部自定义样式及字符）:", Left = 30, Top = 330, Width = 400 });
            txtCustomFooter = new TextBox { Left = 30, Top = 352, Width = 740, Height = 75, Multiline = true, ScrollBars = ScrollBars.Both };

            btnSaveWeCom = new Button { Text = "💾 保存通知参数", Left = 30, Top = 450, Width = 180, Height = 35, BackColor = Color.LightGreen };
            btnSaveWeCom.Click += btnSaveWeCom_Click;
            btnTestWeCom = new Button { Text = "🧪 发送测试通知包", Left = 230, Top = 450, Width = 180, Height = 35 };
            btnTestWeCom.Click += btnTestWeCom_Click;
            pnlWeCom.Controls.AddRange(new Control[] { btnSaveWeCom, btnTestWeCom });

            tabNotify.Controls.Add(pnlWeCom);

            // 订阅底层 logger 输出到屏幕
            Logger.OnLog += msg =>
            {
                if (txtLogs.IsDisposed) return;
                if (txtLogs.InvokeRequired)
                {
                    try { txtLogs.Invoke(new Action<string>(AppendLogText), msg); } catch { }
                }
                else
                {
                    AppendLogText(msg);
                }
            };
        }

        private void AppendLogText(string text)
        {
            if (txtLogs.TextLength > 50000)
            {
                txtLogs.Clear(); // 避免日志累积内存过大
            }
            txtLogs.AppendText(text + Environment.NewLine);
        }

        private void LoadConfigAndInitialize()
        {
            _config = ConfigManager.Load();

            // 绑定到表格
            RefreshDeviceGrid();

            // 扫描页初始值
            txtCidr.Text = _config.IpSources.CidrRanges != null ? string.Join(",", _config.IpSources.CidrRanges.ToArray()) : "";
            chkMssql.Checked = _config.IpSources.Mssql.Enabled;
            txtMssqlConn.Text = _config.IpSources.Mssql.ConnectionString;
            txtMssqlQuery.Text = _config.IpSources.Mssql.QuerySql;
            chkMssqlAutoImport.Checked = _config.IpSources.Mssql.AutoImport;
            ToggleMssqlInputs(chkMssql.Checked);

            // 定时页初始值
            chkAutoStart.Checked = AutoStartManager.IsAutoStartEnabled();
            chkUseCron.Checked = _config.General.UseCron;
            txtCronExpr.Text = _config.General.CronExpression;
            txtIntervalMin.Text = _config.General.SyncIntervalMinutes.ToString();
            ToggleScheduleInputs(chkUseCron.Checked);

            // 推送页初始值
            chkWeComEnabled.Checked = _config.WeCom.Enabled;
            cmbWeComMode.SelectedItem = string.IsNullOrEmpty(_config.WeCom.Mode) ? "Webhook" : _config.WeCom.Mode;
            txtWebhookUrl.Text = _config.WeCom.WebhookUrl;
            txtCorpId.Text = _config.WeCom.AppCorpId;
            txtCorpSecret.Text = _config.WeCom.AppCorpSecret;
            txtAgentId.Text = _config.WeCom.AppAgentId.ToString();
            txtToUser.Text = _config.WeCom.AppToUser;
            txtCustomHeader.Text = _config.WeCom.CustomHeader;
            txtCustomFooter.Text = _config.WeCom.CustomFooter;
            ToggleWeComInputs(chkWeComEnabled.Checked, cmbWeComMode.SelectedItem != null ? cmbWeComMode.SelectedItem.ToString() : "");

            // 开启定时任务服务守护
            _scheduler = new TaskSchedulerService(_config);
            _scheduler.Start();

            // 启用定时刷新主界面设备列表信息的 UI Timer
            _uiRefreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _uiRefreshTimer.Tick += (s, e) => RefreshDeviceGrid();
            _uiRefreshTimer.Start();

            Logger.Info("中控全自动时间同步系统后台守护已启动。");
        }

        private void RefreshDeviceGrid()
        {
            if (dgvDevices.InvokeRequired)
            {
                dgvDevices.Invoke(new Action(RefreshDeviceGrid));
                return;
            }

            dgvDevices.Rows.Clear();
            if (_config.Devices != null)
            {
                foreach (var d in _config.Devices)
                {
                    dgvDevices.Rows.Add(d.Ip, d.Port, d.CommKey, d.Name, d.LastSyncTime, d.LastStatus);
                }
            }
        }

        // --- 记忆设备页事件 ---
        private void btnAddDevice_Click(object sender, EventArgs e)
        {
            dgvDevices.Rows.Add("192.168.1.100", 4370, 0, "手工添加考勤机", "", "待同步");
        }

        private void btnDeleteDevice_Click(object sender, EventArgs e)
        {
            if (dgvDevices.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow r in dgvDevices.SelectedRows)
                {
                    if (!r.IsNewRow) dgvDevices.Rows.Remove(r);
                }
            }
        }

        private void btnSaveDevices_Click(object sender, EventArgs e)
        {
            var newList = new List<DeviceInfo>();
            for (int i = 0; i < dgvDevices.Rows.Count; i++)
            {
                var r = dgvDevices.Rows[i];
                if (r.Cells[0].Value == null) continue;
                newList.Add(new DeviceInfo
                {
                    Ip = r.Cells[0].Value.ToString().Trim(),
                    Port = r.Cells[1].Value != null ? Convert.ToInt32(r.Cells[1].Value) : 4370,
                    CommKey = r.Cells[2].Value != null ? Convert.ToInt32(r.Cells[2].Value) : 0,
                    Name = r.Cells[3].Value != null ? r.Cells[3].Value.ToString() : "中控设备",
                    LastSyncTime = r.Cells[4].Value != null ? r.Cells[4].Value.ToString() : "",
                    LastStatus = r.Cells[5].Value != null ? r.Cells[5].Value.ToString() : "未同步"
                });
            }

            _config.Devices = newList;
            if (ConfigManager.Save(_config))
            {
                MessageBox.Show("记忆设备列表保存并应用成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // --- 局域网扫描页事件 ---
        private void chkMssql_CheckedChanged(object sender, EventArgs e)
        {
            ToggleMssqlInputs(chkMssql.Checked);
        }

        private void ToggleMssqlInputs(bool isEnabled)
        {
            txtMssqlConn.Enabled = isEnabled;
            txtMssqlQuery.Enabled = isEnabled;
            chkMssqlAutoImport.Enabled = isEnabled;
        }

        /// <summary>
        /// 立即导入：跳过网络扫描，直接把 SQL 数据库中的设备按 IP 去重合并进记忆设备库
        /// </summary>
        private void btnImportFromMssql_Click(object sender, EventArgs e)
        {
            string connStr = txtMssqlConn.Text.Trim();
            if (string.IsNullOrEmpty(connStr))
            {
                MessageBox.Show("请先填写数据库连接字符串！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string query = txtMssqlQuery.Text.Trim();

            // 保存当前 MSSQL 参数（含查询语句），以便下次启动时保留
            _config.IpSources.Mssql.Enabled = chkMssql.Checked;
            _config.IpSources.Mssql.ConnectionString = connStr;
            _config.IpSources.Mssql.QuerySql = query;
            ConfigManager.Save(_config);

            btnImportFromMssql.Enabled = false;
            lblStatus.Text = "正在从 SQL Server 导入设备...";

            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    var mssqlCfg = new MssqlConfig { Enabled = true, ConnectionString = connStr, QuerySql = query };
                    var dbDevices = DeviceScanner.QueryFromMssql(mssqlCfg, _config.CommDefaults);

                    this.Invoke(new Action(() =>
                    {
                        btnImportFromMssql.Enabled = true;
                        if (_config.Devices == null) _config.Devices = new List<DeviceInfo>();
                        int[] mergeResult = DeviceScanner.MergeDevicesIntoMemory(_config.Devices, dbDevices);
                        if (mergeResult[0] > 0 || mergeResult[1] > 0)
                        {
                            ConfigManager.Save(_config);
                        }
                        Logger.Info(string.Format("立即导入SQL设备完成：读取 {0} 台，新增 {1} 台，更新 {2} 台（重复IP自动去重）",
                            dbDevices.Count, mergeResult[0], mergeResult[1]));

                        RefreshDeviceGrid();
                        lblStatus.Text = "SQL设备导入完成";
                        MessageBox.Show(string.Format("导入完成：共读取 {0} 台设备，新增 {1} 台，更新 {2} 台（重复IP自动去重）",
                            dbDevices.Count, mergeResult[0], mergeResult[1]), "数据库导入", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }));
                }
                catch (Exception ex)
                {
                    Logger.Error("立即导入SQL设备异常: " + ex.Message);
                    this.Invoke(new Action(() =>
                    {
                        btnImportFromMssql.Enabled = true;
                        lblStatus.Text = "SQL设备导入失败";
                    }));
                }
            });
        }

        /// <summary>
        /// 自动导入勾选：立即持久化到配置，下次自动同步执行前生效
        /// </summary>
        private void chkMssqlAutoImport_CheckedChanged(object sender, EventArgs e)
        {
            _config.IpSources.Mssql.AutoImport = chkMssqlAutoImport.Checked;
            ConfigManager.Save(_config);
        }

        private void btnStartScan_Click(object sender, EventArgs e)
        {
            btnStartScan.Enabled = false;
            lstScanResults.Items.Clear();

            // 收集参数并多线程异步扫描
            string cidrRaw = txtCidr.Text;
            bool mssqlEnabled = chkMssql.Checked;
            string connStr = txtMssqlConn.Text;
            string query = txtMssqlQuery.Text;

            int defaultPort = _config.CommDefaults.Port;
            int defaultCommKey = _config.CommDefaults.CommKey;

            // 保存 IP 源配置
            _config.IpSources.CidrRanges = new List<string>(cidrRaw.Split(new char[] { ',', (char)13, (char)10 }, StringSplitOptions.RemoveEmptyEntries));
            _config.IpSources.Mssql.Enabled = mssqlEnabled;
            _config.IpSources.Mssql.ConnectionString = connStr;
            _config.IpSources.Mssql.QuerySql = query;
            ConfigManager.Save(_config);

            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    Logger.Info("=== 开始多网段与设备源扫描 ===");
                    var targets = new List<string>();

                    // 1. CIDR/网段范围解析
                    var parsedIps = DeviceScanner.ParseIpTargets(_config.IpSources.CidrRanges);
                    targets.AddRange(parsedIps);

                    // 2. 数据库目标提取
                    if (mssqlEnabled)
                    {
                        var dbDevices = DeviceScanner.QueryFromMssql(_config.IpSources.Mssql, _config.CommDefaults);
                        foreach (var dbd in dbDevices)
                        {
                            if (!targets.Contains(dbd.Ip)) targets.Add(dbd.Ip);
                        }
                    }

                    if (targets.Count == 0)
                    {
                        Logger.Warn("未检测到待扫描IP，请检查 CIDR 或数据库配置！");
                        this.Invoke(new Action(() => {
                            btnStartScan.Enabled = true;
                            lblScanProgress.Text = "扫描停止：目标IP为空";
                        }));
                        return;
                    }

                    Logger.Info(string.Format("开始对 {0} 个候选IP进行并发深度探测...", targets.Count));

                    var discovered = DeviceScanner.ScanNetwork(targets, defaultPort, defaultCommKey, (ip, cur, total) =>
                    {
                        this.Invoke(new Action(() =>
                        {
                            prgScan.Maximum = total;
                            prgScan.Value = cur;
                            lblScanProgress.Text = string.Format("扫描进度: {0}/{1} (正在测试 {2})", cur, total, ip);
                        }));
                    });

                    this.Invoke(new Action(() =>
                    {
                        foreach (var d in discovered)
                        {
                            lstScanResults.Items.Add(string.Format("{0}:{1} - {2}", d.Ip, d.Port, d.Name));
                        }
                        btnStartScan.Enabled = true;
                        lblScanProgress.Text = string.Format("扫描完成！共发现 {0} 台在线设备", discovered.Count);
                        Logger.Info(string.Format("扫描全网结束，共找到 {0} 台具有中控特征的在线设备", discovered.Count));
                    }));
                }
                catch (Exception ex)
                {
                    Logger.Error("扫描线程出现致命错误: " + ex.Message);
                    this.Invoke(new Action(() => btnStartScan.Enabled = true));
                }
            });
        }

        private void btnImportSelected_Click(object sender, EventArgs e)
        {
            if (lstScanResults.SelectedItems.Count == 0)
            {
                MessageBox.Show("请先在右侧列表中选择至少一台扫描出来的设备！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int count = 0;
            if (_config.Devices == null) _config.Devices = new List<DeviceInfo>();

            foreach (var item in lstScanResults.SelectedItems)
            {
                string text = item.ToString();
                // 格式如 "192.168.1.201:4370 - 中控发现设备..."
                string[] parts = text.Split(new char[] { ':', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    string ip = parts[0];
                    int port = Convert.ToInt32(parts[1]);

                    // 避免重复导入
                    if (!_config.Devices.Exists(d => d.Ip == ip))
                    {
                        _config.Devices.Add(new DeviceInfo
                        {
                            Ip = ip,
                            Port = port,
                            CommKey = _config.CommDefaults.CommKey,
                            Name = "自动扫描导入-" + ip,
                            LastStatus = "扫描在线"
                        });
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                ConfigManager.Save(_config);
                RefreshDeviceGrid();
                MessageBox.Show(string.Format("成功导入 {0} 台设备至常驻同步记忆库！", count), "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("选中的设备已存在于记忆库中，未执行重复导入。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // --- 定时调度页事件 ---
        private void chkUseCron_CheckedChanged(object sender, EventArgs e)
        {
            ToggleScheduleInputs(chkUseCron.Checked);
        }

        private void ToggleScheduleInputs(bool useCron)
        {
            txtCronExpr.Enabled = useCron;
            txtIntervalMin.Enabled = !useCron;
        }

        private void btnSaveSchedule_Click(object sender, EventArgs e)
        {
            _config.General.UseCron = chkUseCron.Checked;
            _config.General.CronExpression = txtCronExpr.Text.Trim();
            
            int interval;
            if (int.TryParse(txtIntervalMin.Text, out interval))
            {
                _config.General.SyncIntervalMinutes = interval;
            }

            _config.General.AutoStartWithWindows = chkAutoStart.Checked;
            ConfigManager.Save(_config);

            // 更新启动状态
            AutoStartManager.SetAutoStart(chkAutoStart.Checked);

            // 重启调度器
            if (_scheduler != null) _scheduler.ReloadCron();
            MessageBox.Show("自动任务时间计划已更新并立即生效！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // --- 企业微信页事件 ---
        private void chkWeComEnabled_CheckedChanged(object sender, EventArgs e)
        {
            ToggleWeComInputs(chkWeComEnabled.Checked, cmbWeComMode.SelectedItem != null ? cmbWeComMode.SelectedItem.ToString() : "");
        }

        private void cmbWeComMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            ToggleWeComInputs(chkWeComEnabled.Checked, cmbWeComMode.SelectedItem != null ? cmbWeComMode.SelectedItem.ToString() : "");
        }

        private void ToggleWeComInputs(bool enabled, string mode)
        {
            cmbWeComMode.Enabled = enabled;
            txtCustomHeader.Enabled = enabled;
            txtCustomFooter.Enabled = enabled;

            if (!enabled)
            {
                txtWebhookUrl.Enabled = false;
                txtCorpId.Enabled = false;
                txtCorpSecret.Enabled = false;
                txtAgentId.Enabled = false;
                txtToUser.Enabled = false;
            }
            else
            {
                bool isWebhook = (mode == "Webhook");
                txtWebhookUrl.Enabled = isWebhook;
                txtCorpId.Enabled = !isWebhook;
                txtCorpSecret.Enabled = !isWebhook;
                txtAgentId.Enabled = !isWebhook;
                txtToUser.Enabled = !isWebhook;
            }
        }

        private void btnSaveWeCom_Click(object sender, EventArgs e)
        {
            _config.WeCom.Enabled = chkWeComEnabled.Checked;
            _config.WeCom.Mode = cmbWeComMode.SelectedItem != null ? cmbWeComMode.SelectedItem.ToString() : "Webhook";
            _config.WeCom.WebhookUrl = txtWebhookUrl.Text.Trim();
            _config.WeCom.AppCorpId = txtCorpId.Text.Trim();
            _config.WeCom.AppCorpSecret = txtCorpSecret.Text.Trim();
            _config.WeCom.AppAgentId = string.IsNullOrEmpty(txtAgentId.Text) ? 0 : Convert.ToInt32(txtAgentId.Text);
            _config.WeCom.AppToUser = txtToUser.Text.Trim();
            _config.WeCom.CustomHeader = txtCustomHeader.Text;
            _config.WeCom.CustomFooter = txtCustomFooter.Text;

            if (ConfigManager.Save(_config))
            {
                MessageBox.Show("企业微信通知参数保存成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void btnTestWeCom_Click(object sender, EventArgs e)
        {
            btnTestWeCom.Enabled = false;
            var testConfig = new WeComConfig
            {
                Enabled = true,
                Mode = cmbWeComMode.SelectedItem != null ? cmbWeComMode.SelectedItem.ToString() : "Webhook",
                WebhookUrl = txtWebhookUrl.Text.Trim(),
                AppCorpId = txtCorpId.Text.Trim(),
                AppCorpSecret = txtCorpSecret.Text.Trim(),
                AppAgentId = string.IsNullOrEmpty(txtAgentId.Text) ? 0 : Convert.ToInt32(txtAgentId.Text),
                AppToUser = txtToUser.Text.Trim(),
                CustomHeader = txtCustomHeader.Text,
                CustomFooter = txtCustomFooter.Text
            };

            ThreadPool.QueueUserWorkItem(state =>
            {
                string successList = "  - 测试设备A (192.168.1.100:4370) => 成功" + Environment.NewLine + "  - 测试设备B (192.168.1.101:4370) => 成功";
                string failList = "  - 测试故障设备 (192.168.1.250:4370) => 失败原因: 连接超时";
                
                bool ok = WeComNotifier.SendSyncReport(testConfig, 3, 2, 1, successList, failList);
                
                this.Invoke(new Action(() =>
                {
                    btnTestWeCom.Enabled = true;
                    if (ok)
                        MessageBox.Show("企业微信测试消息发送成功，请查收！", "消息已发出", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else
                        MessageBox.Show("企业微信测试消息发送失败，请确认参数是否正确并在下方日志页排查错误代码！", "发送失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
            });
        }

        // --- 实时手动同步任务 ---
        private void btnSyncNow_Click(object sender, EventArgs e)
        {
            if (SyncEngine.IsRunning)
            {
                MessageBox.Show("时间同步任务已在执行中，请勿重复触发！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnSyncNow.Enabled = false;
            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    SyncEngine.ExecuteSync(_config, msg => {
                        lblStatus.Text = msg;
                    });
                }
                finally
                {
                    this.Invoke(new Action(() => {
                        btnSyncNow.Enabled = true;
                        lblStatus.Text = "手动同步任务完成";
                        RefreshDeviceGrid();
                    }));
                }
            });
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 如果是在 Windows 托盘模式静默，可以取消关闭并隐藏。
            // 这里我们默认提供关闭释放
            if (_scheduler != null) _scheduler.Stop();
            if (_uiRefreshTimer != null) _uiRefreshTimer.Stop();
            base.OnFormClosing(e);
        }
    }
}
