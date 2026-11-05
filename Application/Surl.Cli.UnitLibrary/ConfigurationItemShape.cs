using System.Collections.Frozen;

namespace Surl.Cli;

/// <summary>
/// The shape of one layer-2 option's item (ADR-0101 decisions 3 and 4): its Argument column, its
/// separator, what follows the separator and its own keys and flags. <see cref="Reading"/> is the
/// <see cref="OptionArgumentReading{T}"/> an option row's <c>WithArgument</c> takes.
/// </summary>
internal sealed class ConfigurationItemShape
{
    /// <summary>Creates the shape and its reading.</summary>
    /// <param name="argument">The Argument column, e.g. <c>&lt;name&gt;=&lt;path&gt;[;readonly]</c>; also the <c>expected</c> refusal's shape.</param>
    /// <param name="separator">The separator the head splits at first, or null when the whole head is the name.</param>
    /// <param name="valueKind">What follows the separator; <see cref="ConfigurationItemValueKind.None"/> when there is no separator.</param>
    /// <param name="attributes">The option's keys and flags, lower-case, and what each takes.</param>
    /// <param name="accessWords">The access words a grant takes, lower-case, for <see cref="ConfigurationItemValueKind.Grants"/>.</param>
    public ConfigurationItemShape(
        string argument,
        char? separator,
        ConfigurationItemValueKind valueKind,
        IReadOnlyDictionary<string, ConfigurationItemAttributeKind> attributes,
        IReadOnlyList<string> accessWords)
    {
        Argument = argument;
        Separator = separator;
        ValueKind = valueKind;
        Attributes = attributes.ToFrozenDictionary(StringComparer.Ordinal);
        AccessWords = accessWords;
        Reading = new(
            (string item, out ConfigurationItem value) => ConfigurationItemReader.Read(this, item, out value),
            OptionArgumentType.Item(argument, DescribeAttributes(Attributes)));
    }

    /// <summary>The Argument column, e.g. <c>&lt;address&gt;:&lt;password&gt;</c>.</summary>
    public string Argument { get; }

    /// <summary>The separator the head splits at first, or null when the whole head is the name.</summary>
    public char? Separator { get; }

    /// <summary>What follows the separator.</summary>
    public ConfigurationItemValueKind ValueKind { get; }

    /// <summary>The option's keys and flags, lower-case, and what each takes.</summary>
    public FrozenDictionary<string, ConfigurationItemAttributeKind> Attributes { get; }

    /// <summary>The access words a grant takes, lower-case.</summary>
    public IReadOnlyList<string> AccessWords { get; }

    /// <summary>The reader and argument type an option row takes (ADR-0046 decision 6).</summary>
    public OptionArgumentReading<ConfigurationItem> Reading { get; }

    /// <summary>The option's own keys and flags, as <c>--aihelp</c> lists them before the shared sentence.</summary>
    private static string DescribeAttributes(FrozenDictionary<string, ConfigurationItemAttributeKind> attributes)
    {
        if (attributes.Count == 0)
        {
            return "no keys or flags";
        }

        var described = attributes.Keys.Order(StringComparer.Ordinal).Select(key => attributes[key] switch
        {
            ConfigurationItemAttributeKind.Flag => key,
            ConfigurationItemAttributeKind.Value => key + "=<value>",
            _ => key + "=<name>,...",
        });
        return "keys and flags " + string.Join(", ", described);
    }
}
