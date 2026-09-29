using System.Globalization;
using PlayStead.Core.Library;
using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Tests.Steam;

public sealed class SteamLocalConfigActivityReaderTests
{
    private const ulong SteamId64Base = 76561197960265728UL;

    [Fact]
    public void Playtime_only_is_converted_from_minutes()
    {
        using var fixture = SteamFixture.Create(
            loginUsers: SteamFixture.LoginUsers(SteamFixture.Account(1)),
            localConfig: SteamFixture.LocalConfig(("730", "120", null)));

        var values = new SteamLocalConfigActivityReader().Read(fixture.Root, ["730"]);

        Assert.Equal(TimeSpan.FromMinutes(120), values["730"].Total);
    }

    [Fact]
    public void Disconnected_playtime_is_added_to_playtime()
    {
        using var fixture = SteamFixture.Create(
            loginUsers: SteamFixture.LoginUsers(SteamFixture.Account(1)),
            localConfig: SteamFixture.LocalConfig(("1643320", "5801", "13")));

        var values = new SteamLocalConfigActivityReader().Read(fixture.Root, ["1643320"]);

        Assert.Equal(TimeSpan.FromMinutes(5814), values["1643320"].Total);
    }

    [Fact]
    public void Missing_or_invalid_counters_are_unknown()
    {
        using var fixture = SteamFixture.Create(
            loginUsers: SteamFixture.LoginUsers(SteamFixture.Account(1)),
            localConfig: SteamFixture.LocalConfig(
                ("missing-playtime", null, "12"),
                ("negative", "-1", null),
                ("invalid", "not-a-number", null),
                ("overflow", long.MaxValue.ToString(CultureInfo.InvariantCulture), "1")));

        var values = new SteamLocalConfigActivityReader().Read(
            fixture.Root,
            ["missing-playtime", "negative", "invalid", "overflow"]);

        Assert.Empty(values);
    }

    [Fact]
    public void Multiple_app_ids_are_read_without_cross_contamination()
    {
        using var fixture = SteamFixture.Create(
            loginUsers: SteamFixture.LoginUsers(SteamFixture.Account(1)),
            localConfig: SteamFixture.LocalConfig(
                ("730", "120", null),
                ("1172710", "3060", "0")));

        var values = new SteamLocalConfigActivityReader().Read(fixture.Root, ["730", "1172710"]);

        Assert.Equal(TimeSpan.FromMinutes(120), values["730"].Total);
        Assert.Equal(TimeSpan.FromMinutes(3060), values["1172710"].Total);
    }

    [Fact]
    public void Most_recent_account_wins_and_other_account_playtime_cannot_win()
    {
        using var fixture = SteamFixture.Create(
            loginUsers: SteamFixture.LoginUsers(
                SteamFixture.Account(1),
                SteamFixture.Account(2, mostRecent: true)),
            localConfigByAccount:
            [
                ("1", SteamFixture.LocalConfig(("730", "9999", null))),
                ("2", SteamFixture.LocalConfig(("730", "120", null)))
            ]);

        var values = new SteamLocalConfigActivityReader().Read(fixture.Root, ["730"]);

        Assert.Equal(TimeSpan.FromMinutes(120), values["730"].Total);
    }

    [Fact]
    public void Multiple_accounts_without_current_marker_are_unknown()
    {
        using var fixture = SteamFixture.Create(
            loginUsers: SteamFixture.LoginUsers(SteamFixture.Account(1), SteamFixture.Account(2)),
            localConfigByAccount:
            [
                ("1", SteamFixture.LocalConfig(("730", "9999", null))),
                ("2", SteamFixture.LocalConfig(("730", "120", null)))
            ]);

        var values = new SteamLocalConfigActivityReader().Read(fixture.Root, ["730"]);

        Assert.Empty(values);
    }

    [Fact]
    public void Current_account_missing_localconfig_does_not_fallback_to_another_account()
    {
        using var fixture = SteamFixture.Create(
            loginUsers: SteamFixture.LoginUsers(
                SteamFixture.Account(1),
                SteamFixture.Account(2, mostRecent: true)),
            localConfigByAccount:
            [("1", SteamFixture.LocalConfig(("730", "9999", null))) ]);

        var values = new SteamLocalConfigActivityReader().Read(fixture.Root, ["730"]);

        Assert.Empty(values);
    }

