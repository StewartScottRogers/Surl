using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// One <c>--aihelp</c> example (ADR-0046 decisions 6 and 7): a <c>surl</c> command line, what
/// it writes and how it exits, and the upstream curl command lines that reach what it serves.
/// <c>&lt;port&gt;</c> stands for the bound port and <c>&lt;path&gt;</c> for a directory.
/// </summary>
/// <param name="Topic">The topic whose page shows it, or <c>overview</c> for the overview's own.</param>
/// <param name="Title">Its <c>###</c> heading.</param>
/// <param name="Precondition">What must be true before it is run.</param>
/// <param name="Arguments">The arguments after <c>surl</c>, each as one argument.</param>
/// <param name="Output">Every line it writes to stdout.</param>
/// <param name="Error">Every line it writes to stderr.</param>
/// <param name="ExitCode">How it exits; once stopped, for a serving example.</param>
/// <param name="ServesUntilStopped">Whether it serves until stopped with Ctrl+C or SIGTERM.</param>
/// <param name="CurlCommandLines">The upstream curl 8.21.0 command lines that reach it, if any.</param>
public sealed record AiHelpExample(
    string Topic,
    string Title,
    AiHelpExamplePrecondition Precondition,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<string> Output,
    IReadOnlyList<string> Error,
    SurlExitCode ExitCode,
    bool ServesUntilStopped,
    IReadOnlyList<string> CurlCommandLines);
