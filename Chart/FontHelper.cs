using SkiaSharp;

namespace ChartCore;

internal static class FontHelper
{
    private static string? _cached;
    private static readonly object _lock = new();

    public static string GetCjkFamilyName()
    {
        if (_cached != null)
            return _cached;

        lock (_lock)
        {
            if (_cached != null)
                return _cached;

            string[] candidates = ["Microsoft YaHei UI", "微软雅黑", "Microsoft YaHei", "Noto Sans SC", "SimHei", "SimSun", "PingFang SC"];
            foreach (var name in candidates)
            {
                using var tf = SKTypeface.FromFamilyName(name);
                if (tf != null)
                {
                    _cached = name;
                    return name;
                }
            }

            using var fallback = SKFontManager.Default.MatchCharacter('汉');
            _cached = fallback?.FamilyName ?? "Arial";
            return _cached;
        }
    }
}
