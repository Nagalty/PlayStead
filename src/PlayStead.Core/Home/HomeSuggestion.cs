using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.Core.ProviderInstallUpdate;
using ProviderMetadata = PlayStead.Core.ProviderGameMetadata.ProviderGameMetadata;

namespace PlayStead.Core.Home;

public sealed record HomeSuggestion(
    GameId GameId,
    string Title,
    ProviderKind Provider,
    string ProviderGameId,
    GameMediaIdentity? MediaIdentity,
    DayOfWeek SelectionDay,
    ProviderMetadata? Metadata = null,
    ProviderInstallUpdateState? UpdateState = null);
