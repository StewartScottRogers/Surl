namespace Surl.Protocol.Abstractions;

/// <summary>
/// The exit codes the <c>surl</c> process returns.
/// </summary>
/// <remarks>
/// One member per row of the exit-code table in ADR-0005
/// (<c>Documentation/Planning/Decisions/ADR-0005-surls-exit-code-table.md</c>), with the rows
/// ADR-0010 (the server-side TLS contract, section 3) and ADR-0031 (the data directory,
/// decisions 3 and 7) add to that table. A failure
/// with a server-side meaning upstream curl also has reuses curl's <c>CURLE_*</c> number
/// (https://curl.se/libcurl/c/libcurl-errors.html, curl 8.21.0); a failure with no upstream
/// counterpart takes a number counting down from 125. A value is never renumbered once it
/// exists; a failure that fits no row is decided in a new ADR, which adds a member.
/// </remarks>
public enum SurlExitCode
{
    /// <summary>
    /// Success: surl served until it was stopped (Ctrl+C or SIGTERM included), or had nothing
    /// to serve, such as <c>--help</c> or <c>--version</c>. The same number as upstream curl's
    /// <c>CURLE_OK</c>.
    /// </summary>
    Ok = 0,

    /// <summary>
    /// The listen URL names a scheme Surl has no server for. The same number as upstream
    /// curl's <c>CURLE_UNSUPPORTED_PROTOCOL</c>.
    /// </summary>
    UnsupportedProtocol = 1,

    /// <summary>
    /// Surl could not start: an unknown option, an option missing its argument, an invalid
    /// option value, or any other command line surl cannot act on that is not a URL error.
    /// The same number and meaning as upstream curl's <c>CURLE_FAILED_INIT</c>. <c>surl</c>
    /// writes the refusal and <c>try 'surl --help' or 'surl --manual' for more information</c>
    /// to stderr (ADR-0007, section 5; ADR-0034, decision 6).
    /// </summary>
    FailedInit = 2,

    /// <summary>
    /// The listen URL cannot be parsed: no host, a bad IPv6 literal, a port outside
    /// 0-65535, or no URL at all. The same number as upstream curl's
    /// <c>CURLE_URL_MALFORMAT</c>.
    /// </summary>
    MalformedUrl = 3,

    /// <summary>
    /// The listen URL's host name does not resolve to any address
    /// (<see cref="ListenerBindFailure.HostNotFound"/>). The same number as upstream curl's
    /// <c>CURLE_COULDNT_RESOLVE_HOST</c>.
    /// </summary>
    CouldNotResolveHost = 6,

    /// <summary>
    /// The data directory's <c>.surl</c> folder or its <c>.surl/lock</c> file cannot be
    /// created (ADR-0031, decision 3). The same number as upstream curl's
    /// <c>CURLE_WRITE_ERROR</c>, which curl returns when it cannot create a directory it was
    /// told to write into.
    /// </summary>
    CouldNotWriteFile = 23,

    /// <summary>
    /// The served directory or file is missing, is not the kind of entry the option asked
    /// for, or cannot be read for lack of permission. The same number as upstream curl's
    /// <c>CURLE_FILE_COULDNT_READ_FILE</c>.
    /// </summary>
    CouldNotReadFile = 37,

    /// <summary>
    /// A listen address cannot be bound: in use, not a local address, or not permitted
    /// (every <see cref="ListenerBindFailure"/> except
    /// <see cref="ListenerBindFailure.HostNotFound"/>). The same number as upstream curl's
    /// <c>CURLE_INTERFACE_FAILED</c>, which curl returns when its own local bind fails.
    /// </summary>
    BindFailed = 45,

    /// <summary>
    /// The server certificate or its key cannot be served: <c>--cert</c> or <c>--key</c> is
    /// missing, unreadable, not in the named format, has a key that does not match the
    /// certificate, needs a <c>--pass</c> it was not given or was given a wrong one, or holds
    /// a key type Surl cannot serve (ADR-0010, section 3). The same number as upstream curl's
    /// <c>CURLE_SSL_CERTPROBLEM</c>.
    /// </summary>
    CertificateProblem = 58,

    /// <summary>
    /// The <c>--cacert</c> file exists but holds no certificate Surl can read (ADR-0010,
    /// section 3); a <c>--cacert</c> that does not exist is <see cref="FailedInit"/>. The
    /// same number as upstream curl's <c>CURLE_SSL_CACERT_BADFILE</c>.
    /// </summary>
    CaCertificateBadFile = 77,

    /// <summary>
    /// Another running surl holds the data directory's <c>.surl/lock</c> file (ADR-0031,
    /// decision 7). No upstream <c>CURLE_*</c> code means a resource another process holds;
    /// the number is the next Surl counts down from 125.
    /// </summary>
    DataDirectoryInUse = 124,

    /// <summary>
    /// An unexpected internal failure: an exception no other member names, reaching the top
    /// of <c>surl</c>. No upstream <c>CURLE_*</c> code is reused; the number is the first
    /// Surl counts down from 125.
    /// </summary>
    InternalError = 125,
}
