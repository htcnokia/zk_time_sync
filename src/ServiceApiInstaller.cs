using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ZkTimeSync
{
    /// <summary>
    /// 基于 Windows 服务控制管理器 API (advapi32.dll) 的服务安装器。
    ///
    /// 与 sc.exe 命令行方案的本质区别：可执行文件路径作为 Unicode 字符串直接传给
    /// CreateService 的 lpBinaryPathName 参数，不经过命令行引号拼接与代码页转换，
    /// 因此能可靠支持含空格（如 C:\Program Files (x86)）或中文字符的安装路径。
    ///
    /// 一次性完成：LocalSystem 账户 + 自动启动 + 延迟启动 + 网络依赖 + 失败自动重启 + 描述。
    /// </summary>
    internal static class ServiceApiInstaller
    {
        private const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
        private const uint SERVICE_ALL_ACCESS = 0xF01FF;
        private const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
        private const uint SERVICE_AUTO_START = 0x00000002;
        private const uint SERVICE_ERROR_NORMAL = 0x00000001;

        private const uint SERVICE_CONFIG_DESCRIPTION = 1;
        private const uint SERVICE_CONFIG_FAILURE_ACTIONS = 2;
        private const uint SERVICE_CONFIG_DELAYED_AUTO_START_INFO = 3;

        // 依据 winsvc.h: SC_ACTION_NONE=0, SC_ACTION_RESTART=1
        private const uint SC_ACTION_RESTART = 1;
        private const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;

        private const int ERROR_SERVICE_EXISTS = 1073;
        private const int ERROR_SERVICE_MARKED_FOR_DELETE = 1072;
        private const int ERROR_ACCESS_DENIED = 5;

        [StructLayout(LayoutKind.Sequential)]
        private struct SC_ACTION
        {
            public uint Type;
            public uint Delay;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_FAILURE_ACTIONS
        {
            public uint dwResetPeriod;
            public IntPtr lpRebootMsg;
            public IntPtr lpCommand;
            public uint cActions;
            public IntPtr lpsaActions;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_DELAYED_AUTO_START_INFO
        {
            public int fDelayedAutostart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_DESCRIPTION
        {
            public IntPtr lpDescription;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenSCManager(string lpMachineName, string lpDatabaseName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateService(
            IntPtr hSCManager,
            string lpServiceName,
            string lpDisplayName,
            uint dwDesiredAccess,
            uint dwServiceType,
            uint dwStartType,
            uint dwErrorControl,
            string lpBinaryPathName,
            string lpLoadOrderGroup,
            IntPtr lpdwTagId,
            string lpDependencies,
            string lpServiceStartName,
            string lpPassword);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool ChangeServiceConfig2(IntPtr hService, uint dwInfoLevel, IntPtr lpInfo);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ChangeServiceConfig(
            IntPtr hService,
            uint dwServiceType,
            uint dwStartType,
            uint dwErrorControl,
            string lpBinaryPathName,
            string lpLoadOrderGroup,
            IntPtr lpdwTagId,
            string lpDependencies,
            string lpServiceStartName,
            string lpPassword,
            string lpDisplayName);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CloseServiceHandle(IntPtr hSCObject);

        /// <summary>把 Win32 错误码转成可读文案（含中文系统下的本地化消息）</summary>
        private static string ErrorText(int code)
        {
            return code + " - " + new Win32Exception(code).Message;
        }

        /// <summary>
        /// 创建服务。binPath 以 "\"" + exePath + "\" -service" 的形式直接传给 API，
        /// 这是注册表中 ImagePath 的标准格式，SCM 启动时会按此解析带空格的路径。
        /// </summary>
        public static string Create(string serviceName, string displayName, string exePath)
        {
            System.Text.StringBuilder log = new System.Text.StringBuilder();

            IntPtr hManager = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
            if (hManager == IntPtr.Zero)
            {
                return "打开服务控制管理器失败：" + ErrorText(Marshal.GetLastWin32Error()) +
                       "。请确认以管理员身份运行。";
            }

            IntPtr hService = IntPtr.Zero;
            try
            {
                // 路径作为 Unicode 字符串直接传入，无命令行转义、无代码页转换问题
                string binPath = "\"" + exePath + "\" -service";
                // 双 null 结尾的服务依赖链：Tcpip + DNS 客户端
                string dependencies = "Tcpip\0Dnscache\0";

                hService = CreateService(
                    hManager,
                    serviceName,
                    displayName,
                    SERVICE_ALL_ACCESS,
                    SERVICE_WIN32_OWN_PROCESS,
                    SERVICE_AUTO_START,
                    SERVICE_ERROR_NORMAL,
                    binPath,
                    null,
                    IntPtr.Zero,
                    dependencies,
                    "LocalSystem",
                    null);

                if (hService == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err == ERROR_SERVICE_EXISTS || err == ERROR_SERVICE_MARKED_FOR_DELETE)
                        return "服务已存在。请先卸载旧服务，或调用 InstallService 的幂等流程。";
                    if (err == ERROR_ACCESS_DENIED)
                        return "权限不足（错误 5）。请以管理员身份运行。";
                    return "创建服务失败：" + ErrorText(err);
                }

                log.AppendLine("服务创建成功：" + binPath);

                // 延迟自动启动，等网络与磁盘就绪后再拉起
                SERVICE_DELAYED_AUTO_START_INFO delayed = new SERVICE_DELAYED_AUTO_START_INFO();
                delayed.fDelayedAutostart = 1;
                IntPtr pDelayed = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SERVICE_DELAYED_AUTO_START_INFO)));
                try
                {
                    Marshal.StructureToPtr(delayed, pDelayed, false);
                    if (!ChangeServiceConfig2(hService, SERVICE_CONFIG_DELAYED_AUTO_START_INFO, pDelayed))
                        log.AppendLine("警告：设置延迟启动失败：" + ErrorText(Marshal.GetLastWin32Error()));
                    else
                        log.AppendLine("延迟自动启动已启用。");
                }
                finally
                {
                    Marshal.FreeHGlobal(pDelayed);
                }

                // 失败自动恢复：首次/二次 60 秒，后续 5 分钟，24 小时重置
                SC_ACTION[] actions = new SC_ACTION[3];
                actions[0].Type = SC_ACTION_RESTART; actions[0].Delay = 60000;
                actions[1].Type = SC_ACTION_RESTART; actions[1].Delay = 60000;
                actions[2].Type = SC_ACTION_RESTART; actions[2].Delay = 300000;

                int actionSize = Marshal.SizeOf(typeof(SC_ACTION));
                IntPtr pActions = Marshal.AllocHGlobal(actionSize * actions.Length);
                try
                {
                    for (int i = 0; i < actions.Length; i++)
                        Marshal.StructureToPtr(actions[i], new IntPtr(pActions.ToInt64() + (long)i * actionSize), false);

                    SERVICE_FAILURE_ACTIONS failure = new SERVICE_FAILURE_ACTIONS();
                    failure.dwResetPeriod = 86400;
                    failure.lpRebootMsg = IntPtr.Zero;
                    failure.lpCommand = IntPtr.Zero;
                    failure.cActions = (uint)actions.Length;
                    failure.lpsaActions = pActions;

                    IntPtr pFailure = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SERVICE_FAILURE_ACTIONS)));
                    try
                    {
                        Marshal.StructureToPtr(failure, pFailure, false);
                        if (!ChangeServiceConfig2(hService, SERVICE_CONFIG_FAILURE_ACTIONS, pFailure))
                            log.AppendLine("警告：设置失败自动重启失败：" + ErrorText(Marshal.GetLastWin32Error()));
                        else
                            log.AppendLine("失败自动重启已启用（60s / 60s / 300s）。");
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pFailure);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pActions);
                }

                // 服务描述
                SERVICE_DESCRIPTION desc = new SERVICE_DESCRIPTION();
                desc.lpDescription = Marshal.StringToHGlobalUni("中控考勤/门禁时间同步服务；断电重启后无需登录自动对时。");
                IntPtr pDesc = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SERVICE_DESCRIPTION)));
                try
                {
                    Marshal.StructureToPtr(desc, pDesc, false);
                    if (!ChangeServiceConfig2(hService, SERVICE_CONFIG_DESCRIPTION, pDesc))
                        log.AppendLine("警告：设置服务描述失败：" + ErrorText(Marshal.GetLastWin32Error()));
                    else
                        log.AppendLine("服务描述已设置。");
                }
                finally
                {
                    Marshal.FreeHGlobal(pDesc);
                    Marshal.FreeHGlobal(desc.lpDescription);
                }

                log.AppendLine("网络依赖：Tcpip / Dnscache；账户：LocalSystem；启动类型：自动（延迟）。");
                return log.ToString();
            }
            finally
            {
                if (hService != IntPtr.Zero) CloseServiceHandle(hService);
                CloseServiceHandle(hManager);
            }
        }
    }
}
