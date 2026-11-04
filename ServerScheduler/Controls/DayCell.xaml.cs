using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ServerScheduler.Models;
using ServerScheduler.Services;

namespace ServerScheduler.Controls;

public sealed partial class DayCell : UserControl
{
    private DateOnly _date;
    private bool _isSelected;
    private bool _isOutsideMonth;
    private bool _isPointerOver;

    public event EventHandler<DateOnly>? DayClicked;

    public DayCell()
    {
        InitializeComponent();
        ActualThemeChanged += (_, _) => RefreshAppearance();
    }

    public void SetDay(DateOnly date, bool isOutsideMonth, bool isToday, bool isSelected, IReadOnlyList<DaySegment> segments)
    {
        _date = date;
        _isOutsideMonth = isOutsideMonth;
        _isSelected = isSelected;

        DayNumber.Text = date.Day.ToString();
        TodayNumber.Text = date.Day.ToString();
        DayNumber.Visibility = isToday ? Visibility.Collapsed : Visibility.Visible;
        TodayBadge.Visibility = isToday ? Visibility.Visible : Visibility.Collapsed;
        MonthLabel.Text = date.Day == 1 ? $"{date.Month}月" : string.Empty;

        var dayHours = ScheduleDayLayout.TotalHours(segments);
        var spansDays = segments.Any(segment => segment.IsMultiDay);
        HoursLabel.Text = segments.Count == 0
            ? string.Empty
            : spansDays
                ? $"{dayHours:0.#}h · 跨天"
                : $"{dayHours:0.#}h";

        ItemsPanel.Children.Clear();
        foreach (var segment in segments.Take(3))
        {
            ItemsPanel.Children.Add(CreateSegmentItem(segment));
        }

        if (segments.Count > 3)
        {
            ItemsPanel.Children.Add(new TextBlock
            {
                Text = $"还有 {segments.Count - 3} 项",
                FontSize = 10,
                Foreground = ThemeResourceHelper.GetBrush("TertiaryTextBrush", ActualTheme),
                Margin = new Thickness(3, 0, 0, 0)
            });
        }

        RefreshAppearance();
    }

    private Border CreateSegmentItem(DaySegment segment)
    {
        var entry = segment.Entry;
        var isForeground = entry.Mode == UsageMode.Foreground;
        var foregroundKey = isForeground ? "AccentBrush" : "BackgroundModeBrush";
        var backgroundKey = isForeground ? "AccentSoftBrush" : "BackgroundModeSoftBrush";

        var itemBorder = new Border
        {
            CornerRadius = new CornerRadius(
                segment.ContinuesFromPreviousDay ? 0 : 4,
                segment.ContinuesToNextDay ? 0 : 4,
                segment.ContinuesToNextDay ? 0 : 4,
                segment.ContinuesFromPreviousDay ? 0 : 4),
            Padding = new Thickness(4, 2, 4, 2),
            Background = ThemeResourceHelper.GetBrush(backgroundKey, ActualTheme)
        };

        itemBorder.Child = new TextBlock
        {
            Text = $"{ScheduleDayLayout.FormatCellRange(segment)} {(isForeground ? "前台" : "后台")}",
            FontSize = 10,
            Foreground = ThemeResourceHelper.GetBrush(foregroundKey, ActualTheme),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var span = ScheduleDayLayout.FormatFullSpan(segment.Occurrence);
        ToolTipService.SetToolTip(itemBorder,
            $"{span}\n{(isForeground ? "前台操作" : "后台运行")} · 共 {entry.DurationHours:0.#} 小时"
            + (segment.IsMultiDay ? $"\n本日占用 {segment.Hours:0.#} 小时" : string.Empty));

        return itemBorder;
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
