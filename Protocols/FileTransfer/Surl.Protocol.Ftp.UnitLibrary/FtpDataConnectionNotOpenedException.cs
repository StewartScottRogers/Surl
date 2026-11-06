namespace Surl.Protocol.Ftp;

/// <summary>
/// Thrown by <see cref="DataConnectionUploadStream"/> when the upload's data connection could
/// not be opened; the server answers <c>425 Cannot open data connection</c>.
/// </summary>
internal sealed class FtpDataConnectionNotOpenedException()
    : Exception("No data connection was opened for the upload.");
