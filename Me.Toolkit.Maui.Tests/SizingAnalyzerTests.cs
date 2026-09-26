using System.Collections.Immutable;
using AwesomeAssertions;
using Me.Toolkit.Maui.Sizing.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Me.Toolkit.Maui.Tests;

/// <summary>
/// <see cref="RelativeXamlAnalyzer"/>: every mistake it exists to catch is caught, and correct XAML —
/// including the demo's own page, which uses nearly every option — produces nothing.
/// </summary>
public class SizingAnalyzerTests
{
    private const string Page = """
        <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                     xmlns:me="https://github.com/melihercan/Me.Toolkit.Maui">
        {0}
        </ContentPage>
        """;

    private static List<Diagnostic> Check(string body) =>
        RelativeXamlAnalyzer.Check("Page.xaml", SourceText.From(string.Format(Page, body))).ToList();

    private static void ShouldReport(string body, string id, string? containing = null)
    {
        var diagnostics = Check(body);

        diagnostics.Should().ContainSingle(d => d.Id == id, "{0} should be reported for {1}", id, body);
        if (containing is not null)
        {
            diagnostics.Single(d => d.Id == id).GetMessage().Should().Contain(containing);
        }
    }

    [Fact]
    public void Correct_XAML_produces_nothing()
    {
        Check("""
            <ContentPage.Resources>
              <Style TargetType="Label">
                <Setter Property="me:RelativeSizing.FontSize" Value="3, To=Window, Min=12, Max=40, Round=Pixels" />
                <Setter Property="me:RelativeSizing.Margin" Value="2, Sides=Left Top" />
              </Style>
            </ContentPage.Resources>
            <VerticalStackLayout>
              <BoxView WidthRequest="{me:Relative 30}" />
              <BoxView WidthRequest="{me:Relative Percent={OnIdiom Phone=80, Desktop=40}}" />
              <BoxView WidthRequest="{me:Relative 90, Breakpoints='600:60 1200:35', BreakpointsBy=Reference, Round=Units}" />
              <BoxView WidthRequest="{me:Relative Portrait=90, Landscape=45}" HeightRequest="{me:Relative 56.25, To=Self}" />
              <BoxView WidthRequest="{me:Relative 50, Source={x:Reference Card}, Offset=-4}" />
              <BoxView WidthRequest="{me:Relative 100, To=Window, SafeArea=True}" Margin="{me:Relative 2, Sides=Horizontal}" />
              <BoxView me:RelativeSizing.WidthRequest="25, Offset=10" me:RelativeSizing.Padding="{Binding Size}" />
            </VerticalStackLayout>
            """).Should().BeEmpty();
    }

    [Fact]
    public void The_demo_page_produces_nothing()
    {
        // The demo uses nearly every option, so this is the widest check that the analyzer does not
        // cry wolf on correct XAML.
        var path = Path.Combine(TestAssemblies.RepositoryRoot, "DemoApp", "MainPage.xaml");

        RelativeXamlAnalyzer.Check(path, SourceText.From(File.ReadAllText(path))).Should().BeEmpty();
    }

    [Fact]
    public void MTKS001_a_Relative_in_a_Style_Setter()
    {
        ShouldReport("""
            <ContentPage.Resources>
              <Style TargetType="Label"><Setter Property="FontSize" Value="{me:Relative 3}" /></Style>
            </ContentPage.Resources>
            """, "MTKS001", "me:RelativeSizing.FontSize");
    }

    [Fact]
    public void MTKS001_in_element_syntax_too()
    {
        ShouldReport("""
            <ContentPage.Resources>
              <Style TargetType="Label"><Setter Property="FontSize"><Setter.Value><me:Relative Percent="3" /></Setter.Value></Setter></Style>
            </ContentPage.Resources>
            """, "MTKS001");
    }

    [Fact]
    public void MTKS002_a_markup_extension_as_the_unnamed_argument()
    {
        ShouldReport("""<BoxView WidthRequest="{me:Relative {OnIdiom Phone=80, Desktop=40}}" />""", "MTKS002", "Percent={OnIdiom");
    }

    [Fact]
    public void MTKS003_nested_inside_another_markup_extension()
    {
        ShouldReport("""<BoxView WidthRequest="{OnIdiom Desktop={me:Relative 30}, Default=100}" />""", "MTKS003", "OnIdiom");
    }

    [Theory]
    [InlineData("""<BoxView WidthRequest="{me:Relative 50, To=Self}" />""")]
    [InlineData("""<BoxView HeightRequest="{me:Relative 50, To=Self, Axis=Height}" />""")]
    [InlineData("""<BoxView HeightRequest="{me:Relative 50, To=Self, Axis=Shorter}" />""")]
    public void MTKS004_Self_measuring_the_dimension_it_sets(string body)
    {
        ShouldReport(body, "MTKS004", "would size the element from the dimension it is setting");
    }

