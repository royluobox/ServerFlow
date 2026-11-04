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
    ("长期周期冲突被识别", RecurringOverlapIsRejected),
    ("50 小时排班均分到 3 个日历日", LongBookingSplitsAcrossDays),
    ("跨天排班在后续日期被识别冲突", LongBookingBlocksFollowingDay),
    ("长排班反向覆盖已有短排班", LongBookingCoversEarlierShortBooking),
    ("长排班结束后不再冲突", LongBookingAllowsLaterBooking),
    ("50 小时预览显示跨 3 天", PreviewShowsLongBookingSpan),
    ("当天结束的预览", PreviewShowsSameDayBooking),
    ("正好到午夜的预览算当天", PreviewTreatsMidnightAsSameDay),
    ("前台与后台同一时段不冲突", ForegroundAndBackgroundDoNotConflict),
    ("后台与后台仍然冲突", BackgroundStillConflictsWithBackground),
    ("后台长排班不阻挡前台", LongBackgroundBookingAllowsForeground)
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

static ScheduleEntry Entry(
    DateOnly date,
    int hour,
    double duration,
    RecurrenceKind recurrence = RecurrenceKind.Once,
    UsageMode mode = UsageMode.Foreground) => new()
{
    StartDate = date,
    StartTime = new TimeOnly(hour, 0),
    DurationHours = duration,
    Recurrence = recurrence,
    Mode = mode
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

static void LongBookingSplitsAcrossDays()
{
    var entry = Entry(new DateOnly(2026, 11, 4), 9, 50);
    var hours = new[] { 4, 5, 6, 7 }
        .Select(day => ScheduleDayLayout.BuildSegments([entry], new DateOnly(2026, 11, day)))
        .Select(segments => segments.Sum(segment => segment.Hours))
        .ToArray();

    Assert(hours[0] == 15, $"首日应为 15 小时，实际 {hours[0]}");
    Assert(hours[1] == 24, $"次日应为全天 24 小时，实际 {hours[1]}");
    Assert(hours[2] == 11, $"第三日应为 11 小时，实际 {hours[2]}");
    Assert(hours[3] == 0, $"结束日之后不应有占用，实际 {hours[3]}");

    var firstDay = ScheduleDayLayout.BuildSegments([entry], new DateOnly(2026, 11, 4)).Single();
    var middleDay = ScheduleDayLayout.BuildSegments([entry], new DateOnly(2026, 11, 5)).Single();
    var lastDay = ScheduleDayLayout.BuildSegments([entry], new DateOnly(2026, 11, 6)).Single();

    Assert(!firstDay.ContinuesFromPreviousDay && firstDay.ContinuesToNextDay, "首日跨天标记错误");
    Assert(middleDay.ContinuesFromPreviousDay && middleDay.ContinuesToNextDay, "次日应为全天承接");
    Assert(lastDay.ContinuesFromPreviousDay && !lastDay.ContinuesToNextDay, "第三日跨天标记错误");
    Assert(ScheduleDayLayout.FormatCellRange(middleDay) == "全天", $"全天文案错误：{ScheduleDayLayout.FormatCellRange(middleDay)}");
    Assert(ScheduleDayLayout.FormatCellRange(lastDay) == "00:00–11:00", $"末日文案错误：{ScheduleDayLayout.FormatCellRange(lastDay)}");
}

static void LongBookingBlocksFollowingDay()
{
    var existing = Entry(new DateOnly(2026, 11, 4), 9, 50);
    var candidate = Entry(new DateOnly(2026, 11, 5), 10, 2);
    Assert(ScheduleConflictService.FindConflict(candidate, [existing]) is not null, "长排班占用的后续日期未被判定冲突");
}

static void LongBookingCoversEarlierShortBooking()
{
    var existing = Entry(new DateOnly(2026, 11, 5), 10, 2);
    var candidate = Entry(new DateOnly(2026, 11, 4), 9, 50);
    Assert(ScheduleConflictService.FindConflict(candidate, [existing]) is not null, "长排班未覆盖已有短排班");
}

static void LongBookingAllowsLaterBooking()
{
    var existing = Entry(new DateOnly(2026, 11, 6), 12, 2);
    var candidate = Entry(new DateOnly(2026, 11, 4), 9, 50);
    Assert(ScheduleConflictService.FindConflict(candidate, [existing]) is null, "长排班结束后仍被误判冲突");
}

static void PreviewShowsLongBookingSpan()
{
    var text = BookingPreview.Describe(new DateOnly(2026, 11, 4), new TimeSpan(9, 0, 0), 50);
    Assert(text.Contains("11月6日 11:00"), $"结束时间错误：{text}");
    Assert(text.Contains("跨 3 个日历日"), $"跨天数量错误：{text}");
}

static void PreviewShowsSameDayBooking()
{
    var text = BookingPreview.Describe(new DateOnly(2026, 11, 4), new TimeSpan(9, 0, 0), 2);
    Assert(text.Contains("当天结束"), $"当天结束文案错误：{text}");
}

static void PreviewTreatsMidnightAsSameDay()
{
    var text = BookingPreview.Describe(new DateOnly(2026, 11, 4), new TimeSpan(21, 0, 0), 3);
    Assert(text.Contains("11月5日 00:00"), $"结束时间错误：{text}");
    Assert(text.Contains("当天结束"), "正好到午夜被误判为跨天");
}

static void ForegroundAndBackgroundDoNotConflict()
{
    var existing = Entry(new DateOnly(2026, 11, 4), 9, 2, mode: UsageMode.Background);
    var candidate = Entry(new DateOnly(2026, 11, 4), 9, 2, mode: UsageMode.Foreground);
    Assert(ScheduleConflictService.FindConflict(candidate, [existing]) is null, "前台与后台被误判冲突");

    var reverse = ScheduleConflictService.FindConflict(existing, [candidate]);
    Assert(reverse is null, "后台与前台被误判冲突");
}

static void BackgroundStillConflictsWithBackground()
{
    var existing = Entry(new DateOnly(2026, 11, 4), 9, 2, mode: UsageMode.Background);
    var candidate = Entry(new DateOnly(2026, 11, 4), 10, 1, mode: UsageMode.Background);
    Assert(ScheduleConflictService.FindConflict(candidate, [existing]) is not null, "后台之间重叠未识别");
}

static void LongBackgroundBookingAllowsForeground()
{
    var existing = Entry(new DateOnly(2026, 11, 4), 9, 50, mode: UsageMode.Background);
    var candidate = Entry(new DateOnly(2026, 11, 5), 10, 2, mode: UsageMode.Foreground);
    Assert(ScheduleConflictService.FindConflict(candidate, [existing]) is null, "后台长排班错误阻挡前台排班");
}
