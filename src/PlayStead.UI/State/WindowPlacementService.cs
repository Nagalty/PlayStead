using System.IO;
using System.Text.Json;

namespace PlayStead.UI.State;

public sealed class WindowPlacementService
{
    private readonly string _filePath;

    public WindowPlacementService(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public async Task SaveAsync(
        WindowPlacementState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        var directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state);

        await File.WriteAllTextAsync(
            _filePath,
            json,
            cancellationToken);
    }

    public async Task<WindowPlacementState?> LoadAsync(
        IReadOnlyList<WindowWorkArea> workAreas,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workAreas);

        if (!File.Exists(_filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(
            _filePath,
            cancellationToken);

        var saved = JsonSerializer.Deserialize<WindowPlacementState>(json);

        if (saved is null)
        {
            return null;
        }

        if (workAreas.Count == 0 ||
            workAreas.Any(area => Intersects(saved, area)))
        {
            return saved;
        }

        var primary = workAreas.FirstOrDefault(area => area.IsPrimary)
            ?? workAreas[0];

        return saved with
        {
            Left = primary.Left + ((primary.Width - saved.Width) / 2),
            Top = primary.Top + ((primary.Height - saved.Height) / 2)
        };
    }

    private static bool Intersects(
        WindowPlacementState window,
        WindowWorkArea area)
    {
        var right = Math.Min(
            window.Left + window.Width,
            area.Left + area.Width);

        var left = Math.Max(
            window.Left,
            area.Left);

        var bottom = Math.Min(
            window.Top + window.Height,
            area.Top + area.Height);

        var top = Math.Max(
            window.Top,
            area.Top);

        return right > left && bottom > top;
    }
}
