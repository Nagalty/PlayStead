using System.Text;
using PlayStead.Platform.Processes;

namespace PlayStead.Platform.Tests.Processes;

public sealed class WindowsProcessImagePathReaderTests
{
    [Fact]
    public void Resolve_uses_limited_information_rights_and_releases_handle()
    {
        var native = new FakeNativeApi
        {
            Result = @"C:/Games/Test/Game.exe"
        };
        var sut = new WindowsProcessImagePathReader(native);

        var result = sut.Resolve(42);

        Assert.Equal(@"C:\Games\Test\Game.exe", result);
        Assert.Equal(WindowsProcessImagePathReader.ProcessQueryLimitedInformation, native.DesiredAccess);
        Assert.Equal(42, native.ProcessId);
        Assert.Equal(1, native.CloseCalls);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void Resolve_is_failure_safe_when_process_cannot_be_opened(int error)
    {
        var native = new FakeNativeApi
        {
            OpenedHandle = 0,
            Error = error
        };
        var sut = new WindowsProcessImagePathReader(native);

        Assert.Null(sut.Resolve(42));
        Assert.Equal(0, native.CloseCalls);
    }

    [Fact]
    public void Resolve_is_failure_safe_for_invalid_handle()
    {
        var native = new FakeNativeApi { OpenedHandle = -1 };
        var sut = new WindowsProcessImagePathReader(native);

        Assert.Null(sut.Resolve(42));
        Assert.Equal(0, native.CloseCalls);
    }

    [Fact]
    public void Resolve_is_failure_safe_when_process_exits_during_query()
    {
        var native = new FakeNativeApi { Error = 6 };
        var sut = new WindowsProcessImagePathReader(native);

        Assert.Null(sut.Resolve(42));
        Assert.Equal(1, native.QueryCalls);
        Assert.Equal(1, native.CloseCalls);
    }

    [Fact]
    public void Resolve_rejects_empty_native_result_and_releases_handle()
    {
        var native = new FakeNativeApi { Result = string.Empty };
        var sut = new WindowsProcessImagePathReader(native);

        Assert.Null(sut.Resolve(42));
        Assert.Equal(1, native.CloseCalls);
    }

    [Fact]
    public void Resolve_retries_when_native_buffer_is_insufficient()
    {
        var native = new FakeNativeApi
        {
            InsufficientBufferOnce = true,
            Result = @"C:\Games\Long\Game.exe"
        };
        var sut = new WindowsProcessImagePathReader(native);

        Assert.Equal(@"C:\Games\Long\Game.exe", sut.Resolve(42));
        Assert.Equal(2, native.QueryCalls);
        Assert.Equal(1, native.CloseCalls);
    }

    [Fact]
    public void Resolve_rejects_malformed_native_path_and_releases_handle()
    {
        var native = new FakeNativeApi { Result = @"relative\Game.exe" };
        var sut = new WindowsProcessImagePathReader(native);

        Assert.Null(sut.Resolve(42));
        Assert.Equal(1, native.CloseCalls);
    }

    private sealed class FakeNativeApi : IWindowsProcessImagePathNativeApi
    {
        public nint OpenedHandle { get; init; } = 123;
        public string? Result { get; init; }
        public int Error { get; set; }
        public bool InsufficientBufferOnce { get; init; }
        public uint DesiredAccess { get; private set; }
        public int ProcessId { get; private set; }
        public int QueryCalls { get; private set; }
        public int CloseCalls { get; private set; }
        public int LastError => Error;

        public nint OpenProcess(uint desiredAccess, bool inheritHandle, int processId)
        {
            DesiredAccess = desiredAccess;
            ProcessId = processId;
            return OpenedHandle;
        }

        public bool QueryFullProcessImageName(
            nint processHandle,
            StringBuilder executablePath,
            ref uint size)
        {
            QueryCalls++;
            if (InsufficientBufferOnce && QueryCalls == 1)
            {
                Error = 122;
                return false;
            }

            if (Result is null)
                return false;

            executablePath.Append(Result);
            size = (uint)Result.Length;
            return true;
        }

        public bool CloseHandle(nint handle)
        {
            CloseCalls++;
            return true;
        }
    }
}
