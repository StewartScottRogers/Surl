#:project Conformance/Surl.Conformance.UnitLibrary/Surl.Conformance.UnitLibrary.csproj
#:property CustomAfterMicrosoftCommonTargets=$(MSBuildProjectDirectory)\CurlPortLibcurlReference.targets

// The Curl port's twin of Run-LibcurlRtspScript.cs (ADR-0089 decision 1 (c)): the same command line
// (LibcurlRtspScript) and the same one-line-per-step report (LibcurlRtspReport), with every call made
// through the port's managed Libcurl on one easy handle - SetOption(Url), SetOption(TimeoutMs), a write
// callback for response bodies, CURLOPT_INTERLEAVEFUNCTION and a read callback, then each step - so Phase 7
// can compare the two reports line by line against a fresh surl each. The port is the thing measured
// here, never an expected value (ADR-0003 decision 6).
//
//   set SURL_CURL_PORT_LIBCURL=<the pinned Libcurl.dll>
//   dotnet run Run-CurlPortLibcurlRtspScript.cs -- [--library <path>] [--timeout <ms>]
//       <rtsp:// URL> <step> [<step> ...]
//
// The steps are Run-LibcurlRtspScript.cs's, and each sets what it sets there: a request is
// SetOption(RtspRequest) then Perform; stream-uri:, transport:, session-id: and client-cseq: the RTSP
// options; body: Upload 0, Postfieldsize and Copypostfields; upload: Postfields null, Postfieldsize -1,
// Upload 1, Infilesize and the read callback's bytes; no-body Upload 0, Postfieldsize -1, Postfields null.
// A string option takes the step's bytes as Latin-1 text, the managed API's form of libcurl's char *.
// SURL_CURL_PORT_LIBCURL is the assembly the driver compiles against (CurlPortLibcurlReference.targets);
// the one it calls is the CurlPortBuilds.json pin of kind library for this platform, or the file
// --library names, checked against that pin (CurlPortLibcurlChoice) before any of its types is loaded and
// loaded, with its dependencies, from that file's folder. Exit codes are Run-LibcurlRtspScript.cs's: 0
// once every step has run, 2 for a malformed command line, 3 when the library is not pinned, and 4,
// printing "Inconclusive: ..." on standard error, when none is pinned or installed for this platform.
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;
using Surl.Conformance;

return await CurlPortLibcurlRtspDriver.RunAsync(args);

/// <summary>Parses the command line, checks the assembly's pin, loads it from its folder and runs the script.</summary>
internal static class CurlPortLibcurlRtspDriver
{
    public static async Task<int> RunAsync(string[] args)
    {
        LibcurlRtspScript script;
        try
        {
            script = LibcurlRtspScript.Parse(args);
        }
        catch (FormatException exception)
        {
            await Console.Error.WriteLineAsync($"Run-CurlPortLibcurlRtspScript.cs: {exception.Message}");
            return 2;
        }

        var choice = await CurlPortLibcurlLoading.ChooseAsync(script.LibraryPath);
        if (choice.LibraryPath is null)
        {
            await Console.Error.WriteLineAsync(choice.Message);
            return choice.ExitCode;
        }

        CurlPortLibcurlLoading.LoadFromFolderOf(choice.LibraryPath);
        await PortRtspScript.RunAsync(script);
        return 0;
    }
}

/// <summary>Chooses the pinned assembly and resolves it, and every assembly beside it, from its own folder.</summary>
internal static class CurlPortLibcurlLoading
{
    public static async Task<CurlPortLibcurlChoice> ChooseAsync(string? requestedPath)
    {
        var root = RepositoryRoot();
        var upstreamPins = UpstreamCurlBuildPins.Parse(await File.ReadAllTextAsync(Path.Combine(root, UpstreamCurlBuildPins.FileName)));
        var portPins = CurlPortClientPins.Parse(await File.ReadAllTextAsync(Path.Combine(root, CurlPortClientPins.FileName)));
        var locator = new CurlPortClientLocator(new FileSystemUpstreamCurlFileAccess());
        return CurlPortLibcurlChoice.Choose(
            locator, portPins, upstreamPins, requestedPath is null ? null : Path.GetFullPath(requestedPath), UpstreamCurlLocator.CurrentPlatform);
    }

    // The driver runs under dotnet run and is never published trimmed; loading the pinned assembly by
    // path is the point of it.
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "A driver run by dotnet run, never trimmed; it loads the pinned port assembly by path on purpose.")]
    public static void LoadFromFolderOf(string libraryPath)
    {
        var folder = Path.GetDirectoryName(libraryPath)!;
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var candidate = Path.Combine(folder, name.Name + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, CurlPortClientPins.FileName)))
            {
                return directory.FullName;
            }
        }

        // dotnet run builds file-based apps outside the repository; the working directory is the
        // fallback, and PinnedLibcurlDriver sets it to the repository root.
        return Directory.GetCurrentDirectory();
    }
}

