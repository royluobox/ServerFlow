using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ServerScheduler.Controls;

public sealed partial class BookingDialog : ContentDialog
{
    public BookingDialog()
    {
        InitializeComponent();
        BookingDatePicker.DateChanged += (_, _) => UpdatePreview();
        StartTimePicker.SelectedTimeChanged += (_, _) => UpdatePreview();
        DurationBox.ValueChanged += (_, _) => UpdatePreview();
        Opened += (_, _) => UpdatePreview();
    }

    private void RepeatOptions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RepeatEndPicker is null || WeeklyHint is null)
        {
            return;
        }

        RepeatEndPicker.Visibility = RepeatOptions.SelectedIndex == 0 ? Visibility.Collapsed : Visibility.Visible;
        WeeklyHint.Visibility = RepeatOptions.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>实时显示结束时间，长时长排班能提前看到会跨到哪一天。</summary>
    private void UpdatePreview()
    {
        if (EndPreview is null)
        {
            return;
        }

        var date = BookingDatePicker.Date?.LocalDateTime.Date;
        var time = StartTimePicker.SelectedTime;
        var hours = DurationBox.Value;

        EndPreview.Text = date is null || time is null
            ? Services.BookingPreview.Empty
            : Services.BookingPreview.Describe(DateOnly.FromDateTime(date.Value), time.Value, hours);
    }
}
