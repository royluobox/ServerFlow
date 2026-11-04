using ServerScheduler.Models;

namespace ServerScheduler.Services;

/// <summary>
/// 把排班按日历日切片，并统一生成界面上显示的文字。
/// </summary>
public static class ScheduleDayLayout
{
    public static List<DaySegment> BuildSegments(IEnumerable<ScheduleEntry> entries, DateOnly date)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        var segments = new List<DaySegment>();

        foreach (var entry in entries)
        {
            foreach (var occurrence in entry.GetOccurrencesOverlapping(date))
            {
                var start = occurrence.Start < dayStart ? dayStart : occurrence.Start;
                var end = occurrence.End > dayEnd ? dayEnd : occurrence.End;

                if (end <= start)
                {
                    continue;
                }

                segments.Add(new DaySegment(
                    occurrence,
                    start,
                    end,
                    occurrence.Start < dayStart,
                    occurrence.End > dayEnd));
            }
        }

        return segments
            .OrderBy(segment => segment.Start)
            .ThenBy(segment => segment.Entry.CreatedAt)
            .ToList();
    }

    public static double TotalHours(IEnumerable<DaySegment> segments) =>
        segments.Sum(segment => segment.Hours);

    /// <summary>紧凑写法，用于月历单元格：全天 / 09:00–24:00 / 00:00–11:00。</summary>
    public static string FormatCellRange(DaySegment segment)
    {
        if (segment.IsFullDay)
        {
            return "全天";
        }

        return $"{segment.Start:HH:mm}–{EndLabel(segment)}";
    }

    /// <summary>详情面板主时间：跨天时给出真实结束日期，承接时从 00:00 起算。</summary>
    public static string FormatDetailRange(DaySegment segment)
    {
        if (segment.IsFullDay)
        {
            return "全天占用";
        }

        if (segment.ContinuesToNextDay)
        {
            return $"{segment.Start:HH:mm} – {segment.Occurrence.End:M月d日 HH:mm}";
        }

        return $"{segment.Start:HH:mm} – {segment.End:HH:mm}";
    }

    /// <summary>整段占用的完整区间，用于提示与承接说明。</summary>
    public static string FormatFullSpan(ScheduleOccurrence occurrence)
    {
        var sameDay = occurrence.Start.Date == occurrence.End.Date;
        return sameDay
            ? $"{occurrence.Start:M月d日 HH:mm} – {occurrence.End:HH:mm}"
            : $"{occurrence.Start:M月d日 HH:mm} – {occurrence.End:M月d日 HH:mm}";
    }

    /// <summary>该日实际占用说明，例如「本日 15 小时 · 整段 50 小时」。</summary>
    public static string FormatDayPortion(DaySegment segment)
    {
        var dayText = $"本日 {segment.Hours:0.#} 小时";
        return segment.IsMultiDay
            ? $"{dayText} · 整段 {segment.Entry.DurationHours:0.#} 小时"
            : dayText;
    }

    private static string EndLabel(DaySegment segment) =>
        segment.ContinuesToNextDay ? "24:00" : segment.End.ToString("HH:mm");
}
