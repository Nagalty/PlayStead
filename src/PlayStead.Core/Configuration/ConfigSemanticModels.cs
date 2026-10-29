namespace PlayStead.Core.Configuration;

public enum ConfigChangeKind { Added, Removed, Modified }
public enum ConfigChangeConfidence { High, Medium, Low }

public sealed record ConfigSemanticChange(
    string Path,
    string? DisplayName,
    string? OldValue,
    string? CurrentValue,
    ConfigChangeKind ChangeKind,
    bool CanRestore,
    ConfigChangeConfidence Confidence,
    string TechnicalPath);

public sealed record ConfigDocument(
    string Format,
    string OriginalText,
    object Value,
    string NewLine = "\r\n",
    bool HasBom = false,
    string? Indentation = "  ");

public interface IConfigKeyPolicy
{
    bool IsProtected(string technicalPath);
    bool IsUserSetting(string technicalPath);
}

public sealed class EmptyConfigKeyPolicy : IConfigKeyPolicy
{
    public bool IsProtected(string technicalPath) => false;
    public bool IsUserSetting(string technicalPath) => false;
}
