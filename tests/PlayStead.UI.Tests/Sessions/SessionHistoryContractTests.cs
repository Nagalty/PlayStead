using System.Reflection;
using PlayStead.Core.Persistence;
using PlayStead.Core.Sessions;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Sessions;

public sealed class SessionHistoryContractTests
{
    private static readonly Assembly UiAssembly =
        typeof(SessionViewModel).Assembly;

    [Fact]
    [Trait("Task11Cycle", "BHistoryContract")]
    public void Session_view_model_constructor_accepts_history_and_detail_dependencies()
    {
        var expectedTypes = new[]
        {
            typeof(ILibraryStore),
            typeof(SessionMonitor),
            typeof(TimeProvider),
            typeof(ISessionStore),
            typeof(ISessionCorrectionStore),
            typeof(ISessionRuntime),
            typeof(SessionCorrectionPolicy)
        };

        var matchingConstructor =
            typeof(SessionViewModel)
                .GetConstructors()
                .SingleOrDefault(
                    constructor =>
                        constructor
                            .GetParameters()
                            .Select(parameter => parameter.ParameterType)
                            .SequenceEqual(expectedTypes));

        Assert.NotNull(matchingConstructor);
    }

    [Fact]
    [Trait("Task11Cycle", "BHistoryContract")]
    public void Session_view_model_exposes_recent_history_projection_and_empty_state()
    {
        var recentSessions =
            typeof(SessionViewModel)
                .GetProperty(
                    "RecentSessions",
                    BindingFlags.Instance |
                    BindingFlags.Public);

        Assert.NotNull(recentSessions);
        Assert.True(recentSessions.CanRead);
        Assert.False(recentSessions.CanWrite);

        var historyItemType =
            RequireUiType(
                "RecentSessionItemViewModel");

        var expectedRecentType =
            typeof(IReadOnlyList<>)
                .MakeGenericType(
                    historyItemType);

        Assert.Equal(
            expectedRecentType,
            recentSessions.PropertyType);

        var hasRecentSessions =
            typeof(SessionViewModel)
                .GetProperty(
                    "HasRecentSessions",
                    BindingFlags.Instance |
                    BindingFlags.Public);

        Assert.NotNull(hasRecentSessions);
        Assert.Equal(
            typeof(bool),
            hasRecentSessions.PropertyType);
        Assert.True(hasRecentSessions.CanRead);
        Assert.False(hasRecentSessions.CanWrite);
    }

    [Fact]
    [Trait("Task11Cycle", "BHistoryContract")]
    public void Recent_history_item_contract_carries_identity_display_duration_and_status_flags()
    {
        var type =
            RequireUiType(
                "RecentSessionItemViewModel");

        Assert.True(type.IsClass);
        Assert.True(type.IsPublic);

        RequireReadableProperty(
            type,
            "SessionId",
            typeof(Guid));

        RequireReadableProperty(
            type,
            "GameId",
            typeof(Guid));

        RequireReadableProperty(
            type,
            "Title",
            typeof(string));

        RequireReadableProperty(
            type,
            "StartedAtLabel",
            typeof(string));

        RequireReadableProperty(
            type,
            "DurationLabel",
            typeof(string));

        RequireReadableProperty(
            type,
            "IsRecovered",
            typeof(bool));

        RequireReadableProperty(
            type,
            "IsCorrected",
            typeof(bool));
    }

    [Fact]
    [Trait("Task11Cycle", "BHistoryContract")]
    public void Session_view_model_exposes_selected_detail_projection()
    {
        var property =
            typeof(SessionViewModel)
                .GetProperty(
                    "SelectedSessionDetail",
                    BindingFlags.Instance |
                    BindingFlags.Public);

        Assert.NotNull(property);
        Assert.Equal(
            typeof(SessionDetailViewModel),
            property.PropertyType);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
    }

    [Fact]
    [Trait("Task11Cycle", "BHistoryContract")]
    public void Session_view_model_exposes_async_recent_session_selection()
    {
        var method =
            typeof(SessionViewModel)
                .GetMethod(
                    "SelectRecentSessionAsync",
                    BindingFlags.Instance |
                    BindingFlags.Public,
                    binder: null,
                    types:
                    [
                        typeof(Guid),
                        typeof(CancellationToken)
                    ],
                    modifiers: null);

        Assert.NotNull(method);
        Assert.Equal(
            typeof(Task),
            method.ReturnType);
    }

    private static Type RequireUiType(
        string shortName)
    {
        var type =
            UiAssembly.GetType(
                $"PlayStead.UI.Sessions.{shortName}",
                throwOnError: false,
                ignoreCase: false);

        Assert.NotNull(type);

        return type!;
    }

    private static void RequireReadableProperty(
        Type declaringType,
        string name,
        Type expectedType)
    {
        var property =
            declaringType.GetProperty(
                name,
                BindingFlags.Instance |
                BindingFlags.Public);

        Assert.NotNull(property);
        Assert.Equal(
            expectedType,
            property.PropertyType);
        Assert.True(property.CanRead);
    }
}
