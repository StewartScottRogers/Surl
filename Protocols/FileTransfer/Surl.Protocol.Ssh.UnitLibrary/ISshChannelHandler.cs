namespace Surl.Protocol.Ssh;

/// <summary>
/// What runs on a session channel once its <c>exec</c> or <c>subsystem</c> request is accepted:
/// SCP (BL-164) or SFTP (BL-165). The server sends its exit status, <c>EOF</c> and <c>CLOSE</c>
/// when it returns (ADR-0054, decision 2).
/// </summary>
internal interface ISshChannelHandler
{
    /// <summary>
    /// Serves the channel until the exchange on it is over.
    /// </summary>
    /// <param name="channel">The channel's data both ways.</param>
    /// <param name="cancellationToken">Cancelled when the connection is cut off.</param>
    /// <returns>The exit status sent as <c>exit-status</c>: 0 when the transfer completed, 1 when anything was refused.</returns>
    Task<uint> RunAsync(ISshChannelDataStream channel, CancellationToken cancellationToken);
}
