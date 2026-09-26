using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Me.Toolkit.Maui.Sizing.Analyzers;

/// <summary>
/// Finds, at build time, the uses of <c>{me:Relative}</c> and <c>me:RelativeSizing</c> that would
/// otherwise throw when the page loads — or, for some, crash the app before it shows anything.
/// </summary>
/// <remarks>
/// It reads the app's XAML, which MAUI passes to analyzers as additional files, and reports at the
/// attribute in the XAML. Everything it reports is a warning, so a false positive can never break a
/// build that did not ask for <c>-warnaserror</c>.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RelativeXamlAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "Usage";
    private const string Help = "https://github.com/melihercan/Me.Toolkit.Maui/blob/master/Me.Toolkit.Maui.Sizing/README.md";

    private static readonly string[] Namespaces = ["https://github.com/melihercan/Me.Toolkit.Maui"];

    internal static readonly DiagnosticDescriptor InSetter = new(
        "MTKS001",
        "{me:Relative} cannot go in a Style Setter",
        "{{me:Relative}} cannot be used in a Style Setter: the size is tracked per element. Use an attached property instead: <Setter Property=\"me:RelativeSizing.{0}\" Value=\"...\" />.",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, helpLinkUri: Help);

    internal static readonly DiagnosticDescriptor PositionalMarkup = new(
        "MTKS002",
        "A markup extension as the unnamed argument of {me:Relative}",
        "A markup extension as {{me:Relative}}'s unnamed first argument is given no target type by MAUI's XAML compiler and throws at startup. Name it: {{me:Relative Percent={1}}}.",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, helpLinkUri: Help);

    internal static readonly DiagnosticDescriptor Nested = new(
        "MTKS003",
        "{me:Relative} nested inside another markup extension",
        "{{me:Relative}} cannot be nested inside {0}: it cannot see the element it sizes there, and the result is applied without its binding. Nest it the other way round: {{me:Relative Percent={{OnIdiom Phone=40, Desktop=20}}}}.",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, helpLinkUri: Help);

    internal static readonly DiagnosticDescriptor SelfReference = new(
        "MTKS004",
        "To=Self measuring the dimension it sets",
        "{0}",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, helpLinkUri: Help);

    internal static readonly DiagnosticDescriptor InvalidOption = new(
        "MTKS005",
        "An invalid relative size",
        "{0}",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, helpLinkUri: Help);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [InSetter, PositionalMarkup, Nested, SelfReference, InvalidOption];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterAdditionalFileAction(AnalyzeFile);
    }

    private static void AnalyzeFile(AdditionalFileAnalysisContext context)
    {
        var file = context.AdditionalFile;
        if (!file.Path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
            || file.GetText(context.CancellationToken) is not { } text)
        {
            return;
        }

        foreach (var diagnostic in Check(file.Path, text))
        {
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>Every problem in one XAML file.</summary>
    internal static IEnumerable<Diagnostic> Check(string path, SourceText text)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(text.ToString(), LoadOptions.SetLineInfo);
        }
        catch (XmlException)
        {
            // Malformed XAML is the XAML compiler's to report.
            return [];
        }

        var ours = document.Descendants()
            .SelectMany(e => e.Attributes())
            .Where(a => a.IsNamespaceDeclaration && IsOurs(a.Value))
            .Select(a => a.Name.LocalName)
            .ToImmutableHashSet(StringComparer.Ordinal);

        if (ours.Count == 0)
        {
            return [];
        }

        var diagnostics = new List<Diagnostic>();
        foreach (var element in document.Descendants())
        {
            CheckElement(element, ours, path, text, diagnostics);
        }

        return diagnostics;
    }

    private static bool IsOurs(string xmlns) =>
        Namespaces.Contains(xmlns, StringComparer.Ordinal)
        || xmlns.StartsWith("clr-namespace:Me.Toolkit.Maui.Sizing", StringComparison.Ordinal)
        || xmlns.StartsWith("using:Me.Toolkit.Maui.Sizing", StringComparison.Ordinal);

    private static void CheckElement(XElement element, ImmutableHashSet<string> ours, string path, SourceText text, List<Diagnostic> diagnostics)
    {
        var isSetter = element.Name.LocalName == "Setter";
        var setterProperty = isSetter ? (string?)element.Attribute("Property") : null;

        foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
        {
            var location = LocationOf(attribute, path, text);

            // me:RelativeSizing.FontSize="3, To=Window" on an element.
            if (IsOurs(attribute.Name.NamespaceName) && attribute.Name.LocalName.StartsWith("RelativeSizing.", StringComparison.Ordinal))
            {
                CheckText(attribute.Value, attribute.Name.LocalName.Substring("RelativeSizing.".Length), location, diagnostics);
                continue;
            }

            // <Setter Property="me:RelativeSizing.FontSize" Value="3, To=Window" />
            if (isSetter && attribute.Name.LocalName == "Value" && setterProperty is not null
                && AttachedProperty(setterProperty, ours) is { } attached)
            {
                CheckText(attribute.Value, attached, location, diagnostics);
                continue;
            }

            foreach (var extension in MarkupExtension.ParseAll(attribute.Value).Where(x => IsRelative(x.Name, ours)))
            {
                var property = isSetter && attribute.Name.LocalName == "Value"
                    ? (setterProperty ?? "")
                    : PropertyName(attribute.Name.LocalName);

                if (isSetter && attribute.Name.LocalName == "Value")
                {
                    diagnostics.Add(Diagnostic.Create(InSetter, location, PropertyName(property)));
                    continue;
                }

                if (extension.Depth > 0)
                {
                    var outer = MarkupExtension.ParseAll(attribute.Value)[0].Name;
                    diagnostics.Add(Diagnostic.Create(Nested, location, outer));
                    continue;
                }

                var options = extension.Named.ToList();
                if (extension.Positional.Count > 0)
                {
                    var first = extension.Positional[0];
                    if (first.StartsWith("{", StringComparison.Ordinal))
                    {
                        diagnostics.Add(Diagnostic.Create(PositionalMarkup, location, property, first));
                    }
                    else
                    {
                        options.Insert(0, new KeyValuePair<string, string>("Percent", first));
                    }
                }

                Report(RelativeOptions.Check(options, property, ignoreCase: false, allowMarkupOnly: true), location, diagnostics);
            }
        }

        // <Setter.Value><me:Relative ... /></Setter.Value>
        if (IsOurs(element.Name.NamespaceName)
            && element.Name.LocalName is "Relative" or "RelativeExtension"
            && element.Parent?.Name.LocalName == "Setter.Value")
        {
            var property = (string?)element.Parent.Parent?.Attribute("Property") ?? "";
            diagnostics.Add(Diagnostic.Create(InSetter, LocationOf(element, path, text), PropertyName(property)));
        }
    }

    private static void CheckText(string value, string property, Location location, List<Diagnostic> diagnostics)
    {
        if (value.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            // A binding or resource, whose value is only known at runtime.
            return;
        }

        var problems = new List<RelativeOptions.Problem>();
        var options = RelativeOptions.SplitText(value, problems);
        problems.AddRange(RelativeOptions.Check(options, property, ignoreCase: true, allowMarkupOnly: false));
        Report(problems, location, diagnostics);
    }

    private static void Report(IEnumerable<RelativeOptions.Problem> problems, Location location, List<Diagnostic> diagnostics)
    {
        foreach (var problem in problems)
        {
            diagnostics.Add(Diagnostic.Create(problem.SelfReference ? SelfReference : InvalidOption, location, problem.Message));
        }
    }

    private static bool IsRelative(string name, ImmutableHashSet<string> ours)
    {
        var colon = name.IndexOf(':');
        return colon > 0
            && ours.Contains(name.Substring(0, colon))
            && name.Substring(colon + 1) is "Relative" or "RelativeExtension";
    }

    // "me:RelativeSizing.FontSize" -> "FontSize", when the prefix is ours.
    private static string? AttachedProperty(string setterProperty, ImmutableHashSet<string> ours)
    {
        var colon = setterProperty.IndexOf(':');
        const string Type = "RelativeSizing.";
        return colon > 0
               && ours.Contains(setterProperty.Substring(0, colon))
               && setterProperty.Substring(colon + 1).StartsWith(Type, StringComparison.Ordinal)
            ? setterProperty.Substring(colon + 1 + Type.Length)
            : null;
    }

    // "WidthRequest", or "Grid.Row" -> "Row", or "Label.FontSize" -> "FontSize".
    private static string PropertyName(string attributeName)
    {
        var dot = attributeName.LastIndexOf('.');
        return dot < 0 ? attributeName : attributeName.Substring(dot + 1);
    }

    private static Location LocationOf(XObject node, string path, SourceText text)
    {
        if (node is not IXmlLineInfo info || !info.HasLineInfo() || info.LineNumber > text.Lines.Count)
        {
            return Location.None;
        }

        var line = text.Lines[info.LineNumber - 1];
        var start = Math.Min(line.Start + Math.Max(0, info.LinePosition - 1), line.End);
        var span = TextSpan.FromBounds(start, line.End);

        return Location.Create(path, span, text.Lines.GetLinePositionSpan(span));
    }
}
