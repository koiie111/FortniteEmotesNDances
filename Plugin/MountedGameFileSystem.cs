using System.Runtime.InteropServices;

namespace FortniteEmotes;

internal sealed class MountedGameFileSystem : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint CreateInterface([MarshalAs(UnmanagedType.LPUTF8Str)] string name, nint returnCode);

    // CS2 is 64-bit on both platforms; the explicit this pointer uses the platform's x64 ABI.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool FileExistsFunction(nint self,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string file,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string pathId);

    private nint _library;
    private readonly nint _fileSystem;
    private readonly FileExistsFunction _fileExists;

    internal MountedGameFileSystem(string gameDirectory, int fileExistsOffset)
    {
        if (fileExistsOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(fileExistsOffset));

        string binary = OperatingSystem.IsWindows()
            ? Path.Combine(gameDirectory, "bin", "win64", "filesystem_stdio.dll")
            : Path.Combine(gameDirectory, "bin", "linuxsteamrt64", "libfilesystem_stdio.so");
        _library = NativeLibrary.Load(binary);
        try
        {
            var factory = Marshal.GetDelegateForFunctionPointer<CreateInterface>(
                NativeLibrary.GetExport(_library, "CreateInterface"));
            _fileSystem = factory("VFileSystem017", nint.Zero);
            if (_fileSystem == nint.Zero)
                throw new InvalidOperationException("VFileSystem017 is unavailable.");

            var vtable = Marshal.ReadIntPtr(_fileSystem);
            _fileExists = Marshal.GetDelegateForFunctionPointer<FileExistsFunction>(
                Marshal.ReadIntPtr(vtable, checked(fileExistsOffset * IntPtr.Size)));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal bool FileExists(string compiledPath)
    {
        ObjectDisposedException.ThrowIf(_library == nint.Zero, this);
        return _fileExists(_fileSystem, compiledPath, "GAME");
    }

    public void Dispose()
    {
        if (_library == nint.Zero)
            return;
        NativeLibrary.Free(_library);
        _library = nint.Zero;
    }
}
