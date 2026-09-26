using System.Collections.Generic;
using System.Text;

namespace Me.Toolkit.Maui.Sizing.Analyzers;

/// <summary>
/// One markup extension parsed out of a XAML attribute value: <c>{me:Relative 30, To=Window}</c>.
/// </summary>
/// <remarks>
/// Only as much of XAML's markup-extension grammar as the checks need: a type name, positional and
/// named arguments separated by commas, nested extensions, and single-quoted values that may contain
/// commas. Anything it cannot read is left alone rather than reported — the XAML compiler is the
/// authority on syntax, this only looks for known mistakes.
/// </remarks>
internal sealed class MarkupExtension
{
    private MarkupExtension(string name, int depth)
    {
        Name = name;
        Depth = depth;
    }

    /// <summary>The type name as written, prefix included: <c>me:Relative</c>.</summary>
    public string Name { get; }

    /// <summary>0 for the outermost extension, 1 for one nested inside it, and so on.</summary>
    public int Depth { get; }

    /// <summary>Unnamed arguments, raw.</summary>
    public List<string> Positional { get; } = [];

    /// <summary>Named arguments, raw values with any enclosing single quotes removed.</summary>
    public List<KeyValuePair<string, string>> Named { get; } = [];

    /// <summary>Every extension in the value, outermost first, nested ones included.</summary>
    public static List<MarkupExtension> ParseAll(string value)
    {
        var all = new List<MarkupExtension>();
        var text = value.Trim();

        // "{}" escapes a literal brace; anything else not starting with one is not markup.
        if (text.Length < 2 || text[0] != '{' || text.StartsWith("{}", System.StringComparison.Ordinal))
        {
            return all;
        }

        Parse(text, 0, all);
        return all;
    }

    private static MarkupExtension? Parse(string text, int depth, List<MarkupExtension> all)
    {
        if (!TryMatchingBrace(text, out var body))
        {
            return null;
        }

        var nameEnd = 0;
        while (nameEnd < body.Length && !char.IsWhiteSpace(body[nameEnd]) && body[nameEnd] != ',')
        {
            nameEnd++;
        }

        var extension = new MarkupExtension(body.Substring(0, nameEnd), depth);
        all.Add(extension);

        foreach (var argument in SplitTopLevel(body.Substring(nameEnd), ','))
        {
            var trimmed = argument.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var equals = IndexOfTopLevel(trimmed, '=');
            var raw = equals < 0 ? trimmed : trimmed.Substring(equals + 1).Trim();

            if (raw.StartsWith("{", System.StringComparison.Ordinal))
            {
                Parse(raw, depth + 1, all);
            }

            if (equals < 0)
            {
                extension.Positional.Add(raw);
            }
            else
            {
                extension.Named.Add(new KeyValuePair<string, string>(trimmed.Substring(0, equals).Trim(), Unquote(raw)));
            }
        }

        return extension;
    }

    private static bool TryMatchingBrace(string text, out string body)
    {
        var depth = 0;
        var quoted = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\'')
            {
                quoted = !quoted;
            }
            else if (!quoted && c == '{')
            {
                depth++;
            }
            else if (!quoted && c == '}' && --depth == 0)
            {
                body = text.Substring(1, i - 1);
                return true;
            }
        }

        body = "";
        return false;
    }

    private static IEnumerable<string> SplitTopLevel(string text, char separator)
    {
        var depth = 0;
        var quoted = false;
        var current = new StringBuilder();

        foreach (var c in text)
        {
            if (c == '\'')
            {
                quoted = !quoted;
            }
            else if (!quoted && c == '{')
            {
                depth++;
            }
            else if (!quoted && c == '}')
            {
                depth--;
            }

            if (c == separator && depth == 0 && !quoted)
            {
                yield return current.ToString();
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        yield return current.ToString();
    }

    private static int IndexOfTopLevel(string text, char target)
    {
        var depth = 0;
        var quoted = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\'')
            {
                quoted = !quoted;
            }
            else if (!quoted && c == '{')
            {
                depth++;
            }
            else if (!quoted && c == '}')
            {
                depth--;
            }
            else if (c == target && depth == 0 && !quoted)
            {
                return i;
            }
        }

        return -1;
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'' ? value.Substring(1, value.Length - 2) : value;
}
