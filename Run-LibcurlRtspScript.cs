#:project Surl.Conformance.UnitLibrary/Surl.Conformance.UnitLibrary.csproj

// Drives the pinned upstream libcurl's RTSP requests (CURLOPT_RTSP_REQUEST) and its interleaved
// receive through a script of steps on one easy handle, so the RTSP requests only libcurl's API
// sends - every one but OPTIONS - can be measured against Record-CurlExchange.ps1 -Raw (ADR-0074
// decision 12). Record-CurlExchange.ps1 -LibcurlRtsp runs it in place of curl.exe:
//
//   dotnet run Run-LibcurlRtspScript.cs -- [--library <path>] [--timeout <ms>]
//       <rtsp:// URL> <step> [<step> ...]
//
// The library is the libcurl pinned in UpstreamCurlBuilds.json for this platform, or the file
// --library names; either way its SHA-256 must match a pin of kind library before it is loaded
// (PinnedLibcurlChoice), and a pinned curl executable is refused. It runs curl_easy_init, sets
// CURLOPT_URL, CURLOPT_TIMEOUT_MS (--timeout, default 10000), a write callback for response bodies
// and CURLOPT_INTERLEAVEFUNCTION, then each step in order on that one handle, so the requests share
// a connection and every option set stays set for the requests after it, as in a libcurl RTSP client:
//
//   OPTIONS DESCRIBE ANNOUNCE SETUP PLAY PAUSE TEARDOWN GET_PARAMETER SET_PARAMETER RECORD RECEIVE
//                         CURLOPT_RTSP_REQUEST RTSPREQ_<name>, then curl_easy_perform
//   stream-uri:<text>     CURLOPT_RTSP_STREAM_URI
//   transport:<text>      CURLOPT_RTSP_TRANSPORT
//   session-id:<text>     CURLOPT_RTSP_SESSION_ID
//   client-cseq:<n>       CURLOPT_RTSP_CLIENT_CSEQ
//   body:<bytes>          the body of ANNOUNCE, GET_PARAMETER and SET_PARAMETER, through
//                         CURLOPT_POSTFIELDSIZE and CURLOPT_COPYPOSTFIELDS
//   upload:<bytes>        the same body through CURLOPT_UPLOAD 1, CURLOPT_INFILESIZE and a read callback
//   no-body               CURLOPT_UPLOAD 0, CURLOPT_POSTFIELDSIZE -1, CURLOPT_POSTFIELDS NULL
//
// Text and bytes take the backslash escapes \r \n \t \0 \\ and \xHH (LibcurlBytes.Unescape).
// Each line of standard output is one step and what it returned (LibcurlRtspReport):
//
//   setopt RTSP_TRANSPORT "RTP/AVP;unicast;client_port=4588-4589": CURLcode 0 (No error)
//   SETUP: CURLcode 0 (No error), status 200, session "12345678", cseq received 3, next cseq 4,
//       body 0 bytes "", interleaved none
//
// where status is CURLINFO_RESPONSE_CODE, session CURLINFO_RTSP_SESSION_ID, cseq received
// CURLINFO_RTSP_CSEQ_RECV, next cseq CURLINFO_RTSP_CLIENT_CSEQ, body what the write callback got
// and interleaved each call of the interleave callback ($, channel, length and payload), every run of
// bytes shown with \xHH outside printable ASCII, or as its SHA-256 past 256 bytes. A failed request
// does not stop the script. It exits 0 once every step has run, whatever each returned; 2 for a
// malformed command line; 3 when the library is not pinned; and 4, printing "Inconclusive: ..." on
// standard error, when no libcurl is pinned or installed for this platform, as on Linux and macOS,
// whose static builds carry no shared library.
using System.Reflection;
using System.Runtime.InteropServices;
using Surl.Conformance;

return await LibcurlRtspDriver.RunAsync(args);

/// <summary>Parses the command line, checks the library's pin, loads it and runs the script.</summary>
internal static class LibcurlRtspDriver
{
    private static readonly List<byte> Body = [];
    private static readonly List<byte[]> Interleaved = [];
    private static byte[] upload = [];
    private static int uploadOffset;

    // The callbacks libcurl calls, kept alive for the whole run.
    private static readonly Libcurl.DataCallback WriteBody = (data, size, count, _) => Collect(data, size * count, Body.AddRange);
    private static readonly Libcurl.DataCallback CollectInterleaved = (data, size, count, _) => Collect(data, size * count, Interleaved.Add);
    private static readonly Libcurl.DataCallback ReadUpload = ReadNextUploadBytes;

