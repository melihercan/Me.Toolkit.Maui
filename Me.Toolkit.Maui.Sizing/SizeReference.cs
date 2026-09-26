namespace Me.Toolkit.Maui.Sizing;

/// <summary>What a relative size is a percentage of.</summary>
public enum SizeReference
{
    /// <summary>
    /// The element's parent, less the parent's padding: the space the element is actually laid out
    /// in, as a CSS percentage refers to the containing block's content box.
    /// </summary>
    Parent = 0,

    /// <summary>The window the element is in, like CSS <c>vw</c> and <c>vh</c>.</summary>
    Window = 1,

    /// <summary>
    /// The main display, in device-independent units. It changes on rotation; on a desktop with
    /// several monitors it is the main one, not necessarily the one the window is on.
    /// </summary>
    Display = 2,

    /// <summary>
    /// The element's own laid-out size, for a fixed aspect ratio: a <c>HeightRequest</c> of 56.25%
    /// of its own width is 16:9. It must measure the other dimension than the one it sets —
    /// <c>HeightRequest</c> against <see cref="SizeAxis.Width"/>, <c>WidthRequest</c> against
    /// <see cref="SizeAxis.Height"/> — or it would chase itself.
    /// </summary>
    Self = 3,
}
