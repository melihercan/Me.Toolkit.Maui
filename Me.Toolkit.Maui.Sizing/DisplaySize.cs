namespace Me.Toolkit.Maui.Sizing;

/// <summary>The main display's size in device-independent units, and when it changes.</summary>
/// <remarks>
/// A seam, because <c>DeviceDisplay.SetCurrent</c> is internal and the net10.0 slice's
/// <see cref="DeviceDisplay.Current"/> throws, so a test has no other way to supply a display.
/// </remarks>
internal interface IDisplaySize
{
    Size? Current { get; }

    event EventHandler? Changed;
}

internal static class DisplaySize
{
    internal static IDisplaySize Current { get; set; } = new DeviceDisplaySize();
}

internal sealed class DeviceDisplaySize : IDisplaySize
{
    private EventHandler? _changed;

    public Size? Current
    {
        get
        {
            // DisplayInfo is in physical pixels; layout deals in device-independent units.
            var info = DeviceDisplay.Current.MainDisplayInfo;

            return info.Density > 0 && info.Width > 0 && info.Height > 0
                ? new Size(info.Width / info.Density, info.Height / info.Density)
                : null;
        }
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
