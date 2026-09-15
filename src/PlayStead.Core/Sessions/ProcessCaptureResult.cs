namespace PlayStead.Core.Sessions;

public sealed record ProcessCaptureResult
{
    public IReadOnlyList<ProcessSnapshot> Processes { get; }
    public bool IsComplete { get; }

    public ProcessCaptureResult(IReadOnlyList<ProcessSnapshot> processes, bool isComplete)
    {
        ArgumentNullException.ThrowIfNull(processes);
        Processes = Array.AsReadOnly(processes.ToArray());
        IsComplete = isComplete;
    }
}
