using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace ZkTimeSync
{
    public static class WeComNotifier
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        static WeComNotifier()
        {
            // .NET Framework 4.5 默认仅启用 SSL3/TLS1.0，企业微信 API 已强制要求 TLS 1.2 及以上，
            // 否则推送会收到 "you are using TLS 1.0/1.1 which is deprecated" 提示并可能被拒绝
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
            }
            catch { }
        }

        /// <summary>
        /// 发送企业微信同步报告
        /// </summary>
        public static bool SendSyncReport(WeComConfig cfg, int total, int successCount, int failCount, string successListText, string failListText)
        {
            if (!cfg.Enabled)
            {
                Logger.Info("企业微信通知未启用，跳过推送。");
                return true;
            }

            try
            {
                // 构建通知内容
                var sb = new StringBuilder();

                // 首部自定义样式/字符
                if (!string.IsNullOrEmpty(cfg.CustomHeader))
                {
                    sb.AppendLine(cfg.CustomHeader);
                }
                else
                {
                    sb.AppendLine("⏰ 【中控考勤/门禁时间自动同步报告】");
                    sb.AppendLine("----------------------------------------");
                }

                sb.AppendLine(string.Format("📅 同步时间(服务器): {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now));
                sb.AppendLine(string.Format("🌏 服务器时区: {0} ({1})", TimeZoneInfo.Local.Id, TimeZoneHelper.FormatOffset(TimeZoneInfo.Local)));
                sb.AppendLine(string.Format("📊 总结统计: 共扫描/配置 {0} 台 | 成功: {1} 台 | 失败: {2} 台", total, successCount, failCount));
                sb.AppendLine();

                // 成功设备列表
                sb.AppendLine("✅ 【成功设备列表】:");
                if (string.IsNullOrWhiteSpace(successListText))
                {
                    sb.AppendLine("  (无)");
                }
                else
                {
                    sb.AppendLine(successListText.TrimEnd());
                }
                sb.AppendLine();

                // 失败设备列表
                sb.AppendLine("❌ 【失败设备列表】:");
                if (string.IsNullOrWhiteSpace(failListText))
                {
                    sb.AppendLine("  (无)");
                }
                else
                {
                    sb.AppendLine(failListText.TrimEnd());
                }

                // 尾部自定义样式/字符
                if (!string.IsNullOrEmpty(cfg.CustomFooter))
                {
                    sb.AppendLine();
                    sb.AppendLine(cfg.CustomFooter);
                }

                string content = sb.ToString();

                // 判断发送模式
                if (string.Equals(cfg.Mode, "App", StringComparison.OrdinalIgnoreCase))
                {
                    return SendByApp(cfg, content);
                }
                else
                {
                    return SendByWebhook(cfg.WebhookUrl, content);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("企业微信推送失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 通过群机器人 Webhook 发送
        /// </summary>
        public static bool SendByWebhook(string webhookUrl, string content)
        {
            if (string.IsNullOrEmpty(webhookUrl))
            {
                Logger.Warn("企业微信 Webhook URL 为空，无法发送！");
                return false;
            }

            try
            {
                var payload = new
                {
                    msgtype = "text",
                    text = new
                    {
                        content = content
                    }
                };

                string json = Serializer.Serialize(payload);
                byte[] data = Encoding.UTF8.GetBytes(json);

                var request = (HttpWebRequest)WebRequest.Create(webhookUrl);
                request.Method = "POST";
                request.ContentType = "application/json; charset=utf-8";
                request.ContentLength = data.Length;
                request.Timeout = 10000;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    string resJson = reader.ReadToEnd();
                    Logger.Info("企业微信 Webhook 推送响应: " + resJson);
                    return resJson.Contains("\"errcode\":0");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Webhook 发送异常: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 通过自建应用 API 发送
        /// </summary>
        public static bool SendByApp(WeComConfig cfg, string content)
        {
            try
            {
                string token = GetAppAccessToken(cfg.AppCorpId, cfg.AppCorpSecret);
                if (string.IsNullOrEmpty(token))
                {
                    Logger.Error("获取企业微信 AccessToken 失败！");
                    return false;
                }

                string url = "https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token=" + token;
                var payload = new
                {
                    touser = string.IsNullOrEmpty(cfg.AppToUser) ? "@all" : cfg.AppToUser,
                    msgtype = "text",
                    agentid = cfg.AppAgentId,
                    text = new
                    {
                        content = content
                    },
                    safe = 0
                };

                string json = Serializer.Serialize(payload);
                byte[] data = Encoding.UTF8.GetBytes(json);

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json; charset=utf-8";
                request.ContentLength = data.Length;
                request.Timeout = 10000;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    string resJson = reader.ReadToEnd();
                    Logger.Info("企业微信自建应用推送响应: " + resJson);
                    return resJson.Contains("\"errcode\":0");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("自建应用推送异常: " + ex.Message);
                return false;
            }
        }

        private static string _cachedToken = "";
        private static DateTime _tokenExpireTime = DateTime.MinValue;

        private static string GetAppAccessToken(string corpId, string corpSecret)
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.Now < _tokenExpireTime)
            {
                return _cachedToken;
            }

            try
            {
                string url = string.Format("https://qyapi.weixin.qq.com/cgi-bin/gettoken?corpid={0}&corpsecret={1}", corpId, corpSecret);
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = 10000;

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    var dict = Serializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(json);
                    if (dict != null && dict.ContainsKey("access_token"))
                    {
                        _cachedToken = dict["access_token"].ToString();
                        int expiresIn = Convert.ToInt32(dict["expires_in"]);
                        _tokenExpireTime = DateTime.Now.AddSeconds(expiresIn - 200);
                        return _cachedToken;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("获取AccessToken失败: " + ex.Message);
            }
            return "";
        }
    }
}
