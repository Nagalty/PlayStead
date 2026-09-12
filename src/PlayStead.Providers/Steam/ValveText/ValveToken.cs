namespace PlayStead.Providers.Steam.ValveText;

internal enum ValveTokenKind
{
    String,
    OpenBrace,
    CloseBrace,
    End
}

internal readonly record struct ValveToken(
    ValveTokenKind Kind,
    string? Value,
    int Offset);
