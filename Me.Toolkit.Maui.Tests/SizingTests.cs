using AwesomeAssertions;
using Me.Toolkit.Maui.Sizing;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using Xunit;

namespace Me.Toolkit.Maui.Tests;

/// <summary>
/// <see cref="RelativeSize"/>, <see cref="RelativeExtension"/> and <see cref="RelativeSizeExtensions"/>.
///
/// Unlike Me.Toolkit.Maui.Nfc, nothing here needs a platform: elements, windows, bindings and the XAML
/// loader all run on the net10.0 slice of Microsoft.Maui.Controls, so these are behavioural tests
/// of the real thing. What cannot run here is the display — <c>DeviceDisplay.Current</c> throws on
/// this slice — so that one reference is supplied through <see cref="IDisplaySize"/>.
///
/// Sizes are driven the way a platform drives them: <see cref="IView.Arrange"/> for an element and
/// <see cref="IWindow.FrameChanged"/> for a window.
/// </summary>
[Collection(XamlLoaderCollection.Name)]
public class SizingTests
{
    public SizingTests() => InlineDispatcher.Install();

    private static void Arrange(VisualElement element, double width, double height) =>
        ((IView)element).Arrange(new Rect(0, 0, width, height));

    private static (Grid Parent, BoxView Child) ParentAndChild(double width = 400, double height = 200)
    {
        var child = new BoxView();
        var parent = new Grid { child };
        Arrange(parent, width, height);

        return (parent, child);
    }

    private static Window InWindow(View content, double width, double height)
    {
        var window = new Window(new ContentPage { Content = content });
        ((IWindow)window).FrameChanged(new Rect(0, 0, width, height));

        return window;
    }

    [Fact]
    public void A_percentage_of_the_parent_width()
    {
        var (_, child) = ParentAndChild(400, 200);

        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(30));

