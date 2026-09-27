namespace PlayStead.Core.Updates;

public readonly record struct SemanticVersion(int Major, int Minor, int Patch, string? PreRelease = null) : IComparable<SemanticVersion>
{
    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value.Trim();
        var plus = text.IndexOf('+');
        if (plus >= 0) text = text[..plus];
        var dash = text.IndexOf('-');
        var core = dash >= 0 ? text[..dash] : text;
        var parts = core.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor) || !int.TryParse(parts[2], out var patch)) return false;
        if (major < 0 || minor < 0 || patch < 0) return false;
        var pre = dash >= 0 ? text[(dash + 1)..] : null;
        if (pre is not null && pre.Length == 0) return false;
        version = new SemanticVersion(major, minor, patch, pre);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        var result = Major.CompareTo(other.Major);
        if (result != 0) return result;
        result = Minor.CompareTo(other.Minor);
        if (result != 0) return result;
        result = Patch.CompareTo(other.Patch);
        if (result != 0) return result;
        if (PreRelease is null) return other.PreRelease is null ? 0 : 1;
        if (other.PreRelease is null) return -1;
        var left = PreRelease.Split('.');
        var right = other.PreRelease.Split('.');
        for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            if (i >= left.Length) return -1;
            if (i >= right.Length) return 1;
            var l = left[i]; var r = right[i];
            var ln = int.TryParse(l, out var li); var rn = int.TryParse(r, out var ri);
            if (ln && rn) { result = li.CompareTo(ri); }
            else if (ln != rn) { result = ln ? -1 : 1; }
            else { result = StringComparer.Ordinal.Compare(l, r); }
            if (result != 0) return result;
        }
        return 0;
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}{(PreRelease is null ? string.Empty : $"-{PreRelease}")}";
}
