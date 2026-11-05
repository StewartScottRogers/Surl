namespace Surl.Console;

/// <summary>
/// Reads the environment variables of the process <c>surl</c> runs in.
/// </summary>
internal sealed class ProcessEnvironmentVariableReader : IEnvironmentVariableReader
{
    /// <inheritdoc/>
    public string? Read(string name) => Environment.GetEnvironmentVariable(name);
}
