using System.ComponentModel;

namespace Me.Toolkit.Maui.Sizing;

/// <summary>
/// Follows one element's reference — its parent, window, the display, a named element or an
/// ancestor — and publishes the resolved size as <see cref="Value"/> for a binding to pick up.
/// </summary>
/// <remarks>
/// <para>
/// The reference itself can change, not just its size: an element is re-parented, a page is pushed
/// into a window, a subtree is moved. So the tracker listens to the element's own
/// <see cref="Element.ParentChanged"/> and <see cref="VisualElement.Window"/> for as long as it
/// lives, and re-resolves the reference when either moves.
/// </para>
/// <para>
/// Subscriptions to anything other than the element are dropped whenever the reference changes,
/// and the display is only listened to while the element is in a window, so a static event never
/// holds an element that has left the visual tree.
/// </para>
/// </remarks>
internal sealed class RelativeSizeTracker : INotifyPropertyChanged
{
    private readonly VisualElement _element;
    private readonly RelativeSize _size;
    private readonly List<Element> _ancestors = [];
    private VisualElement? _reference;
    private Window? _window;
    private Window? _orientationWindow;
    private bool _display;
    private double? _value;

    public RelativeSizeTracker(VisualElement element, RelativeSize size)
    {
        _element = element;
        _size = size;

        _element.ParentChanged += OnTreeChanged;
        _element.PropertyChanged += OnElementPropertyChanged;

        Attach();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The resolved size, or <see langword="null"/> while the reference has none.</summary>
    public double? Value
    {
        get => _value;
        private set
        {
            if (_value == value)
            {
                return;
            }

            _value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }

    private void Attach()
    {
        if (_size.Source is not null)
        {
            Observe(_size.Source);
        }
        else if (_size.AncestorType is not null)
        {
            // Every element between here and the ancestor is watched too: re-parenting any of them
            // can put a different ancestor of that type above this element.
            for (var parent = _element.Parent; parent is not null; parent = parent.Parent)
            {
                parent.ParentChanged += OnTreeChanged;
                _ancestors.Add(parent);

                if (_size.AncestorType.IsInstanceOfType(parent))
                {
                    Observe((VisualElement)parent);
                    break;
                }
            }
        }
        else
        {
            switch (_size.To)
            {
                case SizeReference.Parent:
                    Observe(_element.Parent as VisualElement);
                    break;

                case SizeReference.Window:
                    _window = _element.Window;
                    if (_window is not null)
                    {
                        _window.SizeChanged += OnReferenceChanged;
                    }

                    break;

                case SizeReference.Display:
                    if (_element.Window is not null)
                    {
                        DisplaySize.Current.Changed += OnReferenceChanged;
                        _display = true;
                    }

                    break;
            }
        }

        // Orientation is the window's shape, whatever the size is measured against, so a Portrait
        // or Landscape override listens to the window as well as to the reference. The element's
        // Window property changing already re-attaches, which covers arriving in a window.
        if (_size.DependsOnOrientation)
        {
            _orientationWindow = _element.Window;
            if (_orientationWindow is not null)
            {
                _orientationWindow.SizeChanged += OnReferenceChanged;
            }
        }

        Update();
    }

    private void Detach()
    {
        foreach (var ancestor in _ancestors)
        {
            ancestor.ParentChanged -= OnTreeChanged;
        }

        _ancestors.Clear();

        if (_reference is not null)
        {
            _reference.SizeChanged -= OnReferenceChanged;
            _reference.PropertyChanged -= OnReferencePropertyChanged;
            _reference = null;
        }

        if (_window is not null)
        {
            _window.SizeChanged -= OnReferenceChanged;
            _window = null;
        }

        if (_orientationWindow is not null)
        {
            _orientationWindow.SizeChanged -= OnReferenceChanged;
            _orientationWindow = null;
        }

        if (_display)
        {
            DisplaySize.Current.Changed -= OnReferenceChanged;
            _display = false;
        }
    }

    private void Observe(VisualElement? reference)
    {
        _reference = reference;

        if (_reference is not null)
        {
            // SizeChanged rather than PropertyChanged for Width and Height: it is raised once, after
            // both have changed, so the value is never computed from a half-updated size.
            _reference.SizeChanged += OnReferenceChanged;
            _reference.PropertyChanged += OnReferencePropertyChanged;
        }
    }

    private void Update() =>
        Value = ReferenceSize() is { } size
            ? _size.Resolve(size, _orientationWindow is { } w ? Available(w.Width, w.Height, Thickness.Zero) : null)
            : null;

    private Size? ReferenceSize()
    {
        if (_reference is not null)
        {
            return Available(_reference.Width, _reference.Height, (_reference as IPadding)?.Padding ?? Thickness.Zero);
        }

        if (_window is not null)
        {
            return Available(_window.Width, _window.Height, Thickness.Zero);
        }

        return _display ? DisplaySize.Current.Current : null;
    }

    // Width and Height are -1 until the first layout; that is "no size yet", not a size.
    private static Size? Available(double width, double height, Thickness padding) =>
        width < 0 || height < 0
            ? null
            : new Size(Math.Max(0, width - padding.HorizontalThickness), Math.Max(0, height - padding.VerticalThickness));

    private void Reattach()
    {
        Detach();
        Attach();
    }

    private void OnTreeChanged(object? sender, EventArgs e) => Reattach();

    private void OnElementPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == VisualElement.WindowProperty.PropertyName)
        {
            Reattach();
        }
    }

    private void OnReferenceChanged(object? sender, EventArgs e) => Update();

    private void OnReferencePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPadding.Padding))
        {
            Update();
        }
    }
}
