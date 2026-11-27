using System;
using System.IO;
using System.Web.Script.Serialization;

namespace ZkTimeSync
{
    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath, System.Text.Encoding.UTF8);
                    var cfg = Serializer.Deserialize<AppConfig>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("加载配置文件失败: " + ex.Message);
            }
            return new AppConfig();
        }

        public static bool Save(AppConfig config)
        {
            try
            {
                string json = Serializer.Serialize(config);
                // 格式化输出为可读JSON
                json = JsonFormatter.FormatJson(json);
                File.WriteAllText(ConfigPath, json, System.Text.Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("保存配置文件失败: " + ex.Message);
                return false;
            }
        }
    }

    public static class JsonFormatter
    {
        public static string FormatJson(string json)
        {
            int indent = 0;
            bool inQuotes = false;
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];
                switch (ch)
                {
                    case '"':
                        sb.Append(ch);
                        bool escaped = false;
                        int index = i;
                        while (index > 0 && json[--index] == (char)92) escaped = !escaped;
                        if (!escaped) inQuotes = !inQuotes;
                        break;
                    case '{':
                    case '[':
                        sb.Append(ch);
                        if (!inQuotes)
                        {
                            sb.AppendLine();
                            sb.Append(new string(' ', ++indent * 2));
                        }
                        break;
                    case '}':
                    case ']':
                        if (!inQuotes)
                        {
                            sb.AppendLine();
                            sb.Append(new string(' ', --indent * 2));
                        }
                        sb.Append(ch);
                        break;
                    case ',':
                        sb.Append(ch);
                        if (!inQuotes)
                        {
                            sb.AppendLine();
                            sb.Append(new string(' ', indent * 2));
                        }
                        break;
                    case ':':
                        sb.Append(ch);
                        if (!inQuotes) sb.Append(" ");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }
    }

    public static class Logger
    {
        private static readonly object LockObj = new object();
        public static event Action<string> OnLog;

        public static void Info(string msg)
        {
            Log("INFO", msg);
        }

        public static void Warn(string msg)
        {
            Log("WARN", msg);
        }

        public static void Error(string msg)
        {
            Log("ERROR", msg);
        }

        private static void Log(string level, string msg)
        {
            string line = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] {2}", DateTime.Now, level, msg);
            Console.WriteLine(line);

            if (OnLog != null)
            {
                try { OnLog(line); } catch { }
            }

            lock (LockObj)
            {
                try
                {
                    string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                    if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                    string logFile = Path.Combine(logDir, string.Format("log_{0:yyyyMMdd}.txt", DateTime.Now));
                    File.AppendAllText(logFile, line + Environment.NewLine, System.Text.Encoding.UTF8);
                }
                catch { }
            }
        }
    }
}
