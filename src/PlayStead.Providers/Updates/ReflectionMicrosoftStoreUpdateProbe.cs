namespace PlayStead.Providers.Updates;

public sealed class ReflectionMicrosoftStoreUpdateProbe : IMicrosoftStoreUpdateProbe
{
    public async Task<bool> HasUpdateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var type = Type.GetType("Windows.Services.Store.StoreContext, Windows, ContentType=WindowsRuntime")
            ?? throw new PlatformNotSupportedException("Microsoft Store APIs are unavailable in this installation.");
        var context = type.GetMethod("GetDefault", Type.EmptyTypes)?.Invoke(null, null)
            ?? throw new PlatformNotSupportedException("Microsoft Store context is unavailable.");
        var operation = context.GetType().GetMethod("GetAppAndOptionalStorePackageUpdatesAsync", Type.EmptyTypes)?.Invoke(context, null)
            ?? throw new PlatformNotSupportedException("Microsoft Store update API is unavailable.");
        var updates = await (dynamic)operation;
        cancellationToken.ThrowIfCancellationRequested();
        if (updates is System.Collections.ICollection collection)
            return collection.Count > 0;

        var count = updates?.GetType().GetProperty("Count")?.GetValue(updates);
        return count is int itemCount && itemCount > 0;
    }
}
