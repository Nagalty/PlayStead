using System.Globalization;
using System.Text;

namespace PlayStead.Core.Catalog;

public static class CatalogTitleNormalizer
{
    public static string Normalize(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var decomposed = title.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
            else
                builder.Append(' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
