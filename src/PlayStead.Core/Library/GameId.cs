namespace PlayStead.Core.Library;

public readonly record struct GameId(Guid Value)
{
    public static GameId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
