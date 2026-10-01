namespace Surl.Protocol.Smb;

/// <summary>
/// What decoding one SMB message produced: a request, or the fault that stopped it.
/// </summary>
/// <param name="Fault">Why the message was not decoded; <see cref="SmbRequestFault.None"/> when it was.</param>
/// <param name="Header">The message's header when it could be read, so the server can answer with an error; otherwise <see langword="null"/>.</param>
/// <param name="Request">The request when <paramref name="Fault"/> is <see cref="SmbRequestFault.None"/>; otherwise <see langword="null"/>.</param>
internal sealed record SmbRequestDecoding(SmbRequestFault Fault, SmbHeader? Header, SmbRequest? Request)
{
    /// <summary>
    /// A message decoded into <paramref name="request"/>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The decoding.</returns>
    public static SmbRequestDecoding Decoded(SmbRequest request) => new(SmbRequestFault.None, request.Header, request);

    /// <summary>
    /// A message refused for <paramref name="fault"/>.
    /// </summary>
    /// <param name="fault">Why.</param>
    /// <param name="header">The message's header, when it could be read.</param>
    /// <returns>The decoding.</returns>
    public static SmbRequestDecoding Refused(SmbRequestFault fault, SmbHeader? header) => new(fault, header, null);
}
