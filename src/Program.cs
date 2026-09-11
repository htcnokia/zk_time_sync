using System;
using System.IO;
using System.ServiceProcess;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ZkTimeSync
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // 检查命令行参数：是否作为 Windows 服务运行
            if (args != null && args.Length > 0)
            {
                string cmd = args[0].ToLower().TrimStart('-', '/');

                if (cmd == "service" || cmd == "s")
                {
                    // 作为 Windows 服务运行
                    ServiceBase[] ServicesToRun = new ServiceBase[]
                    {
                        new ZkWindowsService()
                    };
                    ServiceBase.Run(ServicesToRun);
                    return;
                }
                else if (cmd == "install" || cmd == "i")
                {
                    ServiceInstallerHelper.InstallService();
                    return;
                }
                else if (cmd == "uninstall" || cmd == "u")
                {
                    ServiceInstallerHelper.UninstallService();
                    return;
                }
                else if (cmd == "sync")
                {
                    // 纯命令行静默单次同步
                    var cfg = ConfigManager.Load();
                    SyncEngine.ExecuteSync(cfg);
                    return;
                }
            }

            // 检查环境与是否以服务身份非交互启动
            if (!Environment.UserInteractive)
            {
                ServiceBase[] ServicesToRun = new ServiceBase[]
                {
                    new ZkWindowsService()
                };
                ServiceBase.Run(ServicesToRun);
                return;
            }

            // 默认正常桌面 GUI 启动
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    /// <summary>
    /// Windows 服务实现
    /// </summary>
    public class ZkWindowsService : ServiceBase
    {
        private TaskSchedulerService _scheduler;

        public ZkWindowsService()
        {
            this.ServiceName = "ZkTimeSyncService";
            this.CanStop = true;
            this.CanShutdown = true;
        }

        protected override void OnStart(string[] args)
        {
            Logger.Info("中控时间同步 Windows 服务正在启动...");
            var config = ConfigManager.Load();
            _scheduler = new TaskSchedulerService(config);
            _scheduler.Start();
            Logger.Info("中控时间同步 Windows 服务启动完成。");
        }

        protected override void OnStop()
        {
            Logger.Info("中控时间同步 Windows 服务正在停止...");
            if (_scheduler != null)
            {
                _scheduler.Stop();
            }
            Logger.Info("中控时间同步 Windows 服务已停止。");
        }
    }

    /// <summary>
    /// 开机自启管理（Windows 注册表 CurrentUser/Run）
    /// </summary>
    public static class AutoStartManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "ZkTimeSyncAuto";

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue(AppName);
                        return val != null;
                    }
                }
            }
            catch { }
            return false;
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string exePath = Application.ExecutablePath;
                            key.SetValue(AppName, "\"" + exePath + "\"");
                        }
                        else
                        {
                            key.DeleteValue(AppName, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("修改开机自启注册表失败: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// 简易服务安装器（通过 sc.exe 命令）
    /// </summary>
    public static class ServiceInstallerHelper
    {
        public static void InstallService()
        {
            string exePath = Application.ExecutablePath;
            string cmd = string.Format("create ZkTimeSyncService binPath= \"\\\"{0}\\\" -service\" start= auto DisplayName= \"ZKTeco Device Time Sync Service\"", exePath);
            System.Diagnostics.Process.Start("sc.exe", cmd);
            Console.WriteLine("服务安装命令已发送。启动服务请执行: sc start ZkTimeSyncService");
        }

        public static void UninstallService()
        {
            System.Diagnostics.Process.Start("sc.exe", "stop ZkTimeSyncService");
            System.Diagnostics.Process.Start("sc.exe", "delete ZkTimeSyncService");
            Console.WriteLine("服务卸载命令已发送。");
        }
    }
}
