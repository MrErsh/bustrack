using BusTrack.Model;

namespace BusTrack.Core;

public interface ITrackingService
{
    bool IsRunning { get; }
    TrackingSettings? Settings { get; }
    TrackingSnapshot? LastSnapshot { get; }

    /// <summary>Снимок после каждого опроса. Вызывается из фонового потока.</summary>
    event Action<TrackingSnapshot>? SnapshotReceived;

    void Start(TrackingSettings settings);
    void Stop();
    void UpdateSettings(TrackingSettings settings);
}

/// <summary>Фоновый поллинг на PeriodicTimer. Старт/стоп — по кнопке пользователя.</summary>
public sealed class TrackingService : ITrackingService, IDisposable
{
    private readonly TrackingEngine _engine;
    private readonly TimeSpan _interval;
    private readonly Lock _gate = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private TrackingSettings? _settings;
    private TrackingSnapshot? _last;

    public TrackingService(TrackingEngine engine, TimeSpan? interval = null)
    {
        _engine = engine;
        _interval = interval ?? TimeSpan.FromSeconds(30);
    }

    public bool IsRunning
    {
        get { lock (_gate) return _cts is not null; }
    }

    public TrackingSettings? Settings
    {
        get { lock (_gate) return _settings; }
    }

    public TrackingSnapshot? LastSnapshot
    {
        get { lock (_gate) return _last; }
    }

    public event Action<TrackingSnapshot>? SnapshotReceived;

    public void Start(TrackingSettings settings)
    {
        lock (_gate)
        {
            StopCore();
            _settings = settings;
            _engine.ResetNotifications();
            var cts = new CancellationTokenSource();
            _cts = cts;
            _loop = RunLoopAsync(settings, cts.Token);
        }
    }

    public void Stop()
    {
        lock (_gate) StopCore();
    }

    public void UpdateSettings(TrackingSettings settings)
    {
        lock (_gate)
        {
            _settings = settings;
            _engine.ResetNotifications();
            if (_cts is not null)
            {
                StopCore();
                var cts = new CancellationTokenSource();
                _cts = cts;
                _loop = RunLoopAsync(settings, cts.Token);
            }
        }
    }

    private void StopCore()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _loop = null;
    }

    private async Task RunLoopAsync(TrackingSettings settings, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (true)
            {
                var snapshot = await _engine.PollAsync(settings, ct);
                Publish(snapshot);
                if (!await timer.WaitForNextTickAsync(ct))
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // нормальная остановка
        }
    }

    private void Publish(TrackingSnapshot snapshot)
    {
        TrackingSnapshot merged;
        lock (_gate)
        {
            // При ошибке API сохраняем последние известные позиции, но показываем ошибку.
            merged = snapshot.Error is not null && _last is not null
                ? snapshot with { Buses = _last.Buses }
                : snapshot;
            _last = merged;
        }
        SnapshotReceived?.Invoke(merged);
    }

    public void Dispose() => Stop();
}