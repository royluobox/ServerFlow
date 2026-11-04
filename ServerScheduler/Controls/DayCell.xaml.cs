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

    public void SetDay(DateOnly date, bool isOutsideMonth, bool isToday, bool isSelected, IReadOnlyList<ScheduleEntry> schedules)
    {
        _date = date;
        _isOutsideMonth = isOutsideMonth;
        _isSelected = isSelected;

        DayNumber.Text = date.Day.ToString();
        TodayNumber.Text = date.Day.ToString();
        DayNumber.Visibility = isToday ? Visibility.Collapsed : Visibility.Visible;
        TodayBadge.Visibility = isToday ? Visibility.Visible : Visibility.Collapsed;
        MonthLabel.Text = date.Day == 1 ? $"{date.Month}月" : string.Empty;
        HoursLabel.Text = schedules.Count > 0 ? $"{schedules.Sum(item => item.DurationHours):0.#}h" : string.Empty;

        ItemsPanel.Children.Clear();
        foreach (var entry in schedules.OrderBy(item => item.StartTime).Take(3))
        {
            var foregroundKey = entry.Mode == UsageMode.Foreground ? "AccentBrush" : "BackgroundModeBrush";
            var backgroundKey = entry.Mode == UsageMode.Foreground ? "AccentSoftBrush" : "BackgroundModeSoftBrush";
            var itemBorder = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 2, 5, 2),
                Background = ThemeResourceHelper.GetBrush(backgroundKey, ActualTheme)
            };
            itemBorder.Child = new TextBlock
            {
                Text = $"{entry.StartTime:HH:mm}  {(entry.Mode == UsageMode.Foreground ? "前台" : "后台")} · {entry.DurationHours:0.#}h",
                FontSize = 10.5,
                Foreground = ThemeResourceHelper.GetBrush(foregroundKey, ActualTheme),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            ItemsPanel.Children.Add(itemBorder);
        }

        if (schedules.Count > 3)
        {
            ItemsPanel.Children.Add(new TextBlock
            {
                Text = $"还有 {schedules.Count - 3} 项",
                FontSize = 10,
                Foreground = ThemeResourceHelper.GetBrush("TertiaryTextBrush", ActualTheme),
                Margin = new Thickness(3, 0, 0, 0)
            });
        }

        RefreshAppearance();
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
