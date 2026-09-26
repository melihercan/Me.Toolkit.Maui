namespace Me.Toolkit.Maui.Sizing;

/// <summary>
/// Converts the window and display sizes the platform reports into the units pages are laid out in.
/// </summary>
/// <remarks>
/// <para>
/// Everywhere but Mac Catalyst they are the same units. Mac Catalyst shows an app with the iPad
/// idiom — MAUI's template default, <c>UIDeviceFamily</c> 2 — scaled to 77%, and reports the window
/// and display in Mac points while its pages are laid out in iPad points, 1.3 times as many. Found
/// on a Mac mini: a window reporting 1024 wide held a parent measuring 1267, and 3% of the window
/// came out as 30.7 in a layout where 3% of its width is 39.9.
/// </para>
/// <para>
/// 0.77 is Apple's fixed factor for that mode; an app using the Mac idiom is not scaled. The root
/// page's own size would sidestep the question, but a <c>Shell</c> — the root of most MAUI apps,
/// this repository's demo included — does not report one: tried, and every window-relative size in
/// the demo went to zero.
/// </para>
/// </remarks>
internal static class LayoutUnits
{
    /// <summary>
    /// How many reported points make one layout unit. Settable so tests can reproduce Mac Catalyst
    /// from a net10.0 test project.
    /// </summary>
    internal static double PointsPerUnit { get; set; } = Platform();

    internal static Size ToLayoutUnits(double width, double height) =>
        new(width / PointsPerUnit, height / PointsPerUnit);

    /// <summary>
    /// A window's size in layout units, or <see langword="null"/> before the platform has sized it.
    /// </summary>
    /// <remarks>
    /// On iOS and Mac Catalyst the native window's bounds, which UIKit reports in exactly the units
    /// pages are laid out in. Not <see cref="Window.Width"/>: on Mac Catalyst MAUI reports that in
    /// layout units when a window is restored at launch and in Mac points after it is resized, so no
    /// fixed conversion is right for both - a 600-point window read 1010 wide at launch and 779 after
    /// a resize. Everywhere else, and before the native window exists, <see cref="Window.Width"/>.
    /// </remarks>
    internal static Size? WindowSize(Window window)
    {
#if IOS || MACCATALYST
        if (window.Handler?.PlatformView is UIKit.UIWindow platform
            && platform.Bounds.Width > 0
            && platform.Bounds.Height > 0)
        {
            return new Size(platform.Bounds.Width, platform.Bounds.Height);
        }
#endif

        return window.Width < 0 || window.Height < 0 ? null : ToLayoutUnits(window.Width, window.Height);
    }

    /// <summary>
    /// A window's display density: physical pixels per point. Settable so tests can supply one;
    /// <see cref="Window.DisplayDensity"/> asks the platform, and is 1 without it.
    /// </summary>
    internal static Func<Window, double> DensityOf { get; set; } = static window => window.DisplayDensity;

    /// <summary>Physical pixels per layout unit in a window, for rounding to whole pixels.</summary>
    internal static double PixelsPerUnit(Window window) => DensityOf(window) * PointsPerUnit;

    private static double Platform()
    {
#if MACCATALYST
        return UIKit.UIDevice.CurrentDevice.UserInterfaceIdiom == UIKit.UIUserInterfaceIdiom.Mac ? 1 : 0.77;
#else
        return 1;
#endif
    }
}