/// <summary>Runs the script through the port's Libcurl; kept out of the driver so no port type loads before the pin is checked.</summary>
internal static class PortRtspScript
{
    private static readonly List<byte> Body = [];
    private static readonly List<byte[]> Interleaved = [];
    private static byte[] upload = [];
    private static int uploadOffset;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task RunAsync(LibcurlRtspScript script)
    {
        await using CurlLibrary library = CurlGlobal.Init();
        using CurlEasy easy = library.EasyInit();
        easy.SetOption(CurlOption.Url, script.Url);
        easy.SetOption(CurlOption.TimeoutMs, (long)script.Timeout.TotalMilliseconds);
        easy.SetOption(CurlOption.Writefunction, (WriteCallback)(data => { Body.AddRange(data.ToArray()); return data.Length; }));
        easy.SetOption(CurlOption.Interleavefunction, (InterleaveCallback)(data => { Interleaved.Add(data.ToArray()); return data.Length; }));
        easy.SetOption(CurlOption.Readfunction, (ReadCallback)ReadNextUploadBytes);
        foreach (var step in script.Steps)
        {
            Console.WriteLine(step.Kind == LibcurlRtspStepKind.Request
                ? LibcurlRtspReport.RequestLine(step.Request, Perform(easy, step.Request))
                : LibcurlRtspReport.SettingLine(step, Describe(Set(easy, step))));
        }
    }

    private static string Describe(CurlCode code) => $"CURLcode {(int)code} ({CurlErrorText.Easy(code)})";

    private static LibcurlRtspOutcome Perform(CurlEasy easy, LibcurlRtspRequest request)
    {
        Body.Clear();
        Interleaved.Clear();
        uploadOffset = 0;
        var result = easy.SetOption(CurlOption.RtspRequest, (long)request);
        if (result == CurlCode.Ok)
        {
            result = easy.Perform();
        }

        return new LibcurlRtspOutcome(
            Describe(result),
            LongInfo(easy, CurlInfo.ResponseCode),
            easy.GetInfo(CurlInfo.RtspSessionId, out var session) == CurlCode.Ok ? session as string : null,
            LongInfo(easy, CurlInfo.RtspCseqRecv),
            LongInfo(easy, CurlInfo.RtspClientCseq),
            [.. Body],
            [.. Interleaved]);
    }

    private static long LongInfo(CurlEasy easy, CurlInfo info) =>
        easy.GetInfo(info, out var value) == CurlCode.Ok && value is long number ? number : 0;

    // Every SetOption the step needs, stopping at the first that fails; its CurlCode, or Ok.
    private static CurlCode Set(CurlEasy easy, LibcurlRtspStep step)
    {
        var text = Encoding.Latin1.GetString(step.Value);
        Func<CurlCode>[] calls = step.Kind switch
        {
            LibcurlRtspStepKind.StreamUri => [() => easy.SetOption(CurlOption.RtspStreamUri, text)],
            LibcurlRtspStepKind.Transport => [() => easy.SetOption(CurlOption.RtspTransport, text)],
            LibcurlRtspStepKind.SessionId => [() => easy.SetOption(CurlOption.RtspSessionId, text)],
            LibcurlRtspStepKind.ClientCSeq => [() => easy.SetOption(CurlOption.RtspClientCseq, (long)step.Number)],
            LibcurlRtspStepKind.PostFields =>
            [
                () => easy.SetOption(CurlOption.Upload, 0L),
                () => easy.SetOption(CurlOption.Postfieldsize, (long)step.Value.Length),
                () => easy.SetOption(CurlOption.Copypostfields, step.Value),
            ],
            LibcurlRtspStepKind.Upload =>
            [
                () => easy.SetOption(CurlOption.Postfields, null),
                () => easy.SetOption(CurlOption.Postfieldsize, -1L),
                () => easy.SetOption(CurlOption.Upload, 1L),
                () => easy.SetOption(CurlOption.Infilesize, (long)step.Value.Length),
                () => { upload = step.Value; return CurlCode.Ok; },
            ],
            _ =>
            [
                () => easy.SetOption(CurlOption.Upload, 0L),
                () => easy.SetOption(CurlOption.Postfieldsize, -1L),
                () => easy.SetOption(CurlOption.Postfields, null),
            ],
        };

        return calls.Select(call => call()).FirstOrDefault(result => result != CurlCode.Ok, CurlCode.Ok);
    }

    private static long ReadNextUploadBytes(Span<byte> buffer)
    {
        var length = Math.Min(buffer.Length, upload.Length - uploadOffset);
        upload.AsSpan(uploadOffset, length).CopyTo(buffer);
        uploadOffset += length;
        return length;
    }
}
