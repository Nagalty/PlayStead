namespace PlayStead.Platform.Processes.Discovery;

public static class WindowsExecutablePath
{
    public static string NormalizeRoot(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var path = rootPath.Replace('/', '\\');
        if (path.Length < 3 || !IsAsciiLetter(path[0]) ||
            path[1] != ':' || path[2] != '\\')
            throw new ArgumentException(
                "Path must be a fully qualified Windows drive path.", nameof(rootPath));

        var lastComponentEnd = path.Length;
        while (lastComponentEnd > 3 && path[lastComponentEnd - 1] == '\\')
            lastComponentEnd--;

        var componentStart = 3;
        for (var index = 3; index <= lastComponentEnd; index++)
        {
            if (index < lastComponentEnd && path[index] != '\\')
            {
                if (path[index] < ' ' || path[index] is '<' or '>' or ':' or '"' or '|' or '?' or '*')
                    throw new ArgumentException(
                        "Path contains an invalid Windows name character.", nameof(rootPath));
                continue;
            }

            var component = path.AsSpan(componentStart, index - componentStart);
            if (component.Length == 0 && lastComponentEnd > 3 ||
                component.SequenceEqual(".") || component.SequenceEqual("..") ||
                component.Length > 0 && component[^1] is '.' or ' ')
                throw new ArgumentException(
                    "Path contains an untrusted Windows name component.", nameof(rootPath));

            componentStart = index + 1;
        }

        var canonical = Path.GetFullPath(path);
        return canonical.Length > 3 ? canonical.TrimEnd('\\') : canonical;
    }

    public static bool IsStrictlyUnderRoot(string canonicalRoot, string candidatePath)
    {
        var root = NormalizeRoot(canonicalRoot);
        string candidate;
        try
        {
            candidate = NormalizeRoot(candidatePath);
        }
        catch (ArgumentException)
        {
            return false;
        }

        var prefix = root.EndsWith('\\') ? root : root + "\\";
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAsciiLetter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
