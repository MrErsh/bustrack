using System.Collections.ObjectModel;
using BusTrack.Api;
using BusTrack.App.Services;
using BusTrack.Core;
using BusTrack.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusTrack.App.ViewModels;

/// <summary>Строка списка автобусов.</summary>
public sealed record BusItem(string Vehicle, string Stop, string Distance);

public partial class MainViewModel : ObservableObject
{
    private readonly IRouteCatalog _catalog;
    private readonly ISettingsStore _store;
    private readonly ITrackingService _tracking;
    private readonly IBackgroundMode _background;

    [ObservableProperty]
    private string _routeTitle = string.Empty;

    [ObservableProperty]
    private string? _watchedStopTitle;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string? _updatedAtText;

    [ObservableProperty]
    private string? _error;

    public ObservableCollection<BusItem> Buses { get; } = [];

    public MainViewModel(IRouteCatalog catalog, ISettingsStore store, ITrackingService tracking, IBackgroundMode background)
    {
        _catalog = catalog;
        _store = store;
        _tracking = tracking;
        _background = background;
        _tracking.SnapshotReceived += OnSnapshot;
        Refresh();
    }

    /// <summary>Вызывается при появлении экрана: настройки могли измениться.</summary>
    public void Refresh()
    {
        var settings = _store.Load();
        var route = _catalog.Find(settings.RouteId);
        RouteTitle = route is null ? $"Маршрут {settings.RouteId} не найден" : $"Маршрут {route.Number}";
        WatchedStopTitle = route is null || settings.WatchedStopId is null
            ? null
            : "Уведомление: " + (route.Directions.SelectMany(d => d)
                .FirstOrDefault(s => s.Id == settings.WatchedStopId)?.Name ?? settings.WatchedStopId);

        IsRunning = _tracking.IsRunning;
        if (_tracking.LastSnapshot is { } last)
            ApplySnapshot(last);
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        var settings = _store.Load();
        if (_catalog.Find(settings.RouteId) is null)
        {
            Error = "Маршрут не найден — выберите его в настройках";
            return;
        }

        // Android 13+: спрашиваем разрешение на уведомления; трекинг работает и без него.
        if (await Permissions.CheckStatusAsync<Permissions.PostNotifications>() != PermissionStatus.Granted)
            await Permissions.RequestAsync<Permissions.PostNotifications>();

        Error = null;
        _tracking.Start(settings);
        _background.Enable();
        IsRunning = true;
    }

    [RelayCommand]
    private void Stop()
    {
        _tracking.Stop();
        _background.Disable();
        IsRunning = false;
    }

    [RelayCommand]
    private Task SettingsAsync() => Shell.Current.GoToAsync("settings");

    private void OnSnapshot(TrackingSnapshot snapshot)
        => MainThread.BeginInvokeOnMainThread(() => ApplySnapshot(snapshot));

    private void ApplySnapshot(TrackingSnapshot snapshot)
    {
        Error = snapshot.Error;
        UpdatedAtText = "Обновлено " + snapshot.UpdatedAt.ToLocalTime().ToString("HH:mm:ss");

        Buses.Clear();
        foreach (var bus in snapshot.Buses)
        {
            Buses.Add(new BusItem(
                Vehicle: "Борт " + bus.Position.VehicleId,
                Stop: bus.NearestStop?.Name ?? "Остановка не определена",
                Distance: bus.NearestStop is null ? "" : $"{bus.DistanceToStopMeters:0} м"));
        }
    }
}