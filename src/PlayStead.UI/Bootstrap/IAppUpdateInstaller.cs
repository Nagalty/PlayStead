using PlayStead.Core.Updates;

namespace PlayStead.UI.Bootstrap;

public interface IAppUpdateInstaller
{
    bool TryStart(AppUpdateState state);
}