        child.WidthRequest.Should().Be(120);
    }

    [Fact]
    public void It_follows_the_parent_when_the_parent_is_resized()
    {
        var (parent, child) = ParentAndChild(400, 200);
        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(50));

        Arrange(parent, 1000, 200);

        child.WidthRequest.Should().Be(500);
    }

    [Fact]
    public void The_parent_padding_is_excluded()
    {
        // As a CSS percentage refers to the content box: 100% should fill the space the child is
        // actually given, not overflow it by the padding.
        var (parent, child) = ParentAndChild(400, 200);
        parent.Padding = new Thickness(50, 0);

        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(100));

        child.WidthRequest.Should().Be(300);
    }

    [Fact]
    public void A_padding_change_is_picked_up()
    {
        var (parent, child) = ParentAndChild(400, 200);
        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(100));

        parent.Padding = new Thickness(100, 0);

        child.WidthRequest.Should().Be(200);
    }

    [Theory]
    [InlineData(SizeAxis.Width, 40)]
    [InlineData(SizeAxis.Height, 20)]
    [InlineData(SizeAxis.Shorter, 20)]
    [InlineData(SizeAxis.Longer, 40)]
    public void The_axis_picks_the_dimension(SizeAxis axis, double expected)
    {
        var (_, child) = ParentAndChild(400, 200);

        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(10) { Axis = axis });

        child.WidthRequest.Should().Be(expected);
    }

    [Fact]
    public void Shorter_is_unchanged_by_a_rotation()
    {
        var (parent, child) = ParentAndChild(400, 800);
        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(10) { Axis = SizeAxis.Shorter });

        Arrange(parent, 800, 400);

        child.WidthRequest.Should().Be(40);
    }

    [Theory]
    [InlineData(100, 12)]
    [InlineData(500, 15)]
    [InlineData(2000, 32)]
    public void Min_and_Max_clamp_the_result(double parentWidth, double expected)
    {
        var label = new Label();
        Arrange(new Grid { label }, parentWidth, 100);

        label.SetRelativeSize(Label.FontSizeProperty, new RelativeSize(3) { Min = 12, Max = 32 });

        label.FontSize.Should().Be(expected);
    }

    [Fact]
    public void Before_the_first_layout_the_property_keeps_its_default()
    {
        var child = new BoxView();
        _ = new Grid { child };

        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(50));

        child.WidthRequest.Should().Be(-1, "-1 is WidthRequest's default, meaning unset");
    }

    [Fact]
    public void Without_a_parent_the_property_keeps_its_default()
    {
        var label = new Label();
        var fontSize = label.FontSize;

        label.SetRelativeSize(Label.FontSizeProperty, new RelativeSize(50));

        label.FontSize.Should().Be(fontSize);
    }

    [Fact]
    public void Moving_to_another_parent_follows_the_new_parent()
    {
        var (first, child) = ParentAndChild(400, 200);
        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(50));

        var second = new Grid();
        Arrange(second, 100, 100);
        first.Remove(child);
        second.Add(child);

        child.WidthRequest.Should().Be(50);

        Arrange(first, 2000, 200);
        child.WidthRequest.Should().Be(50, "the old parent is no longer observed");
    }

    [Fact]
    public void Removed_from_its_parent_it_returns_to_the_default()
    {
        var (parent, child) = ParentAndChild(400, 200);
        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(50));

        parent.Remove(child);

        child.WidthRequest.Should().Be(-1);
    }

    [Fact]
    public void A_percentage_of_the_window()
    {
        var label = new Label();
        _ = InWindow(new Grid { label }, 1000, 800);

        label.SetRelativeSize(Label.FontSizeProperty, new RelativeSize(2) { To = SizeReference.Window, Axis = SizeAxis.Height });

        label.FontSize.Should().Be(16);
    }

    [Fact]
    public void It_follows_the_window_when_the_window_is_resized()
    {
        var label = new Label();
        var window = InWindow(new Grid { label }, 1000, 800);
        label.SetRelativeSize(Label.FontSizeProperty, new RelativeSize(2) { To = SizeReference.Window });

        ((IWindow)window).FrameChanged(new Rect(0, 0, 500, 800));

        label.FontSize.Should().Be(10);
    }

    [Fact]
    public void A_window_reference_waits_for_the_element_to_reach_a_window()
    {
        var label = new Label();
        var fontSize = label.FontSize;
        var content = new Grid { label };
        label.SetRelativeSize(Label.FontSizeProperty, new RelativeSize(2) { To = SizeReference.Window });

        label.FontSize.Should().Be(fontSize);

        _ = InWindow(content, 1000, 800);

        label.FontSize.Should().Be(20);
    }

    [Fact]
    public void Leaving_the_window_stops_following_it()
    {
        var label = new Label();
        var content = new Grid { label };
        var window = InWindow(content, 1000, 800);
        label.SetRelativeSize(Label.FontSizeProperty, new RelativeSize(2) { To = SizeReference.Window });
        var page = (ContentPage)window.Page!;

        page.Content = null;
        ((IWindow)window).FrameChanged(new Rect(0, 0, 3000, 800));

        label.FontSize.Should().NotBe(60);
    }

    private static (BoxView Child, Window Window) InWindowWithParent(double windowWidth, double windowHeight)
    {
        // The parent is wide whatever the window is doing, as a card usually is — which is why the
        // orientation has to come from the window rather than from the reference.
        var child = new BoxView();
        var parent = new Grid { child };
        var window = InWindow(parent, windowWidth, windowHeight);
        Arrange(parent, 300, 100);

        return (child, window);
    }

    [Fact]
    public void Portrait_and_Landscape_follow_the_window_shape_not_the_reference()
    {
        var (child, window) = InWindowWithParent(400, 800);
        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(50) { Portrait = 90, Landscape = 45 });

        child.WidthRequest.Should().Be(270, "a portrait window, even though the parent is landscape-shaped");

        ((IWindow)window).FrameChanged(new Rect(0, 0, 800, 400));
        child.WidthRequest.Should().Be(135, "rotated to landscape");

        ((IWindow)window).FrameChanged(new Rect(0, 0, 400, 800));
        child.WidthRequest.Should().Be(270, "and back");
    }

    [Theory]
    [InlineData(400, 800, 50)]
    [InlineData(800, 400, 25)]
    public void An_unset_override_falls_back_to_Percent(double windowWidth, double windowHeight, double expectedPercent)
    {
        var (child, _) = InWindowWithParent(windowWidth, windowHeight);

        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(50) { Landscape = 25 });

        child.WidthRequest.Should().Be(300 * expectedPercent / 100);
    }

    [Fact]
    public void A_square_window_counts_as_portrait()
    {
        var (child, _) = InWindowWithParent(600, 600);

        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(50) { Portrait = 10, Landscape = 20 });

        child.WidthRequest.Should().Be(30);
    }

    [Fact]
    public void Outside_a_window_Percent_applies()
    {
        var (_, child) = ParentAndChild(300, 100);

        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(50) { Portrait = 10, Landscape = 20 });

        child.WidthRequest.Should().Be(150);
    }

    [Fact]
    public void Leaving_the_window_stops_following_its_orientation()
    {
        var (child, window) = InWindowWithParent(400, 800);
        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(50) { Portrait = 90, Landscape = 45 });

        ((ContentPage)window.Page!).Content = null;
        ((IWindow)window).FrameChanged(new Rect(0, 0, 800, 400));

        child.WidthRequest.Should().Be(150, "out of the window, Percent applies and the window is no longer observed");
    }

    [Fact]
    public void A_named_element_is_measured_instead_of_the_parent()
    {
        var (_, child) = ParentAndChild(400, 200);
        var card = new ContentView { Padding = new Thickness(10) };
        Arrange(card, 220, 100);

        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(50) { Source = card });

        child.WidthRequest.Should().Be(100);

        Arrange(card, 420, 100);
        child.WidthRequest.Should().Be(200);
    }

    [Fact]
    public void The_nearest_ancestor_of_a_type_is_measured()
    {
        var child = new BoxView();
        var inner = new Grid { child };
        var outer = new ContentView { Content = inner };
        Arrange(inner, 100, 100);
        Arrange(outer, 600, 100);

        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(50) { AncestorType = typeof(ContentView) });

        child.WidthRequest.Should().Be(300);
    }

    [Fact]
    public void Moving_a_subtree_under_another_ancestor_follows_it()
    {
        // The element's own parent does not change here — its parent does — so this holds only
        // because every element on the way up is watched, not just the element.
        var child = new BoxView();
        var inner = new Grid { child };
        var first = new ContentView { Content = inner };
        var second = new ContentView();
        Arrange(first, 600, 100);
        Arrange(second, 200, 100);
        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(50) { AncestorType = typeof(ContentView) });

        first.Content = null;
        second.Content = inner;

        child.WidthRequest.Should().Be(100);
    }

    [Fact]
    public void Setting_the_property_directly_replaces_the_relative_size()
    {
        var (parent, child) = ParentAndChild(400, 200);
        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(50));

        child.WidthRequest = 77;
        Arrange(parent, 1000, 200);

        child.WidthRequest.Should().Be(77);
    }

    [Fact]
    public void SetRelativeSize_returns_the_element_so_calls_can_be_chained()
    {
        var label = new Label();

        label.SetRelativeSize(Label.FontSizeProperty, new RelativeSize(1)).Should().BeSameAs(label);
    }

    [Theory]
    [MemberData(nameof(InvalidSizes))]
    public void Invalid_sizes_are_refused(string name, Action create)
    {
        _ = name;

        create.Should().Throw<ArgumentException>();
    }

    public static TheoryData<string, Action> InvalidSizes() => new()
    {
        { "negative percent", () => _ = new RelativeSize(-1) },
        { "infinite percent", () => _ = new RelativeSize(double.PositiveInfinity) },
        { "NaN percent", () => _ = new RelativeSize(double.NaN) },
        { "negative Min", () => _ = new RelativeSize(1) { Min = -1 } },
        { "NaN Max", () => _ = new RelativeSize(1) { Max = double.NaN } },
        { "Max below Min", () => new Label().SetRelativeSize(Label.FontSizeProperty, new RelativeSize(1) { Min = 20, Max = 10 }) },
        { "Source and AncestorType", () => new Label().SetRelativeSize(Label.FontSizeProperty, new RelativeSize(1) { Source = new Grid(), AncestorType = typeof(Grid) }) },
        { "To and Source", () => new Label().SetRelativeSize(Label.FontSizeProperty, new RelativeSize(1) { To = SizeReference.Window, Source = new Grid() }) },
        { "AncestorType that is not a VisualElement", () => new Label().SetRelativeSize(Label.FontSizeProperty, new RelativeSize(1) { AncestorType = typeof(string) }) },
        { "a property that is not a double", () => new Label().SetRelativeSize(Label.TextProperty, new RelativeSize(1)) },
        { "null element", () => ((Label)null!).SetRelativeSize(Label.FontSizeProperty, new RelativeSize(1)) },
        { "null property", () => new Label().SetRelativeSize(null!, new RelativeSize(1)) },
        { "null size", () => new Label().SetRelativeSize(Label.FontSizeProperty, null!) },
        { "negative Portrait", () => _ = new RelativeSize(1) { Portrait = -1 } },
        { "infinite Landscape", () => _ = new RelativeSize(1) { Landscape = double.PositiveInfinity } },
        { "infinite Offset", () => _ = new RelativeSize(1) { Offset = double.NegativeInfinity } },
        { "Self width from its own width", () => new BoxView().SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(1) { To = SizeReference.Self }) },
        { "Self height from its own height", () => new BoxView().SetRelativeSize(VisualElement.HeightRequestProperty, new RelativeSize(1) { To = SizeReference.Self, Axis = SizeAxis.Height }) },
        { "Self height from its shorter side", () => new BoxView().SetRelativeSize(VisualElement.HeightRequestProperty, new RelativeSize(1) { To = SizeReference.Self, Axis = SizeAxis.Shorter }) },
    };

    private const string Namespaces =
        "xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\" "
        + "xmlns:x=\"http://schemas.microsoft.com/winfx/2009/xaml\" "
        + "xmlns:me=\"https://github.com/melihercan/Me.Toolkit.Maui\"";

    [Fact]
    public void The_markup_extension_works_from_XAML()
    {
        var grid = new Grid().LoadFromXaml($$$"""
            <Grid {{{Namespaces}}}>
              <BoxView x:Name="Box" WidthRequest="{me:Relative 25}" HeightRequest="{me:Relative 50, Axis=Height, Max=60}" />
            </Grid>
            """);
        Arrange(grid, 400, 200);

        var box = grid.FindByName<BoxView>("Box");
        box.WidthRequest.Should().Be(100);
        box.HeightRequest.Should().Be(60);
    }

    [Fact]
    public void The_markup_extension_takes_a_named_source_and_an_ancestor_type()
    {
        var grid = new Grid().LoadFromXaml($$$"""
            <Grid {{{Namespaces}}}>
              <ContentView x:Name="Card" />
              <BoxView x:Name="BySource" WidthRequest="{me:Relative 50, Source={x:Reference Card}}" />
              <BoxView x:Name="ByAncestor" WidthRequest="{me:Relative 10, AncestorType={x:Type Grid}}" />
            </Grid>
            """);
        Arrange(grid, 1000, 200);
        Arrange(grid.FindByName<ContentView>("Card"), 300, 100);

        grid.FindByName<BoxView>("BySource").WidthRequest.Should().Be(150);
        grid.FindByName<BoxView>("ByAncestor").WidthRequest.Should().Be(100);
    }

    [Fact]
    public void The_markup_extension_takes_Portrait_and_Landscape()
    {
        // Nullable doubles from XAML text, and no Percent at all: both orientations are named.
        var grid = new Grid().LoadFromXaml($$$"""
            <Grid {{{Namespaces}}}>
              <BoxView x:Name="Both" WidthRequest="{me:Relative Portrait=90, Landscape=45}" />
              <BoxView x:Name="One" WidthRequest="{me:Relative 50, Landscape=20}" />
            </Grid>
            """);
        var window = InWindow(grid, 400, 800);
        Arrange(grid, 1000, 200);

        grid.FindByName<BoxView>("Both").WidthRequest.Should().Be(900);
        grid.FindByName<BoxView>("One").WidthRequest.Should().Be(500);

        ((IWindow)window).FrameChanged(new Rect(0, 0, 800, 400));

        grid.FindByName<BoxView>("Both").WidthRequest.Should().Be(450);
        grid.FindByName<BoxView>("One").WidthRequest.Should().Be(200);
    }

    [Fact]
    public void The_markup_extension_refuses_to_be_nested_inside_another_and_says_which_way_works()
    {
        // Found by running the demo: inside OnIdiom the target is the OnIdiom extension, MAUI does
        // not expose the element behind it, and the outer result is applied with SetValue, which
        // cannot carry a binding. OnPlatform stands in for OnIdiom here only because it fails the
        // same way before it asks the platform anything.
        var load = () => new Grid().LoadFromXaml($$$"""
            <Grid {{{Namespaces}}}>
              <BoxView WidthRequest="{OnPlatform Default={me:Relative 60}}" />
            </Grid>
            """);

        load.Should().Throw<Exception>().Where(e => (e.InnerException ?? e).Message.Contains("{me:Relative Percent={OnIdiom"));
    }

    [Fact]
    public void The_markup_extension_refuses_a_style_setter_with_a_reason()
    {
        var load = () => new ContentView().LoadFromXaml($$$"""
            <ContentView {{{Namespaces}}}>
              <ContentView.Resources>
                <Style TargetType="Label">
                  <Setter Property="FontSize" Value="{me:Relative 3}" />
                </Style>
              </ContentView.Resources>
            </ContentView>
            """);

        load.Should().Throw<Exception>().Where(e => (e.InnerException ?? e).Message.Contains("Style Setter"));
    }
}

