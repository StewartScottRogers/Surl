namespace Surl.Protocol.Ftp;

/// <summary>
/// What an upstream test case's <c>NODATACONN425</c>, <c>NODATACONN421</c> and <c>NODATACONN150</c>
/// make a data command answer on the control connection when no data connection is made
/// (ADR-0156 decision 2).
/// </summary>
internal enum FtpNoDataConnectionReply
{
    /// <summary>No such word: data commands open their data connection as usual.</summary>
    None,

    /// <summary><c>NODATACONN425</c>: <c>150</c>, then <c>425 Can't open data connection</c>.</summary>
    Refuse425,

    /// <summary><c>NODATACONN421</c>: <c>150</c>, then <c>421 Connection timed out</c>.</summary>
    Timeout421,

    /// <summary><c>NODATACONN150</c>: <c>150</c> and nothing more, ever.</summary>
    OpenOnly150,
}
