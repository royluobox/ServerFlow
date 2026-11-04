using ServerScheduler.Models;
using ServerScheduler.Services;

var tests = new (string Name, Action Run)[]
{
    ("单次排班仅命中开始日期", OnceOccursOnlyOnce),
    ("每日排班遵守截止日期", DailyHonorsEndDate),
    ("每周排班按星期展开", WeeklyUsesStartWeekday),
    ("重叠排班被识别", OverlapIsRejected),
    ("首尾相接不算冲突", AdjacentBookingsAreAllowed),
    ("跨午夜冲突被识别", CrossMidnightOverlapIsRejected),
    ("长期周期冲突被识别", RecurringOverlapIsRejected)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL  {test.Name}: {ex.Message}");
    }
}

Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed");
return failed == 0 ? 0 : 1;

static ScheduleEntry Entry(DateOnly date, int hour, double duration, RecurrenceKind recurrence = RecurrenceKind.Once) => new()
{
    StartDate = date,
    StartTime = new TimeOnly(hour, 0),
    DurationHours = duration,
    Recurrence = recurrence
};

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void OnceOccursOnlyOnce()
{
    var entry = Entry(new DateOnly(2026, 11, 4), 9, 2);
    Assert(entry.OccursOn(new DateOnly(2026, 11, 4)), "开始日期未命中");
    Assert(!entry.OccursOn(new DateOnly(2026, 11, 5)), "单次排班错误重复");
}

static void DailyHonorsEndDate()
{
    var entry = Entry(new DateOnly(2026, 11, 4), 9, 2, RecurrenceKind.Daily);
    entry.EndDate = new DateOnly(2026, 11, 6);
    Assert(entry.GetOccurrences(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 10)).Count() == 3, "每日展开数量错误");
}

static void WeeklyUsesStartWeekday()
{
    var entry = Entry(new DateOnly(2026, 11, 4), 9, 2, RecurrenceKind.Weekly);
    var dates = entry.GetOccurrences(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)).Select(item => DateOnly.FromDateTime(item.Start)).ToList();
    Assert(dates.SequenceEqual([new DateOnly(2026, 11, 4), new DateOnly(2026, 11, 11), new DateOnly(2026, 11, 18), new DateOnly(2026, 11, 25)]), "每周展开日期错误");
}

static void OverlapIsRejected()
{
    var existing = Entry(new DateOnly(2026, 11, 4), 9, 3);
    var candidate = Entry(new DateOnly(2026, 11, 4), 11, 1);
    Assert(ScheduleConflictService.FindConflict(candidate, [existing]) is not null, "重叠未识别");
}

static void AdjacentBookingsAreAllowed()
{
    var existing = Entry(new DateOnly(2026, 11, 4), 9, 2);
    var candidate = Entry(new DateOnly(2026, 11, 4), 11, 1);
    Assert(ScheduleConflictService.FindConflict(candidate, [existing]) is null, "首尾相接被误判");
}

static void CrossMidnightOverlapIsRejected()
{
    var existing = Entry(new DateOnly(2026, 11, 4), 22, 4);
    var candidate = Entry(new DateOnly(2026, 11, 5), 1, 1);
    Assert(ScheduleConflictService.FindConflict(candidate, [existing]) is not null, "跨午夜重叠未识别");
}

static void RecurringOverlapIsRejected()
{
    var existing = Entry(new DateOnly(2026, 11, 4), 9, 2, RecurrenceKind.Weekly);
    var candidate = Entry(new DateOnly(2026, 12, 1), 10, 1, RecurrenceKind.Daily);
    Assert(ScheduleConflictService.FindConflict(candidate, [existing]) is not null, "周期重叠未识别");
}
