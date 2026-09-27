namespace PlayStead.Core.Updates;

/// <summary>Canonical local paths shared by the app-update components.</summary>
public sealed record AppUpdatePaths
{
    public AppUpdatePaths(string updatesRoot)
    {
        if (string.IsNullOrWhiteSpace(updatesRoot))
            throw new ArgumentException("An updates root is required.", nameof(updatesRoot));

        UpdatesRoot = Path.GetFullPath(updatesRoot);
    }

    public string UpdatesRoot { get; }
}
