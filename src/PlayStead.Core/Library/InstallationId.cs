namespace PlayStead.Core.Library;

public readonly record struct InstallationId(Guid Value)
{
    public static InstallationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
