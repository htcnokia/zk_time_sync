using System;
using System.Collections.Generic;

namespace ZkTimeSync
{
    /// <summary>
    /// 时区解析工具。
    ///
    /// .NET Framework 4.5 的 TimeZoneInfo 只认 Windows 时区 ID（如 "China Standard Time"），
    /// 不认 IANA 名称（如 "Asia/Shanghai"），因此这里内置了一份 CLDR windowsZones 映射表，
    /// 将数据库中的 IANA 时区名转换为 Windows 时区 ID 后再交给系统时区库计算，
    /// 这样夏令时（DST）等规则完全由 Windows 时区数据保证准确。
    ///
    /// 同时兼容以下写法：
    ///   · Windows 时区 ID 直写：China Standard Time / UTC
    ///   · 固定偏移量写法：UTC+8 / GMT+08:00 / +08:00 / Etc/GMT-8（POSIX 符号相反）
    /// </summary>
    public static class TimeZoneHelper
    {
        /// <summary>
        /// IANA 时区名 → Windows 时区 ID（来源于 Unicode CLDR windowsZones.xml）
        /// </summary>
        private static readonly Dictionary<string, string> IanaToWindows =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // ===== 东亚 =====
            { "Asia/Shanghai", "China Standard Time" },
            { "Asia/Chongqing", "China Standard Time" },
            { "Asia/Chungking", "China Standard Time" },
            { "Asia/Harbin", "China Standard Time" },
            { "Asia/Macau", "China Standard Time" },
            { "Asia/Macao", "China Standard Time" },
            { "Asia/Hong_Kong", "China Standard Time" },
            { "Hongkong", "China Standard Time" },
            { "PRC", "China Standard Time" },
            { "Asia/Urumqi", "Central Asia Standard Time" },
            { "Asia/Kashgar", "Central Asia Standard Time" },
            { "Asia/Taipei", "Taipei Standard Time" },
            { "ROC", "Taipei Standard Time" },
            { "Asia/Tokyo", "Tokyo Standard Time" },
            { "Japan", "Tokyo Standard Time" },
            { "Asia/Seoul", "Korea Standard Time" },
            { "ROK", "Korea Standard Time" },
            { "Asia/Pyongyang", "Korea Standard Time" },
            { "Asia/Singapore", "Singapore Standard Time" },
            { "Singapore", "Singapore Standard Time" },
            { "Asia/Kuala_Lumpur", "Singapore Standard Time" },
            { "Asia/Kuching", "Singapore Standard Time" },
            { "Asia/Brunei", "Singapore Standard Time" },
            { "Asia/Manila", "Singapore Standard Time" },
            { "Asia/Makassar", "Singapore Standard Time" },
            { "Asia/Dili", "Tokyo Standard Time" },
            { "Asia/Jayapura", "Tokyo Standard Time" },
            { "Asia/Chita", "Yakutsk Standard Time" },
            { "Asia/Ulaanbaatar", "Ulaanbaatar Standard Time" },
            { "Asia/Ulan_Bator", "Ulaanbaatar Standard Time" },
            { "Asia/Choibalsan", "Ulaanbaatar Standard Time" },

