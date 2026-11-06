#:project Conformance/Surl.Conformance.UnitLibrary/Surl.Conformance.UnitLibrary.csproj
#:property CustomAfterMicrosoftCommonTargets=$(MSBuildProjectDirectory)\CurlPortLibcurlReference.targets

// The Curl port's twin of Run-LibcurlWebSocketScript.cs (ADR-0089 decision 1 (c)): the same command
// line (LibcurlWebSocketScript) and the same one-line-per-call report (LibcurlWebSocketReport), with
// every call made through the port's managed Libcurl - CurlGlobal.Init, EasyInit, SetOption(Url),
// SetOption(ConnectOnly, 2), Perform, then WebSocketSend and WebSocketRecv - so Phase 7 can compare the
// two reports line by line against a fresh surl each. The port is the thing measured here, never an
// expected value (ADR-0003 decision 6).
//
//   set SURL_CURL_PORT_LIBCURL=<the pinned Libcurl.dll>
//   dotnet run Run-CurlPortLibcurlWebSocketScript.cs -- [--library <path>] [--recv-timeout <ms>]
//       <ws:// or wss:// URL> <step> [<step> ...]
//
// The steps are Run-LibcurlWebSocketScript.cs's: send:<FLAGS>:<payload>, send*<count>:<FLAGS>:<payload>
// and recv. SURL_CURL_PORT_LIBCURL is the assembly the driver compiles against
// (CurlPortLibcurlReference.targets). The assembly it calls is the one CurlPortBuilds.json pins for this
// platform with kind library, or the file --library names; either way its SHA-256 must match that pin
// (CurlPortLibcurlChoice) before any of its types is loaded, and it and its dependencies are loaded from
// that file's folder. Exit codes are Run-LibcurlWebSocketScript.cs's: 0 once every step has run, 1 when
// Perform fails, 2 for a malformed command line, 3 when the library is not pinned, and 4, printing
// "Inconclusive: ..." on standard error, when none is pinned or installed for this platform.
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;
using Libcurl.WebSocket;
using Surl.Conformance;

return await CurlPortLibcurlWebSocketDriver.RunAsync(args);

/// <summary>Parses the command line, checks the assembly's pin, loads it from its folder and runs the script.</summary>
internal static class CurlPortLibcurlWebSocketDriver
{
    public static async Task<int> RunAsync(string[] args)
    {
        LibcurlWebSocketScript script;
        try
        {
            script = LibcurlWebSocketScript.Parse(args);
        }
        catch (FormatException exception)
        {
            await Console.Error.WriteLineAsync($"Run-CurlPortLibcurlWebSocketScript.cs: {exception.Message}");
            return 2;
        }

        var choice = await CurlPortLibcurlLoading.ChooseAsync(script.LibraryPath);
        if (choice.LibraryPath is null)
        {
            await Console.Error.WriteLineAsync(choice.Message);
            return choice.ExitCode;
        }

        CurlPortLibcurlLoading.LoadFromFolderOf(choice.LibraryPath);
        return await PortWebSocketScript.RunAsync(script);
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
internal static class PortWebSocketScript
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task<int> RunAsync(LibcurlWebSocketScript script)
    {
        await using CurlLibrary library = CurlGlobal.Init();
        using CurlEasy easy = library.EasyInit();
        easy.SetOption(CurlOption.Url, script.Url);
        easy.SetOption(CurlOption.ConnectOnly, 2L);
        var performed = easy.Perform();
        Console.WriteLine(LibcurlWebSocketReport.PerformLine(Describe(performed)));
        if (performed != CurlCode.Ok)
        {
            return 1;
        }

        foreach (var step in script.Steps)
        {
            Console.WriteLine(step.IsReceive ? await ReceiveAsync(easy, script.ReceiveTimeout) : await SendAsync(easy, step, script.ReceiveTimeout));
        }

        return 0;
    }

    private static string Describe(CurlCode code) => $"CURLcode {(int)code} ({CurlErrorText.Easy(code)})";

    // As the upstream driver: a payload taken only in part is sent on from where it stopped, waiting
    // out CURLE_AGAIN, until all of it is sent or a call fails.
    private static async Task<string> SendAsync(CurlEasy easy, LibcurlWebSocketStep step, TimeSpan timeout)
    {
        var deadline = TimeProvider.System.GetUtcNow() + timeout;
        var offset = 0;
        var calls = 0;
        CurlCode result;
        do
        {
            calls++;
            result = easy.WebSocketSend(step.Payload.AsMemory(offset), out var sent, 0, (CurlWebSocketFlags)step.Flags);
            offset += sent;
            if (result == CurlCode.Again && TimeProvider.System.GetUtcNow() < deadline)
            {
                await Task.Delay(20);
                result = CurlCode.Ok;
            }
        }
        while (result == CurlCode.Ok && offset < step.Payload.Length);

        return LibcurlWebSocketReport.SendLine(step, Describe(result), offset, calls);
    }

    private static async Task<string> ReceiveAsync(CurlEasy easy, TimeSpan timeout)
    {
        var buffer = new byte[65536];
        var deadline = TimeProvider.System.GetUtcNow() + timeout;
        while (true)
        {
            var result = easy.WebSocketRecv(buffer, out var received, out var frame);
            if (result == CurlCode.Again && TimeProvider.System.GetUtcNow() < deadline)
            {
                await Task.Delay(20);
                continue;
            }

            if (result != CurlCode.Ok || frame is not { } meta)
            {
                return LibcurlWebSocketReport.ReceiveFailedLine(Describe(result));
            }

            return LibcurlWebSocketReport.ReceiveLine(Describe(result), (uint)meta.Flags, meta.Offset, meta.BytesLeft, buffer.AsSpan(0, received));
        }
    }
}
