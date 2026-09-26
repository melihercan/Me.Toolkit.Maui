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
    /// <param name="property">
    /// A <see cref="double"/> property, such as <c>WidthRequest</c> or <c>FontSize</c>, or a
    /// <see cref="Thickness"/> one, <c>Margin</c> or <c>Padding</c>, which gets the value on every side.
    /// </param>
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

    /// <summary>
    /// Removes a relative size from <paramref name="property"/> and returns it to its default value.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="property">The property a relative size was applied to.</param>
    /// <remarks>
    /// Not the same as <c>RemoveBinding</c>: MAUI keeps the last value a removed binding set, and
    /// <c>ClearValue</c> does not take it back — true of any binding, not just this one. So the
    /// binding is first told there is no size, which makes MAUI apply the property's own default,
    /// and only then removed.
    /// </remarks>
    public static void ClearRelativeSize(this VisualElement element, BindableProperty property)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(property);

        if (Trackers(element).Remove(property, out var tracker))
        {
            tracker.Stop();
            element.RemoveBinding(property);
        }
    }

    // One tracker per element and property, so replacing or clearing a relative size stops the old
    // tracker rather than leaving it listening to a reference nothing reads any more.
    private static readonly BindableProperty TrackersProperty = BindableProperty.CreateAttached(
        "RelativeSizeTrackers", typeof(Dictionary<BindableProperty, RelativeSizeTracker>), typeof(RelativeSizeExtensions),
        defaultValue: null);

    private static Dictionary<BindableProperty, RelativeSizeTracker> Trackers(VisualElement element)
    {
        if (element.GetValue(TrackersProperty) is not Dictionary<BindableProperty, RelativeSizeTracker> trackers)
        {
            trackers = [];
            element.SetValue(TrackersProperty, trackers);
        }

        return trackers;
    }

    private static Thickness ToThickness(double value, ThicknessSides sides) => new(
        sides.HasFlag(ThicknessSides.Left) ? value : 0,
        sides.HasFlag(ThicknessSides.Top) ? value : 0,
        sides.HasFlag(ThicknessSides.Right) ? value : 0,
        sides.HasFlag(ThicknessSides.Bottom) ? value : 0);

    internal static BindingBase CreateBinding(VisualElement element, BindableProperty property, RelativeSize size)
    {
        size.Validate();

        if (property.ReturnType != typeof(double) && property.ReturnType != typeof(Thickness))
        {
            throw new ArgumentException(
                $"{property.DeclaringType.Name}.{property.PropertyName} is a {property.ReturnType.Name}; "
                + "a relative size can only be applied to a double or Thickness property.",
                nameof(property));
        }

        // A size of the element itself must read the other dimension than the one it sets, or every
        // change it makes feeds straight back into it. Shorter and Longer read both.
        if (size.To == SizeReference.Self && size.Source is null && size.AncestorType is null
            && ((property == VisualElement.WidthRequestProperty && size.Axis != SizeAxis.Height)
                || (property == VisualElement.HeightRequestProperty && size.Axis != SizeAxis.Width)))
        {
            throw new ArgumentException(
                $"To=Self on {property.PropertyName} with Axis={size.Axis} would size the element from the "
                + $"dimension it is setting. Use Axis={(property == VisualElement.WidthRequestProperty ? "Height" : "Width")}.",
                nameof(size));
        }

        var trackers = Trackers(element);
        if (trackers.Remove(property, out var previous))
        {
            previous.Stop();
        }

        if (size.Sides != ThicknessSides.All && property.ReturnType != typeof(Thickness))
        {
            throw new ArgumentException(
                $"Sides={size.Sides} applies to a margin or padding; {property.PropertyName} is a {property.ReturnType.Name}.",
                nameof(size));
        }

        var tracker = new RelativeSizeTracker(element, size);
        trackers[property] = tracker;

        // TypedBinding rather than Binding("Value"): a string path is resolved by reflection, which
        // the trimmer cannot see, and iOS and Android Release builds trim. The binding holds the
        // tracker as its source and the element holds the binding, so the tracker lives exactly as
        // long as the binding does. With no reference size the getter reports failure, and the
        // property falls back to its default.
        Tuple<Func<RelativeSizeTracker, object>, string>[] handlers =
            [Tuple.Create<Func<RelativeSizeTracker, object>, string>(static t => t, nameof(RelativeSizeTracker.Value))];

        // A Thickness gets the same value on every side: a margin or padding that scales with the
        // window. Two bindings rather than a conversion, so each stays typed and trim-safe.
        if (property.ReturnType == typeof(Thickness))
        {
            var sides = size.Sides;

            return new TypedBinding<RelativeSizeTracker, Thickness>(
                t => t.Value is { } value ? (ToThickness(value, sides), true) : (default, false),
                setter: null,
                handlers)
            {
                Source = tracker,
                Mode = BindingMode.OneWay,
            };
        }

        return new TypedBinding<RelativeSizeTracker, double>(
            static t => t.Value is { } value ? (value, true) : (default, false),
            setter: null,
            handlers)
        {
            Source = tracker,
            Mode = BindingMode.OneWay,
        };
    }
}
