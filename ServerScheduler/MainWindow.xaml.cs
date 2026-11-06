using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ServerScheduler.Controls;
using ServerScheduler.Models;
using ServerScheduler.Services;
using Windows.Graphics;

namespace ServerScheduler;

public sealed partial class MainWindow : Window
{
    private readonly ScheduleRepository _repository = new();
    private readonly NetworkTimeService _timeService = new();
    private readonly GitHubUpdateService _updateService = new();
    private readonly BookingDialog _bookingDialog = new();
    private List<ScheduleEntry> _schedules = [];
    private DateOnly _displayMonth;
    private DateOnly _selectedDate;
    private bool _isSyncing;

    public MainWindow()
    {
        InitializeComponent();
        _bookingDialog.PrimaryButtonClick += BookingDialog_PrimaryButtonClick;
        Title = "ServerFlow 服务器排班";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new MicaBackdrop();
        AppWindow.Resize(new SizeInt32(1360, 880));

        var today = DateOnly.FromDateTime(DateTime.Now);
        _selectedDate = today;
        _displayMonth = new DateOnly(today.Year, today.Month, 1);

        InitializeCalendarGrid();
        RootGrid.ActualThemeChanged += (_, _) => RenderAll();
        RootGrid.Loaded += MainWindow_Loaded;
        RenderAll();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        string? loadError = null;
        try
        {
            _schedules = await _repository.LoadAsync();
        }
        catch (Exception ex)
        {
            loadError = ex.Message;
            _schedules = [];
        }

        RenderAll();
        ShortcutService.ApplyPendingRebuild();
        await SyncTimeAsync();
        await CheckForUpdatesAsync(true);

        if (loadError is not null)
        {
            await ShowMessageAsync("排班文件读取失败", $"已显示空日历。原文件未被覆盖，请检查：\n{_repository.DataFilePath}\n\n{loadError}");
        }
    }

    private void InitializeCalendarGrid()
    {
        for (var column = 0; column < 7; column++)
        {
            CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var row = 0; row < 6; row++)
        {
            CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }
    }

    private void RenderAll()
    {
        MonthTitle.Text = $"{_displayMonth.Year}年{_displayMonth.Month}月";
        RenderCalendar();
        RenderSelectedDay();
    }

    private void RenderCalendar()
    {
        CalendarGrid.Children.Clear();
        var today = DateOnly.FromDateTime(_timeService.Now.LocalDateTime);
        var firstOfMonth = new DateOnly(_displayMonth.Year, _displayMonth.Month, 1);
        var mondayOffset = ((int)firstOfMonth.DayOfWeek + 6) % 7;
        var gridStart = firstOfMonth.AddDays(-mondayOffset);

        for (var index = 0; index < 42; index++)
        {
            var date = gridStart.AddDays(index);
            var segments = ScheduleDayLayout.BuildSegments(_schedules, date);
            var cell = new DayCell();
            cell.DayClicked += DayCell_DayClicked;
            CalendarGrid.Children.Add(cell);
            Grid.SetRow(cell, index / 7);
            Grid.SetColumn(cell, index % 7);
            cell.SetDay(date, date.Month != _displayMonth.Month, date == today, date == _selectedDate, segments);
        }
    }