            // ===== 东南亚 / 南亚 =====
            { "Asia/Bangkok", "SE Asia Standard Time" },
            { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" },
            { "Asia/Saigon", "SE Asia Standard Time" },
            { "Asia/Phnom_Penh", "SE Asia Standard Time" },
            { "Asia/Vientiane", "SE Asia Standard Time" },
            { "Asia/Jakarta", "SE Asia Standard Time" },
            { "Asia/Pontianak", "SE Asia Standard Time" },
            { "Asia/Yangon", "Myanmar Standard Time" },
            { "Asia/Rangoon", "Myanmar Standard Time" },
            { "Asia/Dhaka", "Bangladesh Standard Time" },
            { "Asia/Kolkata", "India Standard Time" },
            { "Asia/Calcutta", "India Standard Time" },
            { "Asia/Colombo", "Sri Lanka Standard Time" },
            { "Asia/Kathmandu", "Nepal Standard Time" },
            { "Asia/Katmandu", "Nepal Standard Time" },
            { "Asia/Karachi", "Pakistan Standard Time" },
            { "Asia/Kabul", "Afghanistan Standard Time" },

            // ===== 中亚 / 俄罗斯 / 西亚 =====
            { "Asia/Tashkent", "West Asia Standard Time" },
            { "Asia/Samarkand", "West Asia Standard Time" },
            { "Asia/Ashgabat", "West Asia Standard Time" },
            { "Asia/Ashkhabad", "West Asia Standard Time" },
            { "Asia/Aqtobe", "West Asia Standard Time" },
            { "Asia/Almaty", "Central Asia Standard Time" },
            { "Asia/Bishkek", "Central Asia Standard Time" },
            { "Asia/Dushanbe", "Tajikistan Standard Time" },
            { "Asia/Yekaterinburg", "Ekaterinburg Standard Time" },
            { "Asia/Omsk", "Omsk Standard Time" },
            { "Asia/Novosibirsk", "N. Central Asia Standard Time" },
            { "Asia/Novokuznetsk", "N. Central Asia Standard Time" },
            { "Asia/Krasnoyarsk", "North Asia Standard Time" },
            { "Asia/Irkutsk", "North Asia East Standard Time" },
            { "Asia/Yakutsk", "Yakutsk Standard Time" },
            { "Asia/Vladivostok", "Vladivostok Standard Time" },
            { "Asia/Sakhalin", "Sakhalin Standard Time" },
            { "Asia/Magadan", "Magadan Standard Time" },
            { "Asia/Srednekolymsk", "Russia Time Zone 10" },
            { "Asia/Kamchatka", "Russia Time Zone 11" },
            { "Asia/Anadyr", "Russia Time Zone 11" },
            { "Asia/Tehran", "Iran Standard Time" },
            { "Iran", "Iran Standard Time" },
            { "Asia/Baghdad", "Arabic Standard Time" },
            { "Asia/Kuwait", "Arab Standard Time" },
            { "Asia/Riyadh", "Arab Standard Time" },
            { "Asia/Qatar", "Arab Standard Time" },
            { "Asia/Bahrain", "Arab Standard Time" },
            { "Asia/Aden", "Arab Standard Time" },
            { "Asia/Dubai", "Arabian Standard Time" },
            { "Asia/Muscat", "Arabian Standard Time" },
            { "Asia/Beirut", "Middle East Standard Time" },
            { "Asia/Damascus", "Syria Standard Time" },
            { "Asia/Amman", "Jordan Standard Time" },
            { "Asia/Jerusalem", "Israel Standard Time" },
            { "Asia/Tel_Aviv", "Israel Standard Time" },
            { "Israel", "Israel Standard Time" },
            { "Asia/Gaza", "West Bank Standard Time" },
            { "Asia/Hebron", "West Bank Standard Time" },
            { "Asia/Nicosia", "E. Europe Standard Time" },
            { "Asia/Famagusta", "E. Europe Standard Time" },
            { "Asia/Tbilisi", "Georgian Standard Time" },
            { "Asia/Yerevan", "Caucasus Standard Time" },
            { "Asia/Baku", "Azerbaijan Standard Time" },

            // ===== 欧洲 =====
            { "Europe/London", "GMT Standard Time" },
            { "Europe/Dublin", "GMT Standard Time" },
            { "Europe/Lisbon", "GMT Standard Time" },
            { "Europe/Guernsey", "GMT Standard Time" },
            { "Europe/Isle_of_Man", "GMT Standard Time" },
            { "Europe/Jersey", "GMT Standard Time" },
            { "Atlantic/Canary", "GMT Standard Time" },
            { "Atlantic/Madeira", "GMT Standard Time" },
            { "GB", "GMT Standard Time" },
            { "GB-Eire", "GMT Standard Time" },
            { "Eire", "GMT Standard Time" },
            { "Portugal", "GMT Standard Time" },
            { "Europe/Paris", "Romance Standard Time" },
            { "Europe/Madrid", "Romance Standard Time" },
            { "Europe/Brussels", "Romance Standard Time" },
            { "Europe/Monaco", "Romance Standard Time" },
            { "Europe/Amsterdam", "W. Europe Standard Time" },
            { "Europe/Berlin", "W. Europe Standard Time" },
            { "Europe/Zurich", "W. Europe Standard Time" },
            { "Europe/Vienna", "W. Europe Standard Time" },
            { "Europe/Rome", "W. Europe Standard Time" },
            { "Europe/Stockholm", "W. Europe Standard Time" },
            { "Europe/Oslo", "W. Europe Standard Time" },
            { "Europe/Copenhagen", "W. Europe Standard Time" },
            { "Europe/Malta", "W. Europe Standard Time" },
            { "Europe/Gibraltar", "W. Europe Standard Time" },
            { "Europe/Luxembourg", "W. Europe Standard Time" },
            { "Europe/Prague", "Central Europe Standard Time" },
            { "Europe/Budapest", "Central Europe Standard Time" },
            { "Europe/Belgrade", "Central Europe Standard Time" },
            { "Europe/Bratislava", "Central Europe Standard Time" },
            { "Europe/Ljubljana", "Central Europe Standard Time" },
            { "Europe/Warsaw", "Central European Standard Time" },
            { "Poland", "Central European Standard Time" },
            { "Europe/Sarajevo", "Central European Standard Time" },
            { "Europe/Zagreb", "Central European Standard Time" },
            { "Europe/Skopje", "Central European Standard Time" },
            { "Europe/Athens", "GTB Standard Time" },
            { "Europe/Bucharest", "GTB Standard Time" },
            { "Europe/Sofia", "FLE Standard Time" },
            { "Europe/Kiev", "FLE Standard Time" },
            { "Europe/Kyiv", "FLE Standard Time" },
            { "Europe/Helsinki", "FLE Standard Time" },
            { "Europe/Riga", "FLE Standard Time" },
            { "Europe/Vilnius", "FLE Standard Time" },
            { "Europe/Tallinn", "FLE Standard Time" },
            { "Europe/Minsk", "Belarus Standard Time" },
            { "Europe/Moscow", "Russian Standard Time" },
            { "W-SU", "Russian Standard Time" },
            { "Europe/Volgograd", "Russian Standard Time" },
            { "Europe/Samara", "Russia Time Zone 3" },
            { "Europe/Istanbul", "Turkey Standard Time" },
            { "Turkey", "Turkey Standard Time" },
            { "Europe/Reykjavik", "Greenwich Standard Time" },
            { "Atlantic/Reykjavik", "Greenwich Standard Time" },
            { "Iceland", "Greenwich Standard Time" },
            { "Atlantic/Azores", "Azores Standard Time" },
            { "Atlantic/Cape_Verde", "Cape Verde Standard Time" },

            // ===== 北美洲 =====
            { "America/New_York", "Eastern Standard Time" },
            { "America/Toronto", "Eastern Standard Time" },
            { "America/Detroit", "Eastern Standard Time" },
            { "America/Montreal", "Eastern Standard Time" },
            { "America/Nassau", "Eastern Standard Time" },
            { "US/Eastern", "Eastern Standard Time" },
            { "Canada/Eastern", "Eastern Standard Time" },
            { "America/Chicago", "Central Standard Time" },
            { "America/Winnipeg", "Central Standard Time" },
            { "US/Central", "Central Standard Time" },
            { "Canada/Central", "Central Standard Time" },
            { "America/Mexico_City", "Central Standard Time (Mexico)" },
            { "America/Monterrey", "Central Standard Time (Mexico)" },
            { "Mexico/General", "Central Standard Time (Mexico)" },
            { "America/Regina", "Canada Central Standard Time" },
            { "America/Denver", "Mountain Standard Time" },
            { "America/Edmonton", "Mountain Standard Time" },
            { "US/Mountain", "Mountain Standard Time" },
            { "Canada/Mountain", "Mountain Standard Time" },
            { "America/Navajo", "Mountain Standard Time" },
            { "America/Phoenix", "US Mountain Standard Time" },
            { "US/Arizona", "US Mountain Standard Time" },
            { "America/Chihuahua", "Mountain Standard Time (Mexico)" },
            { "America/Mazatlan", "Mountain Standard Time (Mexico)" },
            { "America/Los_Angeles", "Pacific Standard Time" },
            { "America/Vancouver", "Pacific Standard Time" },
            { "US/Pacific", "Pacific Standard Time" },
            { "Canada/Pacific", "Pacific Standard Time" },
            { "America/Tijuana", "Pacific Standard Time (Mexico)" },
            { "America/Anchorage", "Alaskan Standard Time" },
            { "America/Juneau", "Alaskan Standard Time" },
            { "US/Alaska", "Alaskan Standard Time" },
            { "America/Adak", "Aleutian Standard Time" },
            { "Pacific/Honolulu", "Hawaiian Standard Time" },
            { "US/Hawaii", "Hawaiian Standard Time" },
            { "America/St_Johns", "Newfoundland Standard Time" },
            { "Canada/Newfoundland", "Newfoundland Standard Time" },
            { "America/Halifax", "Atlantic Standard Time" },
            { "America/Glace_Bay", "Atlantic Standard Time" },
            { "America/Havana", "Cuba Standard Time" },
            { "Cuba", "Cuba Standard Time" },

            // ===== 中南美洲 =====
            { "America/Guatemala", "Central America Standard Time" },
            { "America/Costa_Rica", "Central America Standard Time" },
            { "America/El_Salvador", "Central America Standard Time" },
            { "America/Managua", "Central America Standard Time" },
            { "America/Tegucigalpa", "Central America Standard Time" },
            { "America/Belize", "Central America Standard Time" },
            { "America/Bogota", "SA Pacific Standard Time" },
            { "America/Lima", "SA Pacific Standard Time" },
            { "America/Guayaquil", "SA Pacific Standard Time" },
            { "America/Panama", "SA Pacific Standard Time" },
            { "America/Jamaica", "SA Pacific Standard Time" },
            { "Brazil/Acre", "SA Pacific Standard Time" },
            { "America/Caracas", "Venezuela Standard Time" },
            { "America/La_Paz", "SA Western Standard Time" },
            { "America/Manaus", "SA Western Standard Time" },
            { "America/Guyana", "SA Western Standard Time" },
            { "America/Puerto_Rico", "SA Western Standard Time" },
            { "America/Santo_Domingo", "SA Western Standard Time" },
            { "America/Barbados", "SA Western Standard Time" },
            { "Brazil/West", "SA Western Standard Time" },
            { "America/Santiago", "Pacific SA Standard Time" },
            { "Chile/Continental", "Pacific SA Standard Time" },
            { "America/Asuncion", "Paraguay Standard Time" },
            { "America/Sao_Paulo", "E. South America Standard Time" },
            { "Brazil/East", "E. South America Standard Time" },
            { "America/Argentina/Buenos_Aires", "Argentina Standard Time" },
            { "America/Buenos_Aires", "Argentina Standard Time" },
            { "America/Montevideo", "Montevideo Standard Time" },
            { "America/Cayenne", "SA Eastern Standard Time" },
            { "America/Paramaribo", "SA Eastern Standard Time" },
            { "America/Belem", "SA Eastern Standard Time" },
            { "America/Fortaleza", "SA Eastern Standard Time" },
            { "America/Recife", "SA Eastern Standard Time" },
            { "America/Cuiaba", "Central Brazilian Standard Time" },
            { "America/Campo_Grande", "Central Brazilian Standard Time" },

            // ===== 非洲 =====
            { "Africa/Cairo", "Egypt Standard Time" },
            { "Egypt", "Egypt Standard Time" },
            { "Africa/Tripoli", "Libya Standard Time" },
            { "Africa/Casablanca", "Morocco Standard Time" },
            { "Africa/El_Aaiun", "Morocco Standard Time" },
            { "Africa/Algiers", "W. Central Africa Standard Time" },
            { "Africa/Lagos", "W. Central Africa Standard Time" },
            { "Africa/Kinshasa", "W. Central Africa Standard Time" },
            { "Africa/Luanda", "W. Central Africa Standard Time" },
            { "Africa/Tunis", "W. Central Africa Standard Time" },
            { "Africa/Ndjamena", "W. Central Africa Standard Time" },
            { "Africa/Abidjan", "Greenwich Standard Time" },
            { "Africa/Accra", "Greenwich Standard Time" },
            { "Africa/Dakar", "Greenwich Standard Time" },
            { "Africa/Bamako", "Greenwich Standard Time" },
            { "Africa/Monrovia", "Greenwich Standard Time" },
            { "Africa/Nairobi", "E. Africa Standard Time" },
            { "Africa/Addis_Ababa", "E. Africa Standard Time" },
            { "Africa/Dar_es_Salaam", "E. Africa Standard Time" },
            { "Africa/Kampala", "E. Africa Standard Time" },
            { "Africa/Khartoum", "E. Africa Standard Time" },
            { "Africa/Juba", "E. Africa Standard Time" },
            { "Africa/Mogadishu", "E. Africa Standard Time" },
            { "Africa/Johannesburg", "South Africa Standard Time" },
            { "Africa/Maputo", "South Africa Standard Time" },
            { "Africa/Harare", "South Africa Standard Time" },
            { "Africa/Windhoek", "Namibia Standard Time" },

            // ===== 大洋洲 =====
            { "Australia/Sydney", "AUS Eastern Standard Time" },
            { "Australia/Melbourne", "AUS Eastern Standard Time" },
            { "Australia/Canberra", "AUS Eastern Standard Time" },
            { "Australia/Brisbane", "E. Australia Standard Time" },
            { "Australia/Adelaide", "Cen. Australia Standard Time" },
            { "Australia/Darwin", "AUS Central Standard Time" },
            { "Australia/Perth", "W. Australia Standard Time" },
            { "Australia/Hobart", "Tasmania Standard Time" },
            { "Pacific/Auckland", "New Zealand Standard Time" },
            { "NZ", "New Zealand Standard Time" },
            { "Antarctica/McMurdo", "New Zealand Standard Time" },
            { "Pacific/Chatham", "Chatham Islands Standard Time" },
            { "Pacific/Fiji", "Fiji Standard Time" },
            { "Pacific/Guam", "West Pacific Standard Time" },
            { "Pacific/Saipan", "West Pacific Standard Time" },
            { "Pacific/Port_Moresby", "West Pacific Standard Time" },
            { "Pacific/Noumea", "Central Pacific Standard Time" },
            { "Pacific/Guadalcanal", "Central Pacific Standard Time" },
            { "Pacific/Norfolk", "Central Pacific Standard Time" },
            { "Pacific/Majuro", "UTC+12" },
            { "Pacific/Tarawa", "UTC+12" },
            { "Pacific/Kwajalein", "UTC+12" },
            { "Kwajalein", "UTC+12" },
            { "Pacific/Enderbury", "UTC+13" },
            { "Pacific/Kiritimati", "UTC+14" },
            { "Pacific/Tongatapu", "Tonga Standard Time" },
            { "Pacific/Apia", "Samoa Standard Time" },

            // ===== UTC 相关 =====
            { "UTC", "UTC" },
            { "Etc/UTC", "UTC" },
            { "Etc/GMT", "UTC" },
            { "Etc/Greenwich", "UTC" },
            { "Etc/Universal", "UTC" },
            { "Etc/Zulu", "UTC" },
            { "Universal", "UTC" },
            { "Zulu", "UTC" },
            { "UCT", "UTC" },
            { "GMT0", "UTC" },
            { "Greenwich", "UTC" },
        };