    [Fact]
    public void Reads_localconfig_while_steam_keeps_the_file_open_for_writing()
    {
        using var fixture = SteamFixture.Create(
            loginUsers: SteamFixture.LoginUsers(SteamFixture.Account(1)),
            localConfig: SteamFixture.LocalConfig(("730", "120", null)));
        var path = Path.Combine(fixture.Root, "userdata", "1", "config", "localconfig.vdf");
        using var steamWriter = new FileStream(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete);

        var values = new SteamLocalConfigActivityReader().Read(fixture.Root, ["730"]);

        Assert.Equal(TimeSpan.FromMinutes(120), values["730"].Total);
    }

    [Fact]
    public async Task Provider_source_projects_localconfig_lifetime_into_metadata()
    {
        using var fixture = SteamFixture.Create(
            loginUsers: SteamFixture.LoginUsers(SteamFixture.Account(1)),
            localConfig: SteamFixture.LocalConfig(("730", "120", "5")),
            manifestAppId: "730");
        var gameId = GameId.New();
        var installation = new GameInstallation(
            InstallationId.New(),
            gameId,
            ProviderKind.Steam,
            "730",
            Path.Combine(fixture.Root, "steamapps", "common", "Test game"),
            null,
            true,
            true,
            DateTimeOffset.UtcNow);

        var source = new SteamLocalProviderActivitySource(
            new WindowsSteamRootLocator([fixture.Root]),
            new SteamLibraryFoldersReader(),
            new SteamAppManifestReader(),
            new SteamLocalConfigActivityReader());

        var result = await source.GetAsync([installation], CancellationToken.None);

        Assert.Equal(TimeSpan.FromMinutes(125), result.Single().TotalPlaytime);
    }

    private sealed class SteamFixture : IDisposable
    {
        private SteamFixture(string root) => Root = root;
        public string Root { get; }

        public static SteamFixture Create(
            string loginUsers,
            string? localConfig = null,
            IReadOnlyList<(string AccountId, string LocalConfig)>? localConfigByAccount = null,
            string? manifestAppId = null)
        {
            var root = Path.Combine(Path.GetTempPath(), "PlayStead-Steam-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "config"));
            File.WriteAllText(Path.Combine(root, "config", "loginusers.vdf"), loginUsers);
            var configs = localConfigByAccount ?? [("1", localConfig ?? LocalConfig())];
            foreach (var (accountId, config) in configs)
            {
                var directory = Path.Combine(root, "userdata", accountId, "config");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "localconfig.vdf"), config);
            }

            if (manifestAppId is not null)
            {
                Directory.CreateDirectory(Path.Combine(root, "steamapps"));
                File.WriteAllText(
                    Path.Combine(root, "steamapps", "appmanifest_" + manifestAppId + ".acf"),
                    $"\"AppState\" {{ \"appid\" \"{manifestAppId}\" \"name\" \"Test game\" }}");
            }

            return new SteamFixture(root);
        }

        public static string Account(int accountId, bool mostRecent = false) =>
            $"\"{SteamId64Base + (ulong)accountId}\"\n{{\n" +
            (mostRecent ? "\t\"MostRecent\"\t\"1\"\n" : string.Empty) +
            "\t\"AccountName\"\t\"test\"\n}";

        public static string LoginUsers(params string[] users) =>
            "\"users\"\n{\n" + string.Join("\n", users) + "\n}";

        public static string LocalConfig(params (string AppId, string? Playtime, string? Disconnected)[] apps) =>
            "\"UserLocalConfigStore\"\n{\n\t\"Software\"\n\t{\n\t\t\"Valve\"\n\t\t{\n\t\t\t\"Steam\"\n\t\t\t{\n\t\t\t\t\"apps\"\n\t\t\t\t{\n" +
            string.Join("\n", apps.Select(app =>
                $"\t\t\t\t\t\"{app.AppId}\"\n\t\t\t\t\t{{\n" +
                (app.Playtime is null ? string.Empty : $"\t\t\t\t\t\t\"Playtime\"\t\"{app.Playtime}\"\n") +
                (app.Disconnected is null ? string.Empty : $"\t\t\t\t\t\t\"PlaytimeDisconnected\"\t\"{app.Disconnected}\"\n") +
                "\t\t\t\t\t}")) +
            "\n\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n}";

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
