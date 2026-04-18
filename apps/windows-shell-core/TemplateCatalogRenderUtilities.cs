using System.Text.RegularExpressions;

namespace JanLabel.WindowsShell.Core;

public static class TemplateCatalogRenderUtilities
{
    public static string ExpandTemplate(string template, IReadOnlyDictionary<string, string> bindings)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        return Regex.Replace(
            template,
            @"\{(?<name>[a-zA-Z0-9_]+)\}",
            (match) =>
            {
                var token = match.Groups["name"].Value;
                return bindings.TryGetValue(token, out var replacement) ? replacement : match.Value;
            });
    }

    public static bool IsBarcodeField(TemplateCatalogDocumentField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        return field.Name.Contains("barcode", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeBarcodeValue(string value)
    {
        var normalized = Regex.Replace(value ?? string.Empty, @"[^0-9A-Za-z]", string.Empty);
        return string.IsNullOrWhiteSpace(normalized) ? "JAN" : normalized;
    }

    public static string EscapeXml(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    public static string EscapePdfText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character > 0x7F)
            {
                throw new DraftPdfUnsupportedTextException("Draft PDF preview currently supports ASCII-only text; encountered non-ASCII content.");
            }

            switch (character)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '(':
                    builder.Append(@"\(");
                    break;
                case ')':
                    builder.Append(@"\)");
                    break;
                case '\r':
                case '\n':
                case '\t':
                    builder.Append(' ');
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }

    public static string FormatNumber(double value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
