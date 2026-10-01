#:project Surl.Conformance.UnitLibrary/Surl.Conformance.UnitLibrary.csproj

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
using System.Text;
using Surl.Conformance;

return await LibcurlWebSocketDriver.RunAsync(args);

/// <summary>Parses the command line, checks the library's pin, loads it and runs the script.</summary>
internal static class LibcurlWebSocketDriver
{
    public static async Task<int> RunAsync(string[] args)
    {
        DriverArguments arguments;
        try
        {
            arguments = DriverArguments.Parse(args);
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

    private static async Task<int> RunScriptAsync(DriverArguments arguments)
    {
        Libcurl.curl_global_init(Libcurl.GlobalDefault);
        IntPtr easy = Libcurl.curl_easy_init();
        try
        {
            Libcurl.curl_easy_setopt(easy, Libcurl.OptionUrl, arguments.Url);
            Libcurl.curl_easy_setopt(easy, Libcurl.OptionConnectOnly, 2);
            int performed = Libcurl.curl_easy_perform(easy);
            Console.WriteLine($"perform: {Libcurl.Describe(performed)}");
            if (performed != 0)
            {
                return 1;
            }

            foreach (DriverStep step in arguments.Steps)
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
    private static async Task<string> SendAsync(IntPtr easy, DriverStep step, TimeSpan timeout)
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

        string shown = step.Payload.Length > 64 ? $"{Shown(step.Payload[..64])}...\"" : $"{Shown(step.Payload)}\"";
        return $"send {Libcurl.FlagNames(step.Flags)} {step.Payload.Length} bytes \"{shown}: {Libcurl.Describe(result)}, sent {offset}{(calls > 1 ? $" in {calls} calls" : string.Empty)}";
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
                return $"recv: {Libcurl.Describe(result)}";
            }

            // struct curl_ws_frame { int age; int flags; curl_off_t offset; curl_off_t bytesleft; size_t len; }
            int flags = Marshal.ReadInt32(frame, 4);
            long offset = Marshal.ReadInt64(frame, 8);
            long bytesLeft = Marshal.ReadInt64(frame, 16);
            byte[] payload = buffer[..(int)received];
            return $"recv: {Libcurl.Describe(result)}, flags {Libcurl.FlagNames((uint)flags)}, offset {offset}, bytesleft {bytesLeft}, {payload.Length} bytes \"{Shown(payload)}\"";
        }
    }

    private static string Shown(byte[] bytes)
    {
        var text = new StringBuilder();
        foreach (byte value in bytes)
        {
            text.Append(value switch
            {
                (byte)'\\' => "\\\\",
                (byte)'"' => "\\\"",
                >= 0x20 and < 0x7F => ((char)value).ToString(),
                _ => $"\\x{value:X2}",
            });
        }

        return text.ToString();
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

/// <summary>One step of the script: a curl_ws_send of a payload with flags, or a curl_ws_recv.</summary>
internal sealed record DriverStep(bool IsReceive, uint Flags, byte[] Payload);

/// <summary>The driver's command line: an optional library, the receive timeout, the URL and the steps.</summary>
internal sealed record DriverArguments(string? LibraryPath, TimeSpan ReceiveTimeout, string Url, IReadOnlyList<DriverStep> Steps)
{
    public static DriverArguments Parse(string[] args)
    {
        string? libraryPath = null;
        int timeoutMilliseconds = 5000;
        string? url = null;
        var steps = new List<DriverStep>();
        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--library":
                    libraryPath = ValueAfter(args, ref index);
                    break;
                case "--recv-timeout":
                    timeoutMilliseconds = int.TryParse(ValueAfter(args, ref index), out int parsed) && parsed > 0
                        ? parsed
                        : throw new FormatException("--recv-timeout takes a positive number of milliseconds.");
                    break;
                default:
                    if (url is null)
                    {
                        url = args[index];
                    }
                    else
                    {
                        steps.Add(ParseStep(args[index]));
                    }

                    break;
            }
        }

        return url is null
            ? throw new FormatException("a ws:// or wss:// URL is required, followed by the steps.")
            : new DriverArguments(libraryPath, TimeSpan.FromMilliseconds(timeoutMilliseconds), url, steps);
    }

    private static string ValueAfter(string[] args, ref int index) =>
        ++index < args.Length ? args[index] : throw new FormatException($"{args[index - 1]} needs a value.");

    private static DriverStep ParseStep(string step)
    {
        if (step == "recv")
        {
            return new DriverStep(true, 0, []);
        }

        string[] parts = step.Split(':', 3);
        int repeat = 1;
        if (parts.Length == 3 && parts[0].StartsWith("send*", StringComparison.Ordinal))
        {
            repeat = int.TryParse(parts[0]["send*".Length..], out int count) && count > 0
                ? count
                : throw new FormatException($"step '{step}' repeats its payload a number of times that is not a positive number.");
            parts[0] = "send";
        }

        if (parts.Length != 3 || parts[0] != "send")
        {
            throw new FormatException($"step '{step}' is neither recv, send:<FLAGS>:<payload> nor send*<count>:<FLAGS>:<payload>.");
        }

        uint flags = 0;
        foreach (string name in parts[1].Split('+'))
        {
            flags |= Libcurl.FlagValue(name) ?? throw new FormatException($"step '{step}' names flag '{name}', not TEXT, BINARY, CONT, CLOSE, PING or PONG.");
        }

        byte[] payload = Unescape(parts[2]);
        return new DriverStep(false, flags, [.. Enumerable.Repeat(payload, repeat).SelectMany(bytes => bytes)]);
    }

    private static byte[] Unescape(string text)
    {
        var bytes = new List<byte>();
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] != '\\' || index + 1 == text.Length)
            {
                bytes.Add(checked((byte)text[index]));
                continue;
            }

            char escape = text[++index];
            if (escape == 'x' && index + 2 < text.Length)
            {
                bytes.Add(Convert.ToByte(text.Substring(index + 1, 2), 16));
                index += 2;
                continue;
            }

            bytes.Add(escape switch { 'r' => 13, 'n' => 10, 't' => 9, '0' => 0, _ => (byte)escape });
        }

        return [.. bytes];
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

    private static readonly (string Name, uint Value)[] Flags =
        [("TEXT", 1), ("BINARY", 2), ("CONT", 4), ("CLOSE", 8), ("PING", 16), ("OFFSET", 32), ("PONG", 64)];

    public static uint? FlagValue(string name) =>
        Flags.Where(flag => flag.Name == name && flag.Name != "OFFSET").Select(flag => (uint?)flag.Value).FirstOrDefault();

    public static string FlagNames(uint flags)
    {
        string names = string.Join('+', Flags.Where(flag => (flags & flag.Value) != 0).Select(flag => flag.Name));
        return names.Length == 0 ? "0" : names;
    }

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
