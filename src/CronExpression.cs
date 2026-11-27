using System;
using System.Collections.Generic;

namespace ZkTimeSync
{
    /// <summary>
    /// 轻量级 CRON 表达式解析器 (兼容标准 5段 / 6段 CRON，如: 0 0 2 * * ? 或 0 2 * * *)
    /// 秒 分 时 日 月 周
    /// </summary>
    public class CronExpression
    {
        private string _expression;
        private List<int> _seconds = new List<int>();
        private List<int> _minutes = new List<int>();
        private List<int> _hours = new List<int>();
        private List<int> _days = new List<int>();
        private List<int> _months = new List<int>();
        private List<int> _dayOfWeeks = new List<int>();

        public CronExpression(string expr)
        {
            _expression = expr.Trim();
            Parse();
        }

        private void Parse()
        {
            string[] parts = _expression.Split(new char[] { ' ', '	' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 5)
            {
                // 分 时 日 月 周 (秒默认0)
                _seconds.Add(0);
                _minutes = ParseField(parts[0], 0, 59);
                _hours = ParseField(parts[1], 0, 23);
                _days = ParseField(parts[2], 1, 31);
                _months = ParseField(parts[3], 1, 12);
                _dayOfWeeks = ParseField(parts[4], 0, 6);
            }
            else if (parts.Length >= 6)
            {
                // 秒 分 时 日 月 周
                _seconds = ParseField(parts[0], 0, 59);
                _minutes = ParseField(parts[1], 0, 59);
                _hours = ParseField(parts[2], 0, 23);
                _days = ParseField(parts[3], 1, 31);
                _months = ParseField(parts[4], 1, 12);
                _dayOfWeeks = ParseField(parts[5], 0, 7); // 0或7代表周日
            }
            else
            {
                throw new ArgumentException("无效的CRON表达式字段数量");
            }
        }

        private List<int> ParseField(string field, int min, int max)
        {
            var res = new List<int>();
            if (field == "*" || field == "?")
            {
                for (int i = min; i <= max; i++) res.Add(i);
                return res;
            }

            // 处理逗号: 1,2,3
            string[] subList = field.Split(',');
            foreach (var sub in subList)
            {
                if (sub.Contains("/"))
                {
                    // 步长: */5 或 10/15
                    string[] stepParts = sub.Split('/');
                    int start = stepParts[0] == "*" ? min : int.Parse(stepParts[0]);
                    int step = int.Parse(stepParts[1]);
                    for (int i = start; i <= max; i += step)
                    {
                        if (!res.Contains(i)) res.Add(i);
                    }
                }
                else if (sub.Contains("-"))
                {
                    // 范围: 1-5
                    string[] rangeParts = sub.Split('-');
                    int start = int.Parse(rangeParts[0]);
                    int end = int.Parse(rangeParts[1]);
                    for (int i = start; i <= end; i++)
                    {
                        if (!res.Contains(i)) res.Add(i);
                    }
                }
                else
                {
                    int val;
                    if (int.TryParse(sub, out val))
                    {
                        if (!res.Contains(val)) res.Add(val);
                    }
                }
            }
            res.Sort();
            return res;
        }

        public bool IsTimeToRun(DateTime dt)
        {
            int sec = dt.Second;
            int min = dt.Minute;
            int hour = dt.Hour;
            int day = dt.Day;
            int month = dt.Month;
            int dow = (int)dt.DayOfWeek;

            if (!_seconds.Contains(sec)) return false;
            if (!_minutes.Contains(min)) return false;
            if (!_hours.Contains(hour)) return false;
            if (!_days.Contains(day)) return false;
            if (!_months.Contains(month)) return false;
            if (!_dayOfWeeks.Contains(dow) && !(_dayOfWeeks.Contains(7) && dow == 0)) return false;

            return true;
        }

        public DateTime? GetNextOccurrence(DateTime from)
        {
            DateTime cur = from.AddSeconds(1);
            cur = new DateTime(cur.Year, cur.Month, cur.Day, cur.Hour, cur.Minute, cur.Second);
            // 预测未来最多 100 天
            for (int i = 0; i < 86400 * 100; i++)
            {
                if (IsTimeToRun(cur))
                {
                    return cur;
                }
                cur = cur.AddSeconds(1);
            }
            return null;
        }
    }
}
