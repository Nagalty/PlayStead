namespace PlayStead.UI.State;

public sealed record WindowPlacementState(
    double Left,
    double Top,
    double Width,
    double Height,
    bool IsMaximized);
