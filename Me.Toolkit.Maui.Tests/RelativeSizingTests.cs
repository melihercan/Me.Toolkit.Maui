using AwesomeAssertions;
using Me.Toolkit.Maui.Sizing;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.Graphics;
using Xunit;

namespace Me.Toolkit.Maui.Tests;

/// <summary>
/// The three additions that make relative sizing practical across an app: <see cref="SizeReference.Self"/>
/// for aspect ratios, <see cref="RelativeSize.Offset"/> for <c>calc(50% - 8)</c>, and
/// <see cref="RelativeSizing"/>'s attached properties — with <see cref="RelativeSize.Parse"/> behind
/// them — so a <see cref="Style"/> can carry a relative size.
/// </summary>
[Collection(XamlLoaderCollection.Name)]
public class RelativeSizingTests
{
    public RelativeSizingTests() => InlineDispatcher.Install();

    private const string Namespaces =
        "xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\" "
        + "xmlns:x=\"http://schemas.microsoft.com/winfx/2009/xaml\" "
        + "xmlns:me=\"https://github.com/melihercan/Me.Toolkit.Maui\"";

    private static void Arrange(VisualElement element, double width, double height) =>
        ((IView)element).Arrange(new Rect(0, 0, width, height));

    private static BoxView ChildOf(double width, double height)
    {
        var child = new BoxView();
        Arrange(new Grid { child }, width, height);

        return child;
    }

    // --- To=Self -------------------------------------------------------------------------------

    [Fact]
    public void Self_keeps_an_aspect_ratio_as_the_element_is_resized()
    {
        var video = new BoxView();
        Arrange(video, 1600, 100);

        video.SetRelativeSize(VisualElement.HeightRequestProperty,
            new RelativeSize(56.25) { To = SizeReference.Self, Axis = SizeAxis.Width });

        video.HeightRequest.Should().Be(900, "16:9 of a 1600-wide element");

        Arrange(video, 800, 900);
        video.HeightRequest.Should().Be(450);
    }

    [Fact]
    public void Self_measures_the_whole_element_padding_included()
    {
        // An aspect ratio is of the outside of the element, unlike a parent, whose padding is the
        // space its children do not get.
        var card = new ContentView { Padding = new Thickness(10) };
        Arrange(card, 200, 100);

        card.SetRelativeSize(VisualElement.HeightRequestProperty, new RelativeSize(50) { To = SizeReference.Self });

        card.HeightRequest.Should().Be(100);
    }

    [Fact]
    public void Self_width_from_its_own_height_is_allowed()
    {
        var box = new BoxView();
        Arrange(box, 100, 300);

        box.SetRelativeSize(VisualElement.WidthRequestProperty,
            new RelativeSize(100) { To = SizeReference.Self, Axis = SizeAxis.Height });

        box.WidthRequest.Should().Be(300);
    }

    // --- Offset --------------------------------------------------------------------------------

    [Fact]
    public void Offset_is_added_after_the_percentage()
    {
        var child = ChildOf(400, 100);

        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(50) { Offset = -8 });

