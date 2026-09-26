namespace Me.Toolkit.Maui.Sizing;

/// <summary>
/// The parts of a window that content should keep clear of — a notch, a display cutout, the status
/// bar and home indicator — in layout units.
/// </summary>
/// <remarks>
/// <para>
/// MAUI 10's <c>SafeAreaEdges</c> decides whether a layout avoids these, but offers nothing
/// cross-platform that reports how big they are; its only getter is iOS-specific and per page. So
/// they are read from the platform: <c>UIWindow.SafeAreaInsets</c> on iOS and Mac Catalyst, the
/// system-bar and display-cutout insets on Android. A Windows window has none.
/// </para>
/// <para>
/// Platforms report new insets around the same time as the rotation that changes them, not always
/// before it, so a size using them is recomputed once more shortly after its window resizes.
/// </para>
/// </remarks>
internal static class SafeArea
{
    /// <summary>A window's safe-area insets in layout units. Settable so tests can supply them.</summary>
    internal static Func<Window, Thickness> InsetsOf { get; set; } = Platform;

    private static Thickness Platform(Window window)
    {
#if IOS || MACCATALYST
        if (window.Handler?.PlatformView is UIKit.UIWindow platform)
        {
            // Points, which on Mac Catalyst are not layout units - see LayoutUnits.
            var insets = platform.SafeAreaInsets;
            var scale = LayoutUnits.PointsPerUnit;

            return new Thickness(insets.Left / scale, insets.Top / scale, insets.Right / scale, insets.Bottom / scale);
        }
#elif ANDROID
        // Android 11 and later: the window's metrics, which know the insets at any time. The view's
        // root insets, below, only exist once the view has been laid out - and an element typically
        // joins its window before that, which on a phone read the insets as zero.
        if (OperatingSystem.IsAndroidVersionAtLeast(30)
            && window.Handler?.PlatformView is Android.App.Activity { WindowManager.CurrentWindowMetrics.WindowInsets: { } metrics }
            && window.DisplayDensity > 0)
        {
            var bars = metrics.GetInsets(Android.Views.WindowInsets.Type.SystemBars() | Android.Views.WindowInsets.Type.DisplayCutout());
            var scale = window.DisplayDensity;

            return new Thickness(bars.Left / scale, bars.Top / scale, bars.Right / scale, bars.Bottom / scale);
        }

        if (window.Handler?.PlatformView is Android.App.Activity { Window.DecorView: { } decor }
            && AndroidX.Core.View.ViewCompat.GetRootWindowInsets(decor) is { } windowInsets
            && window.DisplayDensity > 0)
        {
            // Pixels.
            var insets = windowInsets.GetInsets(
                AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars()
                | AndroidX.Core.View.WindowInsetsCompat.Type.DisplayCutout());
            var density = window.DisplayDensity;

            if (insets is not null)
            {
                return new Thickness(insets.Left / density, insets.Top / density, insets.Right / density, insets.Bottom / density);
            }
        }
#endif

        return Thickness.Zero;
    }
}
