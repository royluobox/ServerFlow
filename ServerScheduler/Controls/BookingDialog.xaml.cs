using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ServerScheduler.Controls;

public sealed partial class BookingDialog : ContentDialog
{
    public BookingDialog()
    {
        InitializeComponent();
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
}
