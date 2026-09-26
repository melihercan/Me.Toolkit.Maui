namespace Me.Toolkit.Maui.Sizing;

/// <summary>
/// The size of the display a window is on, in device-independent units, and when the main display
/// changes.
/// </summary>
/// <remarks>
/// A seam, because <c>DeviceDisplay.SetCurrent</c> is internal and the net10.0 slice's
/// <see cref="DeviceDisplay.Current"/> throws, so a test has no other way to supply a display.
/// </remarks>
internal interface IDisplaySize
{
    Size? SizeFor(Window window);

    event EventHandler? Changed;
}

internal static class DisplaySize
{
    internal static IDisplaySize Current { get; set; } = new DeviceDisplaySize();
}

internal sealed class DeviceDisplaySize : IDisplaySize
{
    private EventHandler? _changed;

    public Size? SizeFor(Window window)
    {
#if WINDOWS
        // The monitor the window is actually on, which MAUI's DeviceDisplay cannot say: it only
        // knows the main one. DisplayArea reports physical pixels, and the window's own density is
        // that monitor's scale, so a window on a 100% monitor beside a 150% main one measures its own.
        if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window platform
            && platform.AppWindow is { } appWindow
            && window.DisplayDensity > 0
            && Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                appWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest) is { } area)
        {
            return new Size(area.OuterBounds.Width / window.DisplayDensity, area.OuterBounds.Height / window.DisplayDensity);
        }
#endif

        // Everywhere else, and on Windows before the window has a platform view: the main display.
        // A phone has one; Mac Catalyst offers no per-window screen to ask.
        var info = DeviceDisplay.Current.MainDisplayInfo;

        return info.Density > 0 && info.Width > 0 && info.Height > 0
            ? new Size(info.Width / info.Density, info.Height / info.Density)
            : null;
    }

    // One subscription to the static event however many elements listen, taken with the first and
    // dropped with the last, so nothing is held by a static once no element is in a window.
    public event EventHandler? Changed
    {
        add
        {
            if (_changed is null)
            {
                DeviceDisplay.Current.MainDisplayInfoChanged += OnMainDisplayInfoChanged;
            }

            _changed += value;
        }
        remove
        {
            _changed -= value;

            if (_changed is null)
            {
                DeviceDisplay.Current.MainDisplayInfoChanged -= OnMainDisplayInfoChanged;
            }
        }
    }

    private void OnMainDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e) =>
        _changed?.Invoke(this, EventArgs.Empty);
}
