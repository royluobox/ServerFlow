namespace ServerScheduler.Models;

/// <summary>
/// 一条排班在某个日历日内实际占用的片段。长时长排班会被裁剪到当天 00:00–24:00，
/// 因此一个 50 小时的排班会在 3 个日期上各产生一个片段。
/// </summary>
public sealed record DaySegment(
    ScheduleOccurrence Occurrence,
    DateTime Start,
    DateTime End,
    bool ContinuesFromPreviousDay,
    bool ContinuesToNextDay)
{
    public ScheduleEntry Entry => Occurrence.Entry;

    /// <summary>本片段在该日内实际占用的小时数（最多 24）。</summary>
    public double Hours => (End - Start).TotalHours;

    public bool IsFullDay => Hours >= 24d - 1e-6;

    public bool IsMultiDay => ContinuesFromPreviousDay || ContinuesToNextDay;
}
