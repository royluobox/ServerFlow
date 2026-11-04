using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ServerScheduler.Services;

public static class ThemeResourceHelper
{
    public static Brush GetBrush(string key, ElementTheme theme)
    {
        var dictionaryKey = theme == ElementTheme.Dark ? "Dark" : "Light";
        if (Application.Current.Resources.ThemeDictionaries.TryGetValue(dictionaryKey, out var dictionary)
            && dictionary is ResourceDictionary themed
            && themed.TryGetValue(key, out var value)
            && value is Brush brush)
        {
            return brush;
        }

        return (Brush)Application.Current.Resources[key];
    }
}

public static class ScheduleText
{
    public static string Duration(double hours)
    {
        if (hours < 1)
        {
            return $"{(int)Math.Round(hours * 60)}分钟";
        }

        return $"{hours:0.#}小时";
    }

    public static string Weekday(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "一",
        DayOfWeek.Tuesday => "二",
        DayOfWeek.Wednesday => "三",
        DayOfWeek.Thursday => "四",
        DayOfWeek.Friday => "五",
        DayOfWeek.Saturday => "六",
        _ => "日"
    };
}
