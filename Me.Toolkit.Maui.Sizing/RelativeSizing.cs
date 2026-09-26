using System.Reflection;

namespace Me.Toolkit.Maui.Sizing;

/// <summary>
/// Attached properties that apply a <see cref="RelativeSize"/>, so it can be set from a
/// <see cref="Style"/> — which <see cref="RelativeExtension"/> cannot be.
/// </summary>
/// <remarks>
/// <para>
/// A style's setters are shared by every element the style applies to, while a relative size has to
/// be tracked per element. An attached property bridges the two: the style shares the
/// <see cref="RelativeSize"/>, which is immutable, and setting the attached property on each element
/// creates that element's own binding.
/// </para>
/// <code>
/// &lt;Style TargetType="Label"&gt;
///     &lt;Setter Property="me:RelativeSizing.FontSize" Value="3, To=Window, Min=12, Max=40" /&gt;
/// &lt;/Style&gt;
/// </code>
/// <para>
/// The value is the text <see cref="RelativeSize.Parse"/> reads. They work directly on an element
/// too, as <c>me:RelativeSizing.WidthRequest="30"</c>. Clearing one — or a style being removed —
/// removes the binding it created.
/// </para>
/// </remarks>
public static class RelativeSizing
{
    /// <summary>A relative size for <see cref="VisualElement.WidthRequest"/>.</summary>
    public static readonly BindableProperty WidthRequestProperty =
        Create("WidthRequest", _ => VisualElement.WidthRequestProperty);

    /// <summary>A relative size for <see cref="VisualElement.HeightRequest"/>.</summary>
    public static readonly BindableProperty HeightRequestProperty =
        Create("HeightRequest", _ => VisualElement.HeightRequestProperty);

    /// <summary>A relative <see cref="View.Margin"/>, the same on every side.</summary>
    public static readonly BindableProperty MarginProperty =
        Create("Margin", _ => View.MarginProperty);

    /// <summary>
    /// A relative <c>Padding</c>, the same on every side, whichever element declares it - a layout,
    /// a <see cref="Border"/>, a <see cref="ContentView"/>, a page.
    /// </summary>
    public static readonly BindableProperty PaddingProperty =
        Create("Padding", element => StaticPropertyOf(element, "PaddingProperty", "Padding"));

    /// <summary>
    /// A relative size for the element's <c>FontSize</c>, whichever control declares it —
    /// <see cref="Label"/>, <see cref="Button"/>, <see cref="Entry"/> or a third-party control with a
    /// public static <c>FontSizeProperty</c>.
    /// </summary>
    public static readonly BindableProperty FontSizeProperty =
        Create("FontSize", FontSizeOf);

    /// <summary>Gets the relative width.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The relative size, or <see langword="null"/> if none is set.</returns>
    public static RelativeSize? GetWidthRequest(BindableObject element) => Get(element, WidthRequestProperty);

    /// <summary>Sets the relative width.</summary>
    /// <param name="element">The element.</param>
    /// <param name="value">The relative size, or <see langword="null"/> to remove it.</param>
    public static void SetWidthRequest(BindableObject element, RelativeSize? value) => Set(element, WidthRequestProperty, value);

    /// <summary>Gets the relative height.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The relative size, or <see langword="null"/> if none is set.</returns>
    public static RelativeSize? GetHeightRequest(BindableObject element) => Get(element, HeightRequestProperty);

    /// <summary>Sets the relative height.</summary>
    /// <param name="element">The element.</param>
    /// <param name="value">The relative size, or <see langword="null"/> to remove it.</param>
    public static void SetHeightRequest(BindableObject element, RelativeSize? value) => Set(element, HeightRequestProperty, value);

    /// <summary>Gets the relative margin.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The relative size, or <see langword="null"/> if none is set.</returns>
    public static RelativeSize? GetMargin(BindableObject element) => Get(element, MarginProperty);

    /// <summary>Sets the relative margin.</summary>
    /// <param name="element">The element.</param>
    /// <param name="value">The relative size, or <see langword="null"/> to remove it.</param>
    public static void SetMargin(BindableObject element, RelativeSize? value) => Set(element, MarginProperty, value);

    /// <summary>Gets the relative padding.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The relative size, or <see langword="null"/> if none is set.</returns>
    public static RelativeSize? GetPadding(BindableObject element) => Get(element, PaddingProperty);

    /// <summary>Sets the relative padding.</summary>
    /// <param name="element">The element.</param>
    /// <param name="value">The relative size, or <see langword="null"/> to remove it.</param>
    public static void SetPadding(BindableObject element, RelativeSize? value) => Set(element, PaddingProperty, value);

    /// <summary>Gets the relative font size.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The relative size, or <see langword="null"/> if none is set.</returns>
    public static RelativeSize? GetFontSize(BindableObject element) => Get(element, FontSizeProperty);

    /// <summary>Sets the relative font size.</summary>
    /// <param name="element">The element.</param>
    /// <param name="value">The relative size, or <see langword="null"/> to remove it.</param>
    public static void SetFontSize(BindableObject element, RelativeSize? value) => Set(element, FontSizeProperty, value);

    private static RelativeSize? Get(BindableObject element, BindableProperty property)
    {
        ArgumentNullException.ThrowIfNull(element);

        return (RelativeSize?)element.GetValue(property);
    }

    private static void Set(BindableObject element, BindableProperty property, RelativeSize? value)
    {
        ArgumentNullException.ThrowIfNull(element);

        element.SetValue(property, value);
    }

    private static BindableProperty Create(string name, Func<VisualElement, BindableProperty> target) =>
        BindableProperty.CreateAttached(
            name,
            typeof(RelativeSize),
            typeof(RelativeSizing),
            defaultValue: null,
            propertyChanged: (bindable, _, value) =>
            {
                var element = bindable as VisualElement
                    ?? throw new NotSupportedException(
                        $"RelativeSizing.{name} applies to a VisualElement, not {bindable.GetType().Name}.");

                var property = target(element);

                if (value is RelativeSize size)
                {
                    element.SetRelativeSize(property, size);
                }
                else
                {
                    // Not RemoveBinding: that leaves the last size behind, so a label taken out of a
                    // relative style would keep its last font size forever.
                    element.ClearRelativeSize(property);
                }
            });

    // Label, Button, Entry, Editor, SearchBar and the pickers all share one FontSizeProperty, but a
    // third-party control may declare its own, so the element's type is asked rather than assumed.
    private static BindableProperty FontSizeOf(VisualElement element) =>
        StaticPropertyOf(element, "FontSizeProperty", "FontSize");

    private static BindableProperty StaticPropertyOf(VisualElement element, string field, string name) =>
        element.GetType().GetField(field, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            ?.GetValue(null) as BindableProperty
        ?? throw new NotSupportedException(
            $"{element.GetType().Name} has no {name} property for RelativeSizing.{name} to set.");
}
