namespace PlayStead.Core.Configuration;

public interface IConfigDocumentAdapter
{
    string Format { get; }
    bool CanHandle(string path, string content);
    ConfigDocument Parse(string path, string content);
    IReadOnlyList<ConfigSemanticChange> Compare(ConfigDocument reference, ConfigDocument current, IConfigKeyPolicy? policy = null);
    ConfigDocument ApplyChanges(ConfigDocument current, IEnumerable<ConfigSemanticChange> selectedChanges);
    string Serialize(ConfigDocument document);
}

public interface IConfigDocumentAdapterResolver
{
    IConfigDocumentAdapter? Resolve(string path, string content);
}

public sealed class ConfigDocumentAdapterResolver(IEnumerable<IConfigDocumentAdapter> adapters) : IConfigDocumentAdapterResolver
{
    private readonly IReadOnlyList<IConfigDocumentAdapter> _adapters = adapters.ToArray();
    public IConfigDocumentAdapter? Resolve(string path, string content) => _adapters.FirstOrDefault(x => x.CanHandle(path, content));
}
