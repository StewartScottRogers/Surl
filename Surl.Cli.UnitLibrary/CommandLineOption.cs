namespace Surl.Cli;

/// <summary>What an option does when it is read.</summary>
internal enum CommandLineOptionKind
{
    /// <summary><c>-h</c>/<c>--help</c>: ends reading with <see cref="CommandLineOutcome.ShowHelp"/>.</summary>
    Help,

    /// <summary><c>-V</c>/<c>--version</c>: ends reading with <see cref="CommandLineOutcome.ShowVersion"/>.</summary>
    Version,

    /// <summary>Takes no argument; sets a value on the command line.</summary>
    Flag,

    /// <summary>Takes one argument, read by its kind.</summary>
    Argument,
}

/// <summary>Sets a flag on a command line: <see langword="true"/> for <c>--name</c>, <see langword="false"/> for <c>--no-name</c>.</summary>
/// <param name="commandLine">The command line so far.</param>
/// <param name="turnOn">Whether the flag was given or negated.</param>
/// <returns>The command line with the flag set.</returns>
internal delegate SurlCommandLine SetFlag(SurlCommandLine commandLine, bool turnOn);

/// <summary>Reads an option's argument and, when it is accepted, sets its value on the command line.</summary>
/// <param name="argument">The argument as given.</param>
/// <param name="commandLine">The command line so far, replaced with the value set when the argument is accepted.</param>
/// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
internal delegate string? ApplyArgument(string argument, ref SurlCommandLine commandLine);

/// <summary>One row of ADR-0007 section 2's option table.</summary>
/// <param name="LongName">The long name without <c>--</c> (<c>max-time</c>).</param>
/// <param name="ShortName">The short name without <c>-</c>, or <see langword="null"/> when there is none.</param>
/// <param name="Kind">What the option does when read.</param>
/// <param name="Negatable">Whether <c>--no-</c> may turn it off.</param>
/// <param name="SetFlag">Sets the value of a <see cref="CommandLineOptionKind.Flag"/>; otherwise null.</param>
/// <param name="ApplyArgument">Reads and applies the argument of an <see cref="CommandLineOptionKind.Argument"/>; otherwise null.</param>
internal sealed record CommandLineOption(
    string LongName,
    char? ShortName,
    CommandLineOptionKind Kind,
    bool Negatable,
    SetFlag? SetFlag,
    ApplyArgument? ApplyArgument);
