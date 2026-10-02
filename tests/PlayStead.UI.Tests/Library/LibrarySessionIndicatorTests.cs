using System.Reflection;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.Sessions;
using PlayStead.UI.Library;
using PlayStead.UI.Sessions;
using PlayStead.UI.Tests.TestSupport;

namespace PlayStead.UI.Tests.Library;

[Collection(PlaySteadWpfApplicationCollection.Name)]
public sealed class LibrarySessionIndicatorTests
{
    [Fact]
    [Trait("Task12", "LibraryLive")]
    public async Task Game_with_active_runtime_session_is_marked_active()
    {
        var activeGameId =
            GameId.New();

        var otherGameId =
            GameId.New();

        var viewModel =
            CreateViewModel(
                CreateLibraryStore(
                    activeGameId,
                    otherGameId),
                CreateMonitorWithSnapshot(
                    activeGameId));

        await viewModel.RefreshAsync(
            CancellationToken.None);

        var activeItem =
            Assert.Single(
                viewModel.Items,
                item =>
                    item.GameId ==
                    activeGameId);

        Assert.True(
            ReadRequiredBool(
                activeItem,
                "IsSessionActive"));

        Assert.Equal(
            "En cours",
            ReadRequiredString(
                activeItem,
                "SessionStatusLabel"));
    }

    [Fact]
    [Trait("Task12", "LibraryLive")]
    public async Task Different_game_is_not_marked_active()
    {
        var activeGameId =
            GameId.New();

        var otherGameId =
            GameId.New();

        var viewModel =
            CreateViewModel(
                CreateLibraryStore(
                    activeGameId,
                    otherGameId),
                CreateMonitorWithSnapshot(
                    activeGameId));

        await viewModel.RefreshAsync(
            CancellationToken.None);

        var inactiveItem =
            Assert.Single(
                viewModel.Items,
                item =>
                    item.GameId ==
                    otherGameId);

        Assert.False(
            ReadRequiredBool(
                inactiveItem,
                "IsSessionActive"));

        Assert.Null(
            ReadOptionalString(
                inactiveItem,
                "SessionStatusLabel"));
    }

    [Fact]
    [Trait("Task12", "LibraryLive")]
    public async Task Steam_update_badge_and_session_badge_can_coexist()
    {
        var steamGameId =
            GameId.New();

        var gogGameId =
            GameId.New();

        var viewModel =
            CreateViewModel(
                CreateLibraryStore(
                    steamGameId,
                    gogGameId),
                CreateMonitorWithSnapshot(
                    steamGameId));

        await viewModel.RefreshAsync(
            CancellationToken.None);

        var steamItem =
            Assert.Single(
                viewModel.Items,
                item =>
                    item.GameId ==
                    steamGameId);

        Assert.True(
            steamItem.HasSteamStatus);

        Assert.True(
            ReadRequiredBool(
                steamItem,
                "IsSessionActive"));

        Assert.Equal(
            "En cours",
            ReadRequiredString(
                steamItem,
                "SessionStatusLabel"));
    }

    [Fact]
    [Trait("Task12", "LibraryLive")]
    public async Task Non_Steam_game_can_be_session_active()
    {
        var steamGameId =
            GameId.New();

        var gogGameId =
            GameId.New();

        var viewModel =
            CreateViewModel(
                CreateLibraryStore(
                    steamGameId,
                    gogGameId),
                CreateMonitorWithSnapshot(
                    gogGameId));

        await viewModel.RefreshAsync(
            CancellationToken.None);

        var gogItem =
            Assert.Single(
                viewModel.Items,
                item =>
                    item.GameId ==
                    gogGameId);

        Assert.Equal(
            ProviderKind.Gog,
            gogItem.Provider);

        Assert.False(
            gogItem.HasSteamStatus);

        Assert.True(
            ReadRequiredBool(
                gogItem,
                "IsSessionActive"));

        Assert.Equal(
            "En cours",
            ReadRequiredString(
                gogItem,
                "SessionStatusLabel"));
    }

