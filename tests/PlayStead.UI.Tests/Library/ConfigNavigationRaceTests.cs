using PlayStead.Core.Configuration;
using PlayStead.UI.Library;
namespace PlayStead.UI.Tests.Library;
public sealed class ConfigNavigationRaceTests
{
    private static ConfigSemanticChange Change(string path) => new(path,null,"1","2",ConfigChangeKind.Modified,true,ConfigChangeConfidence.High,path);
    [Fact]
    public async Task Late_failure_from_old_load_cannot_clear_new_projection()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<ConfigSemanticChange>>();
        var service = new Service { Compare = (path, token) => path == "old" ? pending.Task : Task.FromResult<IReadOnlyList<ConfigSemanticChange>>([Change("new")]) };
        var vm = new ConfigChangesViewModel(service);
        var old = vm.LoadAsync("old", "ref", CancellationToken.None);
        await vm.LoadAsync("new", "ref", CancellationToken.None);
        pending.SetException(new IOException("old missing")); await old;
        Assert.Equal("new", Assert.Single(vm.Changes).DisplayName);
    }
    [Fact]
    public async Task Navigation_during_confirmation_must_not_restore_new_installation()
    {
        var confirm = new TaskCompletionSource<bool>(); var service = new Service();
        var vm = new ConfigChangesViewModel(service, _ => confirm.Task);
        await vm.LoadAsync("old", "ref", CancellationToken.None); vm.Changes[0].IsSelected = true;
        var restore = vm.RestoreSelectedChangesCommand.ExecuteAsync(null);
        await vm.LoadAsync("new", "ref", CancellationToken.None); confirm.SetResult(true); await restore;
        Assert.Equal(0, service.Restores);
    }
    [Fact]
    public async Task Late_restore_completion_after_navigation_does_not_replace_new_rows()
    {
        var pending = new TaskCompletionSource<ConfigRestoreResult>(); var service = new Service { Restore = () => pending.Task };
        var vm = new ConfigChangesViewModel(service, _ => Task.FromResult(true));
        await vm.LoadAsync("old", "ref", CancellationToken.None); vm.Changes[0].IsSelected = true;
        var restore = vm.RestoreSelectedChangesCommand.ExecuteAsync(null);
        await vm.LoadAsync("new", "ref", CancellationToken.None);
        pending.SetResult(new(true,"backup",[Change("old")],null)); await restore;
        Assert.Equal("new", Assert.Single(vm.Changes).DisplayName); Assert.Equal(string.Empty, vm.Feedback);
    }
    private sealed class Service : IConfigSemanticService
    {
        public Func<string,CancellationToken,Task<IReadOnlyList<ConfigSemanticChange>>> Compare = (p,t) => Task.FromResult<IReadOnlyList<ConfigSemanticChange>>([Change(p)]);
        public Func<Task<ConfigRestoreResult>> Restore = () => Task.FromResult(new ConfigRestoreResult(false,null,[],"failed")); public int Restores;
        public Task<IReadOnlyList<ConfigSemanticChange>> CompareConfigAsync(string p,string r,CancellationToken t) => Compare(p,t);
        public Task<ConfigRestoreResult> RestoreConfigValuesAsync(string p,string r,IReadOnlyCollection<string> ids,CancellationToken t) { Restores++; return Restore(); }
    }
}
