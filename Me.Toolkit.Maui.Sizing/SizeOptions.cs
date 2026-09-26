namespace Me.Toolkit.Maui.Sizing;

/// <summary>How a relative size is rounded, before <c>Min</c> and <c>Max</c> are applied.</summary>
public enum SizeRounding
{
    /// <summary>Not rounded.</summary>
    None = 0,

    /// <summary>
    /// To whole device-independent units: tidy values, and a size that steps rather than creeps
    /// during a resize. Not whole pixels on a display scaled by a fraction — see <see cref="Pixels"/>.
    /// </summary>
    Units = 1,

    /// <summary>
    /// To whole physical pixels of the element's window, so edges land on pixels on a 150% display
    /// too — 469.333 rather than 469, which is 704 pixels. Follows the window to a monitor with a
    /// different scale. Outside a window, rounds to whole units.
    /// </summary>
    Pixels = 2,
}

/// <summary>Which sides of a <see cref="Thickness"/> — a margin or padding — get a relative size.</summary>
[Flags]
public enum ThicknessSides
{
    /// <summary>No side.</summary>
    None = 0,

    /// <summary>The left side.</summary>
    Left = 1,

    /// <summary>The top side.</summary>
    Top = 2,

    /// <summary>The right side.</summary>
    Right = 4,

    /// <summary>The bottom side.</summary>
    Bottom = 8,

    /// <summary>Left and right.</summary>
    Horizontal = Left | Right,

    /// <summary>Top and bottom.</summary>
    Vertical = Top | Bottom,

    /// <summary>Every side.</summary>
    All = Horizontal | Vertical,
}

/// <summary>What <c>Breakpoints</c> are compared against.</summary>
public enum BreakpointSource
{
    /// <summary>The window's width, like a CSS media query on the viewport.</summary>
    Window = 0,

    /// <summary>
    /// The width of the reference the size is measured against, like a CSS container query: a card
    /// laid out one way when it is narrow and another when it is wide, wherever it is placed.
    /// </summary>
    Reference = 1,
}
