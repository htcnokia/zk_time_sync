using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ZkTimeSync
{
    /// <summary>
    /// 自动同步与定时任务页 + 企业微信通知设置页。
    /// </summary>
    public partial class MainForm
    {
        // ==================== 自动同步页控件 ====================
        private CheckBox _chkUseCron;
        private TextBox _txtCronExpr;
        private Label _lblCronHint;
        private UiButton _btnPreviewNext;
        private TextBox _txtIntervalMin;
        private Label _lblNextRun;
        private UiButton _btnSaveSchedule;

        private CheckBox _chkSyncOnStartup;
        private Label _lblStartupHint;
        private TextBox _txtStartupDelay;
        private TextBox _txtStartupGuard;
        private TextBox _txtBootWindow;
        private CheckBox _chkAutoStart;
        private UiButton _btnSaveStartup;

        // ==================== 企业微信页控件 ====================
        private CheckBox _chkWeComEnabled;
        private ComboBox _cmbWeComMode;
        private TextBox _txtWebhookUrl;
        private TextBox _txtCorpId;
        private TextBox _txtCorpSecret;
        private TextBox _txtAgentId;
        private TextBox _txtToUser;
        private TextBox _txtCustomHeader;
        private TextBox _txtCustomFooter;
        private UiButton _btnSaveWeCom;
        private UiButton _btnTestWeCom;

        // ==================== 自动同步页 ====================

        private Control BuildSchedulePage()
        {
            TableLayoutPanel page = NewGrid(3, 1, Theme.Bg);
            AddColumns(page, new float[] { 0f, 0f, 0f });
            AddRows(page, new float[] { 0f });

            page.Controls.Add(BuildPlanCard(), 0, 0);
            page.Controls.Add(BuildStartupCard(), 1, 0);
            page.Controls.Add(BuildServiceCard(), 2, 0);

            return page;
        }

        private CardPanel BuildPlanCard()
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0);
            card.Title = "自动同步计划";
            card.Subtitle = "CRON 表达式与固定间隔二选一";

            TableLayoutPanel inner = NewGrid(1, 7, Theme.Surface);
            AddRows(inner, new float[] { 34f, 40f, 48f, 40f, 44f, 48f, 0f });

            _chkUseCron = new UiCheckBox();
            _chkUseCron.Text = "使用 CRON 表达式定时（推荐，例如每天凌晨 2 点整）";
            UiStyle.StyleCheck(_chkUseCron);
            _chkUseCron.Dock = DockStyle.Fill;
            _chkUseCron.Margin = new Padding(0, 0, 0, Theme.Sp4);
            _chkUseCron.CheckedChanged += chkUseCron_CheckedChanged;
            inner.Controls.Add(_chkUseCron, 0, 0);

            TableLayoutPanel cronRow = NewGrid(2, 1, Theme.Surface);
            AddColumns(cronRow, new float[] { 0f, 148f });
            AddRows(cronRow, new float[] { 0f });

            _txtCronExpr = new TextBox();
            UiStyle.StyleInput(_txtCronExpr);
            _txtCronExpr.Dock = DockStyle.Fill;
            _txtCronExpr.Margin = new Padding(0, 6, Theme.Sp8, 6);
            _txtCronExpr.TextChanged += txtCronExpr_TextChanged;
            cronRow.Controls.Add(_txtCronExpr, 0, 0);

            _btnPreviewNext = MakeButton("计算下次", UiButtonKind.Secondary, new Padding(0, 4, 0, 4));
            _btnPreviewNext.Click += btnPreviewNext_Click;
            cronRow.Controls.Add(_btnPreviewNext, 1, 0);
            inner.Controls.Add(cronRow, 0, 1);

            _lblCronHint = UiStyle.CreateLabel(
                "格式：秒 分 时 日 月 周（6 段）。例如 '0 0 2 * * ?' 每天 02:00；'0 */30 * * * ?' 每 30 分钟。",
                Theme.FontTiny, Theme.TextMuted, Theme.Surface);
            _lblCronHint.Dock = DockStyle.Fill;
            _lblCronHint.Margin = new Padding(0);
            inner.Controls.Add(_lblCronHint, 0, 2);

            TableLayoutPanel intRow = NewGrid(3, 1, Theme.Surface);
            AddColumns(intRow, new float[] { 196f, 104f, 0f });
            AddRows(intRow, new float[] { 0f });

            Label lblInterval = UiStyle.CreateLabel("固定间隔执行（分钟）", Theme.FontBody, Theme.TextSecondary, Theme.Surface);
            lblInterval.Dock = DockStyle.Fill;
            lblInterval.Margin = new Padding(0);
            intRow.Controls.Add(lblInterval, 0, 0);

            _txtIntervalMin = new TextBox();
            UiStyle.StyleInput(_txtIntervalMin);
            _txtIntervalMin.Dock = DockStyle.Fill;
            _txtIntervalMin.Margin = new Padding(0, 6, Theme.Sp8, 6);
            intRow.Controls.Add(_txtIntervalMin, 1, 0);

            Label lblIntervalHint = UiStyle.CreateLabel("1 - 10080（最长 7 天）", Theme.FontTiny, Theme.TextMuted, Theme.Surface);
            lblIntervalHint.Dock = DockStyle.Fill;
            lblIntervalHint.Margin = new Padding(0);
            intRow.Controls.Add(lblIntervalHint, 2, 0);
            inner.Controls.Add(intRow, 0, 3);

            _lblNextRun = new Label();
            _lblNextRun.AutoSize = false;
            _lblNextRun.Dock = DockStyle.Fill;
            _lblNextRun.Font = Theme.FontSmall;
            _lblNextRun.ForeColor = Theme.TextMuted;
            _lblNextRun.BackColor = Theme.PrimarySoft;
            _lblNextRun.Padding = new Padding(Theme.Sp8, 0, Theme.Sp8, 0);
            _lblNextRun.TextAlign = ContentAlignment.MiddleLeft;
            _lblNextRun.Margin = new Padding(0, 4, 0, 4);
            _lblNextRun.Text = "打开本页后自动计算下次执行时间。";
            inner.Controls.Add(_lblNextRun, 0, 4);

            _btnSaveSchedule = MakeButton("保存自动计划并立即生效", UiButtonKind.Primary, new Padding(0, 8, 0, 8));
            _btnSaveSchedule.Click += btnSaveSchedule_Click;
            inner.Controls.Add(_btnSaveSchedule, 0, 5);

            card.Controls.Add(inner);
            return card;
        }

        private CardPanel BuildStartupCard()
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(Theme.Sp12, 0, 0, 0);
            card.Title = "启动与断电恢复";
            card.Subtitle = "服务器重启或断电恢复后自动补一次全员对时";

            TableLayoutPanel inner = NewGrid(1, 8, Theme.Surface);
            AddRows(inner, new float[] { 34f, 46f, 40f, 40f, 40f, 40f, 48f, 0f });

            _chkSyncOnStartup = new UiCheckBox();
            _chkSyncOnStartup.Text = "程序启动后自动执行一次全员对时";
            UiStyle.StyleCheck(_chkSyncOnStartup);
            _chkSyncOnStartup.Dock = DockStyle.Fill;
            _chkSyncOnStartup.Margin = new Padding(0, 0, 0, Theme.Sp4);
            _chkSyncOnStartup.CheckedChanged += chkSyncOnStartup_CheckedChanged;
            inner.Controls.Add(_chkSyncOnStartup, 0, 0);

            _lblStartupHint = UiStyle.CreateLabel("", Theme.FontTiny, Theme.TextMuted, Theme.Surface);
            _lblStartupHint.Dock = DockStyle.Fill;
            _lblStartupHint.Margin = new Padding(0, 0, 0, Theme.Sp4);
            inner.Controls.Add(_lblStartupHint, 0, 1);

            inner.Controls.Add(BuildNumberRow("启动后延迟（秒）", 168f, 92f, "等待网络就绪", out _txtStartupDelay), 0, 2);
            inner.Controls.Add(BuildNumberRow("冷启动判定窗口（分钟）", 168f, 92f, "断电恢复强制对时", out _txtBootWindow), 0, 3);
            inner.Controls.Add(BuildNumberRow("防重复保护（分钟）", 168f, 92f, "0 = 不检查", out _txtStartupGuard), 0, 4);

            _chkAutoStart = new UiCheckBox();
            _chkAutoStart.Text = "桌面程序随 Windows 登录启动（服务模式无需此项）";
            UiStyle.StyleCheck(_chkAutoStart);
            _chkAutoStart.Dock = DockStyle.Fill;
            _chkAutoStart.Margin = new Padding(0, 0, 0, Theme.Sp4);
            inner.Controls.Add(_chkAutoStart, 0, 5);

            _btnSaveStartup = MakeButton("保存启动与断电恢复设置", UiButtonKind.Primary, new Padding(0, 8, 0, 8));
            _btnSaveStartup.Click += btnSaveStartup_Click;
            inner.Controls.Add(_btnSaveStartup, 0, 6);

            card.Controls.Add(inner);
            return card;
        }

        private TableLayoutPanel BuildNumberRow(string caption, float captionWidth, float boxWidth, string hint, out TextBox box)
        {
            TableLayoutPanel row = NewGrid(3, 1, Theme.Surface);
            AddColumns(row, new float[] { captionWidth, boxWidth, 0f });
            AddRows(row, new float[] { 0f });

            Label label = UiStyle.CreateLabel(caption, Theme.FontBody, Theme.TextSecondary, Theme.Surface);
            label.Dock = DockStyle.Fill;
            label.Margin = new Padding(0);
            row.Controls.Add(label, 0, 0);

            box = new TextBox();
            UiStyle.StyleInput(box);
            box.Dock = DockStyle.Fill;
            box.Margin = new Padding(0, 6, Theme.Sp8, 6);
            row.Controls.Add(box, 1, 0);

            Label tip = UiStyle.CreateLabel(hint, Theme.FontTiny, Theme.TextMuted, Theme.Surface);
            tip.Dock = DockStyle.Fill;
            tip.Margin = new Padding(0);
            row.Controls.Add(tip, 2, 0);

            return row;
        }

        private void BindSchedulePage()
        {
            GeneralConfig general = _config.General;
            if (general == null)
            {
                general = new GeneralConfig();
                _config.General = general;
            }

            _chkUseCron.Checked = general.UseCron;
            _txtCronExpr.Text = general.CronExpression == null ? "" : general.CronExpression;
            _txtIntervalMin.Text = general.SyncIntervalMinutes.ToString();

            _chkSyncOnStartup.Checked = general.SyncOnStartup;
            _txtStartupDelay.Text = general.StartupSyncDelaySeconds.ToString();
            _txtStartupGuard.Text = general.StartupSyncGuardMinutes.ToString();
            _txtBootWindow.Text = general.BootSyncWindowMinutes.ToString();

            bool autoStart;
            try { autoStart = AutoStartManager.IsAutoStartEnabled(); }
            catch { autoStart = false; }
            _chkAutoStart.Checked = autoStart;

            ToggleScheduleInputs(_chkUseCron.Checked);
            UpdateStartupHint();
        }

        private void ToggleScheduleInputs(bool useCron)
        {
            _txtCronExpr.Enabled = useCron;
            _btnPreviewNext.Enabled = useCron;
            _lblCronHint.ForeColor = useCron ? Theme.TextMuted : Theme.DisabledText;
            _txtIntervalMin.Enabled = !useCron;
        }

        private void chkUseCron_CheckedChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            ToggleScheduleInputs(_chkUseCron.Checked);
            RefreshSchedulePreview();
        }

        private void chkSyncOnStartup_CheckedChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            _txtStartupDelay.Enabled = _chkSyncOnStartup.Checked;
            _txtStartupGuard.Enabled = _chkSyncOnStartup.Checked;
            _txtBootWindow.Enabled = _chkSyncOnStartup.Checked;
            UpdateStartupHint();
        }

        private void txtCronExpr_TextChanged(object sender, EventArgs e)
        {
            if (_loading || _lblNextRun == null) return;
            _lblNextRun.ForeColor = Theme.TextMuted;
            _lblNextRun.Text = "表达式已修改，点击右侧「计算下次」或保存后自动计算。";
        }

        private void btnPreviewNext_Click(object sender, EventArgs e)
        {
            RefreshSchedulePreview();
        }

        private void UpdateStartupHint()
        {
            if (_lblStartupHint == null) return;

            if (!_chkSyncOnStartup.Checked)
            {
                _lblStartupHint.ForeColor = Theme.TextMuted;
                _lblStartupHint.Text = "当前已关闭：程序启动后只按左侧的定时计划执行；断电恢复后若错过时间点，需要等到下一个计划时刻。";
                return;
            }

            int delay;
            int guard;
            int bootWindow;
            bool delayOk = int.TryParse(_txtStartupDelay.Text.Trim(), out delay) && delay >= 0 && delay <= 3600;
            bool guardOk = int.TryParse(_txtStartupGuard.Text.Trim(), out guard) && guard >= 0 && guard <= 1440;
            bool bootOk = int.TryParse(_txtBootWindow.Text.Trim(), out bootWindow) && bootWindow >= 0 && bootWindow <= 1440;

            if (!delayOk || !guardOk || !bootOk)
            {
                _lblStartupHint.ForeColor = Theme.Danger;
                _lblStartupHint.Text = "启动延迟需为 0 - 3600 秒，冷启动窗口与防重复保护需为 0 - 1440 分钟。";
                return;
            }

            _lblStartupHint.ForeColor = Theme.TextSecondary;
            _lblStartupHint.Text = string.Format(
                "策略：启动后等待 {0} 秒对时；开机 {1} 分钟内视为断电恢复并强制对时（不受防重复限制）；同一开机周期内重启时，{2} 分钟内已同步则跳过。",
                delay, bootWindow, guard);
        }

        private void btnSaveSchedule_Click(object sender, EventArgs e)
        {
            if (_config == null) return;

            bool useCron = _chkUseCron.Checked;
            string cron = _txtCronExpr.Text.Trim();

            if (useCron)
            {
                if (cron.Length == 0)
                {
                    MessageBox.Show("请填写 CRON 表达式，例如 '0 0 2 * * ?' 表示每天 02:00。",
                        "表达式为空", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                try
                {
                    CronExpression probe = new CronExpression(cron);
                    probe.GetNextOccurrence(DateTime.Now);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("CRON 表达式无法解析：\r\n\r\n" + ex.Message +
                        "\r\n\r\n格式为：秒 分 时 日 月 周（6 段），例如 '0 0 2 * * ?'。",
                        "表达式无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            int interval;
            if (!int.TryParse(_txtIntervalMin.Text.Trim(), out interval) || interval < 1 || interval > 10080)
            {
                if (!useCron)
                {
                    MessageBox.Show("固定间隔必须是 1 - 10080 之间的整数（分钟）。", "间隔无效",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                interval = _config.General.SyncIntervalMinutes;
            }

            _config.General.UseCron = useCron;
            _config.General.CronExpression = cron;
            _config.General.SyncIntervalMinutes = interval;
            ConfigManager.Save(_config);

            if (_scheduler != null) _scheduler.ReloadCron();
            RefreshSchedulePreview();

            SetStatus("自动同步计划已保存并立即生效。", LogLevel.Success);
            Logger.Info(string.Format("自动同步计划已更新：{0}。",
                useCron ? "CRON " + cron : string.Format("每 {0} 分钟", interval)));
        }

        private void btnSaveStartup_Click(object sender, EventArgs e)
        {
            if (_config == null) return;

            int delay;
            if (!int.TryParse(_txtStartupDelay.Text.Trim(), out delay) || delay < 0 || delay > 3600)
            {
                MessageBox.Show("启动后延迟必须是 0 - 3600 之间的整数（秒）。", "参数无效",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int guard;
            if (!int.TryParse(_txtStartupGuard.Text.Trim(), out guard) || guard < 0 || guard > 1440)
            {
                MessageBox.Show("防重复保护必须是 0 - 1440 之间的整数（分钟），0 表示不检查。", "参数无效",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int bootWindow;
            if (!int.TryParse(_txtBootWindow.Text.Trim(), out bootWindow) || bootWindow < 0 || bootWindow > 1440)
            {
                MessageBox.Show("冷启动判定窗口必须是 0 - 1440 之间的整数（分钟），0 表示关闭强制对时。", "参数无效",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _config.General.SyncOnStartup = _chkSyncOnStartup.Checked;
            _config.General.StartupSyncDelaySeconds = delay;
            _config.General.StartupSyncGuardMinutes = guard;
            _config.General.BootSyncWindowMinutes = bootWindow;
            _config.General.AutoStartWithWindows = _chkAutoStart.Checked;

            string autoStartError = null;
            try { AutoStartManager.SetAutoStart(_chkAutoStart.Checked); }
            catch (Exception ex) { autoStartError = ex.Message; }

            ConfigManager.Save(_config);
            UpdateStartupHint();

            if (autoStartError != null)
            {
                SetStatus("开机自启动设置失败：" + autoStartError, LogLevel.Warn);
                MessageBox.Show("开机自启动项写入失败：\r\n\r\n" + autoStartError +
                    "\r\n\r\n其余设置已保存。可尝试以管理员身份运行本程序后再试。",
                    "部分设置未生效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetStatus("启动与断电恢复设置已保存（下次启动生效）。", LogLevel.Success);
            MessageBox.Show(string.Format(
                "设置已保存，将在下次启动程序时生效：\r\n\r\n启动后延迟：{0} 秒\r\n冷启动判定窗口：{1} 分钟\r\n防重复保护：{2} 分钟\r\n桌面登录自启：{3}",
                delay, bootWindow, guard, _chkAutoStart.Checked ? "已开启" : "未开启"),
                "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>CRON 计算可能耗费数百毫秒，放到线程池避免卡住界面</summary>
        private void RefreshSchedulePreview()
        {
            if (_lblNextRun == null || _chkUseCron == null || _txtIntervalMin == null) return;
            if (_previewBusy) return;

            if (!_chkUseCron.Checked)
            {
                int interval;
                if (!int.TryParse(_txtIntervalMin.Text.Trim(), out interval) || interval < 1)
                {
                    _lblNextRun.ForeColor = Theme.Danger;
                    _lblNextRun.Text = "固定间隔必须是大于 0 的整数（分钟）。";
                    return;
                }
                _lblNextRun.ForeColor = Theme.Primary;
                _lblNextRun.Text = string.Format("固定间隔模式：每 {0} 分钟执行一次，下一次约为 {1:yyyy-MM-dd HH:mm}。",
                    interval, DateTime.Now.AddMinutes(interval));
                return;
            }

            string cron = _txtCronExpr.Text.Trim();
            if (cron.Length == 0)
            {
                _lblNextRun.ForeColor = Theme.Danger;
                _lblNextRun.Text = "请填写 CRON 表达式。";
                return;
            }

            CronExpression expression;
            try
            {
                expression = new CronExpression(cron);
            }
            catch (Exception ex)
            {
                _lblNextRun.ForeColor = Theme.Danger;
                _lblNextRun.Text = "表达式无效：" + ex.Message;
                return;
            }

            _previewBusy = true;
            _lblNextRun.ForeColor = Theme.TextMuted;
            _lblNextRun.Text = "正在计算下一次执行时间…";

            ThreadPool.QueueUserWorkItem(delegate
            {
                string text;
                bool ok;
                DateTime? next = null;
                string failure = null;
                try
                {
                    next = expression.GetNextOccurrence(DateTime.Now);
                }
                catch (Exception ex)
                {
                    failure = ex.Message;
                }

                if (failure != null)
                {
                    ok = false;
                    text = "计算失败：" + failure;
                }
                else if (!next.HasValue)
                {
                    ok = false;
                    text = "该表达式在未来 100 天内没有可执行的时刻，请检查表达式。";
                }
                else
                {
                    ok = true;
                    text = string.Format("下次执行：{0:yyyy-MM-dd HH:mm:ss}（约 {1} 后）",
                        next.Value, DescribeSpan(next.Value - DateTime.Now));
                }

                UiBeginInvoke(delegate
                {
                    _previewBusy = false;
                    if (_lblNextRun == null) return;
                    _lblNextRun.ForeColor = ok ? Theme.Primary : Theme.Danger;
                    _lblNextRun.Text = text;
                });
            });
        }

        private static string DescribeSpan(TimeSpan span)
        {
            if (span.TotalSeconds < 0) span = TimeSpan.Zero;
            if (span.TotalSeconds < 60) return string.Format("{0} 秒", (int)span.TotalSeconds);
            if (span.TotalHours < 1) return string.Format("{0} 分钟", (int)span.TotalMinutes);
            if (span.TotalDays < 1) return string.Format("{0} 小时 {1} 分钟", (int)span.TotalHours, span.Minutes);
            return string.Format("{0} 天 {1} 小时", (int)span.TotalDays, span.Hours);
        }

        // ==================== 企业微信通知页 ====================

        private Control BuildNotifyPage()
        {
            TableLayoutPanel page = NewGrid(1, 2, Theme.Bg);
            AddRows(page, new float[] { 0f, 22f });

            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0);
            card.Title = "企业微信通知";
            card.Subtitle = "同步结束后自动推送成功 / 失败设备清单";

            TableLayoutPanel inner = NewGrid(1, 10, Theme.Surface);
            AddRows(inner, new float[] { 34f, 40f, 40f, 40f, 40f, 26f, 0f, 26f, 0f, 48f });

            _chkWeComEnabled = new UiCheckBox();
            _chkWeComEnabled.Text = "开启企业微信实时同步报告通知";
            UiStyle.StyleCheck(_chkWeComEnabled);
            _chkWeComEnabled.Dock = DockStyle.Fill;
            _chkWeComEnabled.Margin = new Padding(0, 0, 0, Theme.Sp4);
            _chkWeComEnabled.CheckedChanged += chkWeComEnabled_CheckedChanged;
            inner.Controls.Add(_chkWeComEnabled, 0, 0);

            TableLayoutPanel modeRow = NewGrid(3, 1, Theme.Surface);
            AddColumns(modeRow, new float[] { 168f, 150f, 0f });
            AddRows(modeRow, new float[] { 0f });

            Label lblMode = UiStyle.CreateLabel("推送模式", Theme.FontBody, Theme.TextSecondary, Theme.Surface);
            lblMode.Dock = DockStyle.Fill;
            lblMode.Margin = new Padding(0);
            modeRow.Controls.Add(lblMode, 0, 0);

            _cmbWeComMode = new ComboBox();
            _cmbWeComMode.Items.AddRange(new object[] { "Webhook", "App" });
            UiStyle.StyleCombo(_cmbWeComMode);
            _cmbWeComMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbWeComMode.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _cmbWeComMode.Margin = new Padding(0, 0, Theme.Sp8, 0);
            _cmbWeComMode.SelectedIndexChanged += cmbWeComMode_SelectedIndexChanged;
            modeRow.Controls.Add(_cmbWeComMode, 1, 0);

            Label lblModeHint = UiStyle.CreateLabel("自建应用需填写企业 ID、Secret 与 AgentID", Theme.FontTiny, Theme.TextMuted, Theme.Surface);
            lblModeHint.Dock = DockStyle.Fill;
            lblModeHint.Margin = new Padding(0);
            modeRow.Controls.Add(lblModeHint, 2, 0);
            inner.Controls.Add(modeRow, 0, 1);

            _txtWebhookUrl = new TextBox();
            inner.Controls.Add(BuildFieldRow("机器人 Webhook 链接", _txtWebhookUrl, 168f, 0f), 0, 2);

            TableLayoutPanel corpRow = NewGrid(4, 1, Theme.Surface);
            AddColumns(corpRow, new float[] { 168f, 268f, 118f, 0f });
            AddRows(corpRow, new float[] { 0f });
            corpRow.Controls.Add(BuildFieldLabel("企业 ID (CorpID)"), 0, 0);
            _txtCorpId = new TextBox();
            corpRow.Controls.Add(BuildFieldBox(_txtCorpId, Theme.Sp8), 1, 0);
            corpRow.Controls.Add(BuildFieldLabel("应用 Secret"), 2, 0);
            _txtCorpSecret = new TextBox();
            corpRow.Controls.Add(BuildFieldBox(_txtCorpSecret, 0), 3, 0);
            inner.Controls.Add(corpRow, 0, 3);

            TableLayoutPanel agentRow = NewGrid(4, 1, Theme.Surface);
            AddColumns(agentRow, new float[] { 168f, 268f, 118f, 0f });
            AddRows(agentRow, new float[] { 0f });
            agentRow.Controls.Add(BuildFieldLabel("应用 AgentID"), 0, 0);
            _txtAgentId = new TextBox();
            agentRow.Controls.Add(BuildFieldBox(_txtAgentId, Theme.Sp8), 1, 0);
            agentRow.Controls.Add(BuildFieldLabel("接收人"), 2, 0);
            _txtToUser = new TextBox();
            agentRow.Controls.Add(BuildFieldBox(_txtToUser, 0), 3, 0);
            inner.Controls.Add(agentRow, 0, 4);

            Label lblHeader = UiStyle.CreateLabel("消息页眉（首部自定义样式及字符）", Theme.FontSmall, Theme.TextSecondary, Theme.Surface);
            lblHeader.Dock = DockStyle.Fill;
            lblHeader.Margin = new Padding(0);
            inner.Controls.Add(lblHeader, 0, 5);

            _txtCustomHeader = new TextBox();
            _txtCustomHeader.Multiline = true;
            _txtCustomHeader.ScrollBars = ScrollBars.Vertical;
            UiStyle.StyleInput(_txtCustomHeader);
            _txtCustomHeader.Dock = DockStyle.Fill;
            _txtCustomHeader.Margin = new Padding(0, 0, 0, Theme.Sp8);
            inner.Controls.Add(_txtCustomHeader, 0, 6);

            Label lblFooter = UiStyle.CreateLabel("消息页脚（尾部自定义样式及字符）", Theme.FontSmall, Theme.TextSecondary, Theme.Surface);
            lblFooter.Dock = DockStyle.Fill;
            lblFooter.Margin = new Padding(0);
            inner.Controls.Add(lblFooter, 0, 7);

            _txtCustomFooter = new TextBox();
            _txtCustomFooter.Multiline = true;
            _txtCustomFooter.ScrollBars = ScrollBars.Vertical;
            UiStyle.StyleInput(_txtCustomFooter);
            _txtCustomFooter.Dock = DockStyle.Fill;
            _txtCustomFooter.Margin = new Padding(0, 0, 0, Theme.Sp8);
            inner.Controls.Add(_txtCustomFooter, 0, 8);

            TableLayoutPanel buttons = NewGrid(3, 1, Theme.Surface);
            AddColumns(buttons, new float[] { 168f, 168f, 0f });
            AddRows(buttons, new float[] { 0f });

            _btnSaveWeCom = MakeButton("保存通知参数", UiButtonKind.Primary, new Padding(0, 8, 10, 8));
            _btnSaveWeCom.Click += btnSaveWeCom_Click;
            buttons.Controls.Add(_btnSaveWeCom, 0, 0);

            _btnTestWeCom = MakeButton("发送测试通知包", UiButtonKind.Secondary, new Padding(0, 8, 10, 8));
            _btnTestWeCom.Click += btnTestWeCom_Click;
            buttons.Controls.Add(_btnTestWeCom, 1, 0);
            inner.Controls.Add(buttons, 0, 9);

            card.Controls.Add(inner);
            page.Controls.Add(card, 0, 0);

            Label lblNote = UiStyle.CreateLabel(
                "提示：Webhook 机器人适合群通知；App 模式可指定接收人，需要企业微信后台的自建应用凭证。",
                Theme.FontSmall, Theme.TextMuted, Theme.Bg);
            lblNote.Dock = DockStyle.Fill;
            lblNote.Margin = new Padding(Theme.Sp4, Theme.Sp4, 0, 0);
            page.Controls.Add(lblNote, 0, 1);

            return page;
        }

        private static Label BuildFieldLabel(string caption)
        {
            Label label = UiStyle.CreateLabel(caption, Theme.FontBody, Theme.TextSecondary, Theme.Surface);
            label.Dock = DockStyle.Fill;
            label.Margin = new Padding(0);
            return label;
        }

        private static TextBox BuildFieldBox(TextBox box, int rightMargin)
        {
            UiStyle.StyleInput(box);
            box.Dock = DockStyle.Fill;
            box.Margin = new Padding(0, 6, rightMargin, 6);
            return box;
        }

        private static TableLayoutPanel BuildFieldRow(string caption, TextBox box, float captionWidth, float valueWidth)
        {
            TableLayoutPanel row = NewGrid(2, 1, Theme.Surface);
            AddColumns(row, new float[] { captionWidth, valueWidth });
            AddRows(row, new float[] { 0f });
            row.Controls.Add(BuildFieldLabel(caption), 0, 0);
            row.Controls.Add(BuildFieldBox(box, 0), 1, 0);
            return row;
        }

        private void BindNotifyPage()
        {
            WeComConfig wecom = _config.WeCom;
            if (wecom == null)
            {
                wecom = new WeComConfig();
                _config.WeCom = wecom;
            }

            _chkWeComEnabled.Checked = wecom.Enabled;
            _cmbWeComMode.SelectedIndex = string.Equals(wecom.Mode, "App", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            _txtWebhookUrl.Text = wecom.WebhookUrl == null ? "" : wecom.WebhookUrl;
            _txtCorpId.Text = wecom.AppCorpId == null ? "" : wecom.AppCorpId;
            _txtCorpSecret.Text = wecom.AppCorpSecret == null ? "" : wecom.AppCorpSecret;
            _txtAgentId.Text = wecom.AppAgentId <= 0 ? "" : wecom.AppAgentId.ToString();
            _txtToUser.Text = wecom.AppToUser == null ? "" : wecom.AppToUser;
            _txtCustomHeader.Text = wecom.CustomHeader == null ? "" : wecom.CustomHeader;
            _txtCustomFooter.Text = wecom.CustomFooter == null ? "" : wecom.CustomFooter;

            ToggleWeComInputs(_chkWeComEnabled.Checked, SelectedWeComMode());
        }

        private string SelectedWeComMode()
        {
            return _cmbWeComMode.SelectedIndex == 1 ? "App" : "Webhook";
        }

        private void ToggleWeComInputs(bool enabled, string mode)
        {
            bool webhook = enabled && !string.Equals(mode, "App", StringComparison.OrdinalIgnoreCase);
            bool app = enabled && string.Equals(mode, "App", StringComparison.OrdinalIgnoreCase);

            _cmbWeComMode.Enabled = enabled;
            _txtCustomHeader.Enabled = enabled;
            _txtCustomFooter.Enabled = enabled;

            _txtWebhookUrl.Enabled = webhook;
            _txtCorpId.Enabled = app;
            _txtCorpSecret.Enabled = app;
            _txtAgentId.Enabled = app;
            _txtToUser.Enabled = app;

            _btnTestWeCom.Enabled = enabled;
        }

        private void chkWeComEnabled_CheckedChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            ToggleWeComInputs(_chkWeComEnabled.Checked, SelectedWeComMode());
            SetStatus(_chkWeComEnabled.Checked
                ? "已开启企业微信通知：同步结束后将推送成功 / 失败设备清单。"
                : "已关闭企业微信通知。", LogLevel.Info);
        }

        private void cmbWeComMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            ToggleWeComInputs(_chkWeComEnabled.Checked, SelectedWeComMode());
        }

        private WeComConfig SnapshotWeComConfig()
        {
            WeComConfig wecom = new WeComConfig();
            wecom.Enabled = _chkWeComEnabled.Checked;
            wecom.Mode = SelectedWeComMode();
            wecom.WebhookUrl = _txtWebhookUrl.Text.Trim();
            wecom.AppCorpId = _txtCorpId.Text.Trim();
            wecom.AppCorpSecret = _txtCorpSecret.Text.Trim();

            int agentId;
            wecom.AppAgentId = int.TryParse(_txtAgentId.Text.Trim(), out agentId) ? agentId : 0;

            wecom.AppToUser = _txtToUser.Text.Trim();
            wecom.CustomHeader = _txtCustomHeader.Text;
            wecom.CustomFooter = _txtCustomFooter.Text;
            return wecom;
        }

        /// <summary>返回 null 表示校验通过，否则返回给用户看的错误说明</summary>
        private static string ValidateWeCom(WeComConfig wecom)
        {
            if (!wecom.Enabled) return null;

            if (string.Equals(wecom.Mode, "App", StringComparison.OrdinalIgnoreCase))
            {
                if (wecom.AppCorpId.Length == 0) return "App 模式必须填写企业 ID (CorpID)。";
                if (wecom.AppCorpSecret.Length == 0) return "App 模式必须填写应用 Secret。";
                if (wecom.AppAgentId <= 0) return "App 模式必须填写正确的应用 AgentID（正整数）。";
                return null;
            }

            if (wecom.WebhookUrl.Length == 0) return "Webhook 模式必须填写机器人 Webhook 链接。";
            return null;
        }

        private void btnSaveWeCom_Click(object sender, EventArgs e)
        {
            if (_config == null) return;

            WeComConfig wecom = SnapshotWeComConfig();
            string problem = ValidateWeCom(wecom);
            if (problem != null)
            {
                MessageBox.Show(problem, "参数不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _config.WeCom = wecom;
            ConfigManager.Save(_config);
            ToggleWeComInputs(wecom.Enabled, wecom.Mode);

            SetStatus("企业微信通知参数已保存。", LogLevel.Success);
            MessageBox.Show("企业微信通知参数已保存。", "保存成功",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnTestWeCom_Click(object sender, EventArgs e)
        {
            if (_config == null) return;

            WeComConfig wecom = SnapshotWeComConfig();
            if (!wecom.Enabled)
            {
                MessageBox.Show("请先勾选「开启企业微信实时同步报告通知」。", "通知未开启",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string problem = ValidateWeCom(wecom);
            if (problem != null)
            {
                MessageBox.Show(problem, "参数不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnTestWeCom.Enabled = false;
            SetStatus("正在发送企业微信测试通知…", LogLevel.Info);

            ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok = false;
                string failure = null;
                try
                {
                    ok = WeComNotifier.SendSyncReport(
                        wecom,
                        3, 2, 1,
                        "192.168.1.10  考勤机-前台\n192.168.1.11  门禁-侧门",
                        "192.168.1.12  考勤机-车间（连接超时）");
                }
                catch (Exception ex)
                {
                    failure = ex.Message;
                }

                UiBeginInvoke(delegate
                {
                    if (_btnTestWeCom != null) _btnTestWeCom.Enabled = _chkWeComEnabled.Checked;

                    if (failure != null)
                    {
                        SetStatus("测试通知发送失败：" + failure, LogLevel.Error);
                        MessageBox.Show("发送测试通知时出现异常：\r\n\r\n" + failure, "发送失败",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (ok)
                    {
                        SetStatus("测试通知已发送，请在企业微信中确认。", LogLevel.Success);
                        MessageBox.Show("测试通知已发送成功，请在企业微信中确认收到的内容格式。",
                            "发送成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        SetStatus("测试通知发送失败，请检查参数与网络。", LogLevel.Error);
                        MessageBox.Show("企业微信接口返回失败，请检查 Webhook / 应用凭证是否正确，以及服务器能否访问外网。\r\n\r\n详细原因见运行日志。",
                            "发送失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                });
            });
        }
    }
}
