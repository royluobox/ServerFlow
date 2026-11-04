using System.Text.Json;
using ServerScheduler.Models;

namespace ServerScheduler.Services;

public sealed class ScheduleRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string DataFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ServerFlow",
        "schedules.json");

    public async Task<List<ScheduleEntry>> LoadAsync()
    {
        if (!File.Exists(DataFilePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(DataFilePath);
        return await JsonSerializer.DeserializeAsync<List<ScheduleEntry>>(stream, JsonOptions) ?? [];
    }

    public async Task SaveAsync(IEnumerable<ScheduleEntry> schedules)
    {
        var directory = Path.GetDirectoryName(DataFilePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = DataFilePath + ".tmp";

        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, schedules.ToList(), JsonOptions);
            await stream.FlushAsync();
        }

        File.Move(temporaryPath, DataFilePath, true);
    }
}
