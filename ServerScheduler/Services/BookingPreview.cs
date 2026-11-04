namespace ServerScheduler.Services;

/// <summary>添加排班对话框的结束时间预览。</summary>
public static class BookingPreview
{
    public const string Empty = "结束时间：填写开始日期与时长后显示";

    public static string Describe(DateOnly date, TimeSpan startTime, double hours)
    {
        if (!double.IsFinite(hours) || hours <= 0)
        {
            return Empty;
        }

        var start = date.ToDateTime(TimeOnly.FromTimeSpan(startTime));
        var end = start.AddHours(hours);

        // 结束时刻正好是 00:00 时，实际占用到前一天的 24:00，不应算作多占一天。
        var lastOccupiedDay = end.TimeOfDay == TimeSpan.Zero ? end.AddDays(-1).Date : end.Date;
        var dayCount = (lastOccupiedDay - start.Date).Days + 1;

        return dayCount == 1
            ? $"结束时间：{end:M月d日 HH:mm} · 当天结束"
            : $"结束时间：{end:M月d日 HH:mm} · 跨 {dayCount} 个日历日，占用会延续到后续日期";
    }
}
