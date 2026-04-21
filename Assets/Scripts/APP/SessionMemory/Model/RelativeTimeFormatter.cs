using System;

namespace APP.SessionMemory.Model
{
    public static class RelativeTimeFormatter
    {
        public static string Format(long unixMs, DateTimeOffset now)
        {
            var delta = now - DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
            if (delta.TotalSeconds < 60)  return "刚刚";
            if (delta.TotalMinutes < 60)  return $"{(int)delta.TotalMinutes} 分钟前";
            if (delta.TotalHours < 24)    return $"{(int)delta.TotalHours} 小时前";
            if (delta.TotalDays < 2)      return "昨天";
            if (delta.TotalDays < 7)      return $"{(int)delta.TotalDays} 天前";
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).LocalDateTime;
            return $"{dt.Month}月{dt.Day}日";
        }

        public static string Format(long unixMs) => Format(unixMs, DateTimeOffset.UtcNow);
    }
}