    [Fact]
    [Trait("Task12", "LibraryLive")]
    public Task Snapshot_update_reprojects_live_state_without_library_reload() =>
        PlaySteadWpfTestResources.RunAsync(async () =>
    {
        var firstGameId =
            GameId.New();

        var secondGameId =
            GameId.New();

        var monitor =
            CreateMonitorWithSnapshot(
                firstGameId);

        var viewModel =
            CreateViewModel(
                CreateLibraryStore(
                    firstGameId,
                    secondGameId),
                monitor);

        await viewModel.RefreshAsync(
            CancellationToken.None);

        Assert.True(
            ReadRequiredBool(
                Assert.Single(
                    viewModel.Items,
                    item =>
                        item.GameId ==
                        firstGameId),
                "IsSessionActive"));

        var projectionApplied = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LibraryViewModel.Items))
            {
                projectionApplied.TrySetResult();
            }
        };

        PublishSnapshot(
            monitor,
            secondGameId);
        await projectionApplied.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(
            ReadRequiredBool(
                Assert.Single(
                    viewModel.Items,
                    item =>
                        item.GameId ==
                        firstGameId),
                "IsSessionActive"));

        Assert.True(
            ReadRequiredBool(
                Assert.Single(
                    viewModel.Items,
                    item =>
                        item.GameId ==
                    secondGameId),
                "IsSessionActive"));
    });

    [Fact]
    [Trait("Task12", "LibraryLive")]
    public void Library_template_exposes_a_distinct_session_badge_binding()
    {
        var xaml =
            File.ReadAllText(
                FindRepositoryFile(
                    "src",
                    "PlayStead.UI",
                    "Library",
                    "LibraryView.xaml"));

        Assert.Contains(
            "IsSessionActive",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "SessionStatusLabel",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "HasSteamStatus",
            xaml,
            StringComparison.Ordinal);
    }

    private static LibraryViewModel CreateViewModel(
        ILibraryStore libraryStore,
        SessionMonitor monitor)
    {
        var constructor =
            typeof(LibraryViewModel)
                .GetConstructor(
                    [
                        typeof(ILibraryStore),
                        typeof(SessionMonitor)
                    ]);

        Assert.NotNull(
            constructor);

        return Assert.IsType<LibraryViewModel>(
            constructor!.Invoke(
                [
                    libraryStore,
                    monitor
                ]));
    }

    private static bool ReadRequiredBool(
        LibraryItemViewModel item,
        string propertyName)
    {
        var property =
            typeof(LibraryItemViewModel)
                .GetProperty(
                    propertyName,
                    BindingFlags.Instance |
                    BindingFlags.Public);

        Assert.NotNull(
            property);

        Assert.Equal(
            typeof(bool),
            property!.PropertyType);

        return Assert.IsType<bool>(
            property.GetValue(
                item));
    }

    private static string ReadRequiredString(
        LibraryItemViewModel item,
        string propertyName)
    {
        var value =
            ReadOptionalString(
                item,
                propertyName);

        return Assert.IsType<string>(
            value);
    }

    private static string? ReadOptionalString(
        LibraryItemViewModel item,
        string propertyName)
    {
        var property =
            typeof(LibraryItemViewModel)
                .GetProperty(
                    propertyName,
                    BindingFlags.Instance |
                    BindingFlags.Public);

        Assert.NotNull(
            property);

        Assert.Equal(
            typeof(string),
            property!.PropertyType);

        return (string?)
            property.GetValue(
                item);
    }

    private static SessionMonitor CreateMonitorWithSnapshot(
        GameId activeGameId)
    {
        var monitor =
            new SessionMonitor(
                new NeverCalledSessionRuntime(),
                SessionMonitorOptions.Default);

        SetLatestSnapshot(
            monitor,
            CreateSnapshot(
                activeGameId));

        return monitor;
    }

    private static void PublishSnapshot(
        SessionMonitor monitor,
        GameId activeGameId)
    {
        var snapshot =
            CreateSnapshot(
                activeGameId);

        SetLatestSnapshot(
            monitor,
            snapshot);

        var eventField =
            typeof(SessionMonitor)
                .GetField(
                    "SnapshotUpdated",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

        Assert.NotNull(
            eventField);

        var handlers =
            eventField!.GetValue(
                monitor)
            as Action<SessionRuntimeSnapshot>;

        Assert.NotNull(
            handlers);

        handlers!(
            snapshot);
    }

    private static void SetLatestSnapshot(
        SessionMonitor monitor,
        SessionRuntimeSnapshot snapshot)
    {
        var property =
            typeof(SessionMonitor)
                .GetProperty(
                    nameof(
                        SessionMonitor.LatestSnapshot),
                    BindingFlags.Instance |
                    BindingFlags.Public);

        Assert.NotNull(
            property);

        var setter =
            property!.GetSetMethod(
                nonPublic: true);

        Assert.NotNull(
            setter);

        setter!.Invoke(
            monitor,
            [snapshot]);
    }

    private static SessionRuntimeSnapshot CreateSnapshot(
        GameId activeGameId) =>
        new(
            DateTimeOffset.UtcNow,
            [
                CreateGameSession(
                    activeGameId)
            ]);

    private static GameSession CreateGameSession(
        GameId gameId)
    {
        var constructor =
            typeof(GameSession)
                .GetConstructors()
                .Single();

        var now =
            DateTimeOffset.UtcNow;

        var arguments =
            constructor
                .GetParameters()
                .Select(
                    parameter =>
                        CreateArgument(
                            parameter,
                            gameId,
                            now))
                .ToArray();

        return Assert.IsType<GameSession>(
            constructor.Invoke(
                arguments));
    }

    private static object? CreateArgument(
        ParameterInfo parameter,
        GameId gameId,
        DateTimeOffset now)
    {
        if (parameter.ParameterType ==
            typeof(GameId))
        {
            return gameId;
        }

        if (parameter.ParameterType ==
            typeof(Guid))
        {
            return string.Equals(
                    parameter.Name,
                    "GameId",
                    StringComparison.OrdinalIgnoreCase)
                ? gameId.Value
                : Guid.NewGuid();
        }

        if (parameter.ParameterType ==
            typeof(DateTimeOffset))
        {
            return now;
        }

        if (parameter.ParameterType ==
            typeof(DateTimeOffset?))
        {
            return null;
        }

        if (parameter.ParameterType.IsEnum)
        {
            return Activator.CreateInstance(
                parameter.ParameterType);
        }

        if (Nullable.GetUnderlyingType(
                parameter.ParameterType)
            is Type nullableType &&
            nullableType.IsEnum)
        {
            return null;
        }

        throw new InvalidOperationException(
            $"Unsupported GameSession constructor parameter: {parameter.Name} ({parameter.ParameterType}).");
    }

    private static ILibraryStore CreateLibraryStore(
        GameId steamGameId,
        GameId gogGameId)
    {
        var now =
            DateTimeOffset.UtcNow;

        return new StubLibraryStore(
            new LibrarySnapshot(
                [
                    new LogicalGame(
                        steamGameId,
                        "Steam Game",
                        false,
                        now,
                        now),

                    new LogicalGame(
                        gogGameId,
                        "GOG Game",
                        false,
                        now,
                        now)
                ],
                [
                    new GameInstallation(
                        InstallationId.New(),
                        steamGameId,
                        ProviderKind.Steam,
                        "123456",
                        @"G:\SteamLibrary\steamapps\common\Steam Game",
                        20_000_000_000,
                        true,
                        true,
                        now),

                    new GameInstallation(
                        InstallationId.New(),
                        gogGameId,
                        ProviderKind.Gog,
                        "gog-game",
                        @"G:\Games\GOG Game",
                        15_000_000_000,
                        true,
                        true,
                        now)
                ]));
    }

    private static string FindRepositoryFile(
        params string[] relativeParts)
    {
        var starts =
            new[]
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory
            };

        foreach (var start in starts)
        {
            var current =
                new DirectoryInfo(
                    Path.GetFullPath(
                        start));

            while (current is not null)
            {
                var candidateParts =
                    new string[
                        relativeParts.Length + 1];

                candidateParts[0] =
                    current.FullName;

                Array.Copy(
                    relativeParts,
                    0,
                    candidateParts,
                    1,
                    relativeParts.Length);

                var candidate =
                    Path.Combine(
                        candidateParts);

                if (File.Exists(
                        candidate))
                {
                    return candidate;
                }

                current =
                    current.Parent;
            }
        }

        throw new FileNotFoundException(
            $"Could not locate repository file: {Path.Combine(relativeParts)}");
    }

    private sealed class StubLibraryStore(
        LibrarySnapshot snapshot) :
        ILibraryStore
    {
        public Task ApplySourceScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LibrarySnapshot>
            LoadSnapshotAsync(
                CancellationToken cancellationToken) =>
            Task.FromResult(
                snapshot);
    }

    private sealed class NeverCalledSessionRuntime :
        ISessionRuntime
    {
        public Task<SessionRuntimeSnapshot>
            RefreshAsync(
                CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "Library projection must consume SessionMonitor state, not poll the runtime directly.");

        public Task CorrectSessionAsync(
            SessionCorrectionRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
