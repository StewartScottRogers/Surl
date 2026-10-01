namespace Surl.Protocol.Ssh;

/// <summary>
/// Thrown where an SFTP session ends with exit status 1 and no reply: a packet that cannot be
/// framed, one past <c>--max-message</c>, or a refused <c>INIT</c> (ADR-0054, decision 5).
/// </summary>
/// <param name="reason">Why, as the <c>SFTP session ended</c> note says it.</param>
internal sealed class SftpSessionEndedException(string reason) : Exception(reason);
