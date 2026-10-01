using System.Globalization;

namespace Surl.Conformance;

/// <summary>
/// The command line of <c>Run-LibcurlRtspScript.cs</c>, which drives the pinned libcurl's RTSP
/// requests through one easy handle (ADR-0074 decision 12): an optional library, the timeout of each
/// <c>curl_easy_perform</c>, the <c>rtsp://</c> URL and the steps, in order.
/// </summary>
/// <remarks>
/// A step is a request name - <c>OPTIONS</c>, <c>DESCRIBE</c>, <c>ANNOUNCE</c>, <c>SETUP</c>,
/// <c>PLAY</c>, <c>PAUSE</c>, <c>TEARDOWN</c>, <c>GET_PARAMETER</c>, <c>SET_PARAMETER</c>,
/// <c>RECORD</c> or <c>RECEIVE</c> - or an option: <c>stream-uri:&lt;text&gt;</c>,
/// <c>transport:&lt;text&gt;</c>, <c>session-id:&lt;text&gt;</c>, <c>client-cseq:&lt;n&gt;</c>,
/// <c>body:&lt;bytes&gt;</c> (sent with <c>CURLOPT_COPYPOSTFIELDS</c>), <c>upload:&lt;bytes&gt;</c>
/// (sent with <c>CURLOPT_UPLOAD</c> and a read callback) or <c>no-body</c>. Text and bytes take
/// <see cref="LibcurlBytes.Unescape"/>'s backslash escapes.
/// </remarks>
/// <param name="LibraryPath">The libcurl <c>--library</c> names, or <see langword="null"/> for the one pinned for the platform.</param>
/// <param name="Timeout">The <c>CURLOPT_TIMEOUT_MS</c> of each request, from <c>--timeout</c>; 10 seconds by default.</param>
/// <param name="Url">The <c>CURLOPT_URL</c>, set once before the first step.</param>
/// <param name="Steps">The steps, run in order on the one easy handle.</param>
public sealed record LibcurlRtspScript(string? LibraryPath, TimeSpan Timeout, string Url, IReadOnlyList<LibcurlRtspStep> Steps)
{
    internal static readonly Dictionary<string, LibcurlRtspRequest> Requests = new(StringComparer.Ordinal)
    {
        ["OPTIONS"] = LibcurlRtspRequest.Options,
        ["DESCRIBE"] = LibcurlRtspRequest.Describe,
        ["ANNOUNCE"] = LibcurlRtspRequest.Announce,
        ["SETUP"] = LibcurlRtspRequest.Setup,
        ["PLAY"] = LibcurlRtspRequest.Play,
        ["PAUSE"] = LibcurlRtspRequest.Pause,
        ["TEARDOWN"] = LibcurlRtspRequest.Teardown,
        ["GET_PARAMETER"] = LibcurlRtspRequest.GetParameter,
        ["SET_PARAMETER"] = LibcurlRtspRequest.SetParameter,
        ["RECORD"] = LibcurlRtspRequest.Record,
        ["RECEIVE"] = LibcurlRtspRequest.Receive,
    };

    private static readonly Dictionary<string, LibcurlRtspStepKind> ValueOptions = new(StringComparer.Ordinal)
    {
        ["stream-uri"] = LibcurlRtspStepKind.StreamUri,
        ["transport"] = LibcurlRtspStepKind.Transport,
        ["session-id"] = LibcurlRtspStepKind.SessionId,
        ["client-cseq"] = LibcurlRtspStepKind.ClientCSeq,
        ["body"] = LibcurlRtspStepKind.PostFields,
        ["upload"] = LibcurlRtspStepKind.Upload,
    };

    /// <summary>
    /// Parses the driver's arguments: <c>[--library &lt;path&gt;] [--timeout &lt;ms&gt;] &lt;rtsp:// URL&gt; &lt;step&gt;...</c>.
    /// </summary>
    /// <param name="args">The arguments after <c>--</c> on <c>dotnet run</c>'s command line.</param>
    /// <returns>The parsed script.</returns>
    /// <exception cref="FormatException">An option lacks its value, the timeout is not a positive number, the URL is missing or a step is malformed.</exception>
    public static LibcurlRtspScript Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? libraryPath = null;
        var timeoutMilliseconds = 10000;
        string? url = null;
        var steps = new List<LibcurlRtspStep>();
        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--library":
                    libraryPath = ValueAfter(args, ref index);
                    break;
                case "--timeout":
                    timeoutMilliseconds = PositiveNumber(ValueAfter(args, ref index), "--timeout takes a positive number of milliseconds.");
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
            ? throw new FormatException("an rtsp:// URL is required, followed by the steps.")
            : new LibcurlRtspScript(libraryPath, TimeSpan.FromMilliseconds(timeoutMilliseconds), url, steps);
    }

    /// <summary>
    /// Parses one step: a request name, <c>no-body</c>, or <c>&lt;option&gt;:&lt;value&gt;</c>.
    /// </summary>
    /// <param name="step">The step as given on the command line.</param>
    /// <returns>The step.</returns>
    /// <exception cref="FormatException">The step names no request or option, or <c>client-cseq</c>'s value is not a positive number.</exception>
    public static LibcurlRtspStep ParseStep(string step)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (Requests.TryGetValue(step, out var request))
        {
            return new LibcurlRtspStep(LibcurlRtspStepKind.Request, request, [], 0);
        }

        if (step == "no-body")
        {
            return new LibcurlRtspStep(LibcurlRtspStepKind.NoBody, LibcurlRtspRequest.Options, [], 0);
        }

        return ParseValueStep(step);
    }

    private static LibcurlRtspStep ParseValueStep(string step)
    {
        var colon = step.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0 || !ValueOptions.TryGetValue(step[..colon], out var kind))
        {
            throw new FormatException($"step '{step}' is neither a request ({string.Join(", ", Requests.Keys)}), no-body, nor one of {string.Join(", ", ValueOptions.Keys.Select(name => name + ":<value>"))}.");
        }

        var value = step[(colon + 1)..];
        return kind == LibcurlRtspStepKind.ClientCSeq
            ? new LibcurlRtspStep(kind, LibcurlRtspRequest.Options, [], PositiveNumber(value, $"step '{step}' needs a positive number."))
            : new LibcurlRtspStep(kind, LibcurlRtspRequest.Options, LibcurlBytes.Unescape(value), 0);
    }

    private static string ValueAfter(IReadOnlyList<string> args, ref int index) =>
        ++index < args.Count ? args[index] : throw new FormatException($"{args[index - 1]} needs a value.");

    private static int PositiveNumber(string text, string complaint) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0
            ? number
            : throw new FormatException(complaint);
}