        /// <summary>界面上常用的时区候选（IANA 名）</summary>
        public static readonly string[] CommonZones = new string[]
        {
            "Asia/Shanghai", "Asia/Hong_Kong", "Asia/Taipei", "Asia/Macau",
            "Asia/Tokyo", "Asia/Seoul", "Asia/Singapore", "Asia/Bangkok",
            "Asia/Ho_Chi_Minh", "Asia/Jakarta", "Asia/Manila", "Asia/Kolkata",
            "Asia/Dubai", "Asia/Urumqi", "Australia/Sydney", "Europe/London",
            "Europe/Paris", "Europe/Berlin", "Europe/Moscow", "Europe/Istanbul",
            "America/New_York", "America/Chicago", "America/Los_Angeles", "UTC"
        };

        private static readonly Dictionary<string, TimeZoneInfo> FixedZoneCache =
            new Dictionary<string, TimeZoneInfo>(StringComparer.Ordinal);

        /// <summary>
        /// 解析时区名称为 TimeZoneInfo。
        /// 解析失败返回 null 并给出 error 说明。
        /// </summary>
        public static TimeZoneInfo Resolve(string tzName, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(tzName))
            {
                error = "时区为空";
                return null;
            }

            string key = tzName.Trim();

            // 1) 固定偏移量写法：UTC+8 / GMT+08:00 / +08:00 / Etc/GMT-8
            TimeSpan fixedOffset;
            if (TryParseFixedOffset(key, out fixedOffset))
            {
                return CreateFixedZone(fixedOffset);
            }

