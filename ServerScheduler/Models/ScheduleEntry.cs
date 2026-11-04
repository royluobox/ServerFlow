namespace ServerScheduler.Models;

public enum RecurrenceKind
{
    Once,
    Daily,
    Weekly
}

public enum UsageMode
{
    Foreground,
    Background
}

public sealed class ScheduleEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly StartDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public double DurationHours { get; set; } = 2;
    public RecurrenceKind Recurrence { get; set; }
    public DateOnly? EndDate { get; set; }
    public UsageMode Mode { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public bool OccursOn(DateOnly date)
    {
        if (date < StartDate || (EndDate.HasValue && date > EndDate.Value))
        {
            return false;
        }

        return Recurrence switch
        {
            RecurrenceKind.Once => date == StartDate,
            RecurrenceKind.Daily => true,
            RecurrenceKind.Weekly => date.DayOfWeek == StartDate.DayOfWeek,
            _ => false
        };
    }

    public IEnumerable<ScheduleOccurrence> GetOccurrences(DateOnly from, DateOnly to)
    {
        if (to < from || to < StartDate || (EndDate.HasValue && from > EndDate.Value))
        {
            yield break;
        }

        var last = EndDate.HasValue && EndDate.Value < to ? EndDate.Value : to;

        if (Recurrence == RecurrenceKind.Once)
        {
            if (StartDate >= from && StartDate <= last)
            {
                yield return CreateOccurrence(StartDate);
            }
            yield break;
        }

        var date = from < StartDate ? StartDate : from;
        if (Recurrence == RecurrenceKind.Weekly)
        {
            var daysUntilWeeklyDate = ((int)StartDate.DayOfWeek - (int)date.DayOfWeek + 7) % 7;
            date = date.AddDays(daysUntilWeeklyDate);
        }

        var step = Recurrence == RecurrenceKind.Daily ? 1 : 7;
        while (date <= last)
        {
            yield return CreateOccurrence(date);
            date = date.AddDays(step);
        }
    }

    private ScheduleOccurrence CreateOccurrence(DateOnly date)
    {
        var start = date.ToDateTime(StartTime);
        return new ScheduleOccurrence(this, start, start.AddHours(DurationHours));
    }
}

public sealed record ScheduleOccurrence(ScheduleEntry Entry, DateTime Start, DateTime End);
