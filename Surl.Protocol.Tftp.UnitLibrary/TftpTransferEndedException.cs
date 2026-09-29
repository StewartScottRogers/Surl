namespace Surl.Protocol.Tftp;

/// <summary>
/// Thrown by <see cref="TftpUploadStream"/> when a write transfer ends before its last block:
/// the client sent an ERROR or a packet that does not belong, or stayed silent. The lock step
/// has already logged which and answered it, so the content store only deletes the partial
/// file.
/// </summary>
internal sealed class TftpTransferEndedException : Exception
{
    /// <summary>
    /// Creates the exception with its fixed message.
    /// </summary>
    public TftpTransferEndedException()
        : base("The TFTP write transfer ended before its last block.")
    {
    }
}
