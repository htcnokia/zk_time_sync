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
                else if (cmd == "status" || cmd == "st")
                {
                    Console.WriteLine(ServiceInstallerHelper.QueryStatus());
                    return;
                }
                else if (cmd == "sync")
                {
                    // 纯命令行静默单次同步（同步引擎内部已保证 STA 单元）
                    System.IO.Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
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
            this.ServiceName = ServiceInstallerHelper.ServiceName;
            this.CanStop = true;
            this.CanShutdown = true;
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                // Windows 服务启动时工作目录固定为 %System32%，显式切到程序目录，
                // 保证 config.json / logs 的相对定位与 COM 组件查找行为一致。
                System.IO.Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            }
            catch (Exception ex)
            {
                Logger.Warn("设置服务工作目录失败（不影响启动）: " + ex.Message);
            }

            try
            {
                Logger.Info("========== 中控时间同步 Windows 服务启动 (LocalSystem, 无需登录) ==========");
                var config = ConfigManager.Load();
                _scheduler = new TaskSchedulerService(config);
                _scheduler.Start();
                Logger.Info("中控时间同步 Windows 服务启动完成，调度器已就绪。");
            }
            catch (Exception ex)
            {
                // OnStart 抛出未处理异常会被 SCM 视为启动失败；记录完整信息便于排查
                Logger.Error("Windows 服务启动失败: " + ex);
                throw;
            }
        }

        protected override void OnStop()
        {
            Logger.Info("中控时间同步 Windows 服务正在停止...");
            if (_scheduler != null)
            {
                _scheduler.Stop();
                _scheduler = null;
            }
            Logger.Info("中控时间同步 Windows 服务已停止。");
        }

        protected override void OnShutdown()
        {
            // 系统关机/断电时的快速收尾，与 OnStop 共用停止逻辑
            Logger.Info("检测到系统关机，Windows 服务执行收尾...");
            if (_scheduler != null)
            {
                _scheduler.Stop();
                _scheduler = null;
            }
            Logger.Info("Windows 服务关机收尾完成。");
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
    /// Windows 服务安装器（同步执行 sc.exe，逐条校验结果）。
    ///
    /// 面向 Windows Server 无用户登录场景的关键配置：
    ///   - 账户 LocalSystem：不依赖任何用户登录，开机即可运行；
    ///   - start= delayed-auto：延迟自动启动，等网络栈就绪后再拉起服务；
    ///   - depend= Tcpip/Dnscache：显式声明网络依赖，避免断电重启后网络未就绪时抢跑；
    ///   - failure 动作：服务进程异常退出后自动重启（首次/二次 60 秒，后续 5 分钟）。
    /// </summary>
    public static class ServiceInstallerHelper
    {
        public const string ServiceName = "ZkTimeSyncService";
        private const string DisplayName = "ZKTeco Device Time Sync Service";

        /// <summary>同步执行 sc.exe 并返回其标准输出/错误（含退出码）</summary>
        private static string RunSc(string arguments)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = "sc.exe";
                psi.Arguments = arguments;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;

                using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    string error = p.StandardError.ReadToEnd();
                    p.WaitForExit(15000);
                    string combined = (output + " " + error).Trim();
                    return combined + " [exit=" + p.ExitCode + "]";
                }
            }
            catch (Exception ex)
            {
                return "执行 sc.exe 失败: " + ex.Message;
            }
        }

        /// <summary>读取服务当前状态文本（供脚本/界面判断）</summary>
        public static string QueryStatus()
        {
            return RunSc("query " + ServiceName);
        }

        public static void InstallService()
        {
            string exePath = Application.ExecutablePath;
            Console.WriteLine("正在安装/修复系统服务 " + ServiceName + " ...");

            // 1) 若服务已存在先停止并删除，保证安装幂等（重复安装不会残留旧 binPath）
            string q = RunSc("query " + ServiceName);
            if (q.IndexOf("STATE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                q.IndexOf("SERVICE_NAME", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("检测到已存在的服务，先停止并删除...");
                Console.WriteLine("  " + RunSc("stop " + ServiceName));
                Console.WriteLine("  " + RunSc("delete " + ServiceName));
            }

            // 2) 创建服务：LocalSystem + 自动启动 + 命令行 -service 入口
            string create = string.Format(
                "create {0} binPath= \"\\\"{1}\\\" -service\" start= auto obj= LocalSystem DisplayName= \"{2}\"",
                ServiceName, exePath, DisplayName);
            Console.WriteLine("  " + RunSc(create));

            // 3) 延迟自动启动：等网络与磁盘就绪后再拉起，降低断电重启后抢跑概率
            Console.WriteLine("  " + RunSc("config " + ServiceName + " start= delayed-auto"));

            // 4) 网络依赖：Tcpip + DNS 客户端就绪后才启动
            Console.WriteLine("  " + RunSc("config " + ServiceName + " depend= Tcpip/Dnscache"));

            // 5) 失败自动恢复：异常退出后自动重启，避免一次崩溃后再也不对时
            Console.WriteLine("  " + RunSc("failure " + ServiceName +
                " reset= 86400 actions= restart/60000/restart/60000/restart/300000"));

            // 6) 描述
            Console.WriteLine("  " + RunSc("description " + ServiceName +
                " \"中控考勤/门禁时间同步服务；断电重启后无需登录自动对时。\""));

            Console.WriteLine("服务安装命令已执行完毕。当前状态：");
            Console.WriteLine("  " + QueryStatus());
            Console.WriteLine("如未运行，请执行：net start " + ServiceName);
        }

        public static void UninstallService()
        {
            Console.WriteLine("正在停止并卸载系统服务 " + ServiceName + " ...");
            Console.WriteLine("  " + RunSc("stop " + ServiceName));
            Console.WriteLine("  " + RunSc("delete " + ServiceName));
            Console.WriteLine("服务卸载命令已执行完毕。");
        }
    }
}
