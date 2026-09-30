using System.Globalization;
using System.Text;

namespace PlayStead.Core.Catalog;

public static class CanonicalCatalogTitleNormalizer
{
    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var text = value.Replace("™", string.Empty, StringComparison.Ordinal)
            .Replace("®", string.Empty, StringComparison.Ordinal)
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            builder.Append(char.IsPunctuation(c) ? ' ' : char.ToUpperInvariant(c));
        }
        return string.Join(' ', builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
