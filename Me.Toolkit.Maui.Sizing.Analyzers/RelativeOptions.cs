using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Me.Toolkit.Maui.Sizing.Analyzers;

/// <summary>
/// The checks <c>RelativeSize</c> makes at runtime, made here at build time. They mirror its
/// validation rather than share it: the library targets MAUI, which an analyzer cannot load.
/// </summary>
internal static class RelativeOptions
{
    private static readonly string[] Numbers = ["Percent", "Portrait", "Landscape", "Offset", "Min", "Max"];

    private static readonly Dictionary<string, string[]> Enums = new(StringComparer.Ordinal)
    {
        ["To"] = ["Parent", "Window", "Display", "Self"],
        ["Axis"] = ["Width", "Height", "Shorter", "Longer"],
        ["Round"] = ["None", "Units", "Pixels"],
        ["BreakpointsBy"] = ["Window", "Reference"],
        ["Sides"] = ["None", "Left", "Top", "Right", "Bottom", "Horizontal", "Vertical", "All"],
    };

    private static readonly string[] MarkupOnly = ["Source", "AncestorType"];

    private static readonly string[] Thicknesses = ["Margin", "Padding"];

    /// <summary>A problem found, and whether it is the self-referencing rule or a general one.</summary>
    internal readonly struct Problem(bool selfReference, string message)
    {
        public bool SelfReference { get; } = selfReference;

        public string Message { get; } = message;
    }

    /// <summary>
    /// Checks a set of options against the property they size.
    /// </summary>
    /// <param name="options">Name and raw value; values starting with <c>{</c> are markup and not checked.</param>
    /// <param name="property">The property being sized, such as <c>WidthRequest</c>, if known.</param>
    /// <param name="ignoreCase">True for the text form, which <c>RelativeSize.Parse</c> reads case-insensitively.</param>
    /// <param name="allowMarkupOnly">True for the markup extension, which can take Source and AncestorType.</param>
    internal static List<Problem> Check(
        IReadOnlyList<KeyValuePair<string, string>> options, string? property, bool ignoreCase, bool allowMarkupOnly)
    {
        var problems = new List<Problem>();
        var comparison = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var known = Numbers.Concat(Enums.Keys).Concat(["Breakpoints", "SafeArea"]).Concat(MarkupOnly).ToArray();
        var values = new Dictionary<string, string>(comparer: StringComparer.OrdinalIgnoreCase);

        foreach (var option in options)
        {
            var name = known.FirstOrDefault(k => comparison.Equals(k, option.Key));
            if (name is null)
            {
                problems.Add(new(false, $"'{option.Key}' is not an option. Expected {string.Join(", ", known.Except(allowMarkupOnly ? [] : MarkupOnly))}."));
                continue;
            }

            if (!allowMarkupOnly && MarkupOnly.Contains(name))
            {
                problems.Add(new(false, $"{name} cannot be written as text, which cannot name an element or a type. Use {{me:Relative}} for it."));
                continue;
            }

            values[name] = option.Value;
            var value = option.Value.Trim();
            if (value.StartsWith("{", StringComparison.Ordinal))
            {
                continue;
            }

            if (Numbers.Contains(name))
            {
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || double.IsNaN(number))
                {
                    problems.Add(new(false, $"{name}={value} is not a number."));
                }
                else if (number < 0 && name != "Offset")
                {
                    problems.Add(new(false, $"{name}={value} must not be negative."));
                }
            }
            else if (Enums.TryGetValue(name, out var allowed))
            {
                // The text form accepts combinations of Sides: "Left Top" or "Left|Top".
                var parts = name == "Sides" && ignoreCase ? value.Split([' ', '|'], StringSplitOptions.RemoveEmptyEntries) : [value];
                foreach (var part in parts.Where(p => !allowed.Contains(p, comparison)))
                {
                    problems.Add(new(false, $"{name}={part} is not one of {string.Join(", ", allowed)}."));
                }
            }
            else if (name == "SafeArea" && !bool.TryParse(value, out _))
            {
                problems.Add(new(false, $"SafeArea={value} is not True or False."));
            }
            else if (name == "Breakpoints" && BreakpointsProblem(value) is { } breakpoints)
            {
                problems.Add(new(false, breakpoints));
            }
        }

