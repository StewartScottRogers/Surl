using System.Net;
using Surl.Cli;
using Surl.Content;
using Surl.Networking;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The ordered start (ADR-0099, decision 2): the option refusals, the data directory, the content
/// store, the listen-URL refusals, authentication and host keys, the lock, the service state and
/// server TLS, each refusal returned rather than written.
/// </summary>
public static partial class SurlServerComposition
{
    /// <summary>
    /// Runs the ordered start of <paramref name="commandLine"/> up to and including server TLS and
    /// the service state, opening no file for writing: <c>--auth gssapi</c> without
    /// <c>--keytab</c> and an <c>--ssh-ciphers</c> or <c>--ssh-macs</c> name surl cannot offer (2),
    /// a data directory that cannot be opened (37), a TLS-first listen URL with no certificate (58)
    /// or an <c>scp</c> or <c>sftp</c> one with no host key (2), the start files, the data
    /// directory's lock (124 or 23), the service state (37) and the TLS files (58, 2 or 77), in
    /// that order.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="seams">What the host hands the composition.</param>
    /// <param name="cancellationToken">
    /// Cancels the start; a start cancelled while loading the service state is refused with
    /// <see cref="SurlExitCode.Ok"/> and no lines, the same clean stop as one while serving.
    /// </param>
    /// <returns>The refusal, or the composed serving, which holds the lock until disposed.</returns>
    public static async Task<SurlServerStart> ComposeAsync(
        SurlCommandLine commandLine, SurlServerCompositionSeams seams, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(seams);

        if (FindOptionRefusal(commandLine) is { } optionRefusal)
        {
            return Refuse(SurlExitCode.FailedInit, optionRefusal);
        }

        if (commandLine.DataDirectory is { } dataDirectory && !seams.CanOpenDataDirectory(dataDirectory))
        {
            return Refuse(SurlExitCode.CouldNotReadFile, $"(37) Could not open directory {dataDirectory}");
        }

        return await ComposeAvailableAsync(commandLine, seams, cancellationToken);
    }

    /// <summary>
    /// Formats the <c>(45)</c> or <c>(6)</c> message for a listener that could not bind, after
    /// the <c>surl: </c> prefix (ADR-0007 section 5).
    /// </summary>
    /// <param name="bindFailure">The failure the listener factory threw.</param>
    /// <returns>The message.</returns>
    public static string FormatBindFailure(ListenerBindException bindFailure)
    {
        ArgumentNullException.ThrowIfNull(bindFailure);
        var listenUrl = bindFailure.ListenUrl;
        if (bindFailure.Failure == ListenerBindFailure.HostNotFound)
        {
            return $"(6) Could not resolve host: {listenUrl.Host}";
        }

        var address = bindFailure.EndPoint is IPEndPoint endPoint ? endPoint.Address.ToString() : listenUrl.Host;
        var bracketed = address.Contains(':', StringComparison.Ordinal) ? $"[{address}]" : address;
        return $"(45) Could not bind {listenUrl.Scheme}://{bracketed}:{listenUrl.Port}/: {DescribeBindFailure(bindFailure.Failure)}";
    }

    /// <summary>
    /// Formats the <c>(58)</c> message for a listen URL that is TLS from the first byte with
    /// neither <c>--cert</c> nor <c>--self-signed</c>, after the <c>surl: </c> prefix, the URL
    /// written as the status line writes it, with the port as given (ADR-0032, section 10).
    /// </summary>
    /// <param name="listenUrl">The first such listen URL.</param>
    /// <returns>The message.</returns>
    public static string FormatMissingCertificate(ListenUrl listenUrl)
    {
        ArgumentNullException.ThrowIfNull(listenUrl);
        return $"(58) {ListenerStatusLine.FormatBoundListenUrl(listenUrl with { BoundPort = listenUrl.Port })} needs a certificate: "
            + "give --cert <file>, or --self-signed for a throwaway one";
    }

    private static string DescribeBindFailure(ListenerBindFailure failure) => failure switch
    {
        ListenerBindFailure.AddressInUse => "Address already in use",
        ListenerBindFailure.AddressNotAvailable => "Address not available",
        ListenerBindFailure.PermissionDenied => "Permission denied",
        _ => "Bind failed",
    };

    private static SurlServerStart Refuse(SurlExitCode exitCode, params string[] lines) =>
        new(new SurlStartFailure(exitCode, lines), null);

    // --auth gssapi without --keytab (ADR-0057, decision 1), then an --ssh-ciphers or --ssh-macs
    // name surl cannot offer (ADR-0066): the first refusal of the options' consistency.
    private static string? FindOptionRefusal(SurlCommandLine commandLine) =>
        KeytabComposition.IsGssapiWithoutKeytab(commandLine)
            ? "(2) --auth gssapi needs --keytab"
            : SshAlgorithmComposition.FindRefusal(commandLine);

    // A listen URL TLS from the first byte with no certificate to serve (ADR-0032, section 10),
    // then an scp or sftp one with no host key to serve (ADR-0051, decision 4): each refused
    // before any listener binds. A scheme no server answers never gets here: every scheme
    // ListenUrlParser accepts is registered, and it refuses every other one with (1).
    private static SurlServerStart? FindListenUrlRefusal(SurlCommandLine commandLine)
    {
        if (ServerTlsComposition.FindListenUrlWithoutCertificate(commandLine) is { } uncertified)
        {
            return Refuse(SurlExitCode.CertificateProblem, FormatMissingCertificate(uncertified));
        }

        return SshHostKeyComposition.FindListenUrlWithoutHostKey(commandLine) is { } keyless
            ? Refuse(SurlExitCode.FailedInit, SshHostKeyComposition.FormatMissingHostKey(keyless))
            : null;
    }

