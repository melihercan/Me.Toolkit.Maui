using Microsoft.Maui.Controls.Internals;

namespace Me.Toolkit.Maui.Sizing;

/// <summary>Relative sizing from code, the counterpart of <see cref="RelativeExtension"/>.</summary>
public static class RelativeSizeExtensions
{
    /// <summary>
    /// Binds <paramref name="property"/> to a percentage of the parent, window, display or another
    /// element, recomputed whenever that reference changes size.
    /// </summary>
    /// <typeparam name="T">The element's type, returned so calls can be chained.</typeparam>
    /// <param name="element">The element to size.</param>
    /// <param name="property">A <see cref="double"/> property, such as <c>WidthRequest</c> or <c>FontSize</c>.</param>
    /// <param name="size">The relative size.</param>
    /// <returns>The same element.</returns>
    /// <remarks>
    /// Until the reference has a size — before the first layout, or while the element is not in a
    /// window — the property has its default value. Setting the property directly afterwards
    /// replaces the relative size, as it would any binding.
    /// </remarks>
    public static T SetRelativeSize<T>(this T element, BindableProperty property, RelativeSize size)
        where T : VisualElement
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(size);

        element.SetBinding(property, CreateBinding(element, property, size));

        return element;
    }

    internal static BindingBase CreateBinding(VisualElement element, BindableProperty property, RelativeSize size)
    {
        size.Validate();

        if (property.ReturnType != typeof(double))
        {
            throw new ArgumentException(
                $"{property.DeclaringType.Name}.{property.PropertyName} is a {property.ReturnType.Name}; "
                + "a relative size can only be applied to a double property.",
                nameof(property));
        }

        var tracker = new RelativeSizeTracker(element, size);

        // TypedBinding rather than Binding("Value"): a string path is resolved by reflection, which
        // the trimmer cannot see, and iOS and Android Release builds trim. The binding holds the
        // tracker as its source and the element holds the binding, so the tracker lives exactly as
        // long as the binding does. With no reference size the getter reports failure, and the
        // property falls back to its default.
        return new TypedBinding<RelativeSizeTracker, double>(
            static t => t.Value is { } value ? (value, true) : (default, false),
            setter: null,
            handlers: [Tuple.Create<Func<RelativeSizeTracker, object>, string>(static t => t, nameof(RelativeSizeTracker.Value))])
        {
            Source = tracker,
            Mode = BindingMode.OneWay,
        };
    }
}
