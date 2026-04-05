using System.Collections;
using System.Text;

namespace TTSAlbion.Converters;

// ================================
// JSON-like Debug Formatter
// ================================
public static class DebugFormatter
{
    public static string Format(object? value, int indent = 0)
    {
        return FormatInternal(value, indent);
    }

    // ================================
    // Core
    // ================================
    private static string FormatInternal(object? value, int indent)
    {
        if (value == null)
            return "null";

        switch (value)
        {
            case string str:
                return $"\"{Escape(str)}\"";

            case bool b:
                return b.ToString().ToLower(); // true / false

            case IDictionary dict:
                return FormatDictionary(dict, indent);

            case IEnumerable enumerable:
                return FormatEnumerable(enumerable, indent);

            default:
                return IsNumeric(value)
                    ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!
                    : $"\"{Escape(value.ToString() ?? string.Empty)}\"";
        }
    }

    // ================================
    // Dictionary
    // ================================
    private static string FormatDictionary(IDictionary dict, int indent)
    {
        if (dict.Count == 0)
            return "{}";

        var sb = new StringBuilder();
        var spacing = new string(' ', indent);
        var innerSpacing = new string(' ', indent + 2);

        sb.AppendLine("{");

        foreach (DictionaryEntry entry in dict)
        {
            var key = $"\"{Escape(entry.Key?.ToString() ?? "null")}\"";
            var value = FormatInternal(entry.Value, indent + 2);

            sb.AppendLine($"{innerSpacing}{key}: {value},");
        }

        RemoveTrailingComma(sb);
        sb.Append($"{spacing}}}");

        return sb.ToString();
    }

    // ================================
    // Enumerable
    // ================================
    private static string FormatEnumerable(IEnumerable enumerable, int indent)
    {
        var list = enumerable.Cast<object?>().ToList();

        if (list.Count == 0)
            return "[]";

        var sb = new StringBuilder();
        var spacing = new string(' ', indent);
        var innerSpacing = new string(' ', indent + 2);

        sb.AppendLine("[");

        foreach (var item in list)
        {
            var value = FormatInternal(item, indent + 2);
            sb.AppendLine($"{innerSpacing}{value},");
        }

        RemoveTrailingComma(sb);
        sb.Append($"{spacing}]");

        return sb.ToString();
    }

    // ================================
    // Helpers
    // ================================
    private static string Escape(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private static bool IsNumeric(object value)
    {
        return value is sbyte or byte or short or ushort or int or uint
            or long or ulong or float or double or decimal;
    }

    private static void RemoveTrailingComma(StringBuilder sb)
    {
        for (int i = sb.Length - 1; i >= 0; i--)
        {
            if (sb[i] == ',')
            {
                sb.Remove(i, 1);
                break;
            }

            if (!char.IsWhiteSpace(sb[i]))
                break;
        }
    }
}