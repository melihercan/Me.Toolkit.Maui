namespace Me.Toolkit.Maui.Sizing;

/// <summary>Which dimension of the reference a relative size is taken from.</summary>
public enum SizeAxis
{
    /// <summary>The reference's width.</summary>
    Width = 0,

    /// <summary>The reference's height.</summary>
    Height = 1,

    /// <summary>
    /// The smaller of width and height, like CSS <c>vmin</c>. Rotation swaps width and height but
    /// not which is shorter, so this is the usual choice for something that should keep its size
    /// when a phone turns.
    /// </summary>
    Shorter = 2,

    /// <summary>The larger of width and height, like CSS <c>vmax</c>.</summary>
    Longer = 3,
}
