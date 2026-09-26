using System.ComponentModel;
using System.Globalization;

namespace Me.Toolkit.Maui.Sizing;

/// <summary>
/// A size expressed as a percentage of something else — the parent, the window, the display, the
/// element itself, a named element or the nearest ancestor of a type — and recomputed whenever
/// that changes.
/// </summary>
/// <remarks>
/// Apply it with <see cref="RelativeSizeExtensions.SetRelativeSize{T}"/> from code, with
/// <see cref="RelativeExtension"/> from XAML, or with <see cref="RelativeSizing"/>'s attached
/// properties from a <see cref="Style"/>. The last converts from the text form <see cref="Parse"/>
/// reads.
/// </remarks>
[TypeConverter(typeof(RelativeSizeTypeConverter))]
public sealed class RelativeSize
{
    private readonly double _percent;
    private readonly double? _portrait;
    private readonly double? _landscape;
    private readonly double _offset;
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
    /// The percentage to use instead of <see cref="Percent"/> while the window is portrait — taller
    /// than it is wide, or square. <see langword="null"/>, the default, means <see cref="Percent"/>.
    /// </summary>
    /// <remarks>
    /// Orientation is the shape of the element's <em>window</em>, whatever the size is measured
    /// against: a parent card is wider than it is tall on a portrait phone too. On a phone that is
    /// the screen's orientation; on a desktop a tall, narrow window is portrait. Until the element is
    /// in a window, <see cref="Percent"/> applies.
    /// </remarks>
    public double? Portrait
    {
        get => _portrait;
        init => _portrait = value is { } percent ? FiniteNonNegative(percent) : null;
    }

    /// <summary>
    /// The percentage to use instead of <see cref="Percent"/> while the window is landscape — wider
    /// than it is tall. <see langword="null"/>, the default, means <see cref="Percent"/>.
    /// </summary>
    /// <remarks>See <see cref="Portrait"/> for what decides the orientation.</remarks>
    public double? Landscape
    {
        get => _landscape;
        init => _landscape = value is { } percent ? FiniteNonNegative(percent) : null;
    }

    /// <summary>
    /// A fixed amount added after the percentage and before the clamp, which may be negative: a
    /// percent of 50 with an offset of -8 is CSS's <c>calc(50% - 8px)</c>, half the space less a
    /// gap. Defaults to 0.
    /// </summary>
    public double Offset
    {
        get => _offset;
        init => _offset = double.IsFinite(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Must be finite.");
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

    /// <summary>Whether the result depends on the window's orientation.</summary>
    internal bool DependsOnOrientation => Portrait is not null || Landscape is not null;

    /// <summary>
    /// Reads the text form used by <see cref="RelativeSizing"/>'s attached properties: an optional
    /// leading percentage, then comma-separated <c>Name=Value</c> pairs, with the same names as
    /// <see cref="RelativeExtension"/>.
    /// </summary>
    /// <param name="text">For example <c>3, To=Window, Min=12, Max=40</c> or <c>Portrait=90, Landscape=45</c>.</param>
    /// <returns>The relative size.</returns>
    /// <exception cref="FormatException">The text names an unknown option or an unreadable value.</exception>
    /// <remarks>
    /// <see cref="Source"/> and <see cref="AncestorType"/> cannot be written this way, since text
    /// cannot name an element or a type; use <see cref="RelativeExtension"/> for those.
    /// </remarks>
    public static RelativeSize Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        double? percent = null, portrait = null, landscape = null, offset = null, min = null, max = null;
        SizeReference? to = null;
        SizeAxis? axis = null;

        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var equals = part.IndexOf('=');

            if (equals < 0)
            {
                percent = i == 0
                    ? Number(nameof(Percent), part)
                    : throw new FormatException($"'{part}' in \"{text}\": only the first value may be unnamed, and it is the percentage.");
                continue;
            }

            var name = part[..equals].Trim();
            var value = part[(equals + 1)..].Trim();

            switch (name.ToLowerInvariant())
            {
                case "percent": percent = Number(name, value); break;
                case "portrait": portrait = Number(name, value); break;
                case "landscape": landscape = Number(name, value); break;
                case "offset": offset = Number(name, value); break;
                case "min": min = Number(name, value); break;
                case "max": max = Number(name, value); break;
                case "to": to = Enumeration<SizeReference>(name, value); break;
                case "axis": axis = Enumeration<SizeAxis>(name, value); break;
                default:
                    throw new FormatException(
                        $"Unknown option '{name}' in \"{text}\". Expected Percent, Portrait, Landscape, Offset, To, Axis, Min or Max.");
            }
        }

        if (percent is null && portrait is null && landscape is null)
        {
            throw new FormatException($"\"{text}\" gives no percentage.");
        }

        return new RelativeSize(percent ?? 0)
        {
            Portrait = portrait,
            Landscape = landscape,
            Offset = offset ?? 0,
            To = to ?? SizeReference.Parent,
            Axis = axis ?? SizeAxis.Width,
            Min = min ?? 0,
            Max = max ?? double.PositiveInfinity,
        };

        static double Number(string name, string value) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? number
                : throw new FormatException($"{name}={value} is not a number.");

        static T Enumeration<T>(string name, string value)
            where T : struct, Enum =>
            Enum.TryParse<T>(value, ignoreCase: true, out var result) && Enum.IsDefined(result)
                ? result
                : throw new FormatException($"{name}={value} is not one of {string.Join(", ", Enum.GetNames<T>())}.");
    }

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

    /// <summary>Applies the axis, the percentage, the offset and the clamp to a reference size.</summary>
    /// <param name="reference">The size being measured against.</param>
    /// <param name="window">
    /// The element's window size, which decides the orientation, or <see langword="null"/> when it
    /// is not in one — in which case <see cref="Percent"/> applies.
    /// </param>
    internal double Resolve(Size reference, Size? window = null)
    {
        var percent = window switch
        {
            { } w when w.Width > w.Height => Landscape ?? Percent,
            { } => Portrait ?? Percent,
            null => Percent,
        };

        var dimension = Axis switch
        {
            SizeAxis.Width => reference.Width,
            SizeAxis.Height => reference.Height,
            SizeAxis.Shorter => Math.Min(reference.Width, reference.Height),
            SizeAxis.Longer => Math.Max(reference.Width, reference.Height),
            _ => throw new InvalidOperationException($"Unknown axis {Axis}."),
        };

        return Math.Clamp(dimension * percent / 100 + Offset, Min, Max);
    }

    private static double FiniteNonNegative(double value) =>
        double.IsFinite(value) && value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Must be finite and not negative.");
}

/// <summary>
/// Converts the text form of a <see cref="RelativeSize"/>, so a XAML attribute or a
/// <see cref="Setter"/> value can be written as <c>"3, To=Window, Min=12"</c>.
/// </summary>
public sealed class RelativeSizeTypeConverter : TypeConverter
{
    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc/>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text ? RelativeSize.Parse(text) : base.ConvertFrom(context, culture, value);
}
