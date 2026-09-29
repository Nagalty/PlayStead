using System.IO;
using System.Text.Json;

namespace PlayStead.UI.Settings;

public sealed class UiPreferencesStore
{
    private readonly string _filePath;

    public UiPreferencesStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public event EventHandler<UiPreferences>? Changed;

    public async Task<UiPreferences> LoadAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(_filePath))
        {
            return new UiPreferences();
        }

        var json = await File.ReadAllTextAsync(
            _filePath,
            cancellationToken);

        try
        {
            return JsonSerializer.Deserialize<UiPreferences>(json)
                ?? new UiPreferences();
        }
        catch (JsonException)
        {
            return new UiPreferences();
        }
    }

    public async Task SaveAsync(
        UiPreferences preferences,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(preferences);

        await File.WriteAllTextAsync(
            _filePath,
            json,
            cancellationToken);

        Changed?.Invoke(this, preferences);
    }
}
