using System.Text;

namespace PlayStead.Providers.Steam.ValveText;

internal sealed class ValveTextTokenizer
{
    private readonly string _text;
    private int _index;

    public ValveTextTokenizer(string text)
    {
        _text = text;
    }

    public ValveToken Next()
    {
        SkipTrivia();

        if (_index >= _text.Length)
        {
            return new ValveToken(
                ValveTokenKind.End,
                null,
                _index);
        }

        var offset = _index;

        return _text[_index] switch
        {
            '{' => ConsumeSingle(
                ValveTokenKind.OpenBrace,
                offset),

            '}' => ConsumeSingle(
                ValveTokenKind.CloseBrace,
                offset),

            '"' => ReadString(),

            _ => throw new FormatException(
                $"Unexpected character '{_text[_index]}' at offset {_index}.")
        };
    }

    private ValveToken ConsumeSingle(
        ValveTokenKind kind,
        int offset)
    {
        _index++;

        return new ValveToken(
            kind,
            null,
            offset);
    }

    private ValveToken ReadString()
    {
        var offset = _index++;
        var builder = new StringBuilder();

        while (_index < _text.Length)
        {
            var ch = _text[_index++];

            if (ch == '"')
            {
                return new ValveToken(
                    ValveTokenKind.String,
                    builder.ToString(),
                    offset);
            }

            if (ch == '\\' && _index < _text.Length)
            {
                var escaped = _text[_index++];

                if (escaped is '\\' or '"')
                {
                    builder.Append(escaped);
                }
                else
                {
                    builder.Append('\\');
                    builder.Append(escaped);
                }

                continue;
            }

            builder.Append(ch);
        }

        throw new FormatException(
            $"Unterminated quoted string at offset {offset}.");
    }

    private void SkipTrivia()
    {
        while (_index < _text.Length)
        {
            if (char.IsWhiteSpace(_text[_index]))
            {
                _index++;
                continue;
            }

            if (_text[_index] == '/' &&
                _index + 1 < _text.Length &&
                _text[_index + 1] == '/')
            {
                _index += 2;

                while (_index < _text.Length &&
                       _text[_index] is not '\r' and not '\n')
                {
                    _index++;
                }

                continue;
            }

            return;
        }
    }
}
