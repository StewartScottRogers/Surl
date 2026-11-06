using System.Runtime.InteropServices;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Reads a file's extended attribute through the C library's <c>getxattr</c>, which the base class
/// library has no API for, so a test can see what the pinned upstream curl build's <c>--xattr</c>
/// wrote. Linux and macOS only: their <c>getxattr</c> signatures differ, so each has its own entry.
/// </summary>
internal static class ExtendedFileAttributes
{
    private const int ValueCapacity = 4096;

    /// <summary>
    /// Returns the value of the attribute <paramref name="name"/> on <paramref name="path"/> as
    /// UTF-8 text, or <see langword="null"/> when the file does not have it.
    /// </summary>
    public static string? Read(string path, string name)
    {
        var value = new byte[ValueCapacity];
        var length = OperatingSystem.IsMacOS()
            ? MacGetXattr(path, name, value, value.Length, 0, 0)
            : LinuxGetXattr(path, name, value, value.Length);
        return length < 0 ? null : Encoding.UTF8.GetString(value, 0, (int)length);
    }

    [DllImport("libc", EntryPoint = "getxattr", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern nint LinuxGetXattr(string path, string name, byte[] value, nint size);

    [DllImport("libc", EntryPoint = "getxattr", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern nint MacGetXattr(string path, string name, byte[] value, nint size, uint position, int options);
}
