using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ServerScheduler.Models;

public sealed class ScheduleListItem
{
    public required Guid Id { get; init; }
    public required string TimeRange { get; init; }
    public required string ModeText { get; init; }
    public required string RecurrenceText { get; init; }
    public required string Note { get; init; }
    public required Visibility NoteVisibility { get; init; }
    public required Brush ModeForeground { get; init; }
    public required Brush ModeBackground { get; init; }
}