        child.WidthRequest.Should().Be(192, "calc(50% - 8)");
    }

    [Fact]
    public void Offset_is_applied_before_the_clamp()
    {
        var child = ChildOf(400, 100);

        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(10) { Offset = 100, Max = 120 });

        child.WidthRequest.Should().Be(120, "40 + 100 is clamped to 120, not 40 clamped and then offset to 140");
    }

    [Fact]
    public void A_negative_result_is_clamped_to_Min()
    {
        var child = ChildOf(100, 100);

        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(10) { Offset = -50 });

        child.WidthRequest.Should().Be(0);
    }

    [Fact]
    public void The_markup_extension_takes_Offset_and_Self()
    {
        var grid = new Grid().LoadFromXaml($$$"""
            <Grid {{{Namespaces}}}>
              <BoxView x:Name="Half" WidthRequest="{me:Relative 50, Offset=-8}" />
              <BoxView x:Name="Wide" HeightRequest="{me:Relative 50, To=Self}" />
            </Grid>
            """);
        Arrange(grid, 400, 100);
        Arrange(grid.FindByName<BoxView>("Wide"), 300, 20);

        grid.FindByName<BoxView>("Half").WidthRequest.Should().Be(192);
        grid.FindByName<BoxView>("Wide").HeightRequest.Should().Be(150);
    }

    // --- Parse ---------------------------------------------------------------------------------

    [Fact]
    public void Parse_reads_every_option()
    {
        var size = RelativeSize.Parse("3, To=Window, Axis=Shorter, Min=12, Max=40, Offset=-1.5, Portrait=4, Landscape=2");

        size.Percent.Should().Be(3);
        size.To.Should().Be(SizeReference.Window);
        size.Axis.Should().Be(SizeAxis.Shorter);
        size.Min.Should().Be(12);
        size.Max.Should().Be(40);
        size.Offset.Should().Be(-1.5);
        size.Portrait.Should().Be(4);
        size.Landscape.Should().Be(2);
    }

    [Fact]
    public void Parse_leaves_everything_unnamed_at_its_default()
    {
        var size = RelativeSize.Parse("30");

        size.Percent.Should().Be(30);
        size.To.Should().Be(SizeReference.Parent);
        size.Axis.Should().Be(SizeAxis.Width);
        size.Min.Should().Be(0);
        size.Max.Should().Be(double.PositiveInfinity);
        size.Offset.Should().Be(0);
        size.Portrait.Should().BeNull();
        size.Landscape.Should().BeNull();
    }

    [Fact]
    public void Parse_is_case_insensitive_and_needs_no_Percent_when_both_orientations_are_named()
    {
        var size = RelativeSize.Parse("portrait=90, LANDSCAPE=45, to=self, axis=height");

        size.Portrait.Should().Be(90);
        size.Landscape.Should().Be(45);
        size.To.Should().Be(SizeReference.Self);
        size.Axis.Should().Be(SizeAxis.Height);
    }

    [Fact]
    public void Parse_reads_numbers_the_same_in_every_culture()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");

            RelativeSize.Parse("2.5").Percent.Should().Be(2.5);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("To=Window")]
    [InlineData("3, Colour=Red")]
    [InlineData("3, 4")]
    [InlineData("three")]
    [InlineData("3, To=Moon")]
    [InlineData("3, Axis=7")]
    [InlineData("3, Min=lots")]
    public void Parse_refuses_what_it_cannot_read(string text)
    {
        var parse = () => RelativeSize.Parse(text);

        parse.Should().Throw<FormatException>();
    }

    // --- ClearRelativeSize and replacement -----------------------------------------------------

    [Fact]
    public void ClearRelativeSize_returns_the_property_to_its_default()
    {
        // RemoveBinding alone would not: MAUI keeps the last value a removed binding set, for any
        // binding, and ClearValue does not take it back. Found by this test failing at 200.
        var child = ChildOf(400, 100);
        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(50));

        child.ClearRelativeSize(VisualElement.WidthRequestProperty);

        child.WidthRequest.Should().Be(-1);
    }

    [Fact]
    public void Replacing_a_relative_size_follows_the_new_reference()
    {
        var card = new ContentView();
        Arrange(card, 1000, 100);
        var child = ChildOf(400, 100);
        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(50) { Source = card });

        child.SetRelativeSize(VisualElement.WidthRequestProperty, new RelativeSize(10));
        Arrange(card, 3000, 100);

        child.WidthRequest.Should().Be(40, "the card is no longer the reference");
    }

    // --- RelativeSizing attached properties ----------------------------------------------------

    [Fact]
    public void An_attached_property_applies_and_clearing_it_removes_the_binding()
    {
        var child = ChildOf(400, 100);

        RelativeSizing.SetWidthRequest(child, RelativeSize.Parse("50"));
        child.WidthRequest.Should().Be(200);

        RelativeSizing.SetWidthRequest(child, null);
        child.WidthRequest.Should().Be(-1);
    }

    [Fact]
    public void Attached_properties_work_as_XAML_attributes()
    {
        var grid = new Grid().LoadFromXaml($$$"""
            <Grid {{{Namespaces}}}>
              <BoxView x:Name="Box" me:RelativeSizing.WidthRequest="25, Offset=10" me:RelativeSizing.HeightRequest="50, Axis=Height" />
            </Grid>
            """);
        Arrange(grid, 400, 200);

        var box = grid.FindByName<BoxView>("Box");
        box.WidthRequest.Should().Be(110);
        box.HeightRequest.Should().Be(100);
    }

    [Fact]
    public void A_style_gives_each_element_its_own_relative_size()
    {
        // The point of the attached properties: one shared setter, a separate binding per element,
        // each following its own parent.
        var page = new ContentView().LoadFromXaml($$$"""
            <ContentView {{{Namespaces}}}>
              <ContentView.Resources>
                <Style TargetType="Label">
                  <Setter Property="me:RelativeSizing.FontSize" Value="10, Min=5, Max=50" />
                </Style>
              </ContentView.Resources>
              <VerticalStackLayout>
                <Grid x:Name="Narrow"><Label x:Name="Small" /></Grid>
                <Grid x:Name="Wide"><Label x:Name="Large" /></Grid>
              </VerticalStackLayout>
            </ContentView>
            """);
        Arrange(page.FindByName<Grid>("Narrow"), 200, 50);
        Arrange(page.FindByName<Grid>("Wide"), 300, 50);

        page.FindByName<Label>("Small").FontSize.Should().Be(20);
        page.FindByName<Label>("Large").FontSize.Should().Be(30);

        Arrange(page.FindByName<Grid>("Wide"), 2000, 50);
        page.FindByName<Label>("Large").FontSize.Should().Be(50, "clamped to Max");
        page.FindByName<Label>("Small").FontSize.Should().Be(20, "the other label follows its own parent");
    }

    [Fact]
    public void Removing_the_style_removes_the_relative_size()
    {
        var label = new Label();
        var fontSize = label.FontSize;
        Arrange(new Grid { label }, 200, 50);
        var style = new Style(typeof(Label))
        {
            Setters = { new Setter { Property = RelativeSizing.FontSizeProperty, Value = RelativeSize.Parse("10") } },
        };

        label.Style = style;
        label.FontSize.Should().Be(20);

        label.Style = null;
        label.FontSize.Should().Be(fontSize);
    }

    [Fact]
    public void FontSize_finds_the_property_of_whichever_control_it_is_on()
    {
        var button = new Button();
        Arrange(new Grid { button }, 200, 50);

        RelativeSizing.SetFontSize(button, RelativeSize.Parse("10"));

        button.FontSize.Should().Be(20);
    }

    [Fact]
    public void FontSize_on_an_element_without_one_says_so()
    {
        var set = () => RelativeSizing.SetFontSize(new BoxView(), RelativeSize.Parse("10"));

        set.Should().Throw<NotSupportedException>().WithMessage("*BoxView has no FontSize*");
    }
}

/// <summary>
/// Test classes that load XAML at runtime, run one at a time.
/// </summary>
/// <remarks>
/// MAUI's <c>XamlLoader</c> fills a static assembly cache on first use without a lock, so two
/// classes loading XAML in parallel intermittently fail with "An item with the same key has already
/// been added. Key: Microsoft.Maui.Controls". It showed up once there were two such classes: one run
/// in three. An app never hits it — XAML is loaded on the UI thread.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class XamlLoaderCollection
{
    public const string Name = "MAUI runtime XAML loader";
}
