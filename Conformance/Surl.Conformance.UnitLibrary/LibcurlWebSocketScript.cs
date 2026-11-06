using System.Globalization;

namespace Surl.Conformance;

/// <summary>
/// The command line of a libcurl WebSocket driver - <c>Run-LibcurlWebSocketScript.cs</c> (ADR-0071
/// decision 10) and its Curl port twin <c>Run-CurlPortLibcurlWebSocketScript.cs</c> (ADR-0089 decision
/// 1 (c)) - kept here so both drivers take the same arguments and the parsing is unit tested:
/// <c>[--library &lt;path&gt;] [--recv-timeout &lt;ms&gt;] &lt;ws:// or wss:// URL&gt; &lt;step&gt; ...</c>, each step
/// <c>recv</c>, <c>send:&lt;FLAGS&gt;:&lt;payload&gt;</c> or <c>send*&lt;count&gt;:&lt;FLAGS&gt;:&lt;payload&gt;</c>.
/// </summary>
/// <param name="LibraryPath">The library <c>--library</c> names, or <see langword="null"/> for the platform's pin.</param>
/// <param name="ReceiveTimeout">How long a send or receive waits out <c>CURLE_AGAIN</c> (<c>--recv-timeout</c>, default 5000 ms).</param>
/// <param name="Url">The WebSocket URL.</param>
/// <param name="Steps">The steps, in order.</param>
public sealed record LibcurlWebSocketScript(string? LibraryPath, TimeSpan ReceiveTimeout, string Url, IReadOnlyList<LibcurlWebSocketStep> Steps)
{
    private const string RepeatedSend = "send*";

    /// <summary>
    /// Parses a driver's command line.
    /// </summary>
    /// <param name="args">The arguments after <c>--</c>.</param>
    /// <returns>The script.</returns>
    /// <exception cref="FormatException">An option lacks its value, the timeout is not a positive number, no URL is given, or a step is malformed.</exception>
    public static LibcurlWebSocketScript Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? libraryPath = null;
        var timeoutMilliseconds = 5000;
        string? url = null;
        var steps = new List<LibcurlWebSocketStep>();
        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--library":
                    libraryPath = ValueAfter(args, ref index);
                    break;
                case "--recv-timeout":
                    timeoutMilliseconds = PositiveNumber(ValueAfter(args, ref index), "--recv-timeout takes a positive number of milliseconds.");
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
            : new LibcurlWebSocketScript(libraryPath, TimeSpan.FromMilliseconds(timeoutMilliseconds), url, steps);
    }

    /// <summary>
    /// Parses one step: <c>recv</c>, <c>send:&lt;FLAGS&gt;:&lt;payload&gt;</c> or <c>send*&lt;count&gt;:&lt;FLAGS&gt;:&lt;payload&gt;</c>,
    /// the flags <c>+</c>-joined (<see cref="LibcurlWebSocketFlags.ValueOf"/>) and the payload in
    /// <see cref="LibcurlBytes.Unescape"/>'s escapes, repeated <c>count</c> times.
    /// </summary>
    /// <param name="step">The step as written.</param>
    /// <returns>The step.</returns>
    /// <exception cref="FormatException">The step is none of the three forms, its count is not positive, or it names an unknown flag.</exception>
    public static LibcurlWebSocketStep ParseStep(string step)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (step == "recv")
        {
            return new LibcurlWebSocketStep(true, 0, []);
        }

        var parts = step.Split(':', 3);
        if (parts.Length != 3)
        {
            throw NeitherForm(step);
        }

        var repeat = RepeatCountOf(step, parts[0]);
        var flags = FlagsOf(step, parts[1]);
        var payload = LibcurlBytes.Unescape(parts[2]);
        return new LibcurlWebSocketStep(false, flags, [.. Enumerable.Repeat(payload, repeat).SelectMany(bytes => bytes)]);
    }

    private static int RepeatCountOf(string step, string verb) =>
        verb == "send" ? 1
        : verb.StartsWith(RepeatedSend, StringComparison.Ordinal)
            ? PositiveNumber(verb[RepeatedSend.Length..], $"step '{step}' repeats its payload a number of times that is not a positive number.")
            : throw NeitherForm(step);

    private static uint FlagsOf(string step, string names)
    {
        uint flags = 0;
        foreach (var name in names.Split('+'))
        {
            flags |= LibcurlWebSocketFlags.ValueOf(name)
                ?? throw new FormatException($"step '{step}' names flag '{name}', not TEXT, BINARY, CONT, CLOSE, PING or PONG.");
        }

        return flags;
    }

    private static FormatException NeitherForm(string step) =>
        new($"step '{step}' is neither recv, send:<FLAGS>:<payload> nor send*<count>:<FLAGS>:<payload>.");

    private static string ValueAfter(IReadOnlyList<string> args, ref int index) =>
        ++index < args.Count ? args[index] : throw new FormatException($"{args[index - 1]} needs a value.");

    private static int PositiveNumber(string text, string complaint) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0
            ? number
            : throw new FormatException(complaint);
}