/// <summary>
/// <see cref="SizeReference.Display"/>, against a stand-in display. Its own class because it swaps
/// the process-wide <see cref="DisplaySize.Current"/>; xUnit runs one class's tests in sequence, and
/// no other class touches it.
/// </summary>
public sealed class DisplaySizingTests : IDisposable
{
    private readonly IDisplaySize _original = DisplaySize.Current;
    private readonly FakeDisplay _display = new() { Current = new Size(400, 800) };

    public DisplaySizingTests()
    {
        InlineDispatcher.Install();
        DisplaySize.Current = _display;
    }

    public void Dispose() => DisplaySize.Current = _original;

    private static Window InWindow(View content)
    {
        var window = new Window(new ContentPage { Content = content });
        ((IWindow)window).FrameChanged(new Rect(0, 0, 100, 100));

        return window;
    }

    [Fact]
    public void A_percentage_of_the_display_follows_a_rotation()
    {
        var label = new Label();
        _ = InWindow(new Grid { label });
        label.SetRelativeSize(Label.FontSizeProperty, new RelativeSize(5) { To = SizeReference.Display });

        label.FontSize.Should().Be(20);

        _display.Rotate();

        label.FontSize.Should().Be(40);
    }

    [Fact]
    public void Replacing_or_clearing_a_relative_size_stops_the_old_one_listening()
    {
        // Behaviour alone cannot show this — the old binding is gone, so an old tracker still
        // running changes nothing visible. It was found by removing the Stop and watching every
        // other test stay green. The display's listener count is where a leftover tracker shows.
        var label = new Label();
        _ = InWindow(new Grid { label });
        label.SetRelativeSize(Label.FontSizeProperty, new RelativeSize(5) { To = SizeReference.Display });
        _display.Listeners.Should().Be(1);

        label.SetRelativeSize(Label.FontSizeProperty, new RelativeSize(5));
        _display.Listeners.Should().Be(0, "replaced by a parent-relative size");

        label.SetRelativeSize(Label.FontSizeProperty, new RelativeSize(5) { To = SizeReference.Display });
        label.ClearRelativeSize(Label.FontSizeProperty);
        _display.Listeners.Should().Be(0, "cleared");
    }

