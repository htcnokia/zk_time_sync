using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ZkTimeSync
{
    public static class DeviceScanner
    {
        /// <summary>
        /// 解析所有IP配置（CIDR与范围）并生成待扫描IP列表
        /// </summary>
        public static List<string> ParseIpTargets(List<string> cidrOrRangeList)
        {
            var result = new HashSet<string>();
            if (cidrOrRangeList == null) return new List<string>();

            foreach (var item in cidrOrRangeList)
            {
                if (string.IsNullOrWhiteSpace(item)) continue;
                string clean = item.Trim();

                try
                {
                    // 逗号分隔多个
                    if (clean.Contains(","))
                    {
                        var parts = clean.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var sub in parts)
                        {
                            AddSubRange(sub.Trim(), result);
                        }
                    }
                    else
                    {
                        AddSubRange(clean, result);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn("解析IP范围规则出错 [" + clean + "]: " + ex.Message);
                }
            }
            return new List<string>(result);
        }

        private static void AddSubRange(string input, HashSet<string> result)
        {
            if (input.Contains("/"))
            {
                // CIDR 格式如: 192.168.0.0/24
                var ips = ParseCidr(input);
                foreach (var ip in ips) result.Add(ip);
            }
            else if (input.Contains("-"))
            {
                // IP范围 格式如: 192.168.0.1-192.168.0.254
                var parts = input.Split('-');
                if (parts.Length == 2)
                {
                    uint start = IpToUint(IPAddress.Parse(parts[0].Trim()));
                    uint end = IpToUint(IPAddress.Parse(parts[1].Trim()));
                    if (start > end)
                    {
                        uint temp = start;
                        start = end;
                        end = temp;
                    }
                    for (uint cur = start; cur <= end; cur++)
                    {
                        result.Add(UintToIp(cur));
                    }
                }
            }
            else
            {
                // 单个IP
                IPAddress ip;
                if (IPAddress.TryParse(input, out ip))
                {
                    result.Add(input);
                }
            }
        }

        private static List<string> ParseCidr(string cidr)
        {
            var list = new List<string>();
            string[] parts = cidr.Split('/');
            IPAddress baseIp = IPAddress.Parse(parts[0].Trim());
            int prefixBits = int.Parse(parts[1].Trim());

            uint ipUint = IpToUint(baseIp);
            uint mask = 0xFFFFFFFF;
            if (prefixBits < 32)
            {
                mask = mask << (32 - prefixBits);
            }

            uint netUint = ipUint & mask;
            uint hostCount = (1u << (32 - prefixBits));

            // 去除网络地址和广播地址（针对/24到/30）
            uint start = (prefixBits <= 30) ? netUint + 1 : netUint;
            uint end = (prefixBits <= 30) ? netUint + hostCount - 2 : netUint + hostCount - 1;

            for (uint i = start; i <= end; i++)
            {
                list.Add(UintToIp(i));
            }
            return list;
        }

        private static uint IpToUint(IPAddress ip)
        {
            byte[] bytes = ip.GetAddressBytes();
            Array.Reverse(bytes); // 转换为网络字节序主机整型
            return BitConverter.ToUInt32(bytes, 0);
        }

        private static string UintToIp(uint val)
        {
            byte[] bytes = BitConverter.GetBytes(val);
            Array.Reverse(bytes);
            return new IPAddress(bytes).ToString();
        }

        /// <summary>
        /// 从 SQL Server 数据库中查询设备列表
        /// </summary>
        public static List<DeviceInfo> QueryFromMssql(MssqlConfig config, CommDefaultsConfig commDefaults)
        {
            var list = new List<DeviceInfo>();
            if (!config.Enabled || string.IsNullOrWhiteSpace(config.ConnectionString))
            {
                return list;
            }

            try
            {
                using (var conn = new SqlConnection(config.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(config.QuerySql, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!HasColumn(reader, "Ip"))
                            {
                                Logger.Error("查询结果缺少 Ip 列，请检查 QuerySql 是否对 IP 地址列使用了别名 AS Ip");
                                return list;
                            }

                            while (reader.Read())
                            {
                                // 单条记录解析失败不影响其他记录
                                try
                                {
                                    var dev = ParseDeviceRow(reader, commDefaults);
                                    if (dev != null) list.Add(dev);
                                }
                                catch (Exception ex)
                                {
                                    Logger.Warn("解析单条设备记录失败: " + ex.Message);
                                }
                            }
                        }
                    }
                }
                Logger.Info(string.Format("成功从 SQL Server 数据库读取 {0} 台设备记录", list.Count));
            }
            catch (SqlException ex)
            {
                Logger.Error("从 SQL Server 查询设备失败: " + ex.Message);
                LogDatabaseTables(config.ConnectionString);
            }
            catch (Exception ex)
            {
                Logger.Error("从 SQL Server 查询设备失败: " + ex.Message);
            }
            return list;
        }

        /// <summary>
        /// 将单行数据库记录解析为设备信息（字段须返回或别名为: Ip, Port, CommKey, Name）
        /// </summary>
        private static DeviceInfo ParseDeviceRow(IDataRecord reader, CommDefaultsConfig commDefaults)
        {
            // 无 IP 或 IP 为空的记录跳过
            string ip = Convert.ToString(reader["Ip"]);
            if (string.IsNullOrWhiteSpace(ip))
            {
                return null;
            }
            ip = ip.Trim();

            var dev = new DeviceInfo();
            dev.Ip = ip;
            dev.Port = ParseDbInt(reader, "Port", commDefaults.Port);
            dev.CommKey = ParseDbInt(reader, "CommKey", commDefaults.CommKey);

            if (HasColumn(reader, "Name"))
            {
                string name = Convert.ToString(reader["Name"]);
                dev.Name = string.IsNullOrWhiteSpace(name) ? "中控设备 (" + ip + ")" : name.Trim();
            }
            else
            {
                dev.Name = "中控设备 (" + ip + ")";
            }

            // 设备所在地时区（IANA 标准名，如 Asia/Shanghai），列名可为 IANA / Iana / TimeZone
            string iana = ReadStringColumn(reader, "IANA");
            if (string.IsNullOrWhiteSpace(iana)) iana = ReadStringColumn(reader, "TimeZone");
            dev.Iana = string.IsNullOrWhiteSpace(iana) ? "" : iana.Trim();

            return dev;
        }

        private static string ReadStringColumn(IDataRecord reader, string columnName)
        {
            if (!HasColumn(reader, columnName)) return "";
            object val = reader[columnName];
            if (val == null || val == DBNull.Value) return "";
            return val.ToString();
        }

        /// <summary>
        /// 安全解析数据库整数字段：兼容 DBNull、空字符串、前后空格、
        /// 小数形式("4370.00")等，解析失败时回退默认值并记录告警
        /// </summary>
        private static int ParseDbInt(IDataRecord reader, string columnName, int defaultValue)
        {
            if (!HasColumn(reader, columnName)) return defaultValue;

            object val = reader[columnName];
            if (val == null || val == DBNull.Value) return defaultValue;

            string text = val.ToString().Trim();
            if (string.IsNullOrEmpty(text)) return defaultValue;

            int result;
            if (int.TryParse(text, out result)) return result;

            // 兼容数值列返回的小数格式，如 numeric(10,2) 的 "4370.00"
            double dbl;
            if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dbl)
                && dbl == Math.Floor(dbl) && dbl >= int.MinValue && dbl <= int.MaxValue)
            {
                return (int)dbl;
            }

            Logger.Warn(string.Format("数据库列 {0} 的值 \"{1}\" 无法解析为整数，已使用默认值 {2}", columnName, text, defaultValue));
            return defaultValue;
        }

        /// <summary>
        /// 查询失败时列出数据库所有表名，方便排查 QuerySql 中表名/列名是否与目标库一致
        /// </summary>
        private static void LogDatabaseTables(string connectionString)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT name FROM sys.tables ORDER BY name", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var names = new List<string>();
                        while (reader.Read())
                        {
                            names.Add(Convert.ToString(reader["name"]));
                        }
                        Logger.Info("数据库中的所有表: " + string.Join(", ", names.ToArray()));
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 将数据库设备合并进记忆设备列表：按 IP 去重；
        /// 已存在的刷新名称/端口/通讯密码（保留其同步历史），不存在的直接追加。
        /// 返回结果数组: [新增台数, 更新台数]
        /// </summary>
        public static int[] MergeDevicesIntoMemory(List<DeviceInfo> memoryDevices, List<DeviceInfo> dbDevices)
        {
            int added = 0;
            int updated = 0;

            if (dbDevices == null) return new int[] { added, updated };

            foreach (var d in dbDevices)
            {
                if (d == null || string.IsNullOrWhiteSpace(d.Ip)) continue;

                var existing = memoryDevices.Find(x => x != null && x.Ip == d.Ip);
                if (existing == null)
                {
                    memoryDevices.Add(d);
                    added++;
                }
                else
                {
                    existing.Name = d.Name;
                    existing.Port = d.Port;
                    existing.CommKey = d.CommKey;
                    // 时区以数据库为准，但数据库未配置时保留本地已有配置
                    if (!string.IsNullOrWhiteSpace(d.Iana)) existing.Iana = d.Iana;
                    updated++;
                }
            }

            return new int[] { added, updated };
        }

        private static bool HasColumn(IDataRecord dr, string columnName)
        {
            for (int i = 0; i < dr.FieldCount; i++)
            {
                if (dr.GetName(i).Equals(columnName, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 扫描并验证IP是否为中控考勤/门禁设备 (支持并发扫描与Ping/ZK 4370端口探测)
        /// </summary>
        public static List<DeviceInfo> ScanNetwork(List<string> targetIps, int defaultPort, int defaultCommKey, Action<string, int, int> progressCallback = null)
        {
            var discovered = new List<DeviceInfo>();
            int total = targetIps.Count;
            int current = 0;

            // 限制并发最大 30 线程
            var options = new ParallelOptions { MaxDegreeOfParallelism = 30 };

            Parallel.ForEach(targetIps, options, ip =>
            {
                int count = Interlocked.Increment(ref current);
                if (progressCallback != null)
                {
                    progressCallback(ip, count, total);
                }

                if (IsZkDeviceOnline(ip, defaultPort, 1200))
                {
                    lock (discovered)
                    {
                        discovered.Add(new DeviceInfo
                        {
                            Ip = ip,
                            Port = defaultPort,
                            CommKey = defaultCommKey,
                            Name = "中控发现设备 (" + ip + ")",
                            LastStatus = "扫描在线"
                        });
                        Logger.Info(string.Format("发现中控设备在线: {0}:{1}", ip, defaultPort));
                    }
                }
            });

            return discovered;
        }

        /// <summary>
        /// 检查指定IP与端口是否具备中控特征（支持TCP握手与UDP 4370广播响应）
        /// </summary>
        public static bool IsZkDeviceOnline(string ip, int port, int timeoutMs)
        {
            // 步骤1：快速Ping测试
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send(ip, timeoutMs > 800 ? 800 : timeoutMs);
                    if (reply.Status != IPStatus.Success)
                    {
                        // 某些网络禁Ping，但仍尝试端口
                    }
                }
            }
            catch { }

            // 步骤2：TCP 端口探测 (很多较新中控设备开放4370 TCP)
            try
            {
                using (var tcp = new TcpClient())
                {
                    var ar = tcp.BeginConnect(ip, port, null, null);
                    bool success = ar.AsyncWaitHandle.WaitOne(timeoutMs);
                    if (success && tcp.Connected)
                    {
                        tcp.EndConnect(ar);
                        return true;
                    }
                }
            }
            catch { }

            // 步骤3：UDP 4370 握手探测（中控经典UDP协议特征探测包）
            try
            {
                using (var udp = new UdpClient())
                {
                    udp.Client.ReceiveTimeout = timeoutMs;
                    udp.Client.SendTimeout = timeoutMs;
                    var ep = new IPEndPoint(IPAddress.Parse(ip), port);

                    // 中控标准UDP握手数据包 (CMD_CONNECT = 1000)
                    // 头：0x50 0x50, 校验和，Session ID，Reply ID，Command ID (1000)
                    byte[] zkConnectProbe = new byte[] { 
                        0x50, 0x50, 0x82, 0x7d, 0x13, 0x00, 0x00, 0x00, 
                        0xe8, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 
                    };

                    udp.Send(zkConnectProbe, zkConnectProbe.Length, ep);
                    IPEndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
                    byte[] resp = udp.Receive(ref remoteEp);
                    if (resp != null && resp.Length >= 8)
                    {
                        // 响应带有 0x50 0x50 特征头或成功回复
                        if (resp[0] == 0x50 && resp[1] == 0x50)
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }
    }
}
