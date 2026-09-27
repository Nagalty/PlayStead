using PlayStead.Providers.Steam;
using System.Reflection;

namespace PlayStead.Providers.Tests;

public sealed class SteamAppInfoReaderTests
{
    [Fact]
    public void Batch_opens_and_parses_appinfo_once_for_28_exact_appids()
    {
        var expected = Enumerable.Range(1, 28)
            .ToDictionary(value => (uint)value, value => ($"Developer {value}", $"Publisher {value}"));
        var bytes = CreateAppInfo(expected);
        var openCount = 0;
        var reader = new SteamAppInfoReader(_ =>
        {
            openCount++;
            return new MemoryStream(bytes, writable: false);
        });

        var entries = reader.FindMany("ignored.vdf", expected.Keys);

        Assert.Equal(1, openCount);
        Assert.Equal(28, entries.Count);
        foreach (var expectedEntry in expected)
        {
            var actual = entries[expectedEntry.Key];
            Assert.Equal(expectedEntry.Key, actual.AppId);
            Assert.Equal(expectedEntry.Value.Item1, actual.Developer);
            Assert.Equal(expectedEntry.Value.Item2, actual.Publisher);
        }
    }

    [Fact]
    public void Batch_preserves_exact_matching_and_missing_entries_do_not_leak_neighbors()
    {
        var bytes = CreateAppInfo(new Dictionary<uint, (string Developer, string Publisher)>
        {
            [41] = ("Forty One Dev", "Forty One Pub"),
            [42] = ("Forty Two Dev", "Forty Two Pub"),
            [43] = ("Forty Three Dev", "Forty Three Pub")
        });
        var reader = new SteamAppInfoReader(_ => new MemoryStream(bytes, writable: false));

        var entries = reader.FindMany("ignored.vdf", [(uint)42, (uint)999]);

        var match = Assert.Single(entries);
        Assert.Equal((uint)42, match.Key);
        Assert.Equal("Forty Two Dev", match.Value.Developer);
        Assert.Equal("Forty Two Pub", match.Value.Publisher);
        Assert.False(entries.ContainsKey(41));
        Assert.False(entries.ContainsKey(43));
        Assert.False(entries.ContainsKey(999));
    }

    [Fact]
    public void Real_appinfo_resolves_enshrouded_exactly_when_present()
    {
        var path = @"C:\Program Files (x86)\Steam\appcache\appinfo.vdf";
        if (!File.Exists(path)) return;
        var result = new SteamAppInfoReader().Find(path, 1203620, "Enshrouded");
        Assert.NotNull(result);
        Assert.Equal("Keen Games GmbH", result!.Developer);
        Assert.Equal("Keen Games GmbH", result.Publisher);
    }

    [Fact]
    public void Real_appinfo_resolves_a_second_exact_appid_when_present()
    {
        var path = @"C:\Program Files (x86)\Steam\appcache\appinfo.vdf";
        if (!File.Exists(path)) return;
        var result = new SteamAppInfoReader().Find(path, 250820, "SteamVR");
        Assert.NotNull(result);
        Assert.Equal((uint)250820, result!.AppId);
        Assert.Equal("Tool", result.Type);
    }

    [Fact]
    public void Real_appinfo_classifies_Oasis_driver_as_application_when_present()
    {
        var path = @"C:\Program Files (x86)\Steam\appcache\appinfo.vdf";
        if (!File.Exists(path)) return;
        var result = new SteamAppInfoReader().Find(path, 3824490);
        Assert.NotNull(result);
        Assert.Equal("Application", result!.Type);
    }

    [Fact]
    public void Real_appinfo_exposes_public_build_and_depot_manifest_evidence_when_present()
    {
        var path = @"C:\Program Files (x86)\Steam\appcache\appinfo.vdf";
        if (!File.Exists(path)) return;

        var result = new SteamAppInfoReader().Find(path, 553850);

        Assert.NotNull(result);
        Assert.Equal("25480438", result!.PublicBuildId);
        Assert.Equal("9065746766815828060", result.PublicDepotManifests!["553854"]);
    }

