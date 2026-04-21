using System;
using APP.SessionMemory.Model;
using NUnit.Framework;

namespace APP.SessionMemory.Tests
{
    [TestFixture]
    public sealed class RelativeTimeFormatterTests
    {
        private readonly DateTimeOffset _now = new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero);

        private long Ago(TimeSpan d) => (_now - d).ToUnixTimeMilliseconds();

        [Test] public void Seconds_Under60_ReturnsJustNow()
            => Assert.That(RelativeTimeFormatter.Format(Ago(TimeSpan.FromSeconds(30)), _now), Is.EqualTo("刚刚"));

        [Test] public void Minutes_Under60_ReturnsMinutes()
            => Assert.That(RelativeTimeFormatter.Format(Ago(TimeSpan.FromMinutes(5)), _now), Is.EqualTo("5 分钟前"));

        [Test] public void Hours_Under24_ReturnsHours()
            => Assert.That(RelativeTimeFormatter.Format(Ago(TimeSpan.FromHours(3)), _now), Is.EqualTo("3 小时前"));

        [Test] public void Under2Days_ReturnsYesterday()
            => Assert.That(RelativeTimeFormatter.Format(Ago(TimeSpan.FromHours(30)), _now), Is.EqualTo("昨天"));

        [Test] public void Under7Days_ReturnsDaysAgo()
            => Assert.That(RelativeTimeFormatter.Format(Ago(TimeSpan.FromDays(3)), _now), Is.EqualTo("3 天前"));

        [Test] public void Over7Days_ReturnsMonthDay()
        {
            long t = new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            string r = RelativeTimeFormatter.Format(t, _now);
            // 格式化后会按本地时区打印，无法精确断言日（依赖 tz），仅断言含"月"与"日"
            Assert.That(r, Does.Contain("月").And.Contain("日"));
        }
    }
}
