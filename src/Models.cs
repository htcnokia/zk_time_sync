using System;
using System.Collections.Generic;

namespace ZkTimeSync
{
    public class AppConfig
    {
        public GeneralConfig General { get; set; }
        public CommDefaultsConfig CommDefaults { get; set; }
        public IpSourcesConfig IpSources { get; set; }
        public List<DeviceInfo> Devices { get; set; }
        public WeComConfig WeCom { get; set; }

        public AppConfig()
        {
            General = new GeneralConfig();
            CommDefaults = new CommDefaultsConfig();
            IpSources = new IpSourcesConfig();
            Devices = new List<DeviceInfo>();
            WeCom = new WeComConfig();
        }
    }

    public class GeneralConfig
    {
        public bool AutoStartWithWindows { get; set; }
        public int SyncIntervalMinutes { get; set; }
        public string CronExpression { get; set; }
        public bool UseCron { get; set; }
        /// <summary>程序启动/服务器重启后自动执行一次全员对时（防止断电恢复后错过定时同步）</summary>
        public bool SyncOnStartup { get; set; }
        /// <summary>启动后延迟多少秒再执行首次同步（等待网络/数据库就绪）</summary>
        public int StartupSyncDelaySeconds { get; set; }
        /// <summary>启动同步防重复保护：若最近一次同步距今不足 N 分钟则跳过（0 表示不检查）</summary>
        public int StartupSyncGuardMinutes { get; set; }
        /// <summary>
        /// 冷启动判定窗口（分钟）：系统开机至今在该窗口内，视为断电/服务器重启恢复，
        /// 启动同步强制不受防重复保护限制。以系统开机时长为准，不受时钟回退影响。
        /// </summary>
        public int BootSyncWindowMinutes { get; set; }

        public GeneralConfig()
        {
            AutoStartWithWindows = false;
            SyncIntervalMinutes = 60;
            CronExpression = "0 0 2 * * ?";
            UseCron = true;
            SyncOnStartup = true;
            StartupSyncDelaySeconds = 30;
            StartupSyncGuardMinutes = 5;
            BootSyncWindowMinutes = 2;
        }
    }

    public class CommDefaultsConfig
    {
        public int Port { get; set; }
        public int CommKey { get; set; }
        public int TimeoutMs { get; set; }

        public CommDefaultsConfig()
        {
            Port = 4370;
            CommKey = 0;
            TimeoutMs = 3000;
        }
    }

    public class IpSourcesConfig
    {
        public List<string> CidrRanges { get; set; }
        public MssqlConfig Mssql { get; set; }

        public IpSourcesConfig()
        {
            CidrRanges = new List<string>();
            Mssql = new MssqlConfig();
        }
    }

    public class MssqlConfig
    {
        public bool Enabled { get; set; }
        public string ConnectionString { get; set; }
        public string QuerySql { get; set; }
        /// <summary>自动导入：每次自动同步前先把 SQL 中的设备按 IP 去重合并进记忆设备库</summary>
        public bool AutoImport { get; set; }

        public MssqlConfig()
        {
            Enabled = false;
            ConnectionString = "";
            QuerySql = "SELECT IP AS Ip, Port, CommPassword AS CommKey, MachineAlias AS Name, iana AS IANA FROM Machines WHERE Enabled = 1";
            AutoImport = false;
        }
    }

    public class DeviceInfo
    {
        public string Ip { get; set; }
        public int Port { get; set; }
        public int CommKey { get; set; }
        public string Name { get; set; }
        /// <summary>设备所在地时区（IANA 标准名，如 Asia/Shanghai）；留空表示跟随服务器本机时区</summary>
        public string Iana { get; set; }
        public string LastSyncTime { get; set; }
        public string LastStatus { get; set; }

        public DeviceInfo()
        {
            Port = 4370;
            CommKey = 0;
            Name = "中控设备";
            Iana = "";
            LastStatus = "未同步";
            LastSyncTime = "";
        }
    }

    public class WeComConfig
    {
        public bool Enabled { get; set; }
        public string Mode { get; set; }
        public string WebhookUrl { get; set; }
        public string AppCorpId { get; set; }
        public string AppCorpSecret { get; set; }
        public int AppAgentId { get; set; }
        public string AppToUser { get; set; }
        public string CustomHeader { get; set; }
        public string CustomFooter { get; set; }

        public WeComConfig()
        {
            Enabled = false;
            Mode = "Webhook";
            WebhookUrl = "";
            AppCorpId = "";
            AppCorpSecret = "";
            AppAgentId = 0;
            AppToUser = "@all";
            CustomHeader = "【中控考勤/门禁时间自动同步报告】" + Environment.NewLine + "==============================";
            CustomFooter = "==============================" + Environment.NewLine + "提示：如有个别失败，请检查网络联通性及通讯密码设置。";
        }
    }
}
