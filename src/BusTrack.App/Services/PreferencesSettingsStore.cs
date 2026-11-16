using System.Text.Json;
using BusTrack.Model;

namespace BusTrack.App.Services;

public sealed class PreferencesSettingsStore : ISettingsStore
{
    private const string Key = "tracking_settings";

    public TrackingSettings Load()
    {
        var json = Preferences.Get(Key, null);
        if (json is null)
            return TrackingSettings.Default;

        try
        {
            return JsonSerializer.Deserialize<TrackingSettings>(json) ?? TrackingSettings.Default;
        }
        catch (JsonException)
        {
            return TrackingSettings.Default;
        }
    }

    public void Save(TrackingSettings settings)
        => Preferences.Set(Key, JsonSerializer.Serialize(settings));
}