    public static async Task<int> RunAsync(string[] args)
    {
        LibcurlRtspScript script;
        try
        {
            script = LibcurlRtspScript.Parse(args);
        }
        catch (FormatException exception)
        {
            await Console.Error.WriteLineAsync($"Run-LibcurlRtspScript.cs: {exception.Message}");
            return 2;
        }

        var pins = UpstreamCurlBuildPins.Parse(await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), UpstreamCurlBuildPins.FileName)));
        var locator = new UpstreamCurlLocator(new FileSystemUpstreamCurlFileAccess());
        string? requested = script.LibraryPath is null ? null : Path.GetFullPath(script.LibraryPath);
        var choice = PinnedLibcurlChoice.Choose(locator, pins, requested, UpstreamCurlLocator.CurrentPlatform);
        if (choice.LibraryPath is null)
        {
            await Console.Error.WriteLineAsync(choice.Message);
            return choice.ExitCode;
        }

        IntPtr library = NativeLibrary.Load(choice.LibraryPath);
        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), (name, _, _) => name == Libcurl.Name ? library : IntPtr.Zero);
        RunScript(script);
        return 0;
    }

    private static void RunScript(LibcurlRtspScript script)
    {
        Libcurl.curl_global_init(Libcurl.GlobalDefault);
        IntPtr easy = Libcurl.curl_easy_init();
        try
        {
            Libcurl.curl_easy_setopt(easy, Libcurl.OptionUrl, script.Url);
            Libcurl.curl_easy_setopt(easy, Libcurl.OptionTimeoutMilliseconds, (int)script.Timeout.TotalMilliseconds);
            Libcurl.curl_easy_setopt(easy, Libcurl.OptionWriteFunction, Marshal.GetFunctionPointerForDelegate(WriteBody));
            Libcurl.curl_easy_setopt(easy, Libcurl.OptionInterleaveFunction, Marshal.GetFunctionPointerForDelegate(CollectInterleaved));
            Libcurl.curl_easy_setopt(easy, Libcurl.OptionReadFunction, Marshal.GetFunctionPointerForDelegate(ReadUpload));
            foreach (LibcurlRtspStep step in script.Steps)
            {
                Console.WriteLine(step.Kind == LibcurlRtspStepKind.Request
                    ? LibcurlRtspReport.RequestLine(step.Request, Perform(easy, step.Request))
                    : LibcurlRtspReport.SettingLine(step, Libcurl.Describe(Set(easy, step))));
            }
        }
        finally
        {
            Libcurl.curl_easy_cleanup(easy);
            Libcurl.curl_global_cleanup();
        }
    }

    private static LibcurlRtspOutcome Perform(IntPtr easy, LibcurlRtspRequest request)
    {
        Body.Clear();
        Interleaved.Clear();
        uploadOffset = 0;
        int result = Libcurl.curl_easy_setopt(easy, Libcurl.OptionRtspRequest, (int)request);
        if (result == 0)
        {
            result = Libcurl.curl_easy_perform(easy);
        }

        Libcurl.curl_easy_getinfo(easy, Libcurl.InfoResponseCode, out int status);
        Libcurl.curl_easy_getinfo(easy, Libcurl.InfoRtspSessionId, out IntPtr session);
        Libcurl.curl_easy_getinfo(easy, Libcurl.InfoRtspCSeqReceived, out int cseqReceived);
        Libcurl.curl_easy_getinfo(easy, Libcurl.InfoRtspClientCSeq, out int nextClientCSeq);
        return new LibcurlRtspOutcome(
            Libcurl.Describe(result),
            status,
            session == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(session),
            cseqReceived,
            nextClientCSeq,
            [.. Body],
            [.. Interleaved]);
    }

    // Every curl_easy_setopt the step needs, stopping at the first that fails; its CURLcode, or 0.
    private static int Set(IntPtr easy, LibcurlRtspStep step)
    {
        Func<int>[] calls = step.Kind switch
        {
            LibcurlRtspStepKind.StreamUri => [() => Libcurl.curl_easy_setopt(easy, Libcurl.OptionRtspStreamUri, step.Value)],
            LibcurlRtspStepKind.Transport => [() => Libcurl.curl_easy_setopt(easy, Libcurl.OptionRtspTransport, step.Value)],
            LibcurlRtspStepKind.SessionId => [() => Libcurl.curl_easy_setopt(easy, Libcurl.OptionRtspSessionId, step.Value)],
            LibcurlRtspStepKind.ClientCSeq => [() => Libcurl.curl_easy_setopt(easy, Libcurl.OptionRtspClientCSeq, step.Number)],
            LibcurlRtspStepKind.PostFields =>
            [
                () => Libcurl.curl_easy_setopt(easy, Libcurl.OptionUpload, 0),
                () => Libcurl.curl_easy_setopt(easy, Libcurl.OptionPostFieldSize, step.Value.Length),
                () => Libcurl.curl_easy_setopt(easy, Libcurl.OptionCopyPostFields, step.Value),
            ],
            LibcurlRtspStepKind.Upload =>
            [
                () => Libcurl.curl_easy_setopt(easy, Libcurl.OptionPostFields, IntPtr.Zero),
                () => Libcurl.curl_easy_setopt(easy, Libcurl.OptionPostFieldSize, -1),
                () => Libcurl.curl_easy_setopt(easy, Libcurl.OptionUpload, 1),
                () => Libcurl.curl_easy_setopt(easy, Libcurl.OptionInFileSize, step.Value.Length),
                () => { upload = step.Value; return 0; },
            ],
            _ =>
            [
                () => Libcurl.curl_easy_setopt(easy, Libcurl.OptionUpload, 0),
                () => Libcurl.curl_easy_setopt(easy, Libcurl.OptionPostFieldSize, -1),
                () => Libcurl.curl_easy_setopt(easy, Libcurl.OptionPostFields, IntPtr.Zero),
            ],
        };

        return calls.Select(call => call()).FirstOrDefault(result => result != 0);
    }

    // Copies what libcurl handed a callback to the given list; returns the length, which tells
    // libcurl every byte was taken.
    private static nuint Collect(IntPtr data, nuint length, Action<byte[]> add)
    {
        byte[] bytes = new byte[(int)length];
        Marshal.Copy(data, bytes, 0, bytes.Length);
        add(bytes);
        return length;
    }

    private static nuint ReadNextUploadBytes(IntPtr buffer, nuint size, nuint count, IntPtr userData)
    {
        int length = Math.Min((int)(size * count), upload.Length - uploadOffset);
        Marshal.Copy(upload, uploadOffset, buffer, length);
        uploadOffset += length;
        return (nuint)length;
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

/// <summary>The libcurl entry points and constants the driver uses, resolved to the pinned file only.</summary>
internal static class Libcurl
{
    public const string Name = "pinned-libcurl";
    public const int GlobalDefault = 3;

    // CURLoption values from include/curl/curl.h at tag curl-8_21_0: LONG + n, OBJECTPOINT or
    // STRINGPOINT 10000 + n, FUNCTIONPOINT 20000 + n.
    public const int OptionInFileSize = 14;
    public const int OptionUpload = 46;
    public const int OptionPostFieldSize = 60;
    public const int OptionTimeoutMilliseconds = 155;
    public const int OptionRtspRequest = 189;
    public const int OptionRtspClientCSeq = 193;
    public const int OptionUrl = 10002;
    public const int OptionPostFields = 10015;
    public const int OptionCopyPostFields = 10165;
    public const int OptionRtspSessionId = 10190;
    public const int OptionRtspStreamUri = 10191;
    public const int OptionRtspTransport = 10192;
    public const int OptionWriteFunction = 20011;
    public const int OptionReadFunction = 20012;
    public const int OptionInterleaveFunction = 20196;

    // CURLINFO values: STRING 0x100000 + n, LONG 0x200000 + n.
    public const int InfoResponseCode = 0x200002;
    public const int InfoRtspSessionId = 0x100024;
    public const int InfoRtspClientCSeq = 0x200025;
    public const int InfoRtspCSeqReceived = 0x200027;

    /// <summary>libcurl's write, read and interleave callback: size_t (*)(char *, size_t, size_t, void *).</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate nuint DataCallback(IntPtr data, nuint size, nuint count, IntPtr userData);

    public static string Describe(int result) => $"CURLcode {result} ({Marshal.PtrToStringUTF8(curl_easy_strerror(result))})";

    // A NUL-terminated copy of the bytes, for a char * option.
    public static int curl_easy_setopt(IntPtr easy, int option, byte[] value) => curl_easy_setopt_bytes(easy, option, [.. value, 0]);

    [DllImport(Name)]
    public static extern int curl_global_init(int flags);

    [DllImport(Name)]
    public static extern void curl_global_cleanup();

    [DllImport(Name)]
    public static extern IntPtr curl_easy_init();

    // curl_easy_setopt and curl_easy_getinfo are variadic; on win-x64, the only platform with a
    // pinned library, a variadic call passes its arguments as a fixed one does. A C long is 32
    // bits there. libcurl copies every string option, so a pinned array lives long enough.
    [DllImport(Name, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    public static extern int curl_easy_setopt(IntPtr easy, int option, string value);

    [DllImport(Name, EntryPoint = "curl_easy_setopt")]
    private static extern int curl_easy_setopt_bytes(IntPtr easy, int option, byte[] value);

    [DllImport(Name)]
    public static extern int curl_easy_setopt(IntPtr easy, int option, int value);

    [DllImport(Name)]
    public static extern int curl_easy_setopt(IntPtr easy, int option, IntPtr value);

    [DllImport(Name)]
    public static extern int curl_easy_getinfo(IntPtr easy, int info, out int value);

    [DllImport(Name)]
    public static extern int curl_easy_getinfo(IntPtr easy, int info, out IntPtr value);

    [DllImport(Name)]
    public static extern int curl_easy_perform(IntPtr easy);

    [DllImport(Name)]
    public static extern void curl_easy_cleanup(IntPtr easy);

    [DllImport(Name)]
    public static extern IntPtr curl_easy_strerror(int result);
}
