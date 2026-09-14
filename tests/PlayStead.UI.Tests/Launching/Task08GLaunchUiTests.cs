using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using PlayStead.Core.Library;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Launching;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;

namespace PlayStead.UI.Tests.Launching;

public sealed class Task08GLaunchUiTests
{
    [Theory]
    [InlineData(typeof(GameQuickPanelViewModel))]
    [InlineData(typeof(GameDetailViewModel))]
    public void Game_surface_exposes_shared_launch_model_for_zero_one_and_multiple_options(Type surfaceType)
    {
        var property = FindLaunchProperty(surfaceType);

        foreach (var optionCount in new[] { 0, 1, 2 })
        {
            var gameId = GameId.New();
            var item = new LibraryItemViewModel(gameId, "Game", ProviderKind.Steam, "Steam", @"D:\Games\Game", null);
            var installations = new List<GameInstallation>
            {
                Installation(gameId, ProviderKind.Manual, "manual", true),
                Installation(gameId, ProviderKind.Steam, "333", true, false),
                Installation(GameId.New(), ProviderKind.Steam, "444", true)
            };
            var supported = Enumerable.Range(1, optionCount)
                .Select(index => Installation(gameId, ProviderKind.Steam, (index * 111).ToString(), index == optionCount))
                .ToArray();
            installations.AddRange(supported);
            var launcher = new RecordingLauncher();
            var service = new GameLaunchService(launcher);
            var navigation = new NavigationService();
            navigation.Navigate(new NavigationRequest(AppRoute.Library));

            // Allow either composition with the existing launch model or installations + service.
            // No new production API is referenced at compile time, so missing wiring is an assertion failure.
            var arguments = new object[]
            {
                item, navigation, installations, service,
                new GameLaunchViewModel(gameId, installations, service)
            };
            var constructor = surfaceType.GetConstructors().FirstOrDefault(candidate =>
                candidate.GetParameters().Any(parameter =>
                    parameter.ParameterType == typeof(GameLaunchViewModel) ||
                    parameter.ParameterType == typeof(GameLaunchService)) &&
                candidate.GetParameters().All(parameter => arguments.Any(parameter.ParameterType.IsInstanceOfType)));
            Assert.True(constructor is not null, $"{surfaceType.Name} cannot receive the shared launch model or launch service.");
            var surface = constructor!.Invoke(constructor.GetParameters()
                .Select(parameter => arguments.First(parameter.ParameterType.IsInstanceOfType)).ToArray());
            var launch = Assert.IsType<GameLaunchViewModel>(property.GetValue(surface));

            Assert.Equal(supported, launch.LaunchOptions);
            Assert.Equal(optionCount > 0, launch.CanPlay);
            Assert.Equal(optionCount > 1, launch.HasMultipleLaunchOptions);
            Assert.Same(supported.LastOrDefault(), launch.DefaultInstallation);
            Assert.Equal(optionCount > 0, launch.TryPlayDefault());
            if (optionCount == 0)
            {
                Assert.Empty(launcher.OpenedUris);
            }
            else
            {
                Assert.Equal($"steam://rungameid/{optionCount * 111}", Assert.Single(launcher.OpenedUris).AbsoluteUri);
            }

            Assert.Equal(AppRoute.Library, navigation.CurrentRoute);
        }
    }

    [Theory]
    [InlineData("Library/LibraryView.xaml", "GameQuickPanel", typeof(GameQuickPanelViewModel))]
    [InlineData("Library/GameDetailView.xaml", null, typeof(GameDetailViewModel))]
    public void Game_surface_declares_bound_Play_and_conditional_installation_choice(
        string file, string? existingContainerName, Type surfaceType)
    {
        var document = XDocument.Load(FindUiFile(file));
        var scope = existingContainerName is null
            ? document.Root!
            : Assert.Single(document.Descendants(), element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == existingContainerName));
        var elements = ExpandLocalControls(scope).ToArray();
        var play = elements.FirstOrDefault(element => element.Name.LocalName == "Button" &&
            (element.Attribute("Content")?.Value == "Jouer" || element.Descendants().Any(child => child.Attribute("Text")?.Value == "Jouer")));
        Assert.True(play is not null, $"{file}: main Jouer action is missing.");
        Assert.True(HasBinding(play!, "IsEnabled", "CanPlay"), $"{file}: Jouer availability must bind to CanPlay.");
        Assert.True(play!.Attribute("Click") is not null || HasBinding(play, "Command"),
            $"{file}: Jouer has no action hookup.");

        var modelProperty = FindLaunchProperty(surfaceType);
        Assert.Contains(elements.SelectMany(element => element.Attributes()), attribute =>
            attribute.Value.Contains("{Binding", StringComparison.Ordinal) &&
            Regex.IsMatch(attribute.Value, $@"\b{Regex.Escape(modelProperty.Name)}\b"));