    private static async Task<SurlServerStart> ComposeAvailableAsync(
        SurlCommandLine commandLine, SurlServerCompositionSeams seams, CancellationToken cancellationToken)
    {
        var fileSystem = ComposeContentFileSystem(
            commandLine,
            seams.TimeProvider,
            seams.DataDirectoryFileSystem ?? new DiskContentFileSystem(),
            seams.ContentWithoutDataDirectory);
        var contentStore = ComposeContentStore(commandLine, fileSystem);
        if (FindListenUrlRefusal(commandLine) is { } refusal)
        {
            return refusal;
        }

        var (authentication, failure) = ComposeAuthentication(commandLine, seams);
        return authentication is null
            ? new SurlServerStart(failure, null)
            : await LockThenComposeAsync(commandLine, seams, fileSystem, contentStore, authentication, cancellationToken);
    }

    // The --auth words, the --user-file, the --authorized-keys and --keytab files, then the --hostkey
    // and --hostcert files, are checked before the lock is taken and any listener binds (ADR-0032, sections 1 and 2;
    // ADR-0051, decisions 4 and 6; ADR-0057, decision 1).
    private static (ComposedAuthentication? Authentication, SurlStartFailure? Failure) ComposeAuthentication(
        SurlCommandLine commandLine, SurlServerCompositionSeams seams)
    {
        var readStartFile = seams.ReadStartFile ?? File.ReadAllBytes;
        var (policy, accountNames, skippedKeytabEntries, exitCode, failureMessage) =
            AuthenticationComposition.Compose(commandLine, readStartFile, seams.TimeProvider);
        if (policy is null)
        {
            return (null, new SurlStartFailure(exitCode, [failureMessage!]));
        }

        var (sshHostKeys, hostKeyExitCode, hostKeyFailureMessage) = SshHostKeyComposition.Compose(commandLine, readStartFile);
        return sshHostKeys is null
            ? (null, new SurlStartFailure(hostKeyExitCode, [hostKeyFailureMessage!]))
            : (new ComposedAuthentication(policy, accountNames, skippedKeytabEntries, sshHostKeys), null);
    }

    private static async Task<SurlServerStart> LockThenComposeAsync(
        SurlCommandLine commandLine,
        SurlServerCompositionSeams seams,
        IContentFileSystem fileSystem,
        ContentStore contentStore,
        ComposedAuthentication authentication,
        CancellationToken cancellationToken)
    {
        var dataDirectoryLock = commandLine.DataDirectory is { } dataDirectory
            ? seams.TakeDataDirectoryLock(dataDirectory)
            : DataDirectoryLockOutcome.NoLock;
        if (dataDirectoryLock.FailureMessage is { } lockFailure)
        {
            return Refuse(dataDirectoryLock.ExitCode, lockFailure);
        }

        // The lock is handed to the composed serving, or released when the start ends any other way.
        SurlServerStart? start = null;
        try
        {
            start = await ComposeUnderTheLockAsync(
                commandLine, seams, fileSystem, contentStore, authentication, dataDirectoryLock.Holder, cancellationToken);
            return start;
        }
        finally
        {
            if (start?.Serving is null)
            {
                dataDirectoryLock.Holder?.Dispose();
            }
        }
    }

    private static async Task<SurlServerStart> ComposeUnderTheLockAsync(
        SurlCommandLine commandLine,
        SurlServerCompositionSeams seams,
        IContentFileSystem fileSystem,
        ContentStore contentStore,
        ComposedAuthentication authentication,
        IDisposable? dataDirectoryLock,
        CancellationToken cancellationToken)
    {
        var isCertificateConfigured = ServerTlsComposition.IsCertificateConfigured(commandLine);
        (ServiceState? State, string? FailureMessage) loaded;
        try
        {
            loaded = await LoadServiceStateAsync(
                commandLine,
                fileSystem,
                authentication.Policy,
                authentication.AccountNames,
                isCertificateConfigured,
                seams.TimeProvider,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stopped before serving began: the same clean stop as one while serving.
            return Refuse(SurlExitCode.Ok);
        }

        if (loaded.State is null)
        {
            return Refuse(SurlExitCode.CouldNotReadFile, loaded.FailureMessage!);
        }

        IProtocolServer[] servers =
        [
            .. ComposeProtocolServers(
                contentStore,
                loaded.State,
                authentication.Policy,
                authentication.SshHostKeys.HostKeys,
                SshAlgorithmComposition.Compose(authentication.SshHostKeys.HostKeys.SignatureAlgorithms, commandLine),
                isCertificateConfigured,
                commandLine.WsEcho),
            .. ComposeExtendedProtocolServers(
                seams.AreExtensionsTurnedOn,
                seams.ComposeExtendedProtocolServers,
                new ExtendedProtocolServerServices(contentStore, authentication.Policy, isCertificateConfigured)),
        ];
        return ComposeServing(commandLine, seams, servers, authentication, dataDirectoryLock);
    }

    private static SurlServerStart ComposeServing(
        SurlCommandLine commandLine,
        SurlServerCompositionSeams seams,
        IProtocolServer[] servers,
        ComposedAuthentication authentication,
        IDisposable? dataDirectoryLock)
    {
        ServerTlsComposition tls;
        try
        {
            tls = ServerTlsComposition.Compose(commandLine, seams.TimeProvider);
        }
        catch (TlsFileLoadException failure)
        {
            var (exitCode, lines) = ServerTlsComposition.DescribeTlsFileFailure(failure, commandLine.CaCertificateFile);
            return Refuse(exitCode, lines);
        }

        return new SurlServerStart(null, new ComposedServing(commandLine, seams, servers, authentication, tls, dataDirectoryLock));
    }
}
