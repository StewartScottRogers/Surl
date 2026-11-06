#:project Conformance/Surl.Conformance.UnitLibrary/Surl.Conformance.UnitLibrary.csproj

// Drives the pinned upstream libcurl's WebSocket API (curl_ws_send, curl_ws_recv) through a
// script of steps, so the client frames only libcurl's API sends - text, binary, fragments,
// PING, PONG, CLOSE with a code - can be measured against Record-CurlExchange.ps1 -Raw
// (ADR-0071 decision 10). Record-CurlExchange.ps1 -LibcurlWebSocket runs it in place of curl.exe:
//
//   dotnet run Run-LibcurlWebSocketScript.cs -- [--library <path>] [--recv-timeout <ms>]
//       <ws:// or wss:// URL> <step> [<step> ...]
//
// The library is the libcurl pinned in UpstreamCurlBuilds.json for this platform, or the file
// --library names; either way its SHA-256 must match a pin of kind library before it is loaded,
// and a pinned curl executable is refused. It runs curl_easy_init, CURLOPT_URL, CURLOPT_CONNECT_ONLY
// 2 and curl_easy_perform, which sends the upgrade request and reads the 101, then each step:
//
//   send:<FLAGS>:<payload>  one curl_ws_send of the payload with the flags, '+'-joined from TEXT,
//                           BINARY, CONT, CLOSE, PING and PONG (e.g. send:TEXT+CONT:hel). The
//                           payload takes the backslash escapes \r \n \t \0 \\ and \xHH, so a
//                           CLOSE with code 1000 and reason bye is send:CLOSE:\x03\xE8bye.
//                           A payload libcurl takes in part is sent on from where it
//                           stopped, waiting out CURLE_AGAIN up to --recv-timeout.
//   send*<count>:<FLAGS>:<payload>
//                           the same, with the payload repeated count times, so a message
//                           too long for a command line can be sent: send*2097152:BINARY:a.
//   recv                    one curl_ws_recv into a 65536-byte buffer, retried on CURLE_AGAIN
//                           until data comes or --recv-timeout (default 5000 ms) passes.
//
// Each line of standard output is one call and what it returned:
//
//   perform: CURLcode 0 (No error)
//   send TEXT 5 bytes "hello": CURLcode 0 (No error), sent 5
//   recv: CURLcode 0 (No error), flags TEXT, offset 0, bytesleft 0, 5 bytes "hello"
//
// Bytes are shown as text with every byte outside printable ASCII written \xHH. It exits 0 once
// every step has run, whatever each returned; 1 when curl_easy_perform fails (no step runs);
// 2 for a malformed command line; 3 when the library is not pinned; and 4, printing
// "Inconclusive: ..." on standard error, when no libcurl is pinned or installed for this
// platform, as on Linux and macOS, whose static builds carry no shared library.
using System.Reflection;
using System.Runtime.InteropServices;
using Surl.Conformance;

return await LibcurlWebSocketDriver.RunAsync(args);

