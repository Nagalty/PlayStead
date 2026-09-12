using PlayStead.Platform.SingleInstance;
using PlayStead.UI.SingleInstance;

namespace PlayStead.UI.Tests.SingleInstance;

public sealed class InvocationRoutingTests
{
    [Fact]
    public async Task Activate_true_requests_window_activation_exactly_once_and_null_deep_link_does_not_navigate()
    {
        var activator = new RecordingWindowActivator();
        var sut = new AppInvocationHandler(activator);

        await sut.HandleAsync(
            new AppInvocation(
                Activate: true,
                DeepLink: null),
            CancellationToken.None);

        Assert.Equal(1, activator.ActivationCount);
    }

    [Fact]
    public async Task Activate_false_does_not_request_window_activation()
    {
        var activator = new RecordingWindowActivator();
        var sut = new AppInvocationHandler(activator);

        await sut.HandleAsync(
            new AppInvocation(
                Activate: false,
                DeepLink: null),
            CancellationToken.None);

        Assert.Equal(0, activator.ActivationCount);
    }

    private sealed class RecordingWindowActivator : IWindowActivator
    {
        public int ActivationCount { get; private set; }

        public Task ActivateAsync(
            CancellationToken cancellationToken)
        {
            ActivationCount++;
            return Task.CompletedTask;
        }
    }
}