            // 2) IANA 名称 → Windows 时区 ID
            string winId;
            if (IanaToWindows.TryGetValue(key, out winId))
            {
                TimeZoneInfo tzi = TryFindSystemZone(winId);
                if (tzi != null) return tzi;
                error = string.Format("本机时区库中不存在 \"{0}\"（由 \"{1}\" 映射而来）", winId, key);
                return null;
            }

            // 3) 直接当 Windows 时区 ID 使用
            TimeZoneInfo direct = TryFindSystemZone(key);
            if (direct != null) return direct;

            error = string.Format("无法识别的时区 \"{0}\"，请填写 IANA 名称（如 Asia/Shanghai）", key);
            return null;
        }

        /// <summary>
        /// 解析时区；为空时返回服务器本机时区，解析失败返回本机时区并在 error 中说明原因。
        /// </summary>
        public static TimeZoneInfo ResolveOrDefault(string tzName, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(tzName))
            {
                return TimeZoneInfo.Local;
            }

            TimeZoneInfo tz = Resolve(tzName, out error);
            if (tz == null)
            {
                tz = TimeZoneInfo.Local;
            }
            return tz;
        }

        /// <summary>时区名是否可被本机识别</summary>
        public static bool IsKnown(string tzName)
        {
            string err;
            return Resolve(tzName, out err) != null;
        }

        /// <summary>
        /// 计算该时区当前应设置的设备本地时间（基于 UTC 换算，自动考虑夏令时）
        /// </summary>
        public static DateTime GetDeviceLocalTime(TimeZoneInfo tz)
        {
            if (tz == null) return DateTime.Now;
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }

        /// <summary>格式化时区偏移量，如 UTC+08:00</summary>
        public static string FormatOffset(TimeZoneInfo tz)
        {
            if (tz == null) return "";
            return FormatOffset(tz.BaseUtcOffset);
        }

        public static string FormatOffset(TimeSpan offset)
        {
            TimeSpan abs = offset.Ticks < 0 ? offset.Negate() : offset;
            return string.Format("UTC{0}{1:00}:{2:00}", offset.Ticks < 0 ? "-" : "+", (int)abs.TotalHours, abs.Minutes);
        }

        /// <summary>时区的可读描述，如 "Asia/Shanghai (UTC+08:00)"</summary>
        public static string Describe(string tzName, TimeZoneInfo tz)
        {
            if (tz == null) return "未知时区";
            if (string.IsNullOrWhiteSpace(tzName))
            {
                return string.Format("跟随本机 ({0} {1})", tz.Id, FormatOffset(tz));
            }
            return string.Format("{0} ({1})", tzName.Trim(), FormatOffset(tz));
        }

        /// <summary>
        /// 解析固定偏移写法：UTC+8 / UTC+08:00 / GMT-5 / +08:00 / Etc/GMT-8（POSIX 语义，符号相反）
        /// </summary>
        public static bool TryParseFixedOffset(string text, out TimeSpan offset)
        {
            offset = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string s = text.Trim().ToUpperInvariant().Replace(" ", "");

            if (s == "UTC" || s == "GMT" || s == "ETC/UTC" || s == "ETC/GMT" || s == "Z") return true;

            // Etc/GMT+8 => UTC-8；Etc/GMT-8 => UTC+8（POSIX 标准符号与实际偏移相反）
            if (s.StartsWith("ETC/GMT") && s.Length > 7)
            {
                return TryParseSignedNumber(s.Substring(7), true, out offset);
            }

            if (s.StartsWith("UTC")) s = s.Substring(3);
            else if (s.StartsWith("GMT")) s = s.Substring(3);
            if (s.Length == 0) return false;

            return TryParseSignedNumber(s, false, out offset);
        }

        private static bool TryParseSignedNumber(string s, bool invertSign, out TimeSpan offset)
        {
            offset = TimeSpan.Zero;
            if (string.IsNullOrEmpty(s)) return false;

            bool negative = false;
            if (s[0] == '+' || s[0] == '-')
            {
                negative = (s[0] == '-');
                s = s.Substring(1);
            }
            else if (s[0] < '0' || s[0] > '9')
            {
                return false;
            }
            if (s.Length == 0) return false;

            double hours;
            if (s.IndexOf(':') >= 0)
            {
                string[] hm = s.Split(':');
                if (hm.Length != 2) return false;
                int h, m;
                if (!int.TryParse(hm[0], out h) || !int.TryParse(hm[1], out m)) return false;
                if (m < 0 || m > 59) return false;
                hours = h + m / 60.0;
            }
            else
            {
                int val;
                if (!int.TryParse(s, out val)) return false;
                if (s.Length <= 2) hours = val;                 // ±H / ±HH
                else if (s.Length == 4) hours = (val / 100) + (val % 100) / 60.0;  // ±HHMM
                else return false;
            }

            if (hours > 14) return false;
            if (invertSign) negative = !negative;

            double minutes = hours * 60;
            offset = TimeSpan.FromMinutes(negative ? -minutes : minutes);
            return true;
        }

        private static TimeZoneInfo CreateFixedZone(TimeSpan offset)
        {
            string id = "Fixed" + FormatOffset(offset);
            lock (FixedZoneCache)
            {
                TimeZoneInfo cached;
                if (FixedZoneCache.TryGetValue(id, out cached)) return cached;

                string name = FormatOffset(offset);
                TimeZoneInfo tz = TimeZoneInfo.CreateCustomTimeZone(id, offset, name, name);
                FixedZoneCache[id] = tz;
                return tz;
            }
        }

        private static TimeZoneInfo TryFindSystemZone(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
            }
            catch (TimeZoneNotFoundException)
            {
                return null;
            }
            catch (InvalidTimeZoneException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
