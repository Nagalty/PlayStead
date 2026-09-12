namespace PlayStead.Providers.Steam.ValveText;

public static class ValveTextParser
{
    public static IReadOnlyDictionary<string, object> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var tokenizer = new ValveTextTokenizer(text);

        return ParseBlock(
            tokenizer,
            stopOnCloseBrace: false);
    }

    private static IReadOnlyDictionary<string, object> ParseBlock(
        ValveTextTokenizer tokenizer,
        bool stopOnCloseBrace)
    {
        var result = new Dictionary<string, object>(
            StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            var token = tokenizer.Next();

            if (token.Kind == ValveTokenKind.End)
            {
                if (stopOnCloseBrace)
                {
                    throw new FormatException(
                        "Unclosed Valve text block.");
                }

                return result;
            }

            if (token.Kind == ValveTokenKind.CloseBrace)
            {
                if (!stopOnCloseBrace)
                {
                    throw new FormatException(
                        $"Unexpected closing brace at offset {token.Offset}.");
                }

                return result;
            }

            if (token.Kind != ValveTokenKind.String)
            {
                throw new FormatException(
                    $"Expected key at offset {token.Offset}.");
            }

            var key = token.Value!;
            var valueToken = tokenizer.Next();

            object value = valueToken.Kind switch
            {
                ValveTokenKind.String =>
                    valueToken.Value!,

                ValveTokenKind.OpenBrace =>
                    ParseBlock(
                        tokenizer,
                        stopOnCloseBrace: true),

                _ => throw new FormatException(
                    $"Expected value or block for '{key}' at offset {valueToken.Offset}.")
            };

            result[key] = value;
        }
    }
}
