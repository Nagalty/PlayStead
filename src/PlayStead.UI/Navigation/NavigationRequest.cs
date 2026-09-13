namespace PlayStead.UI.Navigation;

public sealed record NavigationRequest(
    AppRoute Route,
    object? Parameter = null,
    bool AddToHistory = true);
