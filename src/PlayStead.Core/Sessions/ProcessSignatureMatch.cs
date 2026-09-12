namespace PlayStead.Core.Sessions;

public sealed record ProcessSignatureMatch(
    Guid GameId,
    IReadOnlyList<ProcessSnapshot> MainProcesses,
    IReadOnlyList<ProcessSnapshot> AuxiliaryProcesses,
    IReadOnlyList<ProcessSnapshot> ExcludedProcesses)
{
    public bool HasMainProcess => MainProcesses.Count > 0;
}
