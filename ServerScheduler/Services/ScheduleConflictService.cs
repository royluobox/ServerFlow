using ServerScheduler.Models;

namespace ServerScheduler.Services;

public sealed record ScheduleConflict(ScheduleOccurrence Candidate, ScheduleOccurrence Existing);

public static class ScheduleConflictService
{
    private const int LookAheadDays = 370;

    public static ScheduleConflict? FindConflict(ScheduleEntry candidate, IEnumerable<ScheduleEntry> existingEntries)
    {
        // 前台操作与后台运行互不冲突：只有同一使用方式之间才需要抢占同一时段。
        var existing = existingEntries
            .Where(entry => entry.Id != candidate.Id && entry.Mode == candidate.Mode)
            .ToList();
        var rangeEnd = candidate.EndDate ?? candidate.StartDate.AddDays(LookAheadDays);
        if (candidate.Recurrence == RecurrenceKind.Once)
        {
            rangeEnd = candidate.StartDate;
        }

        foreach (var occurrence in candidate.GetOccurrences(candidate.StartDate, rangeEnd))
        {
            var lookBehind = (int)Math.Ceiling(candidate.DurationHours / 24) + 8;
            var from = DateOnly.FromDateTime(occurrence.Start.AddDays(-lookBehind));
            var to = DateOnly.FromDateTime(occurrence.End);

            foreach (var other in existing.SelectMany(entry => entry.GetOccurrences(from, to)))
            {
                if (occurrence.Start < other.End && other.Start < occurrence.End)
                {
                    return new ScheduleConflict(occurrence, other);
                }
            }
        }

        return null;
    }
}
