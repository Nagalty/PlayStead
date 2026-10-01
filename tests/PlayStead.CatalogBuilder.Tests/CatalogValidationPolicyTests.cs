using System.Text.Json;

namespace PlayStead.CatalogBuilder.Tests;

public sealed class CatalogValidationPolicyTests
{
    [Fact]
    public void File_uri_policy_ignores_free_text_but_rejects_provider_reference()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "CatalogValidation", "file-uri-cases.json")));
        var title = document.RootElement.GetProperty("titleFalsePositive");
        var provider = document.RootElement.GetProperty("providerReferenceRejected");

        Assert.False(ProviderReferencesContainFileUri(title));
        Assert.True(ProviderReferencesContainFileUri(provider));
    }

    private static bool ProviderReferencesContainFileUri(JsonElement entry)
    {
        if (!entry.TryGetProperty("providerReferences", out var references) || references.ValueKind != JsonValueKind.Array)
            return false;
        return references.EnumerateArray().Any(reference => reference.EnumerateObject().Any(property => property.Value.ValueKind == JsonValueKind.String && property.Value.GetString()!.Contains("file://", StringComparison.OrdinalIgnoreCase)));
    }
}
