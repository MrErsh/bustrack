using BusTrack.App.ViewModels;

namespace BusTrack.App.Converters;

/// <summary>Заголовки направлений — жирным, остановки — обычным текстом.</summary>
public sealed class RouteInfoTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Header { get; set; }
    public DataTemplate? Stop { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        => item is RouteInfoItem { IsHeader: true } ? Header! : Stop!;
}