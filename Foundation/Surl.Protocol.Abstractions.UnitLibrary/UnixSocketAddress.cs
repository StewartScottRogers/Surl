namespace Surl.Protocol.Abstractions;

/// <summary>
/// The Unix domain socket a listen URL listens on instead of a TCP port (ADR-0094, decision 6):
/// a socket file's path, or on Linux an abstract socket name.
/// </summary>
public sealed record UnixSocketAddress
{
    /// <summary>
    /// Creates the address of a socket file at <paramref name="name"/>, or of the abstract
    /// socket <paramref name="name"/> when <paramref name="isAbstract"/> is set.
    /// </summary>
    /// <param name="name">
    /// The socket file's path, used as given (relative to the working directory when relative),
    /// or the abstract name without its leading NUL, as curl's <c>--abstract-unix-socket</c> takes it.
    /// </param>
    /// <param name="isAbstract">Whether <paramref name="name"/> is an abstract socket name rather than a path.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public UnixSocketAddress(string name, bool isAbstract)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
        IsAbstract = isAbstract;
    }

    /// <summary>
    /// The socket file's path, or the abstract name without its leading NUL.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Whether <see cref="Name"/> is a Linux abstract socket name rather than a path.
    /// </summary>
    public bool IsAbstract { get; }

    /// <summary>
    /// The socket in curl's own words: <c>unix://</c> and the path (<c>unix:///tmp/surl.sock</c>),
    /// or <c>unix://@</c> and the abstract name, the <c>@</c> standing for the leading NUL as
    /// <c>ss</c> and <c>netstat</c> show it (ADR-0094, decision 6).
    /// </summary>
    public string UnixUrl => IsAbstract ? $"unix://@{Name}" : $"unix://{Name}";
}
