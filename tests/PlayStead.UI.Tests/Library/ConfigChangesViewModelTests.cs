using PlayStead.Core.Configuration;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class ConfigChangesViewModelTests
{
    [Fact]
    public async Task No_changes_do_not_expose_summary_or_restore()
    {
        var vm = new ConfigChangesViewModel(new FakeService([]));
        await vm.LoadAsync("current.ini", "reference.ini", CancellationToken.None);
        Assert.False(vm.HasChanges);
        Assert.False(vm.CanRestoreSelectedChanges);
    }

    [Fact]
    public async Task Changes_are_unselected_and_non_restorable_rows_cannot_be_selected()
    {
        var changes = new[]
        {
            new ConfigSemanticChange("Graphics.Vsync", null, "true", "false", ConfigChangeKind.Modified, true, ConfigChangeConfidence.High, "Graphics\u001fVsync"),
            new ConfigSemanticChange("mods", null, "[]", "[1]", ConfigChangeKind.Modified, false, ConfigChangeConfidence.High, "mods")
        };
        var vm = new ConfigChangesViewModel(new FakeService(changes));
        await vm.LoadAsync("current.ini", "reference.ini", CancellationToken.None);
        Assert.Equal("2 réglages ont changé", vm.Summary);
        Assert.All(vm.Changes, x => Assert.False(x.IsSelected));
        Assert.False(vm.Changes[1].CanSelect);
        vm.Changes[0].IsSelected = true;
        Assert.True(vm.CanRestoreSelectedChanges);
    }

    private sealed class FakeService(IReadOnlyList<ConfigSemanticChange> changes) : IConfigSemanticService
    {
        public Task<IReadOnlyList<ConfigSemanticChange>> CompareConfigAsync(string currentPath, string referencePath, CancellationToken cancellationToken) => Task.FromResult(changes);
        public Task<ConfigRestoreResult> RestoreConfigValuesAsync(string currentPath, string referencePath, IReadOnlyCollection<string> selectedChangeIds, CancellationToken cancellationToken) => Task.FromResult(new ConfigRestoreResult(true, "backup", changes.Where(x => selectedChangeIds.Contains(x.Path)).ToArray(), null));
    }
}
