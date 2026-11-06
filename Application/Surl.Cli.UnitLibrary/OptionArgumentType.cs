namespace Surl.Cli;

/// <summary>
/// What an option's argument is and which values it takes, as <c>--aihelp</c> gives them
/// (ADR-0046 decision 5): one per <see cref="OptionArgumentReader"/> read method, and one per
/// option kind that has no reader.
/// </summary>
/// <param name="Name">The argument type (<c>seconds</c>, <c>flag</c>, <c>none</c>).</param>
/// <param name="AllowedValues">The values the option takes, as the reader or kind accepts them.</param>
internal sealed record OptionArgumentType(string Name, string AllowedValues)
{
    /// <summary>The <c>-h</c>/<c>--help</c> subject.</summary>
    public static OptionArgumentType OptionalSubject { get; } =
        new("optional subject", "a category, all, category or an option; see surl --help category");

    /// <summary>The <c>--aihelp</c> topic.</summary>
    public static OptionArgumentType OptionalTopic { get; } = new("optional topic", "a topic or all; see surl --aihelp");

    /// <summary><c>-V</c>/<c>--version</c> and <c>-M</c>/<c>--manual</c>, which take nothing.</summary>
    public static OptionArgumentType None { get; } = new("none", "none");

    /// <summary>A flag with no <c>--no-</c> form.</summary>
    public static OptionArgumentType NotNegatableFlag { get; } = new("flag", "no --no- form");

    /// <summary>A flag <c>--no-&lt;name&gt;</c> turns off.</summary>
    /// <param name="longName">The flag's long name without <c>--</c>.</param>
    /// <returns>The argument type, naming the flag's own <c>--no-</c> form.</returns>
    public static OptionArgumentType NegatableFlag(string longName) => new("flag", $"--no-{longName} turns it off");

    /// <summary>
    /// The sentence every layer-2 item option's allowed values end with (ADR-0101 decision 3).
    /// </summary>
    public const string ItemSyntax =
        "fields percent-encode %, ; and the separator, and , inside a list; an unknown key or flag is refused";

    /// <summary>
    /// A layer-2 item option's argument type (ADR-0101 decision 3): named by its shape, its allowed
    /// values its own keys followed by <see cref="ItemSyntax"/>.
    /// </summary>
    /// <param name="shape">The option's Argument column, e.g. <c>&lt;name&gt;=&lt;path&gt;[;readonly]</c>.</param>
    /// <param name="keys">The option's own keys and flags, described.</param>
    /// <returns>The argument type.</returns>
    public static OptionArgumentType Item(string shape, string keys) => new(shape, $"{keys}; {ItemSyntax}");
}
