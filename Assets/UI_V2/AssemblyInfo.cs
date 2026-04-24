using System.Runtime.CompilerServices;

// 允许 APP.Settings.Tests 访问 APP.UI_V2 的 internal 成员（测试钩子）
[assembly: InternalsVisibleTo("APP.Settings.Tests")]