    [Fact]
    public void Real_appinfo_exposes_windows_launch_target_for_GuildWars2_when_present()
    {
        var path = @"C:\Program Files (x86)\Steam\appcache\appinfo.vdf";
        if (!File.Exists(path)) return;

        var result = new SteamAppInfoReader().Find(path, 1284210);

        Assert.NotNull(result);
        var launch = Assert.Single(result!.LaunchConfigurations!,
            x => x.OsList?.Contains("windows", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Equal("Gw2-64.exe", launch.Executable);
    }

    [Fact]
    public void Real_appinfo_exposes_missing_windows_target_for_stale_3DMark_when_present()
    {
        var appInfoPath = @"C:\Program Files (x86)\Steam\appcache\appinfo.vdf";
        var installPath = @"G:\SteamLibrary\steamapps\common\3DMark";
        if (!File.Exists(appInfoPath) || !Directory.Exists(installPath)) return;

        var result = new SteamAppInfoReader().Find(appInfoPath, 223850);

        Assert.NotNull(result);
        var launch = Assert.Single(result!.LaunchConfigurations!,
            x => x.OsList?.Contains("windows", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Equal("bin/x64/3DMark.exe", launch.Executable.Replace('\\', '/'));
        Assert.False(File.Exists(Path.Combine(installPath, launch.Executable.Replace('/', Path.DirectorySeparatorChar))));
    }

    private static byte[] CreateAppInfo(
        IReadOnlyDictionary<uint, (string Developer, string Publisher)> entries)
    {
        var assembly = Assembly.Load("ValveKeyValue");
        var objectType = assembly.GetType("ValveKeyValue.KVObject", throwOnError: true)!;
        var serializerType = assembly.GetType("ValveKeyValue.KVSerializer", throwOnError: true)!;
        var formatType = assembly.GetType("ValveKeyValue.KVSerializationFormat", throwOnError: true)!;
        var optionsType = assembly.GetType("ValveKeyValue.KVSerializerOptions", throwOnError: true)!;
        var tableType = assembly.GetType("ValveKeyValue.StringTable", throwOnError: true)!;
        var serializer = serializerType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [Enum.Parse(formatType, "KeyValues1Binary")])!;
        var stringTable = Activator.CreateInstance(tableType)!;
        var payloads = new List<(uint AppId, byte[] Payload)>();
        var options = Activator.CreateInstance(optionsType)!;
        optionsType.GetProperty("StringTable")!.SetValue(options, stringTable);
        var add = objectType.GetMethod("Add", [typeof(string), objectType])!;
        var serialize = serializerType.GetMethod(
            "Serialize",
            [typeof(Stream), objectType, typeof(string), optionsType])!;

        foreach (var entry in entries)
        {
            var root = objectType.GetMethod("Collection", Type.EmptyTypes)!.Invoke(null, null)!;
            add.Invoke(root, ["developer", Activator.CreateInstance(objectType, entry.Value.Developer)!]);
            add.Invoke(root, ["publisher", Activator.CreateInstance(objectType, entry.Value.Publisher)!]);
            using var payload = new MemoryStream();
            serialize.Invoke(serializer, [payload, root, "appinfo", options]);
            payloads.Add((entry.Key, payload.ToArray()));
        }

        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(0x07564429u);
        writer.Write(1u);
        writer.Write(0L);

        foreach (var payload in payloads)
        {
            writer.Write(payload.AppId);
            writer.Write(checked((uint)(60 + payload.Payload.Length)));
            writer.Write(0u);
            writer.Write(0u);
            writer.Write(0ul);
            writer.Write(new byte[20]);
            writer.Write(0u);
            writer.Write(new byte[20]);
            writer.Write(payload.Payload);
        }

        writer.Write(0u);
        writer.Write(0u);
        var stringTableOffset = output.Position;
        var tableValues = (string[])tableType.GetMethod("ToArray")!.Invoke(stringTable, null)!;
        writer.Write(checked((uint)tableValues.Length));
        foreach (var value in tableValues)
        {
            writer.Write(System.Text.Encoding.UTF8.GetBytes(value));
            writer.Write((byte)0);
        }

        output.Position = 8;
        writer.Write(stringTableOffset);
        writer.Flush();
        return output.ToArray();
    }
}
