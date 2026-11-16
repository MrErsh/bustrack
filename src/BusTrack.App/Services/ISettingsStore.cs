using BusTrack.Model;

namespace BusTrack.App.Services;

/// <summary>Хранение настроек трекинга между запусками.</summary>
public interface ISettingsStore
{
    TrackingSettings Load();
    void Save(TrackingSettings settings);
}