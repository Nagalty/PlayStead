namespace PlayStead.UI.Library;

public sealed class LibraryViewStateAdapter
{
    public void CaptureVerticalOffset(
        LibraryViewModel viewModel,
        double verticalOffset)
    {
        ArgumentNullException.ThrowIfNull(
            viewModel);

        viewModel.SetVerticalOffset(
            verticalOffset);
    }

    public double GetRestoreVerticalOffset(
        LibraryViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(
            viewModel);

        return viewModel.VerticalOffset;
    }
}
