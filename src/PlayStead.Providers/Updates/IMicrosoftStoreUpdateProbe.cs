namespace PlayStead.Providers.Updates;

public interface IMicrosoftStoreUpdateProbe
{
    Task<bool> HasUpdateAsync(CancellationToken cancellationToken = default);
}
