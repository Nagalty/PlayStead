using PlayStead.Providers.Steam.ValveText;

namespace PlayStead.Providers.Tests.Steam;

public sealed class ValveTextParserTests
{
    [Fact]
    public void Parse_reads_nested_blocks_and_escaped_backslashes()
    {
        const string text = """
        "libraryfolders"
        {
            "0"
            {
                "path" "G:\\SteamLibrary"
            }
        }
        """;

        var root = ValveTextParser.Parse(text);

        var folders =
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
                root["libraryfolders"]);

        var zero =
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
                folders["0"]);

        Assert.Equal(@"G:\SteamLibrary", zero["path"]);
    }

    [Fact]
    public void Parse_decodes_escaped_quotes_inside_values()
    {
        const string text = """
        "AppState"
        {
            "name" "A \"quoted\" game"
        }
        """;

        var root = ValveTextParser.Parse(text);

        var appState =
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
                root["AppState"]);

        Assert.Equal("A \"quoted\" game", appState["name"]);
    }

    [Fact]
    public void Parse_ignores_line_comments()
    {
        const string text = """
        // root comment
        "AppState"
        {
            "appid" "730" // trailing comment
        }
        """;

        var root = ValveTextParser.Parse(text);

        var appState =
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
                root["AppState"]);

        Assert.Equal("730", appState["appid"]);
    }

    [Fact]
    public void Parse_throws_format_exception_for_unclosed_block()
    {
        const string text = "\"AppState\" { \"appid\" \"730\"";

        Assert.Throws<FormatException>(() => ValveTextParser.Parse(text));
    }

    [Fact]
    public void Parse_reports_offset_for_unterminated_quoted_string()
    {
        const string text = "\"AppState\" { \"name\" \"Broken";

        var ex = Assert.Throws<FormatException>(
            () => ValveTextParser.Parse(text));

        Assert.Contains("offset", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_reports_offset_for_unexpected_character()
    {
        const string text = "\"AppState\" @";

        var ex = Assert.Throws<FormatException>(
            () => ValveTextParser.Parse(text));

        Assert.Contains("offset", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
