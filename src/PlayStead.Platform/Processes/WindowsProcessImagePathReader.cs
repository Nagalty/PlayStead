using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using PlayStead.Platform.Processes.Discovery;

namespace PlayStead.Platform.Processes;

internal sealed class WindowsProcessImagePathReader
{
    internal const uint ProcessQueryLimitedInformation = 0x1000;
    private const int ErrorInsufficientBuffer = 122;
    private const int InitialBufferLength = 260;
    private const int MaximumBufferLength = 32768;

    private readonly IWindowsProcessImagePathNativeApi _native;

    public WindowsProcessImagePathReader()
        : this(new WindowsProcessImagePathNativeApi())
    {
    }

    internal WindowsProcessImagePathReader(
        IWindowsProcessImagePathNativeApi native)
    {
        ArgumentNullException.ThrowIfNull(native);
        _native = native;
    }

    public string? Resolve(int processId)
        => ResolveWithDiagnostics(processId).NormalizedPath;

    internal WindowsProcessImagePathResolution ResolveWithDiagnostics(int processId)
    {
        nint handle = 0;
        try
        {
            handle = _native.OpenProcess(
                ProcessQueryLimitedInformation,
                inheritHandle: false,
                processId);

            if (handle is 0 or -1)
                return Failure("OpenProcessFailed", _native.LastError);

            for (var capacity = InitialBufferLength;
                 capacity <= MaximumBufferLength;
                 capacity = Math.Min(capacity * 2, MaximumBufferLength))
            {
                var path = new StringBuilder(capacity);
                var length = (uint)path.Capacity;

                if (_native.QueryFullProcessImageName(
                        handle,
                        path,
                        ref length))
                {
                    var rawPath = path.ToString();
                    if (length == 0 || string.IsNullOrWhiteSpace(rawPath))
                        return Failure("EmptyPath", null);

                    var normalized = Normalize(rawPath);
                    return normalized is null
                        ? Failure("MalformedPath", null, rawPath)
                        : new WindowsProcessImagePathResolution(
                            rawPath,
                            normalized,
                            null,
                            null);
                }

                if (_native.LastError != ErrorInsufficientBuffer ||
                    capacity == MaximumBufferLength)
                {
                    return Failure(
                        _native.LastError == ErrorInsufficientBuffer
                            ? "InsufficientBuffer"
                            : "QueryFailed",
                        _native.LastError);
                }
            }

            return Failure("QueryFailed", _native.LastError);
        }
        catch (Win32Exception error)
        {
            return Failure("Win32Exception", error.NativeErrorCode);
        }
        catch (UnauthorizedAccessException)
        {
            return Failure("AccessDenied", null);
        }
        catch (InvalidOperationException)
        {
            return Failure("ProcessExited", null);
        }
        catch (NotSupportedException)
        {
            return Failure("NotSupported", null);
        }
        catch (DllNotFoundException)
        {
            return Failure("NativeApiUnavailable", null);
        }
        catch (EntryPointNotFoundException)
        {
            return Failure("NativeApiUnavailable", null);
        }
        finally
        {
            if (handle is not 0 and not -1)
                _native.CloseHandle(handle);
        }
    }

    private static WindowsProcessImagePathResolution Failure(
        string category,
        int? nativeErrorCode,
        string? rawPath = null) =>
        new(rawPath, null, category, nativeErrorCode);

    private static string? Normalize(string path)
    {
        try
        {
            return WindowsExecutablePath.NormalizeRoot(path);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

internal sealed record WindowsProcessImagePathResolution(
    string? RawPath,
    string? NormalizedPath,
    string? FailureCategory,
    int? NativeErrorCode);

internal interface IWindowsProcessImagePathNativeApi
{
    int LastError { get; }

    nint OpenProcess(
        uint desiredAccess,
        bool inheritHandle,
        int processId);

    bool QueryFullProcessImageName(
        nint processHandle,
        StringBuilder executablePath,
        ref uint size);

    bool CloseHandle(nint handle);
}

internal sealed class WindowsProcessImagePathNativeApi :
    IWindowsProcessImagePathNativeApi
{
    public int LastError => Marshal.GetLastWin32Error();

    public nint OpenProcess(
        uint desiredAccess,
        bool inheritHandle,
        int processId)
        => NativeMethods.OpenProcess(
            desiredAccess,
            inheritHandle,
            processId);

    public bool QueryFullProcessImageName(
        nint processHandle,
        StringBuilder executablePath,
        ref uint size)
        => NativeMethods.QueryFullProcessImageName(
            processHandle,
            flags: 0,
            executablePath,
            ref size);

    public bool CloseHandle(nint handle)
        => NativeMethods.CloseHandle(handle);

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(nint handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern nint OpenProcess(
            uint desiredAccess,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
            int processId);

        [DllImport(
            "kernel32.dll",
            EntryPoint = "QueryFullProcessImageNameW",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryFullProcessImageName(
            nint processHandle,
            uint flags,
            StringBuilder executablePath,
            ref uint size);
    }
}
