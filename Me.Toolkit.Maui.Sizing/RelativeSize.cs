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
    private readonly string? _breakpoints;
    private readonly (double MinWidth, double Percent)[] _breakpointList = [];

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
    /// Percentages by window width, like CSS media queries: <c>600:50 1200:33</c> is
    /// <see cref="Percent"/> below 600, 50 from 600 and 33 from 1200. Widths are the window's, in
    /// layout units; pairs are separated by spaces. <see langword="null"/>, the default, means none.
    /// </summary>
    /// <remarks>
    /// Cannot be combined with <see cref="Portrait"/> or <see cref="Landscape"/>, which would compete
    /// to choose the percentage. Until the element is in a window, <see cref="Percent"/> applies.
    /// </remarks>
    public string? Breakpoints
    {
        get => _breakpoints;
        init
        {
            _breakpointList = string.IsNullOrWhiteSpace(value) ? [] : ParseBreakpoints(value);
            _breakpoints = _breakpointList.Length == 0 ? null : value;
        }
    }

    /// <summary>
    /// How the result is rounded, before <see cref="Min"/> and <see cref="Max"/> so it never escapes
    /// them. Defaults to <see cref="SizeRounding.None"/>.
    /// </summary>
    /// <remarks>
    /// Rounding a value close to 100% of its parent can round up past the space available, by up to
    /// half a unit or pixel, which layout then trims.
    /// </remarks>
    public SizeRounding Round { get; init; }

    /// <summary>
    /// Which sides of a <see cref="Thickness"/> property — <c>Margin</c>, <c>Padding</c> — get the
    /// value; the others are 0. Defaults to <see cref="ThicknessSides.All"/>. Only meaningful for a
    /// <see cref="Thickness"/> property.
    /// </summary>
    public ThicknessSides Sides { get; init; } = ThicknessSides.All;

    /// <summary>
    /// What <see cref="Breakpoints"/> are compared against: the window's width, the default, or the
    /// reference's own width, like a CSS container query.
    /// </summary>
    public BreakpointSource BreakpointsBy { get; init; } = BreakpointSource.Window;

    /// <summary>
    /// With <see cref="SizeReference.Window"/>, measure the window less its safe area — a notch, a
    /// display cutout, the status bar and home indicator — so full-width content on a phone in
    /// landscape is not cut by the notch. Defaults to <see langword="false"/>; a desktop window has
    /// no safe area to subtract.
    /// </summary>
    public bool SafeArea { get; init; }

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

    /// <summary>
    /// Whether the percentage is chosen by the window — its orientation or its width — whatever the
    /// size is measured against.
    /// </summary>
    internal bool DependsOnWindow =>
        Portrait is not null
        || Landscape is not null
        || (_breakpointList.Length > 0 && BreakpointsBy == BreakpointSource.Window)
        || Round == SizeRounding.Pixels;

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
        string? breakpoints = null;
        SizeRounding? round = null;
        bool? safeArea = null;
        ThicknessSides? sides = null;
        BreakpointSource? breakpointsBy = null;

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
                case "breakpoints": breakpoints = value; break;
                case "round": round = Enumeration<SizeRounding>(name, value); break;
                case "sides": sides = Flags<ThicknessSides>(name, value); break;
                case "breakpointsby": breakpointsBy = Enumeration<BreakpointSource>(name, value); break;
                case "safearea":
                    safeArea = bool.TryParse(value, out var sa)
                        ? sa
                        : throw new FormatException($"{name}={value} is not True or False.");
                    break;
                default:
                    throw new FormatException(
                        $"Unknown option '{name}' in \"{text}\". Expected Percent, Portrait, Landscape, Breakpoints, BreakpointsBy, Offset, Round, Sides, SafeArea, To, Axis, Min or Max.");
            }
        }

        if (percent is null && portrait is null && landscape is null && breakpoints is null)
        {
            throw new FormatException($"\"{text}\" gives no percentage.");
        }

        try
        {
            return new RelativeSize(percent ?? 0)
            {
                Breakpoints = breakpoints,
                Round = round ?? SizeRounding.None,
                Sides = sides ?? ThicknessSides.All,
                BreakpointsBy = breakpointsBy ?? BreakpointSource.Window,
                SafeArea = safeArea ?? false,
                Portrait = portrait,
                Landscape = landscape,
                Offset = offset ?? 0,
                To = to ?? SizeReference.Parent,
                Axis = axis ?? SizeAxis.Width,
                Min = min ?? 0,
                Max = max ?? double.PositiveInfinity,
            };
        }
        catch (ArgumentException e)
        {
            // A value that parsed as a number but is out of range - a negative percent, a
            // malformed breakpoint - is still a problem with the text.
            throw new FormatException($"\"{text}\": {e.Message}", e);
        }

        static double Number(string name, string value) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? number
                : throw new FormatException($"{name}={value} is not a number.");

        // "Left Top" or "Left|Top" as well as the named combinations.
        static T Flags<T>(string name, string value)
            where T : struct, Enum
        {
            var combined = 0;
            foreach (var part in value.Split(['|', ' '], StringSplitOptions.RemoveEmptyEntries))
            {
                combined |= Convert.ToInt32(Enumeration<T>(name, part));
            }

            return (T)Enum.ToObject(typeof(T), combined);
        }

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

        if (_breakpointList.Length > 0 && (Portrait is not null || Landscape is not null))
        {
            throw new ArgumentException(
                "Set Breakpoints or Portrait/Landscape, not both: they would compete to choose the percentage.");
        }

        if (Sides == ThicknessSides.None || (Sides & ~ThicknessSides.All) != 0)
        {
            throw new ArgumentException($"Sides={Sides} names no side, or one that does not exist.");
        }

        if (SafeArea && (To != SizeReference.Window || Source is not null || AncestorType is not null))
        {
            throw new ArgumentException("SafeArea applies to To=Window, the only reference that includes a notch or cutout.");
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
    /// <param name="pixelsPerUnit">
    /// Physical pixels per layout unit in the element's window, for <see cref="SizeRounding.Pixels"/>.
    /// </param>
    internal double Resolve(Size reference, Size? window = null, double pixelsPerUnit = 1)
    {
        var percent = _breakpointList.Length > 0
            ? (BreakpointsBy == BreakpointSource.Reference ? ByWidth(reference.Width) : window is { } bw ? ByWidth(bw.Width) : Percent)
            : window switch
            {
                null => Percent,
                { } w when w.Width > w.Height => Landscape ?? Percent,
                { } => Portrait ?? Percent,
            };

        var dimension = Axis switch
        {
            SizeAxis.Width => reference.Width,
            SizeAxis.Height => reference.Height,
            SizeAxis.Shorter => Math.Min(reference.Width, reference.Height),
            SizeAxis.Longer => Math.Max(reference.Width, reference.Height),
            _ => throw new InvalidOperationException($"Unknown axis {Axis}."),
        };

        var value = dimension * percent / 100 + Offset;

        var rounded = Round switch
        {
            SizeRounding.Units => Math.Round(value, MidpointRounding.AwayFromZero),
            SizeRounding.Pixels when pixelsPerUnit > 0 => Math.Round(value * pixelsPerUnit, MidpointRounding.AwayFromZero) / pixelsPerUnit,
            SizeRounding.Pixels => Math.Round(value, MidpointRounding.AwayFromZero),
            _ => value,
        };

        return Math.Clamp(rounded, Min, Max);
    }

    private double ByWidth(double windowWidth)
    {
        var percent = Percent;

        foreach (var (minWidth, breakpointPercent) in _breakpointList)
        {
            if (windowWidth >= minWidth)
            {
                percent = breakpointPercent;
            }
        }

        return percent;
    }

    private static (double MinWidth, double Percent)[] ParseBreakpoints(string text)
    {
        var list = new List<(double, double)>();

        foreach (var pair in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = pair.IndexOf(':');
            if (colon < 0
                || !double.TryParse(pair[..colon], NumberStyles.Float, CultureInfo.InvariantCulture, out var width)
                || !double.TryParse(pair[(colon + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)
                || !double.IsFinite(width) || width < 0 || !double.IsFinite(percent) || percent < 0)
            {
                throw new ArgumentException(
                    $"'{pair}' in Breakpoints \"{text}\" is not width:percent, both non-negative numbers.");
            }

            list.Add((width, percent));
        }

        var sorted = list.OrderBy(b => b.Item1).ToArray();
        for (var i = 1; i < sorted.Length; i++)
        {
            if (sorted[i].Item1 == sorted[i - 1].Item1)
            {
                throw new ArgumentException($"Breakpoints \"{text}\" names width {sorted[i].Item1} twice.");
            }
        }

        return sorted;
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