    private void RenderSelectedDay()
    {
        var today = DateOnly.FromDateTime(_timeService.Now.LocalDateTime);
        SelectedDateTitle.Text = $"{_selectedDate.Month}月{_selectedDate.Day}日";
        SelectedDateSubtitle.Text = $"{_selectedDate.Year}年 · 星期{ScheduleText.Weekday(_selectedDate.DayOfWeek)}"
            + (_selectedDate == today ? " · 今天" : string.Empty);

        var segments = ScheduleDayLayout.BuildSegments(_schedules, _selectedDate);
        var items = new List<ScheduleListItem>();

        foreach (var segment in segments)
        {
            var entry = segment.Entry;
            var isForeground = entry.Mode == UsageMode.Foreground;
            var recurrenceText = entry.Recurrence switch
            {
                RecurrenceKind.Once => "单次",
                RecurrenceKind.Daily => entry.EndDate.HasValue ? $"每天 · 至{entry.EndDate.Value:M月d日}" : "每天重复",
                RecurrenceKind.Weekly => $"每周{ScheduleText.Weekday(entry.StartDate.DayOfWeek)}"
                    + (entry.EndDate.HasValue ? $" · 至{entry.EndDate.Value:M月d日}" : string.Empty),
                _ => string.Empty
            };

            var detail = segment.ContinuesFromPreviousDay
                ? $"承接 {segment.Occurrence.Start:M月d日 HH:mm} 起 · 本日 {segment.Hours:0.#} 小时"
                : segment.IsMultiDay
                    ? $"本日 {segment.Hours:0.#} 小时 · 整段 {entry.DurationHours:0.#} 小时"
                    : string.Empty;

            items.Add(new ScheduleListItem
            {
                Id = entry.Id,
                TimeRange = ScheduleDayLayout.FormatDetailRange(segment),
                ModeText = isForeground ? "前台操作" : "后台运行",
                RecurrenceText = recurrenceText,
                DetailText = detail,
                DetailVisibility = string.IsNullOrEmpty(detail) ? Visibility.Collapsed : Visibility.Visible,
                Note = entry.Note,
                NoteVisibility = string.IsNullOrWhiteSpace(entry.Note) ? Visibility.Collapsed : Visibility.Visible,
                ModeForeground = ThemeResourceHelper.GetBrush(isForeground ? "AccentBrush" : "BackgroundModeBrush", RootGrid.ActualTheme),
                ModeBackground = ThemeResourceHelper.GetBrush(isForeground ? "AccentSoftBrush" : "BackgroundModeSoftBrush", RootGrid.ActualTheme)
            });
        }

        ScheduleList.ItemsSource = items;
        EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var foregroundHours = segments.Where(item => item.Entry.Mode == UsageMode.Foreground).Sum(item => item.Hours);
        var backgroundHours = segments.Where(item => item.Entry.Mode == UsageMode.Background).Sum(item => item.Hours);
        var hoursText = foregroundHours > 0 && backgroundHours > 0
            ? $"前台 {foregroundHours:0.#} 小时 · 后台 {backgroundHours:0.#} 小时"
            : $"本日占用 {foregroundHours + backgroundHours:0.#} 小时";
        var continuesToNextDay = segments.Any(segment => segment.ContinuesToNextDay);
        DaySummary.Text = items.Count == 0
            ? string.Empty
            : $"共 {items.Count} 项 · {hoursText}"
                + (continuesToNextDay ? " · 延续到次日" : string.Empty);
    }

    private void DayCell_DayClicked(object? sender, DateOnly date)
    {
        _selectedDate = date;
        if (date.Year != _displayMonth.Year || date.Month != _displayMonth.Month)
        {
            _displayMonth = new DateOnly(date.Year, date.Month, 1);
        }
        RenderAll();
    }

    private void PreviousMonth_Click(object sender, RoutedEventArgs e)
    {
        _displayMonth = _displayMonth.AddMonths(-1);
        RenderAll();
    }

