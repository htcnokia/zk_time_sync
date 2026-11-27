using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace ZkTimeSync
{
    /// <summary>
    /// 记忆设备管理页 + 局域网扫描 / 数据库导入页。
    /// 记忆设备表支持双击就地编辑，时区列可用右键菜单从常用 IANA 时区中挑选。
    /// </summary>
    public partial class MainForm
    {
        // ==================== 记忆设备表列索引 ====================
        private const int ColIp = 0;
        private const int ColPort = 1;
        private const int ColKey = 2;
        private const int ColIana = 3;
        private const int ColName = 4;
        private const int ColTime = 5;
        private const int ColStatus = 6;

        private DataGridView _dgvDevices;
        private UiButton _btnSetTz;
        private Label _lblDeviceHint;
        private ContextMenuStrip _tzMenu;

        /// <summary>刷新设备表期间抑制编辑事件，避免被误判为“未保存修改”</summary>
        private bool _refreshingGrid;

        // ==================== 扫描页控件 ====================
        private TextBox _txtCidr;
        private CheckBox _chkMssql;
        private UiButton _btnImportFromMssql;
        private CheckBox _chkMssqlAutoImport;
        private TextBox _txtMssqlConn;
        private TextBox _txtMssqlQuery;
        private UiButton _btnStartScan;
        private UiProgress _prgScan;
        private Label _lblScanProgress;
        private ListBox _lstScanResults;
        private UiButton _btnImportSelected;
        private Label _lblScanSummary;

        private readonly List<ScanResultItem> _scanItems = new List<ScanResultItem>();

        /// <summary>扫描 / 数据库导入结果条目（带来源与是否已导入标记）</summary>
        private class ScanResultItem
        {
            public DeviceInfo Device;
            public bool FromDatabase;
            public bool AlreadyImported;

            public override string ToString()
            {
                string state = AlreadyImported ? "已导入" : (FromDatabase ? "数据库" : "在线");
                string tz = (Device.Iana == null || Device.Iana.Length == 0) ? "跟随服务器时区" : Device.Iana;
                return string.Format("[{0}] {1}:{2}   {3}   ·   {4}", state, Device.Ip, Device.Port, Device.Name, tz);
            }
        }

        // ==================== 记忆设备管理页 ====================

        private Control BuildDevicesPage()
        {
            TableLayoutPanel page = NewGrid(1, 4, Theme.Bg);
            AddRows(page, new float[] { 104f, 70f, 22f, 0f });

            page.Controls.Add(BuildStatsRow(
                new string[] { "设备总数", "已配置时区", "同步成功", "同步失败" },
                new string[] { "记忆库中的设备台数", "按 IANA 时区独立对时", "最近一次成功的台数", "需要排查的设备台数" },
                new Color[] { Theme.Accent, Theme.Primary, Theme.Success, Theme.Danger }), 0, 0);

            TableLayoutPanel tools = NewGrid(4, 1, Theme.Bg);
            AddColumns(tools, new float[] { 112f, 112f, 124f, 168f });
            AddRows(tools, new float[] { 0f });

            UiButton btnAdd = MakeButton("手工添加", UiButtonKind.Secondary, new Padding(0, 19, 10, 19));
            btnAdd.Click += btnAddDevice_Click;
            tools.Controls.Add(btnAdd, 0, 0);

            UiButton btnDelete = MakeButton("删除选中", UiButtonKind.Danger, new Padding(0, 19, 10, 19));
            btnDelete.Click += btnDeleteDevice_Click;
            tools.Controls.Add(btnDelete, 1, 0);

            _btnSetTz = MakeButton("设置时区", UiButtonKind.Secondary, new Padding(0, 19, 10, 19));
            _btnSetTz.Click += btnSetTimezone_Click;
            tools.Controls.Add(_btnSetTz, 2, 0);

            UiButton btnSave = MakeButton("保存并应用", UiButtonKind.Primary, new Padding(0, 19, 0, 19));
            btnSave.Click += btnSaveDevices_Click;
            tools.Controls.Add(btnSave, 3, 0);

            page.Controls.Add(tools, 0, 1);

            _lblDeviceHint = UiStyle.CreateLabel("", Theme.FontSmall, Theme.TextMuted, Theme.Bg);
            _lblDeviceHint.Dock = DockStyle.Fill;
            _lblDeviceHint.Margin = new Padding(Theme.Sp4, 0, 0, Theme.Sp4);
            page.Controls.Add(_lblDeviceHint, 0, 2);

            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0);
            card.Title = "记忆设备库";
            card.Subtitle = "IP / 端口 / 通讯密码 / IANA 时区可直接双击编辑；时区留空表示跟随服务器时区";

            _dgvDevices = new DataGridView();
            _dgvDevices.Dock = DockStyle.Fill;
            _dgvDevices.Margin = new Padding(0);
            UiStyle.StyleGrid(_dgvDevices);
            _dgvDevices.BackColor = Theme.Surface;
            _dgvDevices.ReadOnly = false;
            _dgvDevices.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _dgvDevices.CellBeginEdit += DgvDevices_CellBeginEdit;
            _dgvDevices.CellValueChanged += DgvDevices_CellValueChanged;
            _dgvDevices.CellFormatting += DgvDevices_CellFormatting;
            _dgvDevices.CellMouseDown += DgvDevices_CellMouseDown;
            _dgvDevices.CellDoubleClick += DgvDevices_CellDoubleClick;
            _dgvDevices.CellToolTipTextNeeded += DgvDevices_CellToolTipTextNeeded;

            card.Controls.Add(_dgvDevices);
            page.Controls.Add(card, 0, 3);

            BuildTimezoneMenu();
            return page;
        }

        private void BindDeviceGrid()
        {
            _dgvDevices.Columns.Clear();
            AddTextColumn(_dgvDevices, "Ip", "设备 IP 地址", 108f, true);
            AddTextColumn(_dgvDevices, "Port", "端口", 56f, true);
            AddTextColumn(_dgvDevices, "CommKey", "通讯密码", 84f, true);
            AddTextColumn(_dgvDevices, "Iana", "设备所在地时区 (IANA)", 156f, true);
            AddTextColumn(_dgvDevices, "Name", "设备名称", 148f, true);
            AddTextColumn(_dgvDevices, "LastSyncTime", "上次同步时间", 128f, false);
            AddTextColumn(_dgvDevices, "LastStatus", "上次同步结果", 176f, false);
        }

        private static void AddTextColumn(DataGridView grid, string name, string header, float weight, bool editable)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.FillWeight = weight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            column.ReadOnly = !editable;
            column.Resizable = DataGridViewTriState.False;
            if (!editable)
            {
                column.DefaultCellStyle.BackColor = Theme.SurfaceAlt;
                column.DefaultCellStyle.ForeColor = Theme.TextSecondary;
            }
            grid.Columns.Add(column);
        }

        /// <summary>把记忆库中的设备重新灌入表格（编辑中或正在刷新时跳过）</summary>
        private void RefreshDeviceGrid()
        {
            if (_dgvDevices == null || _config == null || _config.Devices == null) return;
            if (_gridDirty || _refreshingGrid) return;

            int firstDisplayed = -1;
            try
            {
                if (_dgvDevices.Rows.Count > 0) firstDisplayed = _dgvDevices.FirstDisplayedScrollingRowIndex;
            }
            catch { firstDisplayed = -1; }

            _refreshingGrid = true;
            try
            {
                _dgvDevices.Rows.Clear();
                for (int i = 0; i < _config.Devices.Count; i++)
                {
                    DeviceInfo device = _config.Devices[i];
                    if (device == null) continue;
                    _dgvDevices.Rows.Add(
                        device.Ip,
                        device.Port,
                        device.CommKey,
                        device.Iana,
                        device.Name,
                        device.LastSyncTime,
                        device.LastStatus);
                }
            }
            finally
            {
                _refreshingGrid = false;
            }

            if (firstDisplayed >= 0 && firstDisplayed < _dgvDevices.Rows.Count)
            {
                try { _dgvDevices.FirstDisplayedScrollingRowIndex = firstDisplayed; }
                catch { }
            }
        }

        private void DgvDevices_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (_refreshingGrid) return;
            _gridDirty = true;
            UpdateDeviceHint();
        }

        private void DgvDevices_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_refreshingGrid || _loading) return;
            if (e.RowIndex < 0) return;
            _gridDirty = true;
            UpdateDeviceHint();
        }

        private void DgvDevices_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex == ColIana && _tzMenu != null)
            {
                _tzMenu.Show(_dgvDevices, _dgvDevices.PointToClient(Cursor.Position));
            }
        }

        private void DgvDevices_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _dgvDevices == null) return;
            DataGridViewRow row = _dgvDevices.Rows[e.RowIndex];
            if (row == null) return;

            if (e.ColumnIndex == ColIana)
            {
                string iana = CellText(row, ColIana);
                if (iana.Length == 0) e.CellStyle.ForeColor = Theme.TextMuted;
                else if (IsUsableTimezone(iana)) e.CellStyle.ForeColor = Theme.Primary;
                else e.CellStyle.ForeColor = Theme.Danger;
            }
            else if (e.ColumnIndex == ColStatus)
            {
                string status = CellText(row, ColStatus);
                if (status.Length == 0 || status == "未同步" || status == "待同步" || status == "扫描在线" || status == "数据库记录")
                    e.CellStyle.ForeColor = Theme.TextMuted;
                else if (status.IndexOf("成功", StringComparison.Ordinal) >= 0)
                    e.CellStyle.ForeColor = Theme.SuccessText;
                else
                    e.CellStyle.ForeColor = Theme.Danger;
            }
        }

        private void DgvDevices_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex < 0) return;
            string iana = CellText(_dgvDevices.Rows[e.RowIndex], ColIana);
            if (iana.Length == 0)
            {
                e.ToolTipText = "留空表示跟随服务器本机时区。";
            }
            else if (!IsUsableTimezone(iana))
            {
                e.ToolTipText = "无法识别的 IANA 时区名，同步时将退回服务器本机时区。可双击单元格从常用时区中挑选。";
            }
            else
            {
                string error;
                TimeZoneInfo zone = TimeZoneHelper.Resolve(iana, out error);
                e.ToolTipText = zone == null
                    ? "无法解析该 IANA 时区名。"
                    : string.Format("{0}（UTC{1}）", iana, TimeZoneHelper.FormatOffset(zone));
            }
        }

        private void DgvDevices_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (e.ColumnIndex == ColIana && _tzMenu != null)
            {
                _dgvDevices.ContextMenuStrip = _tzMenu;
                _dgvDevices.CurrentCell = _dgvDevices.Rows[e.RowIndex].Cells[e.ColumnIndex];
            }
            else
            {
                _dgvDevices.ContextMenuStrip = null;
            }
        }

        private void BuildTimezoneMenu()
        {
            _tzMenu = new ContextMenuStrip();
            _tzMenu.Font = Theme.FontBody;
            _tzMenu.ShowImageMargin = false;

            ToolStripMenuItem follow = new ToolStripMenuItem("跟随服务器本机时区（清空该格）");
            follow.Click += delegate { ApplyTimezoneToSelected(""); };
            _tzMenu.Items.Add(follow);
            _tzMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem group = new ToolStripMenuItem("常用时区（IANA）");
            for (int i = 0; i < TimeZoneHelper.CommonZones.Length; i++)
            {
                string iana = TimeZoneHelper.CommonZones[i];
                string error;
                TimeZoneInfo zone = TimeZoneHelper.Resolve(iana, out error);

                ToolStripMenuItem item = new ToolStripMenuItem();
                item.Text = zone == null
                    ? iana
                    : string.Format("{0}   ({1})", iana, TimeZoneHelper.FormatOffset(zone));
                item.Tag = iana;
                item.Click += TzMenuItem_Click;
                group.DropDownItems.Add(item);
            }
            _tzMenu.Items.Add(group);
        }

        private void TzMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || item.Tag == null) return;
            ApplyTimezoneToSelected(Convert.ToString(item.Tag));
        }

        private void btnSetTimezone_Click(object sender, EventArgs e)
        {
            if (_tzMenu == null) return;
            _tzMenu.Show(_btnSetTz, new Point(0, _btnSetTz.Height));
        }

        private void ApplyTimezoneToSelected(string iana)
        {
            if (_dgvDevices == null) return;
            iana = iana == null ? "" : iana.Trim();
            _dgvDevices.EndEdit();

            HashSet<int> indexes = new HashSet<int>();
            foreach (DataGridViewCell cell in _dgvDevices.SelectedCells)
            {
                if (cell != null && cell.RowIndex >= 0) indexes.Add(cell.RowIndex);
            }
            if (indexes.Count == 0 && _dgvDevices.CurrentRow != null && _dgvDevices.CurrentRow.Index >= 0)
            {
                indexes.Add(_dgvDevices.CurrentRow.Index);
            }
            if (indexes.Count == 0)
            {
                SetStatus("请先在设备表中选中要设置时区的行。", LogLevel.Warn);
                return;
            }

            int changed = 0;
            foreach (int index in indexes)
            {
                if (index < 0 || index >= _dgvDevices.Rows.Count) continue;
                DataGridViewRow row = _dgvDevices.Rows[index];
                if (row == null) continue;
                row.Cells[ColIana].Value = iana;
                changed++;
            }

            if (changed == 0) return;
            _gridDirty = true;
            UpdateDeviceHint();
            _dgvDevices.Invalidate();
            SetStatus(string.Format("已将 {0} 台设备的时区设置为 {1}，点击「保存并应用」后生效。",
                changed, iana.Length == 0 ? "跟随服务器本机时区" : iana), LogLevel.Info);
        }

        private void btnAddDevice_Click(object sender, EventArgs e)
        {
            if (_dgvDevices == null || _config == null || _config.Devices == null) return;
            _dgvDevices.EndEdit();

            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _dgvDevices.Rows.Count; i++)
            {
                string ip = CellText(_dgvDevices.Rows[i], ColIp);
                if (ip.Length > 0) used.Add(ip);
            }

            string candidate = "";
            for (int host = 100; host <= 254; host++)
            {
                string ip = string.Format("192.168.1.{0}", host);
                if (!used.Contains(ip))
                {
                    candidate = ip;
                    break;
                }
            }
            if (candidate.Length == 0)
            {
                SetStatus("默认网段内已无空闲地址，请直接双击表格手工填写 IP。", LogLevel.Warn);
                return;
            }

            int port = _config.CommDefaults == null ? 4370 : _config.CommDefaults.Port;
            int commKey = _config.CommDefaults == null ? 0 : _config.CommDefaults.CommKey;

            int rowIndex = _dgvDevices.Rows.Add(candidate, port, commKey, "", "手工添加考勤机", "", "待同步");
            _gridDirty = true;
            UpdateDeviceHint();

            _dgvDevices.CurrentCell = _dgvDevices.Rows[rowIndex].Cells[ColIp];
            _dgvDevices.Rows[rowIndex].Selected = true;
            SetStatus("已新增一行设备，请补齐 IP 后点击「保存并应用」。", LogLevel.Info);
        }

        private void btnDeleteDevice_Click(object sender, EventArgs e)
        {
            if (_dgvDevices == null) return;
            _dgvDevices.EndEdit();

            List<int> targets = new List<int>();
            foreach (DataGridViewRow row in _dgvDevices.SelectedRows)
            {
                if (row != null && !row.IsNewRow && row.Index >= 0) targets.Add(row.Index);
            }
            if (targets.Count == 0)
            {
                SetStatus("请先选中要删除的设备行（可按住 Ctrl 多选）。", LogLevel.Warn);
                return;
            }

            DialogResult answer = MessageBox.Show(
                string.Format("确定要从记忆库中删除选中的 {0} 台设备吗？此操作在点击「保存并应用」后才会写入配置文件。", targets.Count),
                "删除确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;

            targets.Sort();
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if (targets[i] < _dgvDevices.Rows.Count) _dgvDevices.Rows.RemoveAt(targets[i]);
            }

            _gridDirty = true;
            UpdateDeviceHint();
            SetStatus(string.Format("已移除 {0} 台设备，点击「保存并应用」后生效。", targets.Count), LogLevel.Info);
        }

        private void btnSaveDevices_Click(object sender, EventArgs e)
        {
            if (_dgvDevices == null || _config == null) return;
            _dgvDevices.EndEdit();

            List<DeviceInfo> devices = new List<DeviceInfo>();
            List<string> problems = new List<string>();
            List<string> warnings = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int rowNumber = 0;

            for (int i = 0; i < _dgvDevices.Rows.Count; i++)
            {
                DataGridViewRow row = _dgvDevices.Rows[i];
                if (row == null || row.IsNewRow) continue;
                rowNumber++;

                string ip = CellText(row, ColIp);
                string portText = CellText(row, ColPort);
                string keyText = CellText(row, ColKey);
                string iana = CellText(row, ColIana);
                string name = CellText(row, ColName);

                if (ip.Length == 0)
                {
                    problems.Add(string.Format("第 {0} 行：设备 IP 不能为空。", rowNumber));
                    continue;
                }

                IPAddress parsed;
                if (!IPAddress.TryParse(ip, out parsed))
                {
                    problems.Add(string.Format("第 {0} 行：\"{1}\" 不是合法的 IP 地址。", rowNumber, ip));
                    continue;
                }
                if (!seen.Add(ip))
                {
                    problems.Add(string.Format("第 {0} 行：IP {1} 重复出现。", rowNumber, ip));
                    continue;
                }

                int port;
                if (!TryParseCell(portText, out port) || port < 1 || port > 65535)
                {
                    problems.Add(string.Format("第 {0} 行：端口必须是 1 - 65535 之间的整数。", rowNumber));
                    continue;
                }

                int commKey;
                if (!TryParseCell(keyText, out commKey) || commKey < 0 || commKey > 999999)
                {
                    problems.Add(string.Format("第 {0} 行：通讯密码必须是 0 - 999999 之间的整数。", rowNumber));
                    continue;
                }

                if (iana.Length > 0 && !IsUsableTimezone(iana))
                {
                    warnings.Add(string.Format("第 {0} 行：{1} 台（{2}）的时区 \"{3}\" 无法识别。", rowNumber, name.Length == 0 ? "未命名" : name, ip, iana));
                }

                DeviceInfo device = new DeviceInfo();
                device.Ip = ip;
                device.Port = port;
                device.CommKey = commKey;
                device.Name = name.Length == 0 ? "中控设备" : name;
                device.Iana = iana;

                DeviceInfo previous = FindDevice(_config.Devices, ip);
                device.LastSyncTime = previous == null ? "" : previous.LastSyncTime;
                device.LastStatus = previous == null ? "未同步" : previous.LastStatus;
                devices.Add(device);
            }

            if (problems.Count > 0)
            {
                MessageBox.Show("以下内容需要修正后才能保存：\r\n\r\n" + string.Join("\r\n", problems.ToArray()),
                    "设备表校验未通过", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetStatus(string.Format("设备表校验未通过，共 {0} 处问题。", problems.Count), LogLevel.Warn);
                return;
            }

            if (warnings.Count > 0)
            {
                DialogResult answer = MessageBox.Show(
                    "以下时区无法识别，保存后这些设备将跟随服务器本机时区：\r\n\r\n" + string.Join("\r\n", warnings.ToArray()) +
                    "\r\n\r\n是否继续保存？",
                    "时区确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;
            }

            _config.Devices = devices;
            ConfigManager.Save(_config);

            _gridDirty = false;
            RefreshDeviceGrid();
            UpdateDeviceHint();
            UpdateOverviewStats();

            SetStatus(string.Format("设备库已保存并应用：共 {0} 台设备。", devices.Count), LogLevel.Success);
            Logger.Info(string.Format("记忆设备库已更新，共 {0} 台设备。", devices.Count));
        }

        private void UpdateDeviceHint()
        {
            if (_lblDeviceHint == null || _config == null || _config.Devices == null) return;

            if (_gridDirty)
            {
                _lblDeviceHint.ForeColor = Theme.WarningText;
                _lblDeviceHint.Text = "有未保存的修改：点击「保存并应用」后生效（保存前暂停自动刷新）。";
                return;
            }

            int total = _config.Devices.Count;
            int withTz = 0;
            for (int i = 0; i < _config.Devices.Count; i++)
            {
                DeviceInfo device = _config.Devices[i];
                if (device != null && device.Iana != null && device.Iana.Trim().Length > 0) withTz++;
            }

            _lblDeviceHint.ForeColor = Theme.TextMuted;
            _lblDeviceHint.Text = string.Format("共 {0} 台设备，其中 {1} 台按 IANA 时区独立对时；双击单元格可编辑，时区格可右键挑选。", total, withTz);
        }

        /// <summary>空串表示跟随服务器时区，属合法配置</summary>
        private static bool IsUsableTimezone(string iana)
        {
            if (iana == null) return false;
            if (iana.Trim().Length == 0) return true;
            if (TimeZoneHelper.IsKnown(iana)) return true;
            TimeSpan offset;
            return TimeZoneHelper.TryParseFixedOffset(iana, out offset);
        }

        // ==================== 扫描与导入页 ====================

        private Control BuildScanPage()
        {
            TableLayoutPanel page = NewGrid(2, 1, Theme.Bg);
            AddColumns(page, new float[] { 0f, 360f });
            AddRows(page, new float[] { 0f });

            page.Controls.Add(BuildScanLeftCard(), 0, 0);

            CardPanel right = new CardPanel();
            right.Dock = DockStyle.Fill;
            right.Margin = new Padding(Theme.Sp12, 0, 0, 0);
            right.Title = "第二步 · 导入记忆库";
            right.Subtitle = "按 IP 去重，已存在的设备自动刷新配置";

            TableLayoutPanel inner = NewGrid(1, 3, Theme.Surface);
            AddRows(inner, new float[] { 0f, 46f, 24f });

            _lstScanResults = new ListBox();
            _lstScanResults.Dock = DockStyle.Fill;
            _lstScanResults.Margin = new Padding(0);
            _lstScanResults.Font = Theme.FontBody;
            _lstScanResults.BackColor = Theme.Surface;
            _lstScanResults.ForeColor = Theme.TextPrimary;
            _lstScanResults.BorderStyle = BorderStyle.FixedSingle;
            _lstScanResults.DrawMode = DrawMode.OwnerDrawFixed;
            _lstScanResults.ItemHeight = 28;
            _lstScanResults.IntegralHeight = false;
            _lstScanResults.SelectionMode = SelectionMode.MultiExtended;
            _lstScanResults.DrawItem += LstScanResults_DrawItem;
            inner.Controls.Add(_lstScanResults, 0, 0);

            _btnImportSelected = MakeButton("将选中设备导入记忆库", UiButtonKind.Primary, new Padding(0, 7, 0, 7));
            _btnImportSelected.Click += btnImportSelected_Click;
            inner.Controls.Add(_btnImportSelected, 0, 1);

            _lblScanSummary = UiStyle.CreateLabel("", Theme.FontTiny, Theme.TextMuted, Theme.Surface);
            _lblScanSummary.Dock = DockStyle.Fill;
            _lblScanSummary.Margin = new Padding(0);
            inner.Controls.Add(_lblScanSummary, 0, 2);

            right.Controls.Add(inner);
            page.Controls.Add(right, 1, 0);

            return page;
        }

        private CardPanel BuildScanLeftCard()
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0);
            card.Title = "第一步 · 配置设备来源";
            card.Subtitle = "支持 CIDR 网段、起止 IP 范围与 SQL Server 设备表";

            TableLayoutPanel inner = NewGrid(1, 12, Theme.Surface);
            AddRows(inner, new float[] { 24f, 62f, 30f, 40f, 30f, 24f, 38f, 24f, 0f, 46f, 22f, 20f });

            Label lblCidr = UiStyle.CreateLabel("CIDR / IP 范围（逗号、分号或换行分隔）", Theme.FontSmall, Theme.TextSecondary, Theme.Surface);
            lblCidr.Dock = DockStyle.Fill;
            lblCidr.Margin = new Padding(0);
            inner.Controls.Add(lblCidr, 0, 0);

            _txtCidr = new TextBox();
            _txtCidr.Multiline = true;
            _txtCidr.ScrollBars = ScrollBars.Vertical;
            UiStyle.StyleInput(_txtCidr);
            _txtCidr.Dock = DockStyle.Fill;
            _txtCidr.Margin = new Padding(0, 0, 0, Theme.Sp8);
            inner.Controls.Add(_txtCidr, 0, 1);

            _chkMssql = new UiCheckBox();
            _chkMssql.Text = "从 SQL Server 设备表加载设备";
            UiStyle.StyleCheck(_chkMssql);
            _chkMssql.Dock = DockStyle.Fill;
            _chkMssql.Margin = new Padding(0);
            _chkMssql.CheckedChanged += chkMssql_CheckedChanged;
            inner.Controls.Add(_chkMssql, 0, 2);

            TableLayoutPanel importRow = NewGrid(2, 1, Theme.Surface);
            AddColumns(importRow, new float[] { 132f, 0f });
            AddRows(importRow, new float[] { 0f });

            _btnImportFromMssql = MakeButton("立即从库导入", UiButtonKind.Secondary, new Padding(0, 4, Theme.Sp8, 4));
            _btnImportFromMssql.Click += btnImportFromMssql_Click;
            importRow.Controls.Add(_btnImportFromMssql, 0, 0);

            Label lblImportHint = UiStyle.CreateLabel("按 IP 去重合并，已存在的设备刷新名称 / 端口 / 密码 / 时区", Theme.FontTiny, Theme.TextMuted, Theme.Surface);
            lblImportHint.Dock = DockStyle.Fill;
            lblImportHint.Margin = new Padding(0);
            importRow.Controls.Add(lblImportHint, 1, 0);
            inner.Controls.Add(importRow, 0, 3);

            _chkMssqlAutoImport = new UiCheckBox();
            _chkMssqlAutoImport.Text = "自动导入：每次自动同步前先刷新 SQL 设备信息";
            UiStyle.StyleCheck(_chkMssqlAutoImport);
            _chkMssqlAutoImport.Dock = DockStyle.Fill;
            _chkMssqlAutoImport.Margin = new Padding(0);
            _chkMssqlAutoImport.CheckedChanged += chkMssql_CheckedChanged;
            inner.Controls.Add(_chkMssqlAutoImport, 0, 4);

            Label lblConn = UiStyle.CreateLabel("数据库连接字符串", Theme.FontSmall, Theme.TextSecondary, Theme.Surface);
            lblConn.Dock = DockStyle.Fill;
            lblConn.Margin = new Padding(0);
            inner.Controls.Add(lblConn, 0, 5);

            _txtMssqlConn = new TextBox();
            UiStyle.StyleInput(_txtMssqlConn);
            _txtMssqlConn.Dock = DockStyle.Fill;
            _txtMssqlConn.Margin = new Padding(0, 0, 0, Theme.Sp8);
            inner.Controls.Add(_txtMssqlConn, 0, 6);

            Label lblQuery = UiStyle.CreateLabel("SQL 查询语句（列别名：Ip, Port, CommKey, Name, IANA）", Theme.FontSmall, Theme.TextSecondary, Theme.Surface);
            lblQuery.Dock = DockStyle.Fill;
            lblQuery.Margin = new Padding(0);
            inner.Controls.Add(lblQuery, 0, 7);

            _txtMssqlQuery = new TextBox();
            _txtMssqlQuery.Multiline = true;
            _txtMssqlQuery.ScrollBars = ScrollBars.Vertical;
            UiStyle.StyleInput(_txtMssqlQuery);
            _txtMssqlQuery.Dock = DockStyle.Fill;
            _txtMssqlQuery.Margin = new Padding(0, 0, 0, Theme.Sp8);
            inner.Controls.Add(_txtMssqlQuery, 0, 8);

            _btnStartScan = MakeButton("开始扫描并检测中控设备", UiButtonKind.Primary, new Padding(0, 7, 0, 7));
            _btnStartScan.Click += btnStartScan_Click;
            inner.Controls.Add(_btnStartScan, 0, 9);

            _prgScan = new UiProgress();
            _prgScan.Dock = DockStyle.Fill;
            _prgScan.Margin = new Padding(0, 7, 0, 7);
            _prgScan.BackColor = Theme.Surface;
            inner.Controls.Add(_prgScan, 0, 10);

            _lblScanProgress = UiStyle.CreateLabel("扫描进度：未开始", Theme.FontSmall, Theme.TextMuted, Theme.Surface);
            _lblScanProgress.Dock = DockStyle.Fill;
            _lblScanProgress.Margin = new Padding(0);
            inner.Controls.Add(_lblScanProgress, 0, 11);

            card.Controls.Add(inner);
            return card;
        }

        private void LstScanResults_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _lstScanResults.Items.Count) return;

            ScanResultItem item = _lstScanResults.Items[e.Index] as ScanResultItem;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color back = selected ? Theme.RowSelected : Theme.Surface;
            Color fore = Theme.TextPrimary;
            if (item != null)
            {
                if (item.AlreadyImported) fore = Theme.TextMuted;
                else if (item.FromDatabase) fore = Theme.TextSecondary;
            }

            using (SolidBrush brush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            string text = item == null ? Convert.ToString(_lstScanResults.Items[e.Index]) : item.ToString();
            Rectangle rect = new Rectangle(e.Bounds.X + Theme.Sp8, e.Bounds.Y, e.Bounds.Width - Theme.Sp16, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, text, Theme.FontBody, rect, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private void BindScanPage()
        {
            IpSourcesConfig sources = _config.IpSources;
            if (sources == null)
            {
                sources = new IpSourcesConfig();
                sources.CidrRanges = new List<string>();
                _config.IpSources = sources;
            }

            _txtCidr.Text = sources.CidrRanges == null ? "" : string.Join(", ", sources.CidrRanges.ToArray());

            MssqlConfig mssql = _config.IpSources.Mssql;
            if (mssql == null)
            {
                mssql = new MssqlConfig();
                _config.IpSources.Mssql = mssql;
            }

            _chkMssql.Checked = mssql.Enabled;
            _chkMssqlAutoImport.Checked = mssql.AutoImport;
            _txtMssqlConn.Text = mssql.ConnectionString == null ? "" : mssql.ConnectionString;
            _txtMssqlQuery.Text = mssql.QuerySql == null ? "" : mssql.QuerySql;
            ToggleMssqlInputs(_chkMssql.Checked);

            _lstScanResults.Items.Clear();
            _scanItems.Clear();
            UpdateScanSummary();
        }

        private void ToggleMssqlInputs(bool enabled)
        {
            _txtMssqlConn.Enabled = enabled;
            _txtMssqlQuery.Enabled = enabled;
            _btnImportFromMssql.Enabled = enabled;
            _chkMssqlAutoImport.Enabled = enabled;
        }

        private void chkMssql_CheckedChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            ToggleMssqlInputs(_chkMssql.Checked);
            if (_chkMssqlAutoImport.Enabled)
            {
                SetStatus(_chkMssqlAutoImport.Checked
                    ? "已开启自动导入：每次自动同步前会先刷新 SQL 设备表。"
                    : "已关闭自动导入。", LogLevel.Info);
            }
        }

        private MssqlConfig CollectMssqlConfig()
        {
            MssqlConfig mssql = new MssqlConfig();
            mssql.Enabled = _chkMssql.Checked;
            mssql.ConnectionString = _txtMssqlConn.Text.Trim();
            mssql.QuerySql = _txtMssqlQuery.Text.Trim();
            mssql.AutoImport = _chkMssqlAutoImport.Checked;
            return mssql;
        }

        private void PersistIpSources()
        {
            List<string> ranges = SplitTargets(_txtCidr.Text);
            if (_config.IpSources == null) _config.IpSources = new IpSourcesConfig();
            _config.IpSources.CidrRanges = ranges;
            _config.IpSources.Mssql = CollectMssqlConfig();
        }

        /// <summary>按逗号 / 分号 / 换行 / 制表符拆分多段输入</summary>
        private static List<string> SplitTargets(string text)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;

            string[] parts = text.Split(new char[] { ',', '，', '\r', '\n', ';', '；', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length > 0 && !result.Contains(part)) result.Add(part);
            }
            return result;
        }

        private void btnImportFromMssql_Click(object sender, EventArgs e)
        {
            if (_loading || _config == null) return;
            if (_gridDirty)
            {
                MessageBox.Show("记忆设备表中有未保存的修改，请先点击「保存并应用」，再执行导入。",
                    "存在未保存的修改", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MssqlConfig mssql = CollectMssqlConfig();
            if (mssql.ConnectionString.Length == 0 || mssql.QuerySql.Length == 0)
            {
                MessageBox.Show("请先填写数据库连接字符串与查询语句。", "参数不完整",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CommDefaultsConfig commDefaults = _config.CommDefaults;
            _btnImportFromMssql.Enabled = false;
            SetStatus("正在从 SQL Server 读取设备表…", LogLevel.Info);

            ThreadPool.QueueUserWorkItem(delegate
            {
                List<DeviceInfo> dbDevices = null;
                Exception failure = null;
                try
                {
                    dbDevices = DeviceScanner.QueryFromMssql(mssql, commDefaults);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                UiBeginInvoke(delegate
                {
                    if (_btnImportFromMssql != null) _btnImportFromMssql.Enabled = _chkMssql.Checked;

                    if (failure != null)
                    {
                        SetStatus("数据库导入失败：" + failure.Message, LogLevel.Error);
                        Logger.Error("数据库导入失败: " + failure.Message);
                        MessageBox.Show("从数据库读取设备失败：\r\n\r\n" + failure.Message, "导入失败",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    int count = dbDevices == null ? 0 : dbDevices.Count;
                    if (count == 0)
                    {
                        SetStatus("数据库中未查询到设备记录。", LogLevel.Warn);
                        MessageBox.Show("查询已执行，但没有返回任何设备记录。请检查查询语句与数据表内容。",
                            "无记录", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    int[] merged = DeviceScanner.MergeDevicesIntoMemory(_config.Devices, dbDevices);
                    ConfigManager.Save(_config);
                    RefreshDeviceGrid();
                    UpdateOverviewStats();

                    SetStatus(string.Format("数据库导入完成：新增 {0} 台，更新 {1} 台。", merged[0], merged[1]), LogLevel.Success);
                    Logger.Info(string.Format("数据库导入完成：新增 {0} 台，更新 {1} 台。", merged[0], merged[1]));
                    MessageBox.Show(string.Format("导入完成。\r\n\r\n新增设备：{0} 台\r\n更新配置：{1} 台", merged[0], merged[1]),
                        "导入成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            });
        }

        private void btnStartScan_Click(object sender, EventArgs e)
        {
            if (_scanning) return;

            MssqlConfig mssql = CollectMssqlConfig();
            List<string> ranges = SplitTargets(_txtCidr.Text);

            if (ranges.Count == 0 && !mssql.Enabled)
            {
                MessageBox.Show("请至少配置一种设备来源：填写 CIDR / IP 范围，或勾选「从 SQL Server 设备表加载设备」。",
                    "缺少设备来源", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            PersistIpSources();
            ConfigManager.Save(_config);

            int port = _config.CommDefaults == null ? 4370 : _config.CommDefaults.Port;
            int commKey = _config.CommDefaults == null ? 0 : _config.CommDefaults.CommKey;

            _scanning = true;
            _btnStartScan.Enabled = false;
            _btnImportFromMssql.Enabled = false;
            _prgScan.Value = 0;
            _lblScanProgress.Text = "扫描进度：正在解析目标地址…";
            SetStatus("开始扫描设备来源，请稍候…", LogLevel.Info);

            ThreadPool.QueueUserWorkItem(delegate
            {
                List<DeviceInfo> discovered = new List<DeviceInfo>();
                Exception failure = null;

                try
                {
                    if (mssql.Enabled && mssql.ConnectionString.Length > 0)
                    {
                        List<DeviceInfo> dbDevices = DeviceScanner.QueryFromMssql(mssql, _config.CommDefaults);
                        for (int i = 0; i < dbDevices.Count; i++)
                        {
                            DeviceInfo device = dbDevices[i];
                            if (device == null) continue;
                            device.LastStatus = "数据库记录";
                            discovered.Add(device);
                        }
                        Logger.Info(string.Format("从数据库读取到 {0} 台设备，继续扫描网段。", dbDevices.Count));
                    }

                    if (ranges.Count > 0)
                    {
                        List<string> targets = DeviceScanner.ParseIpTargets(ranges);
                        Logger.Info(string.Format("网段解析完成，共 {0} 个待探测地址。", targets.Count));
                        List<DeviceInfo> online = DeviceScanner.ScanNetwork(targets, port, commKey, ScanProgressCallback);
                        discovered.AddRange(online);
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                UiBeginInvoke(delegate
                {
                    _scanning = false;
                    if (_btnStartScan != null) _btnStartScan.Enabled = true;
                    if (_btnImportFromMssql != null) _btnImportFromMssql.Enabled = _chkMssql.Checked;

                    if (failure != null)
                    {
                        _prgScan.Value = 0;
                        _lblScanProgress.Text = "扫描进度：失败";
                        SetStatus("扫描失败：" + failure.Message, LogLevel.Error);
                        Logger.Error("扫描失败: " + failure.Message);
                        MessageBox.Show("扫描过程中出现错误：\r\n\r\n" + failure.Message, "扫描失败",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    _prgScan.Value = 100;
                    BuildScanResultItems(discovered);
                    _lblScanProgress.Text = string.Format("扫描进度：完成，共 {0} 条结果。", _scanItems.Count);
                    SetStatus(string.Format("扫描完成，共发现 {0} 条设备记录。", _scanItems.Count), LogLevel.Success);
                });
            });
        }

        private void ScanProgressCallback(string ip, int current, int total)
        {
            if (total <= 0) return;
            int percent = (int)(current * 100L / total);
            string text = string.Format("扫描进度：{0}/{1}   {2}", current, total, ip);

            UiBeginInvoke(delegate
            {
                if (_prgScan != null) _prgScan.Value = percent;
                if (_lblScanProgress != null) _lblScanProgress.Text = text;
            });
        }

        private void BuildScanResultItems(List<DeviceInfo> discovered)
        {
            HashSet<string> imported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _scanItems.Count; i++)
            {
                ScanResultItem old = _scanItems[i];
                if (old != null && old.AlreadyImported && old.Device != null) imported.Add(old.Device.Ip);
            }

            List<ScanResultItem> items = new List<ScanResultItem>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (discovered != null)
            {
                for (int i = 0; i < discovered.Count; i++)
                {
                    DeviceInfo device = discovered[i];
                    if (device == null || string.IsNullOrEmpty(device.Ip)) continue;
                    if (!seen.Add(device.Ip)) continue;

                    ScanResultItem item = new ScanResultItem();
                    item.Device = device;
                    item.FromDatabase = string.Equals(device.LastStatus, "数据库记录", StringComparison.Ordinal);
                    item.AlreadyImported = imported.Contains(device.Ip) || FindDevice(_config.Devices, device.Ip) != null;
                    items.Add(item);
                }
            }

            _scanItems.Clear();
            _scanItems.AddRange(items);
            ReloadScanListItems();
            UpdateScanSummary();
        }

        private void ReloadScanListItems()
        {
            _lstScanResults.BeginUpdate();
            try
            {
                _lstScanResults.Items.Clear();
                for (int i = 0; i < _scanItems.Count; i++)
                {
                    _lstScanResults.Items.Add(_scanItems[i]);
                }
            }
            finally
            {
                _lstScanResults.EndUpdate();
            }
        }

        private void UpdateScanSummary()
        {
            if (_lblScanSummary == null) return;

            if (_scanItems.Count == 0)
            {
                _lblScanSummary.ForeColor = Theme.TextMuted;
                _lblScanSummary.Text = "尚未扫描：配置好左侧来源后点击「开始扫描并检测中控设备」。";
                return;
            }

            int imported = 0;
            for (int i = 0; i < _scanItems.Count; i++)
            {
                if (_scanItems[i] != null && _scanItems[i].AlreadyImported) imported++;
            }

            _lblScanSummary.ForeColor = Theme.TextMuted;
            _lblScanSummary.Text = string.Format("共 {0} 条结果，其中已导入 {1} 条；灰色条目表示已在记忆库中。", _scanItems.Count, imported);
        }

        private void btnImportSelected_Click(object sender, EventArgs e)
        {
            if (_config == null) return;
            if (_gridDirty)
            {
                MessageBox.Show("记忆设备表中有未保存的修改，请先点击「保存并应用」，再执行导入。",
                    "存在未保存的修改", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<ScanResultItem> picked = new List<ScanResultItem>();
            for (int i = 0; i < _lstScanResults.SelectedIndices.Count; i++)
            {
                int index = _lstScanResults.SelectedIndices[i];
                if (index >= 0 && index < _scanItems.Count) picked.Add(_scanItems[index]);
            }
            if (picked.Count == 0)
            {
                MessageBox.Show("请先在右侧列表中选中要导入的设备（可按住 Ctrl 或 Shift 多选）。",
                    "未选择设备", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int added = 0;
            int updated = 0;
            for (int i = 0; i < picked.Count; i++)
            {
                ScanResultItem item = picked[i];
                if (item == null || item.Device == null) continue;
                DeviceInfo source = item.Device;

                DeviceInfo existing = FindDevice(_config.Devices, source.Ip);
                if (existing == null)
                {
                    DeviceInfo device = new DeviceInfo();
                    device.Ip = source.Ip;
                    device.Port = source.Port;
                    device.CommKey = source.CommKey;
                    device.Name = string.IsNullOrEmpty(source.Name) ? "中控设备" : source.Name;
                    device.Iana = source.Iana == null ? "" : source.Iana;
                    device.LastSyncTime = "";
                    device.LastStatus = "待同步";
                    _config.Devices.Add(device);
                    added++;
                }
                else
                {
                    existing.Port = source.Port;
                    existing.CommKey = source.CommKey;
                    if (!string.IsNullOrEmpty(source.Name)) existing.Name = source.Name;
                    if (!string.IsNullOrEmpty(source.Iana)) existing.Iana = source.Iana;
                    updated++;
                }
                item.AlreadyImported = true;
            }

            ConfigManager.Save(_config);
            RefreshDeviceGrid();
            UpdateOverviewStats();
            _lstScanResults.Invalidate();
            UpdateScanSummary();

            SetStatus(string.Format("导入完成：新增 {0} 台，更新 {1} 台。", added, updated), LogLevel.Success);
            Logger.Info(string.Format("扫描结果导入完成：新增 {0} 台，更新 {1} 台。", added, updated));
        }
    }
}