/// <summary>Parses the command line, checks the library's pin, loads it and runs the script.</summary>
internal static class LibcurlWebSocketDriver
{
    public static async Task<int> RunAsync(string[] args)
    {
        LibcurlWebSocketScript arguments;
        try
        {
            arguments = LibcurlWebSocketScript.Parse(args);
        }
        catch (FormatException exception)
        {
            await Console.Error.WriteLineAsync($"Run-LibcurlWebSocketScript.cs: {exception.Message}");
            return 2;
        }

        (string? libraryPath, int refusal) = await FindPinnedLibraryAsync(arguments.LibraryPath);
        if (libraryPath is null)
        {
            return refusal;
        }

        IntPtr library = NativeLibrary.Load(libraryPath);
        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), (name, _, _) => name == Libcurl.Name ? library : IntPtr.Zero);
        return await RunScriptAsync(arguments);
    }

    // The library to load and 0, or no library and the exit code saying why: 3 when the file is
    // not a pinned libcurl, 4 (inconclusive) when none is pinned or installed for this platform.
    private static async Task<(string? Path, int ExitCode)> FindPinnedLibraryAsync(string? requestedPath)
    {
        try
        {
            var pins = UpstreamCurlBuildPins.Parse(await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), UpstreamCurlBuildPins.FileName)));
            var locator = new UpstreamCurlLocator(new FileSystemUpstreamCurlFileAccess());
            if (requestedPath is not null)
            {
                string fullPath = Path.GetFullPath(requestedPath);
                locator.RequirePinnedLibrary(pins, fullPath);
                return (fullPath, 0);
            }

            var location = locator.LocateLibrary(pins, UpstreamCurlLocator.CurrentPlatform);
            if (location.Build is null)
            {
                await Console.Error.WriteLineAsync($"Inconclusive: {location.Message}");
                return (null, 4);
            }

            return (location.Build.DefaultPath, 0);
        }
        catch (Exception exception) when (exception is UnpinnedUpstreamCurlException or FileNotFoundException)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            return (null, 3);
        }
    }

    private static async Task<int> RunScriptAsync(LibcurlWebSocketScript arguments)
    {
        Libcurl.curl_global_init(Libcurl.GlobalDefault);
        IntPtr easy = Libcurl.curl_easy_init();
        try
        {
            Libcurl.curl_easy_setopt(easy, Libcurl.OptionUrl, arguments.Url);
            Libcurl.curl_easy_setopt(easy, Libcurl.OptionConnectOnly, 2);
            int performed = Libcurl.curl_easy_perform(easy);
            Console.WriteLine(LibcurlWebSocketReport.PerformLine(Libcurl.Describe(performed)));
            if (performed != 0)
            {
                return 1;
            }

            foreach (LibcurlWebSocketStep step in arguments.Steps)
            {
                Console.WriteLine(step.IsReceive ? await ReceiveAsync(easy, arguments.ReceiveTimeout) : await SendAsync(easy, step, arguments.ReceiveTimeout));
            }

            return 0;
        }
        finally
        {
            Libcurl.curl_easy_cleanup(easy);
            Libcurl.curl_global_cleanup();
        }
    }

    // A payload libcurl takes only in part is sent on from where it stopped, as curl_ws_send's
    // documentation says, waiting out CURLE_AGAIN, until all of it is sent or a call fails.
    private static async Task<string> SendAsync(IntPtr easy, LibcurlWebSocketStep step, TimeSpan timeout)
    {
        DateTimeOffset deadline = TimeProvider.System.GetUtcNow() + timeout;
        int offset = 0;
        int calls = 0;
        int result;
        do
        {
            calls++;
            result = Libcurl.curl_ws_send(easy, step.Payload[offset..], (nuint)(step.Payload.Length - offset), out nuint sent, 0, step.Flags);
            offset += (int)sent;
            if (result == Libcurl.ResultAgain && TimeProvider.System.GetUtcNow() < deadline)
            {
                await Task.Delay(20);
                result = 0;
            }
        }
        while (result == 0 && offset < step.Payload.Length);

        return LibcurlWebSocketReport.SendLine(step, Libcurl.Describe(result), offset, calls);
    }

    private static async Task<string> ReceiveAsync(IntPtr easy, TimeSpan timeout)
    {
        byte[] buffer = new byte[65536];
        DateTimeOffset deadline = TimeProvider.System.GetUtcNow() + timeout;
        while (true)
        {
            int result = Libcurl.curl_ws_recv(easy, buffer, (nuint)buffer.Length, out nuint received, out IntPtr frame);
            if (result == Libcurl.ResultAgain && TimeProvider.System.GetUtcNow() < deadline)
            {
                await Task.Delay(20);
                continue;
            }

            if (result != 0 || frame == IntPtr.Zero)
            {
                return LibcurlWebSocketReport.ReceiveFailedLine(Libcurl.Describe(result));
            }

            // struct curl_ws_frame { int age; int flags; curl_off_t offset; curl_off_t bytesleft; size_t len; }
            int flags = Marshal.ReadInt32(frame, 4);
            long offset = Marshal.ReadInt64(frame, 8);
            long bytesLeft = Marshal.ReadInt64(frame, 16);
            return LibcurlWebSocketReport.ReceiveLine(Libcurl.Describe(result), (uint)flags, offset, bytesLeft, buffer.AsSpan(0, (int)received));
        }
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, UpstreamCurlBuildPins.FileName)))
            {
                return directory.FullName;
            }
        }

        // dotnet run builds file-based apps outside the repository; the working directory is the
        // fallback, and Record-CurlExchange.ps1 sets it to the repository root.
        return Directory.GetCurrentDirectory();
    }
}

/// <summary>The libcurl entry points the driver calls, resolved to the pinned file only.</summary>
internal static class Libcurl
{
    public const string Name = "pinned-libcurl";
    public const int GlobalDefault = 3;
    public const int OptionUrl = 10002;
    public const int OptionConnectOnly = 141;
    public const int ResultAgain = 81;

    public static string Describe(int result) => $"CURLcode {result} ({Marshal.PtrToStringUTF8(curl_easy_strerror(result))})";

    [DllImport(Name)]
    public static extern int curl_global_init(int flags);

    [DllImport(Name)]
    public static extern void curl_global_cleanup();

    [DllImport(Name)]
    public static extern IntPtr curl_easy_init();

    // curl_easy_setopt is variadic; on win-x64, the only platform with a pinned library, a
    // variadic call passes its arguments as a fixed one does. A C long is 32 bits there.
    [DllImport(Name, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    public static extern int curl_easy_setopt(IntPtr easy, int option, string value);

    [DllImport(Name)]
    public static extern int curl_easy_setopt(IntPtr easy, int option, int value);

    [DllImport(Name)]
    public static extern int curl_easy_perform(IntPtr easy);

    [DllImport(Name)]
    public static extern void curl_easy_cleanup(IntPtr easy);

    [DllImport(Name)]
    public static extern IntPtr curl_easy_strerror(int result);

    [DllImport(Name)]
    public static extern int curl_ws_send(IntPtr easy, byte[] buffer, nuint length, out nuint sent, long fragmentSize, uint flags);

    [DllImport(Name)]
    public static extern int curl_ws_recv(IntPtr easy, byte[] buffer, nuint length, out nuint received, out IntPtr frame);
}
