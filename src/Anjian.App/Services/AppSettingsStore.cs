using System.Text.Json;
using Anjian.Core;

namespace Anjian.App.Services;

public sealed class AppSettingsStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Anjian2026", "settings.json");

    public PlaybackOptions Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new PlaybackOptions();
            var value = JsonSerializer.Deserialize<PlaybackOptions>(File.ReadAllText(_path)) ?? new PlaybackOptions();
            value.Validate();
            return value;
        }
        catch
        {
            return new PlaybackOptions();
        }
    }

    public void Save(PlaybackOptions value)
    {
        value.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _path, overwrite: true);
    }
}
