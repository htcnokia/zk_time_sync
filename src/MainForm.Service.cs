using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ZkTimeSync
{
    /// <summary>
    /// “系统服务”卡片：以图形界面完成服务的安装 / 启动 / 停止 / 卸载 / COM 注册，
    /// 并实时显示服务运行状态。所有需要管理员权限的动作统一走 ServiceInstallerHelper
    /// 的 UAC 提权通道，执行结果回显在卡片内，不依赖命令行脚本。
    /// </summary>
    public partial class MainForm
    {
        private Label _lblServiceState;
        private Label _lblServiceDetail;
        private UiButton _btnInstallService;
        private UiButton _btnStartService;
        private UiButton _btnStopService;
        private UiButton _btnUninstallService;
        private UiButton _btnRegisterCom;
        private UiButton _btnRefreshService;

        // 服务操作在后台线程执行，避免 UAC 等待期间冻结界面
        private volatile bool _serviceBusy;

        private Control BuildServiceCard()
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0);
            card.Title = "系统服务（无人值守模式）";
            card.Subtitle = "安装后以 LocalSystem 运行，断电重启无需登录即自动对时";

            TableLayoutPanel inner = NewGrid(1, 6, Theme.Surface);
            AddRows(inner, new float[] { 62f, 40f, 40f, 40f, 0f, 30f });

            // 第一行：大号状态指示
            Panel statePanel = new Panel();
            statePanel.Dock = DockStyle.Fill;
            statePanel.Margin = new Padding(0, 0, 0, Theme.Sp4);
            statePanel.BackColor = Theme.Surface;
            statePanel.Paint += delegate(object s, PaintEventArgs e)
            {
                Theme.FillRound(e.Graphics,
                    new Rectangle(0, 4, statePanel.Width - 1, statePanel.Height - 9),
                    Theme.RadiusMd, Theme.PrimarySoft);
            };

            _lblServiceState = UiStyle.CreateLabel("检测中…", Theme.FontTitle, Theme.TextPrimary, Theme.PrimarySoft);
            _lblServiceState.SetBounds(Theme.Sp16, 10, 280, 24);
            statePanel.Controls.Add(_lblServiceState);

            _lblServiceDetail = UiStyle.CreateLabel("正在读取服务状态", Theme.FontSmall, Theme.TextSecondary, Theme.PrimarySoft);
            _lblServiceDetail.SetBounds(Theme.Sp16, 34, statePanel.Width - Theme.Sp32, 18);
            statePanel.Controls.Add(_lblServiceDetail);
            inner.Controls.Add(statePanel, 0, 0);

            // 第二行：主操作 —— 安装成系统服务（一键安装 + 启动 + 检测）
            _btnInstallService = MakeButton("安装成系统服务（自动提权）", UiButtonKind.Primary, new Padding(0, 4, 0, 4));
            _btnInstallService.Click += btnInstallService_Click;
            inner.Controls.Add(_btnInstallService, 0, 1);

            // 第三行：启动 / 停止
            TableLayoutPanel runRow = NewGrid(2, 1, Theme.Surface);
            AddColumns(runRow, new float[] { 0f, 0f });
            AddRows(runRow, new float[] { 0f });
            _btnStartService = MakeButton("启动服务", UiButtonKind.Secondary, new Padding(0, 4, Theme.Sp8, 4));
            _btnStartService.Click += btnStartService_Click;
            runRow.Controls.Add(_btnStartService, 0, 0);
            _btnStopService = MakeButton("停止服务", UiButtonKind.Secondary, new Padding(Theme.Sp8, 4, 0, 4));
            _btnStopService.Click += btnStopService_Click;
            runRow.Controls.Add(_btnStopService, 1, 0);
            inner.Controls.Add(runRow, 0, 2);

            // 第四行：注册 COM / 卸载
            TableLayoutPanel advRow = NewGrid(2, 1, Theme.Surface);
            AddColumns(advRow, new float[] { 0f, 0f });
            AddRows(advRow, new float[] { 0f });
            _btnRegisterCom = MakeButton("注册 SDK 组件", UiButtonKind.Secondary, new Padding(0, 4, Theme.Sp8, 4));
            _btnRegisterCom.Click += btnRegisterCom_Click;
            advRow.Controls.Add(_btnRegisterCom, 0, 0);
            _btnUninstallService = MakeButton("卸载服务", UiButtonKind.Danger, new Padding(Theme.Sp8, 4, 0, 4));
            _btnUninstallService.Click += btnUninstallService_Click;
            advRow.Controls.Add(_btnUninstallService, 1, 0);
            inner.Controls.Add(advRow, 0, 3);

            // 第五行：结果输出区（可滚动只读文本框）
            _txtServiceResult = new TextBox();
            UiStyle.StyleInput(_txtServiceResult);
            _txtServiceResult.Multiline = true;
            _txtServiceResult.ReadOnly = true;
            _txtServiceResult.ScrollBars = ScrollBars.Vertical;
            _txtServiceResult.Dock = DockStyle.Fill;
            _txtServiceResult.Margin = new Padding(0, Theme.Sp4, 0, 0);
            _txtServiceResult.Text = "点击上方按钮后，操作结果会显示在这里。";
            inner.Controls.Add(_txtServiceResult, 0, 4);

            // 第六行：刷新状态链接式按钮
            _btnRefreshService = new UiButton();
            _btnRefreshService.Kind = UiButtonKind.Ghost;
            _btnRefreshService.Text = "刷新状态";
            _btnRefreshService.Dock = DockStyle.Right;
            _btnRefreshService.Margin = new Padding(0);
            _btnRefreshService.Click += btnRefreshService_Click;
            inner.Controls.Add(_btnRefreshService, 0, 5);

            card.Controls.Add(inner);
            return card;
        }

        private TextBox _txtServiceResult;

        /// <summary>把服务状态枚举映射为 Fluent 色板与文案</summary>
        private void RefreshServiceState()
        {
            if (_lblServiceState == null) return;

            ServiceInstallerHelper.ServiceState state = ServiceInstallerHelper.GetServiceState();
            string text = ServiceInstallerHelper.StateText(state);
            _lblServiceState.Text = text;

            Color color;
            switch (state)
            {
                case ServiceInstallerHelper.ServiceState.Running:
                    color = Theme.Success;
                    _lblServiceDetail.Text = "服务正在运行；服务器重启（无需登录）后会自动对时。";
                    break;
                case ServiceInstallerHelper.ServiceState.Stopped:
                    color = Theme.Warning;
                    _lblServiceDetail.Text = "服务已安装但当前未运行，可点击“启动服务”。";
                    break;
                case ServiceInstallerHelper.ServiceState.NotInstalled:
                    color = Theme.TextMuted;
                    _lblServiceDetail.Text = "尚未安装系统服务，点击上方主按钮一键安装。";
                    break;
                default:
                    color = Theme.Warning;
                    _lblServiceDetail.Text = "服务状态无法获取，请检查权限或服务管理器。";
                    break;
            }
            _lblServiceState.ForeColor = color;
        }

        /// <summary>统一的动作执行入口：后台线程执行 + 结果回显 + 状态刷新</summary>
        private void RunServiceAction(string action, string title)
        {
            if (_serviceBusy)
            {
                MessageBox.Show(this, "上一个服务操作尚未完成，请稍候。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _serviceBusy = true;
            SetServiceButtonsEnabled(false);
            _txtServiceResult.Text = "正在执行：" + title + "\r\n如需授权，请在弹出的用户账户控制窗口中点击“是”。";
            Logger.Info("[服务管理] " + title);

            Thread worker = new Thread(delegate()
            {
                string result = "";
                try { result = ServiceInstallerHelper.ExecuteWithElevation(action); }
                catch (Exception ex) { result = "执行异常：" + ex.Message; }

                // 结果统一回传 UI 线程，避免跨线程访问控件；窗体已关闭时静默跳过
                try
                {
                    BeginInvoke(new Action(delegate()
                    {
                        _txtServiceResult.Text = result;
                        RefreshServiceState();
                        SetServiceButtonsEnabled(true);
                        _serviceBusy = false;
                        Logger.Info("[服务管理] " + title + " 完成：" + result.Replace("\n", " | "));
                    }));
                }
                catch (Exception) { }
            });
            worker.IsBackground = true;
            worker.Name = "ZkServiceActionWorker";
            worker.Start();
        }

        private void SetServiceButtonsEnabled(bool enabled)
        {
            _btnInstallService.Enabled = enabled;
            _btnStartService.Enabled = enabled;
            _btnStopService.Enabled = enabled;
            _btnUninstallService.Enabled = enabled;
            _btnRegisterCom.Enabled = enabled;
            _btnRefreshService.Enabled = enabled;
        }

        private void btnInstallService_Click(object sender, EventArgs e)
        {
            RunServiceAction("install", "安装成系统服务");
        }

        private void btnStartService_Click(object sender, EventArgs e)
        {
            RunServiceAction("start", "启动服务");
        }

        private void btnStopService_Click(object sender, EventArgs e)
        {
            RunServiceAction("stop", "停止服务");
        }

        private void btnUninstallService_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this,
                    "卸载后，断电重启将不再自动对时。确认卸载系统服务？",
                    "确认卸载", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            RunServiceAction("uninstall", "卸载系统服务");
        }

        private void btnRegisterCom_Click(object sender, EventArgs e)
        {
            RunServiceAction("regcom", "注册中控 SDK COM 组件");
        }

        private void btnRefreshService_Click(object sender, EventArgs e)
        {
            RefreshServiceState();
        }
    }
}
