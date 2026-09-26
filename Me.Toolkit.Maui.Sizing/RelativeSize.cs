namespace Me.Toolkit.Maui.Sizing;

/// <summary>
/// A size expressed as a percentage of something else — the parent, the window, the display, a
/// named element or the nearest ancestor of a type — and recomputed whenever that changes.
/// </summary>
/// <remarks>
/// Apply it with <see cref="RelativeSizeExtensions.SetRelativeSize{T}"/> from code, or with
/// <see cref="RelativeExtension"/> from XAML, which builds one of these.
/// </remarks>
public sealed class RelativeSize
{
    private readonly double _percent;
    private readonly double _min;
    private readonly double _max = double.PositiveInfinity;

    /// <summary>Creates a relative size.</summary>
    /// <param name="percent">The percentage of the reference, so <c>30</c> is 30%.</param>
    public RelativeSize(double percent)
    {
        Percent = percent;
    }

    /// <summary>The percentage of the reference, so <c>30</c> is 30%. Not negative.</summary>
    public double Percent
    {
        get => _percent;
        init => _percent = FiniteNonNegative(value);
    }

    /// <summary>
    /// What the size is a percentage of. Defaults to <see cref="SizeReference.Parent"/>. Leave it
    /// at the default when <see cref="Source"/> or <see cref="AncestorType"/> is set.
    /// </summary>
    public SizeReference To { get; init; } = SizeReference.Parent;

    /// <summary>Which dimension of the reference to use. Defaults to <see cref="SizeAxis.Width"/>.</summary>
    public SizeAxis Axis { get; init; } = SizeAxis.Width;

    /// <summary>The smallest value produced, whatever the reference does. Defaults to 0.</summary>
    public double Min
    {
        get => _min;
        init => _min = FiniteNonNegative(value);
    }

    /// <summary>
    /// The largest value produced, whatever the reference does. Defaults to no limit. Together with
    /// <see cref="Min"/> this is CSS's <c>clamp()</c>: a relative font is unreadable in a small
    /// window and absurd on a 4K display without one.
    /// </summary>
    public double Max
    {
        get => _max;
        init => _max = double.IsNaN(value) || value < 0
            ? throw new ArgumentOutOfRangeException(nameof(value), value, "Must not be negative.")
            : value;
    }

    /// <summary>
    /// A specific element to measure against instead of <see cref="To"/>, such as one named with
    /// <c>x:Reference</c>. Its padding is excluded, as the parent's is.
    /// </summary>
    public VisualElement? Source { get; init; }

    /// <summary>
    /// Measure against the nearest ancestor of this type instead of <see cref="To"/>, found by
    /// walking up from the element's parent. Its padding is excluded, as the parent's is.
    /// </summary>
    public Type? AncestorType { get; init; }

    /// <summary>Checks the combination, which the individual setters cannot.</summary>
    internal void Validate()
    {
        if (Max < Min)
        {
            throw new ArgumentException($"Max ({Max}) is less than Min ({Min}).");
        }

        if (Source is not null && AncestorType is not null)
        {
            throw new ArgumentException("Set Source or AncestorType, not both.");
        }

        if ((Source is not null || AncestorType is not null) && To != SizeReference.Parent)
        {
            throw new ArgumentException(
                $"To={To} conflicts with {(Source is not null ? "Source" : "AncestorType")}, which names the reference itself. Leave To unset.");
        }

        if (AncestorType is not null && !typeof(VisualElement).IsAssignableFrom(AncestorType))
        {
            throw new ArgumentException(
                $"AncestorType {AncestorType} is not a VisualElement, so it has no size to measure.");
        }
    }

    /// <summary>Applies the axis, the percentage and the clamp to a reference size.</summary>
    internal double Resolve(Size reference)
    {
        var dimension = Axis switch
        {
            SizeAxis.Width => reference.Width,
            SizeAxis.Height => reference.Height,
            SizeAxis.Shorter => Math.Min(reference.Width, reference.Height),
            SizeAxis.Longer => Math.Max(reference.Width, reference.Height),
            _ => throw new InvalidOperationException($"Unknown axis {Axis}."),
        };

        return Math.Clamp(dimension * Percent / 100, Min, Max);
    }

    private static double FiniteNonNegative(double value) =>
        double.IsFinite(value) && value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Must be finite and not negative.");
}
