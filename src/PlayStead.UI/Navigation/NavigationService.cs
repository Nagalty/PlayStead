namespace PlayStead.UI.Navigation;

public sealed class NavigationService
{
    private readonly Stack<RouteState> _history = new();

    private RouteState _current = new(
        AppRoute.Home,
        Parameter: null);

    public AppRoute CurrentRoute =>
        _current.Route;

    public object? CurrentParameter =>
        _current.Parameter;

    public bool CanGoBack =>
        _history.Count > 0;

    public event EventHandler? Changed;

    public void Navigate(
        NavigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        var next =
            new RouteState(
                request.Route,
                request.Parameter);

        if (_current == next)
        {
            return;
        }

        if (request.AddToHistory)
        {
            _history.Push(
                _current);
        }

        _current =
            next;

        Changed?.Invoke(
            this,
            EventArgs.Empty);
    }

    public bool GoBack()
    {
        if (_history.Count == 0)
        {
            return false;
        }

        _current =
            _history.Pop();

        Changed?.Invoke(
            this,
            EventArgs.Empty);

        return true;
    }

    private sealed record RouteState(
        AppRoute Route,
        object? Parameter);
}
