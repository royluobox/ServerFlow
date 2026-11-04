using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ServerScheduler.Models;
using ServerScheduler.Services;

namespace ServerScheduler.Controls;

public sealed partial class DayCell : UserControl
{
    private const double TrackGap = 2;
    private const double MinimumBlockHeight = 2.5;

    private DateOnly _date;
    private bool _isSelected;
    private bool _isOutsideMonth;
    private bool _isPointerOver;
    private IReadOnlyList<DaySegment> _segments = [];

    public event EventHandler<DateOnly>? DayClicked;

    public DayCell()
    {
        InitializeComponent();
        ActualThemeChanged += (_, _) =>
        {
            RefreshAppearance();
            RenderHoursLabel(_segments);
            RenderChips(_segments);
            LayoutTimeline();
        };
    }

    public void SetDay(DateOnly date, bool isOutsideMonth, bool isToday, bool isSelected, IReadOnlyList<DaySegment> segments)
    {
        _date = date;
        _isOutsideMonth = isOutsideMonth;
        _isSelected = isSelected;
        _segments = segments;

        DayNumber.Text = date.Day.ToString();
        TodayNumber.Text = date.Day.ToString();
        DayNumber.Visibility = isToday ? Visibility.Collapsed : Visibility.Visible;
        TodayBadge.Visibility = isToday ? Visibility.Visible : Visibility.Collapsed;
        MonthLabel.Text = date.Day == 1 ? $"{date.Month}月" : string.Empty;
        RenderHoursLabel(segments);
        TimelineTrack.Visibility = segments.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        RenderChips(segments);
        LayoutTimeline();
        RefreshAppearance();
    }

    private void RenderHoursLabel(IReadOnlyList<DaySegment> segments)
    {
        HoursLabel.Inlines.Clear();
        if (segments.Count == 0)
        {
            return;
        }

        var foreground = segments.Where(item => item.Entry.Mode == UsageMode.Foreground).Sum(item => item.Hours);
        var background = segments.Where(item => item.Entry.Mode == UsageMode.Background).Sum(item => item.Hours);
        var secondary = ThemeResourceHelper.GetBrush("SecondaryTextBrush", ActualTheme);

        // 前台与后台互不冲突，两者同时存在时分别着色显示，避免相加超过 24 小时造成误读。
        if (foreground > 0 && background > 0)
        {
            HoursLabel.Inlines.Add(new Run
            {
                Text = $"{foreground:0.#}",
                Foreground = ThemeResourceHelper.GetBrush("AccentBrush", ActualTheme)
            });
            HoursLabel.Inlines.Add(new Run { Text = "+", Foreground = secondary });
            HoursLabel.Inlines.Add(new Run
            {
                Text = $"{background:0.#}h",
                Foreground = ThemeResourceHelper.GetBrush("BackgroundModeBrush", ActualTheme)
            });
        }
        else
        {
            HoursLabel.Inlines.Add(new Run
            {
                Text = $"{foreground + background:0.#}h",
                Foreground = foreground > 0
                    ? ThemeResourceHelper.GetBrush("AccentBrush", ActualTheme)
                    : background > 0
                        ? ThemeResourceHelper.GetBrush("BackgroundModeBrush", ActualTheme)
                        : secondary
            });
        }

        if (segments.Any(item => item.IsMultiDay))
        {
            HoursLabel.Inlines.Add(new Run { Text = " · 跨天", Foreground = secondary });
        }
    }

    private void RenderChips(IReadOnlyList<DaySegment> segments)
    {
        ItemsPanel.Children.Clear();
        foreach (var segment in segments.Take(3))
        {
            ItemsPanel.Children.Add(CreateChip(segment));
        }

        if (segments.Count > 3)
        {
            ItemsPanel.Children.Add(new TextBlock
            {
                Text = $"还有 {segments.Count - 3} 项",
                FontSize = 9.5,
                Foreground = ThemeResourceHelper.GetBrush("TertiaryTextBrush", ActualTheme),
                Margin = new Thickness(2, 0, 0, 0)
            });
        }
    }

