using System.Security.Cryptography;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Smb;
using Surl.Protocol.Ssh;

namespace Surl.Conformance;

/// <summary>
/// The file-system server halves of ADR-0080 decision 2 (BL-397): Surl's own SSH server, which
/// answers <c>scp</c> and <c>sftp</c> cases from the case's folder, and its SMB server, which
/// answers <c>smb</c> cases from a <c>TESTS</c> share. One run shares one RSA host key, whose
/// hashes are <c>%SSHSRVMD5</c> and <c>%SSHSRVSHA256</c>, and one RSA client key pair, which
/// every SSH case gets as <c>%LOGDIR/server/curl_client_key</c> and its <c>.pub</c>.
/// </summary>
/// <remarks>
/// The SSH server serves the case's folder as its root, so a case's <c>%SFTP_PWD</c> and
/// <c>%SCP_PWD</c> - upstream's absolute path of that folder as its sshd sees it - are empty
/// here: <c>sftp://host:port%SFTP_PWD/%LOGDIR/file</c> names <c>/log/file</c>, the same file.
/// Uploads, listings, dot files and symbolic links are all allowed, as upstream's sshd allows
/// them to the user running the tests.
/// </remarks>
internal sealed class UpstreamTestCaseFileServers : IDisposable
{
    /// <summary>The share upstream's SMB test server serves each case's <c>&lt;data&gt;</c> from.</summary>
    public const string SmbShareName = "TESTS";

    /// <summary>Where the case's client key pair goes, relative to <c>%LOGDIR</c>.</summary>
    public const string ClientKeyPath = "server/curl_client_key";

    private static readonly ContentExposureOptions CaseFolderExposure =
        ContentExposureOptions.ServeEverythingInsideTheRoot with { AllowUploads = true, MaxUploadBytes = 0 };

    private readonly RSA hostRsa;
    private readonly SshHostKeySet hostKeys = new();
    private readonly byte[] clientKeyBlob;

    public UpstreamTestCaseFileServers()
    {
        hostRsa = RSA.Create(2048);
        var hostKey = SshHostKey.FromRsa(hostRsa);
        hostKeys.TryAdd(hostKey, out _);
        HostKeyMd5 = Md5Hex(hostKey.PublicKeyBlob.Span);
        HostKeySha256 = Convert.ToBase64String(SHA256.HashData(hostKey.PublicKeyBlob.Span)).TrimEnd('=');
        var (privateKeyPem, publicKeyLine) = SshTestKeys.RsaUserKeyPair();
        clientKeyBlob = Convert.FromBase64String(publicKeyLine.Split(' ')[1]);
        ClientKeyFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [ClientKeyPath] = Encoding.ASCII.GetBytes(privateKeyPem + "\n"),
            [ClientKeyPath + ".pub"] = Encoding.ASCII.GetBytes(publicKeyLine + "\n"),
        };
    }

    /// <summary>The host key's algorithm: <c>%SSHKEYALGO</c>.</summary>
    public string HostKeyAlgorithm => "ssh-rsa";

    /// <summary>The host key's MD5, 32 lower-case hex digits: <c>%SSHSRVMD5</c>.</summary>
    public string HostKeyMd5 { get; }

    /// <summary>
    /// The host key's SHA-256 in base64 without padding, as <c>ssh-keygen -l</c> writes it and
    /// upstream's sshserver.pl takes it: <c>%SSHSRVSHA256</c>.
    /// </summary>
    public string HostKeySha256 { get; }

    /// <summary>The client key pair's files by path relative to <c>%LOGDIR</c>.</summary>
    public IReadOnlyDictionary<string, byte[]> ClientKeyFiles { get; }

    /// <summary>
    /// The login name the SSH server half lets in, <c>%USER</c>: the run's user name without the
    /// characters a case's unquoted <c>-u %USER:</c> would split or misread (a blank, a colon),
    /// as upstream's sshd lets in the user running the tests; <c>surl</c> when none is left.
    /// </summary>
    public static string SshLoginNameFor(string userName)
    {
        var name = new string([.. userName.Where(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')]);
        return name.Length > 0 ? name : "surl";
    }

    /// <summary>
    /// Makes the SSH server that serves <paramref name="caseDirectory"/> to
    /// <paramref name="userName"/> alone, logging in with the run's client key.
    /// </summary>
    public IConnectionProtocolServer CreateSsh(string caseDirectory, string userName) =>
        new SshProtocolServer(
            hostKeys,
            SshAlgorithmOffer.Default(hostKeys.SignatureAlgorithms, AesGcm.IsSupported),
            new UpstreamTestCaseSshLoginPolicy(userName, clientKeyBlob),
            new SshSystemRandomSource(),
            new ContentStore(caseDirectory, new DiskContentFileSystem(), CaseFolderExposure));

    /// <summary>
    /// Makes the SMB server that serves <paramref name="smbRoot"/>, whose top-level folders are
    /// its shares, to any login: a case tests curl's login, not Surl's accounts.
    /// </summary>
    public static IConnectionProtocolServer CreateSmb(string smbRoot) =>
        new SmbProtocolServer(
            new ContentStore(smbRoot, new DiskContentFileSystem(), CaseFolderExposure),
            new AnonymousAuthenticationPolicy());

    /// <inheritdoc/>
    public void Dispose() => hostRsa.Dispose();

    // MD5 names the key as curl's --hostpubmd5 pins it; it secures nothing here.
#pragma warning disable CA5351
    private static string Md5Hex(ReadOnlySpan<byte> blob) => Convert.ToHexStringLower(MD5.HashData(blob));
#pragma warning restore CA5351
}
