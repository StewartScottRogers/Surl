namespace Surl.Composition;

/// <summary>
/// Reads one of the process's environment variables: the seam <c>surl</c>'s <c>CommandLineRunner</c>
/// reads <c>SURL_EXTENSIONS</c> through, so a test sets the switch without touching the
/// process (ADR-0083, decision 6).
/// </summary>
public interface IEnvironmentVariableReader
{
    /// <summary>Reads the variable <paramref name="name"/>.</summary>
    /// <param name="name">The variable's name, such as <c>SURL_EXTENSIONS</c>.</param>
    /// <returns>Its value, or <see langword="null"/> when it is not set.</returns>
    string? Read(string name);
}
