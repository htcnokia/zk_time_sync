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
                else if (cmd.StartsWith(ServiceInstallerHelper.ElevatedActionPrefix) && args.Length >= 2)
                {
                    // GUI 提权子进程：由父进程以 runas 启动，执行后写回结果文件并退出
                    string action = cmd.Substring(ServiceInstallerHelper.ElevatedActionPrefix.Length);
                    ServiceInstallerHelper.RunElevatedChild(action, args[1]);
                    return;
                }
                else if (cmd == "install" || cmd == "i")
                {
                    Console.WriteLine(ServiceInstallerHelper.InstallService());
                    return;
                }
                else if (cmd == "uninstall" || cmd == "u")
                {
                    Console.WriteLine(ServiceInstallerHelper.UninstallService());
                    return;
                }
                else if (cmd == "start")
                {
                    Console.WriteLine(ServiceInstallerHelper.StartService());
                    return;
                }
                else if (cmd == "stop")
                {
                    Console.WriteLine(ServiceInstallerHelper.StopService());
                    return;
                }
                else if (cmd == "regcom")
                {
                    Console.WriteLine(ServiceInstallerHelper.RegisterCom());
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
            return RunProcess("sc.exe", arguments, 15000);
        }

        /// <summary>同步执行指定程序并返回其标准输出/错误（含退出码）</summary>
        private static string RunProcess(string fileName, string arguments, int timeoutMs)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = fileName;
                psi.Arguments = arguments;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;

                using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    string error = p.StandardError.ReadToEnd();
                    p.WaitForExit(timeoutMs);
                    string combined = (output + " " + error).Trim();
                    return combined + " [exit=" + p.ExitCode + "]";
                }
            }
            catch (Exception ex)
            {
                return "执行 " + fileName + " 失败: " + ex.Message;
            }
        }

        /// <summary>读取服务当前状态文本（供脚本/界面判断）</summary>
        public static string QueryStatus()
        {
            return RunSc("query " + ServiceName);
        }

        /// <summary>返回服务运行状态枚举，供界面图标/文案使用</summary>
        public enum ServiceState
        {
            NotInstalled,
            Running,
            Stopped,
            Unknown
        }

        /// <summary>用 ServiceController 精确读取状态，不依赖 sc.exe 文本解析</summary>
        public static ServiceState GetServiceState()
        {
            try
            {
                using (ServiceController sc = new ServiceController(ServiceName))
                {
                    switch (sc.Status)
                    {
                        case ServiceControllerStatus.Running: return ServiceState.Running;
                        case ServiceControllerStatus.Stopped: return ServiceState.Stopped;
                        default: return ServiceState.Unknown;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // 服务不存在时 ServiceController 构造或访问 Status 抛此异常
                return ServiceState.NotInstalled;
            }
            catch (Exception)
            {
                return ServiceState.Unknown;
            }
        }

        /// <summary>当前进程是否具备管理员权限</summary>
        public static bool IsAdministrator()
        {
            try
            {
                System.Security.Principal.WindowsIdentity identity =
                    System.Security.Principal.WindowsIdentity.GetCurrent();
                System.Security.Principal.WindowsPrincipal principal =
                    new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>安装服务，返回结果文本（供命令行与 GUI 提权共用）</summary>
        public static string InstallService()
        {
            System.Text.StringBuilder log = new System.Text.StringBuilder();
            string exePath = Application.ExecutablePath;
            log.AppendLine("正在安装/修复系统服务 " + ServiceName + " ...");

            // 1) 若服务已存在先停止并删除，保证安装幂等（重复安装不会残留旧 binPath）
            string q = RunSc("query " + ServiceName);
            if (q.IndexOf("STATE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                q.IndexOf("SERVICE_NAME", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                log.AppendLine("检测到已存在的服务，先停止并删除...");
                log.AppendLine("  " + RunSc("stop " + ServiceName));
                log.AppendLine("  " + RunSc("delete " + ServiceName));
            }

            // 2) 创建服务：LocalSystem + 自动启动 + 命令行 -service 入口
            string create = string.Format(
                "create {0} binPath= \"\\\"{1}\\\" -service\" start= auto obj= LocalSystem DisplayName= \"{2}\"",
                ServiceName, exePath, DisplayName);
            log.AppendLine("  " + RunSc(create));

            // 3) 延迟自动启动：等网络与磁盘就绪后再拉起，降低断电重启后抢跑概率
            log.AppendLine("  " + RunSc("config " + ServiceName + " start= delayed-auto"));

            // 4) 网络依赖：Tcpip + DNS 客户端就绪后才启动
            log.AppendLine("  " + RunSc("config " + ServiceName + " depend= Tcpip/Dnscache"));

            // 5) 失败自动恢复：异常退出后自动重启，避免一次崩溃后再也不对时
            log.AppendLine("  " + RunSc("failure " + ServiceName +
                " reset= 86400 actions= restart/60000/restart/60000/restart/300000"));

            // 6) 描述
            log.AppendLine("  " + RunSc("description " + ServiceName +
                " \"中控考勤/门禁时间同步服务；断电重启后无需登录自动对时。\""));

            // 7) 一次提权内完成安装 + 启动 + 检测，避免二次 UAC 弹窗
            ServiceState state = GetServiceState();
            if (state == ServiceState.Stopped)
            {
                log.AppendLine("正在启动服务...");
                log.AppendLine("  " + RunSc("start " + ServiceName));
                state = GetServiceState();
            }
            log.AppendLine("安装命令执行完毕。当前状态：" + StateText(state));
            log.AppendLine(state == ServiceState.Running
                ? "服务已在运行，断电重启后将自动对时。"
                : "服务尚未运行，可执行 -start 启动，或重启服务器后自动运行。");
            return log.ToString();
        }

        /// <summary>卸载服务，返回结果文本</summary>
        public static string UninstallService()
        {
            System.Text.StringBuilder log = new System.Text.StringBuilder();
            log.AppendLine("正在停止并卸载系统服务 " + ServiceName + " ...");
            log.AppendLine("  " + RunSc("stop " + ServiceName));
            log.AppendLine("  " + RunSc("delete " + ServiceName));
            log.AppendLine("卸载命令执行完毕。当前状态：" + StateText(GetServiceState()));
            return log.ToString();
        }

        /// <summary>启动服务，返回结果文本</summary>
        public static string StartService()
        {
            return "启动服务：\n  " + RunSc("start " + ServiceName) +
                   "\n当前状态：" + StateText(GetServiceState());
        }

        /// <summary>停止服务，返回结果文本</summary>
        public static string StopService()
        {
            return "停止服务：\n  " + RunSc("stop " + ServiceName) +
                   "\n当前状态：" + StateText(GetServiceState());
        }

        /// <summary>机器级注册 32 位 zkemkeeper COM 组件（LocalSystem 服务必需）</summary>
        public static string RegisterCom()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dll = Path.GetFullPath(Path.Combine(baseDir, "..", "sdk_x86", "zkemkeeper.dll"));
            if (!File.Exists(dll))
                return "注册失败：未找到组件 " + dll;

            string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string regsvr32 = Path.Combine(sysDir, "regsvr32.exe");
            // 64 位系统必须用 SysWOW64 下的 32 位 regsvr32 注册 32 位控件
            string wow64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "SysWOW64", "regsvr32.exe");
            if (File.Exists(wow64)) regsvr32 = wow64;

            string result = RunProcess("\"" + regsvr32 + "\"", "/s \"" + dll + "\"", 30000);
            return "机器级注册中控 SDK COM 组件：\n  " + dll + "\n  " + result +
                   "\n（/s 静默注册；若失败请确认已以管理员身份运行）";
        }

        public static string StateText(ServiceState state)
        {
            switch (state)
            {
                case ServiceState.Running: return "运行中";
                case ServiceState.Stopped: return "已停止";
                case ServiceState.NotInstalled: return "未安装";
                default: return "未知";
            }
        }

        // ==================== GUI 提权执行通道 ====================

        /// <summary>提权动作结果文件的命令行参数前缀</summary>
        public const string ElevatedActionPrefix = "elevated-";

        /// <summary>
        /// 提权执行指定动作。若当前非管理员，以 runas 动词拉起本程序的高权限副本执行，
        /// 子进程把结果写入 resultFile，父进程等待并读取后返回给界面显示。
        /// 已具备管理员权限时直接就地执行。
        /// </summary>
        public static string ExecuteWithElevation(string action)
        {
            if (IsAdministrator())
                return ExecuteAction(action);

            string resultFile = Path.Combine(Path.GetTempPath(),
                "zktimesync_elevated_" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                if (File.Exists(resultFile)) File.Delete(resultFile);

                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.Arguments = "-" + ElevatedActionPrefix + action + " \"" + resultFile + "\"";
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;

                System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi);
                if (!p.WaitForExit(60000))
                {
                    try { p.Kill(); } catch (Exception) { }
                    return "提权操作超时（60 秒）。请检查是否弹出了用户账户控制窗口。";
                }

                if (!File.Exists(resultFile))
                    return "提权进程未写回结果。可能取消了用户账户控制授权，或被安全软件拦截。";

                string result = File.ReadAllText(resultFile);
                File.Delete(resultFile);
                return result;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // 错误码 1223 = 用户取消了 UAC 提示
                if (ex.NativeErrorCode == 1223)
                    return "已取消授权。安装/卸载服务需要管理员权限，操作未执行。";
                return "提权失败：" + ex.Message;
            }
            catch (Exception ex)
            {
                return "提权执行失败：" + ex.Message;
            }
        }

        /// <summary>按动作名执行，返回结果文本（提权子进程与就地调用共用）</summary>
        public static string ExecuteAction(string action)
        {
            switch (action)
            {
                case "install": return InstallService();
                case "uninstall": return UninstallService();
                case "start": return StartService();
                case "stop": return StopService();
                case "regcom": return RegisterCom();
                default: return "未知操作：" + action;
            }
        }

        /// <summary>提权子进程入口：执行动作并把结果写入父进程指定的文件</summary>
        public static void RunElevatedChild(string action, string resultFile)
        {
            string result;
            try { result = ExecuteAction(action); }
            catch (Exception ex) { result = "操作执行异常：" + ex.Message; }

            try
            {
                if (!string.IsNullOrEmpty(resultFile))
                    File.WriteAllText(resultFile, result, System.Text.Encoding.UTF8);
            }
            catch (Exception) { }
        }
    }
}
