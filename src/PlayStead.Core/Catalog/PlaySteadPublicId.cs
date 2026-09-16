namespace PlayStead.Core.Catalog;

public readonly record struct PlaySteadPublicId
{
    private const string GamePrefix = "PlayStead-";
    private const string DlcPrefix = "PlayStead-DLC-";

    private PlaySteadPublicId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static PlaySteadPublicId Parse(string value)
    {
        if (!TryParse(value, out var result))
        {
            throw new FormatException(
                $"Invalid PlayStead public id: '{value}'.");
        }

        return result;
    }

    public static bool TryParse(
        string? value,
        out PlaySteadPublicId result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        var digits =
            trimmed.StartsWith(
                DlcPrefix,
                StringComparison.Ordinal)
                ? trimmed[DlcPrefix.Length..]
                : trimmed.StartsWith(
                    GamePrefix,
                    StringComparison.Ordinal)
                    ? trimmed[GamePrefix.Length..]
                    : string.Empty;

        if (digits.Length < 6 ||
            !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        result = new PlaySteadPublicId(trimmed);
        return true;
    }

    public override string ToString() =>
        Value;
}
