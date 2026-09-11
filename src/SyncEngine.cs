using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ZkTimeSync
{
    public class SyncEngine
    {
        private static readonly object _syncLock = new object();
        private static bool _isRunning = false;

        public static bool IsRunning
        {
            get { return _isRunning; }
        }

        public static void ExecuteSync(AppConfig config, Action<string> logCallback = null)
        {
            lock (_syncLock)
            {
                if (_isRunning)
                {
                    Logger.Warn("上一次时间同步任务仍在进行中，本次跳过。");
                    return;
                }
                _isRunning = true;
            }

            try
            {
                Logger.Info("================ 开始执行考勤/门禁时间同步任务 ================");
                if (logCallback != null) logCallback("开始执行同步任务...");

                // 1. 合并设备列表：配置中记忆的设备 + MSSQL 查询出的设备
                var allDevices = new Dictionary<string, DeviceInfo>();
                if (config.Devices != null)
                {
                    foreach (var d in config.Devices)
                    {
                        if (!string.IsNullOrWhiteSpace(d.Ip))
                        {
                            allDevices[d.Ip.Trim()] = d;
                        }
                    }
                }

                // 从 MSSQL 动态加载
                if (config.IpSources != null && config.IpSources.Mssql != null && config.IpSources.Mssql.Enabled)
                {
                    var sqlDevices = DeviceScanner.QueryFromMssql(config.IpSources.Mssql, config.CommDefaults);

                    if (config.IpSources.Mssql.AutoImport)
                    {
                        // 自动导入模式：同步前先把 SQL 设备按 IP 去重合并进记忆设备库
                        // （新增设备、刷新已有设备信息，并随本次同步一起执行）
                        if (config.Devices == null) config.Devices = new List<DeviceInfo>();
                        int[] mergeResult = DeviceScanner.MergeDevicesIntoMemory(config.Devices, sqlDevices);
                        Logger.Info(string.Format("自动导入SQL设备完成：新增 {0} 台，更新 {1} 台", mergeResult[0], mergeResult[1]));

                        foreach (var d in config.Devices)
                        {
                            if (!string.IsNullOrWhiteSpace(d.Ip) && !allDevices.ContainsKey(d.Ip.Trim()))
                            {
                                allDevices[d.Ip.Trim()] = d;
                            }
                        }
                    }
                    else
                    {
                        foreach (var sd in sqlDevices)
                        {
                            if (!allDevices.ContainsKey(sd.Ip))
                            {
                                allDevices[sd.Ip] = sd;
                            }
                        }
                    }
                }

                int total = allDevices.Count;
                int successCount = 0;
                int failCount = 0;

                var successBuilder = new StringBuilder();
                var failBuilder = new StringBuilder();

                foreach (var kv in allDevices)
                {
                    var dev = kv.Value;
                    string targetIp = dev.Ip;
                    int port = dev.Port > 0 ? dev.Port : config.CommDefaults.Port;
                    int commKey = dev.CommKey >= 0 ? dev.CommKey : config.CommDefaults.CommKey;

                    Logger.Info(string.Format("正在同步设备 [{0}] {1}:{2} ...", dev.Name, targetIp, port));

                    bool success = false;
                    string errorMsg = "";

                    using (var zk = new ZkDeviceHelper())
                    {
                        if (zk.Connect(targetIp, port, commKey, config.CommDefaults.TimeoutMs))
                        {
                            if (zk.SyncTime(out errorMsg))
                            {
                                success = true;
                                dev.LastStatus = "同步成功";
                                dev.LastSyncTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                Logger.Info(string.Format("设备 [{0}] {1} 同步成功！", dev.Name, targetIp));
                            }
                            else
                            {
                                dev.LastStatus = "设置时间失败: " + errorMsg;
                                Logger.Warn(string.Format("设备 [{0}] {1} 同步失败: {2}", dev.Name, targetIp, errorMsg));
                            }
                            zk.Disconnect();
                        }
                        else
                        {
                            dev.LastStatus = "连接失败/超时";
                            errorMsg = "网络不可达或端口未开放/密码错误";
                        }
                    }

                    if (success)
                    {
                        successCount++;
                        successBuilder.AppendLine(string.Format("  - {0} ({1}:{2}) => 成功", dev.Name, targetIp, port));
                    }
                    else
                    {
                        failCount++;
                        failBuilder.AppendLine(string.Format("  - {0} ({1}:{2}) => 失败原因: {3}", dev.Name, targetIp, port, string.IsNullOrEmpty(errorMsg) ? dev.LastStatus : errorMsg));
                    }

                    if (logCallback != null)
                    {
                        logCallback(string.Format("设备 {0} ({1}): {2}", dev.Name, dev.Ip, dev.LastStatus));
                    }

                    Thread.Sleep(200); // 设备间轻微延时避免网络突发冲击
                }

                // 2. 保存记忆最新的设备状态
                config.Devices = new List<DeviceInfo>(allDevices.Values);
                ConfigManager.Save(config);

                Logger.Info(string.Format("时间同步完成: 总计 {0} 台 | 成功: {1} 台 | 失败: {2} 台", total, successCount, failCount));

                // 3. 发送企业微信通知
                if (config.WeCom != null && config.WeCom.Enabled)
                {
                    WeComNotifier.SendSyncReport(config.WeCom, total, successCount, failCount, successBuilder.ToString(), failBuilder.ToString());
                }
            }
            catch (Exception ex)
            {
                Logger.Error("执行同步引擎异常: " + ex.Message);
            }
            finally
            {
                lock (_syncLock)
                {
                    _isRunning = false;
                }
            }
        }
    }
}
