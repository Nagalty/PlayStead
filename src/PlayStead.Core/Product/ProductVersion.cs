using System.Reflection;

namespace PlayStead.Core.Product;

public static class ProductVersion
{
    public static string Current =>
        typeof(ProductVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.4.1-dev";

    public static string Version => Current;
}
