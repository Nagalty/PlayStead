namespace PlayStead.Core.Identity;

public readonly record struct IdentityDecisionId(Guid Value)
{
    public static IdentityDecisionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
