using Microsoft.Maui.Controls.Xaml;

namespace Me.Toolkit.Maui.Sizing;

/// <summary>
/// Sizes a property as a percentage of the parent, the window, the display or another element, in
/// XAML: <c>WidthRequest="{me:Relative 30}"</c> is 30% of the parent's width.
/// </summary>
/// <remarks>
/// <para>
/// The value is recomputed whenever the reference changes size — a window resized on a desktop, a
/// phone rotated, a parent laid out again — so it adapts the way CSS <c>%</c> and <c>vw</c> do.
/// </para>
/// <para>
/// It works on any <see cref="double"/> bindable property of a <see cref="VisualElement"/> -
/// <c>WidthRequest</c>, <c>HeightRequest</c>, <c>FontSize</c>, <c>Spacing</c> and so on - and on
/// <see cref="Thickness"/> ones, <c>Margin</c> and <c>Padding</c>, as a uniform thickness. It cannot be
/// used in a <see cref="Style"/> setter, because the size is tracked per element; for styles use
/// <see cref="RelativeSizing"/>'s attached properties.
/// </para>
/// </remarks>
[ContentProperty(nameof(Percent))]
[RequireService([typeof(IProvideValueTarget)])]
public sealed class RelativeExtension : IMarkupExtension<BindingBase>
{
    /// <summary>The percentage of the reference, so <c>30</c> is 30%.</summary>
    public double Percent { get; set; }

    /// <summary>
    /// The percentage while the window is portrait, instead of <see cref="Percent"/>. Unset means
    /// <see cref="Percent"/>. See <see cref="RelativeSize.Portrait"/>.
    /// </summary>
    public double? Portrait { get; set; }

    /// <summary>
    /// The percentage while the window is landscape, instead of <see cref="Percent"/>. Unset means
    /// <see cref="Percent"/>. See <see cref="RelativeSize.Landscape"/>.
    /// </summary>
    public double? Landscape { get; set; }

    /// <summary>
    /// A fixed amount added after the percentage, which may be negative: <c>{me:Relative 50, Offset=-8}</c>
    /// is half the space less a gap. See <see cref="RelativeSize.Offset"/>.
    /// </summary>
    public double Offset { get; set; }

    /// <summary>
    /// Percentages by window width, like CSS media queries: <c>Breakpoints='600:50 1200:33'</c>. See
    /// <see cref="RelativeSize.Breakpoints"/>.
    /// </summary>
    public string? Breakpoints { get; set; }

    /// <summary>Rounds the result to whole units. See <see cref="RelativeSize.Round"/>.</summary>
    public bool Round { get; set; }

    /// <summary>What the size is a percentage of. Defaults to <see cref="SizeReference.Parent"/>.</summary>
    public SizeReference To { get; set; } = SizeReference.Parent;

    /// <summary>Which dimension of the reference to use. Defaults to <see cref="SizeAxis.Width"/>.</summary>
    public SizeAxis Axis { get; set; } = SizeAxis.Width;

    /// <summary>The smallest value produced. Defaults to 0.</summary>
    public double Min { get; set; }

    /// <summary>The largest value produced. Defaults to no limit.</summary>
    public double Max { get; set; } = double.PositiveInfinity;

    /// <summary>An element to measure against instead of <see cref="To"/>, usually <c>{x:Reference Name}</c>.</summary>
    public VisualElement? Source { get; set; }

    /// <summary>Measure against the nearest ancestor of this type instead of <see cref="To"/>.</summary>
    public Type? AncestorType { get; set; }

    /// <inheritdoc/>
    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var target = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget
            ?? throw new InvalidOperationException("{Relative} needs to know the element it is applied to.");

        if (target.TargetObject is Setter)
        {
            throw new NotSupportedException(
                "{Relative} cannot be used in a Style Setter: the size is tracked per element. Use an attached property instead: <Setter Property=\"me:RelativeSizing.FontSize\" Value=\"3, To=Window\" />.");
        }

        // Inside OnIdiom or OnPlatform the target is that extension, not the element, and MAUI does
        // not expose the element behind it. The outer extension's result is also applied with
        // SetValue rather than SetBinding, so a binding could not survive the trip anyway. The other
        // nesting works, because there OnIdiom only supplies a number.
        if (target.TargetObject is IMarkupExtension outer)
        {
            throw new NotSupportedException(
                $"{{Relative}} cannot be nested inside {outer.GetType().Name.Replace("Extension", "")}. "
                + "Nest it the other way round, as a named argument: {me:Relative Percent={OnIdiom Phone=40, Desktop=20}}.");
        }

        var element = target.TargetObject as VisualElement
            ?? throw new NotSupportedException(
                $"{{Relative}} applies to a VisualElement, not {target.TargetObject?.GetType().Name ?? "null"}.");

        var property = target.TargetProperty as BindableProperty
            ?? throw new NotSupportedException("{Relative} applies to a bindable property.");

        return RelativeSizeExtensions.CreateBinding(element, property, new RelativeSize(Percent)
        {
            Portrait = Portrait,
            Landscape = Landscape,
            Offset = Offset,
            Breakpoints = Breakpoints,
            Round = Round,
            To = To,
            Axis = Axis,
            Min = Min,
            Max = Max,
            Source = Source,
            AncestorType = AncestorType,
        });
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
