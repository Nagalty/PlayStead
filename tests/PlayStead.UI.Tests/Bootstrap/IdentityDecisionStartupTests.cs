using Microsoft.Extensions.DependencyInjection;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Notifications;
using PlayStead.Core.Persistence;
using PlayStead.Data.Identity;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class IdentityDecisionStartupTests
{
    [Fact]
    public async Task Build_registers_the_identity_decision_runtime_with_an_empty_singleton_candidate_source()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

        var layout = UserDataLayout.FromRoot(root);

        using var host = PlaySteadHost.Build(layout);

        Assert.IsType<SqliteIdentityDecisionStore>(
            host.Services.GetRequiredService<IIdentityDecisionStore>());
        Assert.IsType<SqliteIdentityDecisionService>(
            host.Services.GetRequiredService<IIdentityDecisionService>());
        Assert.IsType<IdentityDecisionContextProvider>(
            host.Services.GetRequiredService<IIdentityDecisionContextProvider>());
        Assert.IsType<IdentityDecisionContextGateway>(
            host.Services.GetRequiredService<IIdentityDecisionContextGateway>());
        Assert.IsType<IdentityDecisionNotificationOrchestrator>(
            host.Services.GetRequiredService<IIdentityDecisionNotificationOrchestrator>());
        Assert.IsType<IdentityDecisionApplicationService>(
            host.Services.GetRequiredService<IIdentityDecisionApplicationService>());

        var source = host.Services.GetRequiredService<IIdentityDecisionCandidateSource>();

        Assert.IsType<EmptyIdentityDecisionCandidateSource>(source);
        Assert.Same(source, host.Services.GetRequiredService<IIdentityDecisionCandidateSource>());
        Assert.Empty(await source.GetCandidatesAsync(GameId.New(), CancellationToken.None));
    }
}
