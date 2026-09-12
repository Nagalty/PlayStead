using PlayStead.Core.Steam;

namespace PlayStead.Providers.Steam.Remote;

public sealed class SteamCmdAppInfoParser :
    ISteamCmdAppInfoParser
{
    public SteamRemoteEvidenceResult Parse(
        string appId,
        string branchName,
        string rawOutput,
        DateTimeOffset observedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        ArgumentNullException.ThrowIfNull(rawOutput);

        try
        {
            if (!TryExtractAppObject(
                    rawOutput,
                    appId,
                    out var appObjectText))
            {
                return Malformed();
            }

            var parser = new VdfObjectParser(
                appObjectText);

            var root = parser.ParseRootObject(
                appId);

            if (!root.TryGetObject(
                    "depots",
                    out var depots))
            {
                return Malformed();
            }

            if (!depots.TryGetObject(
                    "branches",
                    out var branches))
            {
                return Malformed();
            }

            if (!branches.TryGetObject(
                    branchName,
                    out var branch))
            {
                return new SteamRemoteEvidenceResult(
                    SteamRemoteEvidenceStatus.BranchUnavailable,
                    Evidence: null,
                    SteamRemoteFailureKind.BranchUnavailable);
            }

            branch.TryGetString(
                "buildid",
                out var buildId);

            var depotManifestIds =
                new SortedDictionary<string, string>(
                    StringComparer.Ordinal);

            foreach (var pair in depots.Objects)
            {
                if (!IsNumeric(pair.Key) ||
                    !pair.Value.TryGetObject(
                        "manifests",
                        out var manifests) ||
                    !manifests.TryGetObject(
                        branchName,
                        out var branchManifest) ||
                    !branchManifest.TryGetString(
                        "gid",
                        out var manifestId) ||
                    string.IsNullOrWhiteSpace(manifestId))
                {
                    continue;
                }

                depotManifestIds[pair.Key] =
                    manifestId;
            }

            return new SteamRemoteEvidenceResult(
                SteamRemoteEvidenceStatus.Success,
                new SteamRemoteEvidence(
                    appId,
                    branchName,
                    string.IsNullOrWhiteSpace(buildId)
                        ? null
                        : buildId,
                    new Dictionary<string, string>(
                        depotManifestIds,
                        StringComparer.Ordinal),
                    observedAtUtc,
                    SteamRemoteEvidenceSource.SteamCmdAnonymous),
                FailureKind: null);
        }
        catch (VdfParseException)
        {
            return Malformed();
        }
    }

    private static SteamRemoteEvidenceResult Malformed()
        => new(
            SteamRemoteEvidenceStatus.RefreshFailed,
            Evidence: null,
            SteamRemoteFailureKind.MalformedOutput);

    private static bool IsNumeric(
        string value)
        => value.Length > 0 &&
           value.All(char.IsAsciiDigit);

    private static bool TryExtractAppObject(
        string rawOutput,
        string appId,
        out string appObjectText)
    {
        appObjectText = string.Empty;

        var needle = $"\"{appId}\"";
        var searchIndex = 0;

        while (searchIndex < rawOutput.Length)
        {
            var keyIndex = rawOutput.IndexOf(
                needle,
                searchIndex,
                StringComparison.Ordinal);

            if (keyIndex < 0)
            {
                return false;
            }

            var cursor =
                keyIndex + needle.Length;

            while (cursor < rawOutput.Length &&
                   char.IsWhiteSpace(rawOutput[cursor]))
            {
                cursor++;
            }

            if (cursor < rawOutput.Length &&
                rawOutput[cursor] == '{')
            {
                if (!TryFindBalancedObjectEnd(
                        rawOutput,
                        cursor,
                        out var objectEnd))
                {
                    return false;
                }

                appObjectText = rawOutput.Substring(
                    keyIndex,
                    objectEnd - keyIndex + 1);

                return true;
            }

            searchIndex =
                keyIndex + needle.Length;
        }

        return false;
    }

    private static bool TryFindBalancedObjectEnd(
        string text,
        int openingBraceIndex,
        out int closingBraceIndex)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = openingBraceIndex;
             i < text.Length;
             i++)
        {
            var ch = text[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (ch == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '{')
            {
                depth++;
                continue;
            }

            if (ch != '}')
            {
                continue;
            }

            depth--;

            if (depth == 0)
            {
                closingBraceIndex = i;
                return true;
            }

            if (depth < 0)
            {
                break;
            }
        }

        closingBraceIndex = -1;
        return false;
    }

    private sealed class VdfObjectParser
    {
        private readonly string _text;
        private int _position;

        public VdfObjectParser(
            string text)
        {
            _text = text;
        }

        public VdfObject ParseRootObject(
            string expectedRootKey)
        {
            SkipWhitespace();

            var rootKey = ReadQuotedString();

            if (!string.Equals(
                    rootKey,
                    expectedRootKey,
                    StringComparison.Ordinal))
            {
                throw new VdfParseException();
            }

            SkipWhitespace();
            Expect('{');

            var result = ParseObjectBody();

            SkipWhitespace();

            if (_position != _text.Length)
            {
                throw new VdfParseException();
            }

            return result;
        }

        private VdfObject ParseObjectBody()
        {
            var result = new VdfObject();

            while (true)
            {
                SkipWhitespace();

                if (_position >= _text.Length)
                {
                    throw new VdfParseException();
                }

                if (_text[_position] == '}')
                {
                    _position++;
                    return result;
                }

                var key = ReadQuotedString();

                SkipWhitespace();

                if (_position >= _text.Length)
                {
                    throw new VdfParseException();
                }

                if (_text[_position] == '{')
                {
                    _position++;

                    result.Objects[key] =
                        ParseObjectBody();

                    continue;
                }

                var value = ReadQuotedString();

                result.Strings[key] =
                    value;
            }
        }

        private string ReadQuotedString()
        {
            SkipWhitespace();

            Expect('"');

            var result =
                new System.Text.StringBuilder();

            var escaped = false;

            while (_position < _text.Length)
            {
                var ch = _text[_position++];

                if (escaped)
                {
                    result.Append(ch);
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (ch == '"')
                {
                    return result.ToString();
                }

                result.Append(ch);
            }

            throw new VdfParseException();
        }

        private void SkipWhitespace()
        {
            while (_position < _text.Length &&
                   char.IsWhiteSpace(
                       _text[_position]))
            {
                _position++;
            }
        }

        private void Expect(
            char expected)
        {
            if (_position >= _text.Length ||
                _text[_position] != expected)
            {
                throw new VdfParseException();
            }

            _position++;
        }
    }

    private sealed class VdfObject
    {
        public Dictionary<string, VdfObject> Objects { get; } =
            new(StringComparer.Ordinal);

        public Dictionary<string, string> Strings { get; } =
            new(StringComparer.Ordinal);

        public bool TryGetObject(
            string key,
            out VdfObject value)
            => Objects.TryGetValue(
                key,
                out value!);

        public bool TryGetString(
            string key,
            out string? value)
        {
            if (Strings.TryGetValue(
                    key,
                    out var found))
            {
                value = found;
                return true;
            }

            value = null;
            return false;
        }
    }

    private sealed class VdfParseException :
        Exception;
}