    private void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        _displayMonth = _displayMonth.AddMonths(1);
        RenderAll();
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        var today = DateOnly.FromDateTime(_timeService.Now.LocalDateTime);
        _selectedDate = today;
        _displayMonth = new DateOnly(today.Year, today.Month, 1);
        RenderAll();
    }

    private async void AddSchedule_Click(object sender, RoutedEventArgs e)
    {
        _bookingDialog.BookingDatePicker.Date = new DateTimeOffset(_selectedDate.ToDateTime(TimeOnly.MinValue));
        _bookingDialog.StartTimePicker.SelectedTime = new TimeSpan(9, 0, 0);
        _bookingDialog.DurationBox.Value = 2;
        _bookingDialog.RepeatOptions.SelectedIndex = 0;
        _bookingDialog.ModeOptions.SelectedIndex = 0;
        _bookingDialog.RepeatEndPicker.Date = null;
        _bookingDialog.NoteBox.Text = string.Empty;
        _bookingDialog.DialogInfo.IsOpen = false;
        _bookingDialog.XamlRoot = RootGrid.XamlRoot;
        await _bookingDialog.ShowAsync();
    }

    private async void BookingDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        var deferral = args.GetDeferral();
        _bookingDialog.IsPrimaryButtonEnabled = false;

        try
        {
            if (_bookingDialog.BookingDatePicker.Date is not DateTimeOffset pickedDate)
            {
                ShowDialogError("请选择开始日期。");
                return;
            }

            if (!double.IsFinite(_bookingDialog.DurationBox.Value) || _bookingDialog.DurationBox.Value <= 0)
            {
                ShowDialogError("请输入有效的占用时长。");
                return;
            }

            var recurrence = _bookingDialog.RepeatOptions.SelectedIndex switch
            {
                1 => RecurrenceKind.Daily,
                2 => RecurrenceKind.Weekly,
                _ => RecurrenceKind.Once
            };

            if (recurrence == RecurrenceKind.Daily && _bookingDialog.DurationBox.Value > 24)
            {
                ShowDialogError("每天重复时，单次时长不能超过 24 小时，否则任务会与自己重叠。");
                return;
            }

            var startDate = DateOnly.FromDateTime(pickedDate.LocalDateTime);
            var endDate = recurrence != RecurrenceKind.Once && _bookingDialog.RepeatEndPicker.Date.HasValue
                ? DateOnly.FromDateTime(_bookingDialog.RepeatEndPicker.Date.Value.LocalDateTime)
                : (DateOnly?)null;

            if (endDate.HasValue && endDate.Value < startDate)
            {
                ShowDialogError("重复截止日期不能早于开始日期。");
                return;
            }

            var candidate = new ScheduleEntry
            {
                StartDate = startDate,
                StartTime = TimeOnly.FromTimeSpan(_bookingDialog.StartTimePicker.SelectedTime ?? new TimeSpan(9, 0, 0)),
                DurationHours = _bookingDialog.DurationBox.Value,
                Recurrence = recurrence,
                EndDate = endDate,
                Mode = _bookingDialog.ModeOptions.SelectedIndex == 1 ? UsageMode.Background : UsageMode.Foreground,
                Note = _bookingDialog.NoteBox.Text.Trim(),
                CreatedAt = _timeService.Now
            };

            var conflict = ScheduleConflictService.FindConflict(candidate, _schedules);
            if (conflict is not null)
            {
                var other = conflict.Existing;
                var mode = other.Entry.Mode == UsageMode.Foreground ? "前台操作" : "后台运行";
                ShowDialogError($"与 {ScheduleDayLayout.FormatFullSpan(other)} 的{mode}排班冲突，请调整时间或时长。");
                return;
            }

            _schedules.Add(candidate);
            await _repository.SaveAsync(_schedules);
            _selectedDate = candidate.StartDate;
            _displayMonth = new DateOnly(candidate.StartDate.Year, candidate.StartDate.Month, 1);
            RenderAll();
            args.Cancel = false;
        }
        catch (Exception ex)
        {
            ShowDialogError($"保存失败：{ex.Message}");
        }
        finally
        {
            _bookingDialog.IsPrimaryButtonEnabled = true;
            deferral.Complete();
        }
    }

    private void ShowDialogError(string message)
    {
        _bookingDialog.DialogInfo.Message = message;
        _bookingDialog.DialogInfo.IsOpen = true;
    }

    private async void DeleteSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Guid id })
        {
            return;
        }

        var entry = _schedules.FirstOrDefault(item => item.Id == id);
        if (entry is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "删除这条排班？",
            Content = entry.Recurrence == RecurrenceKind.Once
                ? "此操作只删除这次占用。"
                : "这是周期排班，删除后会移除整条重复规则。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            _schedules.Remove(entry);
            await _repository.SaveAsync(_schedules);
            RenderAll();
        }
        catch (Exception ex)
        {
            _schedules.Add(entry);
            await ShowMessageAsync("删除失败", ex.Message);
        }
    }

    private async void SyncTime_Click(object sender, RoutedEventArgs e)
    {
        await SyncTimeAsync();
    }

    private async Task SyncTimeAsync()
    {
        if (_isSyncing)
        {
            return;
        }

        _isSyncing = true;
        NetworkStatus.Text = "正在校准网络时间…";
        try
        {
            var success = await _timeService.SyncAsync();
            NetworkStatus.Text = success
                ? $"网络时间已校准 · {_timeService.LastSource} {_timeService.Now:HH:mm}"
                : _timeService.LastSync.HasValue
                    ? $"网络不可用 · 使用 {_timeService.LastSource} 上次校准"
                    : "网络不可用 · 使用本机时间";
            RenderAll();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(false);
    }

    private async Task CheckForUpdatesAsync(bool automatic)
    {
        UpdateStatus.Text = "更新：检查中…";
        var result = await _updateService.CheckAsync();

        if (!result.Success)
        {
            UpdateStatus.Text = "更新：检查失败";
            if (!automatic)
            {
                await ShowMessageAsync("检查更新失败",
                    $"{result.Error}\n\n可稍后重试，或访问 GitHub Releases 查看。");
            }
            return;
        }

        if (result.IsAvailable)
        {
            UpdateStatus.Text = $"更新：发现 {result.TagName}";
            if (automatic)
            {
                return;
            }

            await ShowUpdateDialogAsync(result);
        }
        else
        {
            UpdateStatus.Text = $"更新：已是最新 v{result.CurrentVersion.ToString(3)}";
            if (!automatic)
            {
                await ShowMessageAsync("已是最新版本",
                    $"当前版本 v{result.CurrentVersion.ToString(3)}。\n\n启动时会自动检查一次，也可随时点击“检查更新”。");
            }
        }
    }

    private async Task ShowUpdateDialogAsync(UpdateCheckResult result)
    {
        var description = new TextBlock
        {
            Text = $"当前版本 v{result.CurrentVersion.ToString(3)}，最新版本 {result.TagName}。\n\n"
                + "点击“立即更新”会自动下载并安装，完成后应用重新启动，排班数据不受影响。"
                + "勾选下方选项时，重启后会按新版本重绘桌面快捷方式与图标。",
            TextWrapping = TextWrapping.Wrap
        };

        var progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Visibility = Visibility.Collapsed
        };

        var progressText = new TextBlock
        {
            FontSize = 12,
            Opacity = 0.7,
            Visibility = Visibility.Collapsed
        };

        var shortcutOption = new CheckBox
        {
            Content = "更新后重建桌面快捷方式",
            IsChecked = true
        };

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(description);
        panel.Children.Add(shortcutOption);
        panel.Children.Add(progressBar);
        panel.Children.Add(progressText);

        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "发现新版本",
            Content = panel,
            PrimaryButtonText = "立即更新",
            CloseButtonText = "稍后",
            DefaultButton = ContentDialogButton.Primary
        };

        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            args.Cancel = true;

            if (result.PackageUri is null)
            {
                await GitHubUpdateService.LaunchReleasePageAsync(result.ReleaseUrl);
                deferral.Complete();
                return;
            }

            try
            {
                dialog.IsPrimaryButtonEnabled = false;
                dialog.CloseButtonText = string.Empty;
                shortcutOption.IsEnabled = false;
                progressBar.Visibility = Visibility.Visible;
                progressText.Visibility = Visibility.Visible;

                if (shortcutOption.IsChecked == true)
                {
                    ShortcutService.RequestRebuildAfterUpdate();
                }

                var progress = new Progress<double>(value =>
                {
                    progressBar.Value = value;
                    progressText.Text = $"正在下载… {value:P0}";
                });

                var packagePath = await PackageUpdater.DownloadAsync(result.PackageUri, progress, CancellationToken.None);
                progressText.Text = "正在校验安装包…";
                await PackageUpdater.VerifyAsync(packagePath, result.ChecksumsUri, CancellationToken.None);

                progressText.Text = "正在安装，应用即将重新启动…";
                PackageUpdater.StartInstallerAndExit(packagePath);
                await Task.Delay(800);
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                dialog.IsPrimaryButtonEnabled = true;
                dialog.CloseButtonText = "关闭";
                progressBar.Visibility = Visibility.Collapsed;
                progressText.Visibility = Visibility.Collapsed;
                shortcutOption.IsEnabled = true;
                description.Text = "更新失败：" + ex.Message
                    + "\n\n可稍后重试，或前往 GitHub Releases 手动下载安装包。";
            }
            finally
            {
                deferral.Complete();
            }
        };

        await dialog.ShowAsync();
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "知道了"
        };
        await dialog.ShowAsync();
    }
}