    [Theory]
    [InlineData("""<BoxView WidthRequest="{me:Relative 30, Colour=Red}" />""", "is not an option")]
    [InlineData("""<BoxView WidthRequest="{me:Relative 30, To=Moon}" />""", "To=Moon")]
    [InlineData("""<BoxView WidthRequest="{me:Relative 30, Min=lots}" />""", "not a number")]
    [InlineData("""<BoxView WidthRequest="{me:Relative -30}" />""", "must not be negative")]
    [InlineData("""<BoxView WidthRequest="{me:Relative 30, Breakpoints='600-50'}" />""", "width:percent")]
    [InlineData("""<BoxView WidthRequest="{me:Relative 30, Breakpoints='600:50 600:40'}" />""", "twice")]
    [InlineData("""<BoxView WidthRequest="{me:Relative 30, Breakpoints='600:50', Landscape=20}" />""", "not both")]
    [InlineData("""<BoxView WidthRequest="{me:Relative 30, Sides=Left}" />""", "margin or padding")]
    [InlineData("""<BoxView WidthRequest="{me:Relative 30, SafeArea=True}" />""", "To=Window")]
    [InlineData("""<BoxView WidthRequest="{me:Relative 30, To=Window, Source={x:Reference Card}}" />""", "conflicts with Source")]
    [InlineData("""<BoxView me:RelativeSizing.WidthRequest="3, Colour=Red" />""", "is not an option")]
    [InlineData("""<BoxView me:RelativeSizing.WidthRequest="3, 4" />""", "only the first value may be unnamed")]
    [InlineData("""<BoxView me:RelativeSizing.WidthRequest="To=Window" />""", "gives no percentage")]
    [InlineData("""<BoxView me:RelativeSizing.WidthRequest="3, Source=Card" />""", "cannot be written as text")]
    [InlineData("""<ContentPage.Resources><Style TargetType="Label"><Setter Property="me:RelativeSizing.FontSize" Value="3, Round=Maybe" /></Style></ContentPage.Resources>""", "Round=Maybe")]
    public void MTKS005_invalid_options(string body, string message)
    {
        ShouldReport(body, "MTKS005", message);
    }

    [Fact]
    public void A_diagnostic_points_at_the_offending_attribute()
    {
        var diagnostic = Check("""
            <VerticalStackLayout>
              <BoxView WidthRequest="{me:Relative 30}" />
              <BoxView WidthRequest="{me:Relative 30, Colour=Red}" />
            </VerticalStackLayout>
            """).Single();

        // Zero-based: the ContentPage tag and its two xmlns lines are 0-2, the stack layout 3, the
        // correct BoxView 4 - and the one with the unknown option 5.
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.Should().Be(5);
        diagnostic.Location.GetLineSpan().Path.Should().Be("Page.xaml");
    }

    [Fact]
    public void XAML_without_the_library_namespace_is_left_alone()
    {
        RelativeXamlAnalyzer.Check("Other.xaml", SourceText.From("""
            <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui" xmlns:me="clr-namespace:Somebody.Else">
              <BoxView WidthRequest="{me:Relative 30, Colour=Red}" />
            </ContentPage>
            """)).Should().BeEmpty();
    }

    [Fact]
    public void Malformed_XAML_is_left_to_the_XAML_compiler()
    {
        RelativeXamlAnalyzer.Check("Broken.xaml", SourceText.From("<ContentPage><unclosed")).Should().BeEmpty();
    }

    [Fact]
    public async Task It_runs_through_the_compiler_on_xaml_additional_files_and_formats_every_message()
    {
        // End to end, as MSBuild runs it: registered on additional files, reporting in the XAML, and
        // each rule's message formatting without throwing - the braces in "{me:Relative}" have to be
        // escaped in a format string, and were not at first.
        var xaml = string.Format(Page, """
            <ContentPage.Resources>
              <Style TargetType="Label"><Setter Property="FontSize" Value="{me:Relative 3}" /></Style>
            </ContentPage.Resources>
            <VerticalStackLayout>
              <BoxView WidthRequest="{me:Relative {OnIdiom Phone=80}}" />
              <BoxView WidthRequest="{OnIdiom Desktop={me:Relative 30}}" />
              <BoxView WidthRequest="{me:Relative 50, To=Self}" />
              <BoxView WidthRequest="{me:Relative 30, Colour=Red}" />
            </VerticalStackLayout>
            """);

        var compilation = CSharpCompilation.Create("App", [CSharpSyntaxTree.ParseText("class C {}")]);
        var options = new AnalyzerOptions([new InMemoryText("MainPage.xaml", xaml), new InMemoryText("notes.txt", xaml)]);

        var diagnostics = await compilation
            .WithAnalyzers([new RelativeXamlAnalyzer()], options)
            .GetAnalyzerDiagnosticsAsync();

        diagnostics.Select(d => d.Id).Should().BeEquivalentTo(["MTKS001", "MTKS002", "MTKS003", "MTKS004", "MTKS005"]);
        diagnostics.Should().OnlyContain(d => d.Location.GetLineSpan().Path == "MainPage.xaml", "only .xaml files are read");
        foreach (var diagnostic in diagnostics)
        {
            diagnostic.GetMessage().Should().NotBeNullOrWhiteSpace();
        }
    }

    private sealed class InMemoryText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
