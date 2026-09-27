using System.Globalization;

namespace PlayStead.UI;

internal static class UiDisplayCulture
{
    public static CultureInfo Current { get; } =
        CultureInfo.GetCultureInfo("fr-FR");
}