    [Fact]
    public void The_display_is_only_listened_to_while_the_element_is_in_a_window()
    {
        // DeviceDisplay's event is static. Subscribing an element that never reaches a window, or
        // staying subscribed after it leaves, would hold it for the life of the process.
        var label = new Label();
        var content = new Grid { label };
        label.SetRelativeSize(Label.FontSizeProperty, new RelativeSize(5) { To = SizeReference.Display });

        _display.Listeners.Should().Be(0);

        var window = InWindow(content);
        _display.Listeners.Should().Be(1);

        ((ContentPage)window.Page!).Content = null;
        _display.Listeners.Should().Be(0);
    }

    private sealed class FakeDisplay : IDisplaySize
    {
        private EventHandler? _changed;

        public Size? Current { get; set; }

        public int Listeners => _changed?.GetInvocationList().Length ?? 0;

        public event EventHandler? Changed
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        public void Rotate()
        {
            Current = new Size(Current!.Value.Height, Current.Value.Width);
            _changed?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>
/// A dispatcher that runs everything where it is asked, installed process-wide.
/// </summary>
/// <remarks>
/// A binding delivers a source change through its target's dispatcher, and a bare test process has
/// none, so without this every update after the first fails with "The dispatcher was not found".
/// An app always has one. MAUI's own unit tests install the same kind of stub.
/// </remarks>
internal sealed class InlineDispatcher : IDispatcher, IDispatcherProvider
{
    private static readonly InlineDispatcher Instance = new();

    public static void Install() => DispatcherProvider.SetCurrent(Instance);

    public bool IsDispatchRequired => false;

    public IDispatcher GetForCurrentThread() => this;

    public bool Dispatch(Action action)
    {
        action();
        return true;
    }

    public bool DispatchDelayed(TimeSpan delay, Action action) => Dispatch(action);

    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
}