    private Border CreateChip(DaySegment segment)
    {
        var entry = segment.Entry;
        var isForeground = entry.Mode == UsageMode.Foreground;
        var foregroundKey = isForeground ? "AccentBrush" : "BackgroundModeBrush";
        var backgroundKey = isForeground ? "AccentSoftBrush" : "BackgroundModeSoftBrush";

        var chip = new Border
        {
            CornerRadius = CornerFor(segment, 4),
            Padding = new Thickness(3, 1, 3, 1),
            Background = ThemeResourceHelper.GetBrush(backgroundKey, ActualTheme)
        };

        chip.Child = new TextBlock
        {
            Text = ScheduleDayLayout.FormatCellRange(segment),
            FontSize = 9.5,
            Foreground = ThemeResourceHelper.GetBrush(foregroundKey, ActualTheme),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        ToolTipService.SetToolTip(chip, BuildTooltip(segment));
        return chip;
    }

    private void LayoutTimeline()
    {
        TimelineCanvas.Children.Clear();

        var width = TimelineCanvas.ActualWidth;
        var height = TimelineCanvas.ActualHeight;
        if (width <= 0 || height <= 0 || _segments.Count == 0)
        {
            return;
        }

        // 每 6 小时一条刻度，帮助判断块落在一天中的位置。
        for (var hour = 6; hour < 24; hour += 6)
        {
            var tick = new Border
            {
                Width = width,
                Height = 1,
                Background = ThemeResourceHelper.GetBrush("TertiaryTextBrush", ActualTheme),
                Opacity = 0.16
            };
            Canvas.SetTop(tick, hour / 24d * height);
            TimelineCanvas.Children.Add(tick);
        }

        var hasForeground = _segments.Any(segment => segment.Entry.Mode == UsageMode.Foreground);
        var hasBackground = _segments.Any(segment => segment.Entry.Mode == UsageMode.Background);
        var laneCount = hasForeground && hasBackground ? 2 : 1;
        var laneWidth = (width - TrackGap * (laneCount - 1)) / laneCount;

        foreach (var segment in _segments)
        {
            var isForeground = segment.Entry.Mode == UsageMode.Foreground;
            var lane = laneCount == 1 ? 0 : isForeground ? 0 : 1;
            var blockHeight = Math.Max(MinimumBlockHeight, segment.Hours / 24d * height);
            var top = Math.Clamp(segment.Start.TimeOfDay.TotalHours / 24d * height, 0, height - Math.Min(blockHeight, height));

            var block = new Border
            {
                Width = Math.Max(2, laneWidth),
                Height = Math.Min(blockHeight, height),
                CornerRadius = CornerFor(segment, 3),
                Background = ThemeResourceHelper.GetBrush(
                    isForeground ? "AccentBrush" : "BackgroundModeBrush",
                    ActualTheme)
            };

            ToolTipService.SetToolTip(block, BuildTooltip(segment));
            Canvas.SetLeft(block, lane * (laneWidth + TrackGap));
            Canvas.SetTop(block, top);
            TimelineCanvas.Children.Add(block);
        }
    }

    private static CornerRadius CornerFor(DaySegment segment, double radius)
    {
        return new CornerRadius(
            segment.ContinuesFromPreviousDay ? 0 : radius,
            segment.ContinuesToNextDay ? 0 : radius,
            segment.ContinuesToNextDay ? 0 : radius,
            segment.ContinuesFromPreviousDay ? 0 : radius);
    }

    private static string BuildTooltip(DaySegment segment)
    {
        var occurrence = segment.Occurrence;
        var mode = segment.Entry.Mode == UsageMode.Foreground ? "前台操作" : "后台运行";
        var text = $"{ScheduleDayLayout.FormatFullSpan(occurrence)}\n{mode} · 共 {segment.Entry.DurationHours:0.#} 小时";

        if (segment.IsMultiDay)
        {
            text += $"\n本日占用 {segment.Hours:0.#} 小时";
        }

        return text;
    }

    private void TimelineCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        LayoutTimeline();
    }

    private void RefreshAppearance()
    {
        RootBorder.Background = _isSelected
            ? ThemeResourceHelper.GetBrush("CalendarCellSelectedBrush", ActualTheme)
            : _isPointerOver
                ? ThemeResourceHelper.GetBrush("CalendarCellHoverBrush", ActualTheme)
                : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        RootBorder.BorderBrush = _isSelected
            ? ThemeResourceHelper.GetBrush("AccentBrush", ActualTheme)
            : ThemeResourceHelper.GetBrush("CalendarCellBorderBrush", ActualTheme);
        DayNumber.Foreground = ThemeResourceHelper.GetBrush(_isOutsideMonth ? "OutsideMonthBrush" : "PrimaryTextBrush", ActualTheme);
    }

    private void RootBorder_Tapped(object sender, TappedRoutedEventArgs e)
    {
        DayClicked?.Invoke(this, _date);
    }

    private void RootBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOver = true;
        RefreshAppearance();
    }

    private void RootBorder_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOver = false;
        RefreshAppearance();
    }
}