        Assert.Contains(elements, element => HasBinding(element, "ItemsSource", "LaunchOptions"));
        Assert.Contains(elements, element => HasBinding(element, "Visibility", "HasMultipleLaunchOptions"));
    }

    [Theory]
    [InlineData("GameQuickPanelViewModel", 2)]
    [InlineData("GameDetailViewModel", 1)]
    public void MainWindow_supplies_launch_context_when_constructing_game_surface(string typeName, int oldArgumentCount)
    {
        var source = File.ReadAllText(FindUiFile("MainWindow.xaml.cs"));
        Assert.True(typeof(PlayStead.UI.MainWindow).GetConstructors().Any(constructor =>
                constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(GameLaunchService))),
            "MainWindow must receive the shared GameLaunchService from DI.");
        var calls = ConstructorArguments(source, typeName).ToArray();
        Assert.NotEmpty(calls);
        Assert.All(calls, arguments => Assert.True(TopLevelArgumentCount(arguments) > oldArgumentCount,
            $"MainWindow constructs {typeName} without launch context."));

        // Source contract for the composition boundary, complementing real VM tests above.
        // The library snapshot must supply options for a game, not only its default installation.
        Assert.Matches(@"(?s)(?:Get\w*Installations\s*\([^;]*?(?:gameId|GameId)|Installations[^;]*?GameId)", source);
        Assert.DoesNotContain("steam://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Process.Start", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_host_resolves_real_shell_launcher_and_shared_launch_service_without_launching()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", nameof(Task08GLaunchUiTests), Guid.NewGuid().ToString("N"));
        using var host = PlaySteadHost.Build(UserDataLayout.FromRoot(root));
        // Resolve only: never call Open on the real shell launcher or start the host.
        Assert.IsType<ShellExternalUriLauncher>(host.Services.GetService<IExternalUriLauncher>());
        var service = host.Services.GetService<GameLaunchService>();
        Assert.NotNull(service);
        Assert.Same(service, host.Services.GetService<GameLaunchService>());
    }

    private static PropertyInfo FindLaunchProperty(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.PropertyType == typeof(GameLaunchViewModel)).ToArray();
        Assert.True(properties.Length == 1, $"{type.Name} must expose one public GameLaunchViewModel (found {properties.Length}).");
        return properties[0];
    }

    private static bool HasBinding(XElement element, string attributeName, string? property = null)
    {
        var value = element.Attribute(attributeName)?.Value;
        return value is not null && value.Contains("{Binding", StringComparison.Ordinal) &&
            (property is null || Regex.IsMatch(value, $@"\b{Regex.Escape(property)}\b"));
    }

    private static IEnumerable<XElement> ExpandLocalControls(XElement root)
    {
        foreach (var element in root.DescendantsAndSelf())
        {
            yield return element;
            const string prefix = "clr-namespace:PlayStead.UI.";
            if (!element.Name.NamespaceName.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var folder = element.Name.NamespaceName[prefix.Length..].Split(';')[0].Replace('.', '/');
            var path = Path.Combine(UiDirectory(), folder, element.Name.LocalName + ".xaml");
            if (File.Exists(path))
            {
                foreach (var nested in XDocument.Load(path).Root!.DescendantsAndSelf())
                {
                    yield return nested;
                }
            }
        }
    }

    private static IEnumerable<string> ConstructorArguments(string source, string typeName)
    {
        foreach (Match match in Regex.Matches(source, $@"new\s+{Regex.Escape(typeName)}\s*\("))
        {
            var start = match.Index + match.Length;
            var depth = 1;
            for (var index = start; index < source.Length; index++)
            {
                if (source[index] == '(') depth++;
                if (source[index] == ')') depth--;
                if (depth == 0)
                {
                    yield return source[start..index];
                    break;
                }
            }
        }
    }

    private static int TopLevelArgumentCount(string arguments)
    {
        var depth = 0;
        var count = string.IsNullOrWhiteSpace(arguments) ? 0 : 1;
        foreach (var character in arguments)
        {
            if (character == '(') depth++;
            if (character == ')') depth--;
            if (character == ',' && depth == 0) count++;
        }
        return count;
    }

    private static string UiDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "src", "PlayStead.UI");
            if (Directory.Exists(path)) return path;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("PlayStead.UI source directory not found.");
    }

    private static string FindUiFile(string relativePath) => Path.Combine(UiDirectory(), relativePath);

    private static GameInstallation Installation(GameId gameId, ProviderKind provider, string id, bool preferred, bool present = true) =>
        new(InstallationId.New(), gameId, provider, id, $@"D:\Games\{id}", null, preferred, present,
            new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero));

    private sealed class RecordingLauncher : IExternalUriLauncher
    {
        public List<Uri> OpenedUris { get; } = [];
        public void Open(Uri uri) => OpenedUris.Add(uri);
    }
}
