using System.Collections.ObjectModel;
using BusTrack.Api;
using BusTrack.App.Services;
using BusTrack.Core;
using BusTrack.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusTrack.App.ViewModels;

/// <summary>Обёртки для Picker: MAUI показывает строку, а не record.</summary>
public sealed record RouteItem(BusRoute Route)
{
    public string Label => $"{Route.Number} — {Route.Name}";
}

public sealed record StopItem(Stop? Stop)
{
    public string Label => Stop?.Name ?? "Не уведомлять";
}

public partial class SettingsViewModel : ObservableObject
{
    public static readonly int[] MinutesOptions = [1, 2, 3, 5, 7, 10, 15];

    private readonly ISettingsStore _store;
    private readonly ITrackingService _tracking;

    public IReadOnlyList<RouteItem> RouteItems { get; }
    public IReadOnlyList<int> Minutes => MinutesOptions;

    [ObservableProperty]
    private ObservableCollection<StopItem> _stopItems = [new(null)];

    [ObservableProperty]
    private RouteItem? _selectedRouteItem;

    [ObservableProperty]
    private StopItem? _selectedStopItem;

    [ObservableProperty]
    private int _selectedMinutes;

    [ObservableProperty]
    private string? _error;

    public SettingsViewModel(IRouteCatalog catalog, ISettingsStore store, ITrackingService tracking)
    {
        _store = store;
        _tracking = tracking;
        RouteItems = catalog.Routes.Select(r => new RouteItem(r)).ToList();

        var settings = store.Load();
        SelectedRouteItem = RouteItems.FirstOrDefault(r => r.Route.Id == settings.RouteId);
        RebuildStops(settings.WatchedStopId);
        SelectedMinutes = MinutesOptions.Contains(settings.NotifyBeforeMinutes)
            ? settings.NotifyBeforeMinutes
            : 5;
    }

    partial void OnSelectedRouteItemChanged(RouteItem? value)
    {
        // При смене маршрута прежняя остановка невалидна — сбрасываем.
        RebuildStops(null);
    }

    private void RebuildStops(string? selectStopId)
    {
        var stops = SelectedRouteItem?.Route.Directions
            .SelectMany(d => d)
            .DistinctBy(s => s.Id)
            .Select(s => new StopItem(s))
            .ToList() ?? [];

        StopItems = new ObservableCollection<StopItem>([.. new StopItem[] { new(null) }, .. stops]);
        SelectedStopItem = StopItems.FirstOrDefault(s => s.Stop?.Id == selectStopId) ?? StopItems[0];
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedRouteItem is null)
        {
            Error = "Выберите маршрут";
            return;
        }

        var settings = new TrackingSettings(
            SelectedRouteItem.Route.Id,
            SelectedStopItem?.Stop?.Id,
            SelectedMinutes);

        _store.Save(settings);
        if (_tracking.IsRunning)
            _tracking.UpdateSettings(settings);

        await Shell.Current.GoToAsync("..");
    }
}