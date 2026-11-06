using System.Text;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The replies an SCP peer sends (ADR-0054, decisions 3 and 4): the one-byte acknowledgement
/// <c>\0</c>, and an error line, <c>\x01scp: &lt;message&gt;</c> and LF, in the shape OpenSSH's
/// <c>scp</c> writes.
/// </summary>
internal static class ScpReply
{
    /// <summary>The acknowledgement, one <c>\0</c> byte.</summary>
    public static ReadOnlyMemory<byte> Ok { get; } = new byte[] { 0 };

    /// <summary>
    /// The error line carrying <paramref name="message"/>.
    /// </summary>
    /// <param name="message">
    /// The message after <c>scp: </c>, naming nothing but the client's own path as
    /// <see cref="SshLogText"/> renders it (ADR-0006 section 3).
    /// </param>
    /// <returns><c>\x01scp: &lt;message&gt;</c> and LF.</returns>
    public static byte[] Error(string message) => Encoding.UTF8.GetBytes($"\u0001scp: {message}\n");

    /// <summary>
    /// The error line carrying a fault rule's chosen message as it was given, with no <c>scp: </c>
    /// before it (ADR-0124 decision 4, "a message line sent after byte 1"; ADR-0135).
    /// </summary>
    /// <param name="message">The message, non-empty and holding no CR or LF.</param>
    /// <returns><c>\x01&lt;message&gt;</c> and LF.</returns>
    public static byte[] ChosenError(string message) => Encoding.UTF8.GetBytes($"\u0001{message}\n");
}
