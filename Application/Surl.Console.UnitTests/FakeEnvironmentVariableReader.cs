using Surl.Composition;

namespace Surl.Console;

/// <summary>An environment with the variables a test gives it, and no other.</summary>
/// <param name="variables">Each set variable's value, by name.</param>
internal sealed class FakeEnvironmentVariableReader(IReadOnlyDictionary<string, string> variables) : IEnvironmentVariableReader
{
    /// <summary>An environment where no variable is set.</summary>
    public static FakeEnvironmentVariableReader Empty { get; } = new(new Dictionary<string, string>());

    /// <summary>An environment where <c>SURL_EXTENSIONS=0</c> turns the extended servers off.</summary>
    public static FakeEnvironmentVariableReader ExtensionsTurnedOff { get; } =
        new(new Dictionary<string, string> { [ExtendedProtocolServers.SwitchVariable] = ExtendedProtocolServers.TurnedOffValue });

    public string? Read(string name) => variables.GetValueOrDefault(name);
}