        CheckCombinations(values, property, problems);
        return problems;
    }

    private static void CheckCombinations(Dictionary<string, string> values, string? property, List<Problem> problems)
    {
        string? Get(string name) => values.TryGetValue(name, out var v) ? v.Trim() : null;
        bool Is(string name, string expected) => string.Equals(Get(name), expected, StringComparison.OrdinalIgnoreCase);

        var to = Get("To") ?? "Parent";
        var axis = Get("Axis") ?? "Width";

        if (Get("Breakpoints") is not null && (Get("Portrait") is not null || Get("Landscape") is not null))
        {
            problems.Add(new(false, "Set Breakpoints or Portrait/Landscape, not both: they would compete to choose the percentage."));
        }

        if (Get("Source") is not null && Get("AncestorType") is not null)
        {
            problems.Add(new(false, "Set Source or AncestorType, not both."));
        }

        if ((Get("Source") is not null || Get("AncestorType") is not null) && Get("To") is not null && !Is("To", "Parent"))
        {
            problems.Add(new(false, $"To={to} conflicts with {(Get("Source") is not null ? "Source" : "AncestorType")}, which names the reference itself. Leave To unset."));
        }

        if (Is("SafeArea", "True") && (!string.Equals(to, "Window", StringComparison.OrdinalIgnoreCase) || Get("Source") is not null || Get("AncestorType") is not null))
        {
            problems.Add(new(false, "SafeArea applies to To=Window, the only reference that includes a notch or cutout."));
        }

        if (property is null)
        {
            return;
        }

        if (Get("Sides") is { } sides && !Is("Sides", "All") && !Thicknesses.Contains(property, StringComparer.Ordinal))
        {
            problems.Add(new(false, $"Sides={sides} applies to a margin or padding; {property} is not one."));
        }

        if (string.Equals(to, "Self", StringComparison.OrdinalIgnoreCase)
            && Get("Source") is null && Get("AncestorType") is null
            && ((property == "WidthRequest" && !string.Equals(axis, "Height", StringComparison.OrdinalIgnoreCase))
                || (property == "HeightRequest" && !string.Equals(axis, "Width", StringComparison.OrdinalIgnoreCase))))
        {
            var other = property == "WidthRequest" ? "Height" : "Width";
            problems.Add(new(true, $"To=Self on {property} with Axis={axis} would size the element from the dimension it is setting. Use Axis={other}."));
        }
    }

    private static string? BreakpointsProblem(string text)
    {
        var widths = new HashSet<double>();

        foreach (var pair in text.Split([' '], StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = pair.IndexOf(':');
            if (colon < 0
                || !double.TryParse(pair.Substring(0, colon), NumberStyles.Float, CultureInfo.InvariantCulture, out var width)
                || !double.TryParse(pair.Substring(colon + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)
                || width < 0 || percent < 0 || double.IsInfinity(width) || double.IsInfinity(percent))
            {
                return $"'{pair}' in Breakpoints '{text}' is not width:percent, both non-negative numbers.";
            }

            if (!widths.Add(width))
            {
                return $"Breakpoints '{text}' names width {width.ToString(CultureInfo.InvariantCulture)} twice.";
            }
        }

        return null;
    }

    /// <summary>
    /// Splits the text form <c>"3, To=Window, Min=12"</c> into options, reporting what
    /// <c>RelativeSize.Parse</c> would refuse about its shape.
    /// </summary>
    internal static List<KeyValuePair<string, string>> SplitText(string text, List<Problem> problems)
    {
        var options = new List<KeyValuePair<string, string>>();
        var parts = text.Split([','], StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();

        for (var i = 0; i < parts.Length; i++)
        {
            var equals = parts[i].IndexOf('=');
            if (equals < 0)
            {
                if (i == 0)
                {
                    options.Add(new("Percent", parts[i]));
                }
                else
                {
                    problems.Add(new(false, $"'{parts[i]}': only the first value may be unnamed, and it is the percentage."));
                }

                continue;
            }

            options.Add(new(parts[i].Substring(0, equals).Trim(), parts[i].Substring(equals + 1).Trim()));
        }

        if (!options.Any(o => o.Key.Equals("Percent", StringComparison.OrdinalIgnoreCase)
                               || o.Key.Equals("Portrait", StringComparison.OrdinalIgnoreCase)
                               || o.Key.Equals("Landscape", StringComparison.OrdinalIgnoreCase)
                               || o.Key.Equals("Breakpoints", StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add(new(false, $"\"{text}\" gives no percentage."));
        }

        return options;
    }
}
