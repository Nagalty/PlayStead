namespace PlayStead.UI.SingleInstance;

public interface IWindowActivator
{
    Task ActivateAsync(
        CancellationToken cancellationToken);
}
