using System.Security.Cryptography;
using Surl.Cli;
using Surl.Output;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Ssh;

namespace Surl.Console;

/// <summary>
/// The SSH server's host keys as the command line asks for them (ADR-0051, decision 4): every
/// <c>--hostkey</c> file, read whenever one is given, and with <c>--throwaway-hostkey</c> a
/// throwaway RSA 3072-bit key, made only when an <c>scp</c> or <c>sftp</c> listen URL is served.
/// Each file is read before any listener binds, and a file that cannot be used ends the start
/// with ADR-0051 decision 4's exit code and text.
/// </summary>
/// <param name="HostKeys">Every key the SSH server serves; empty when none is given.</param>
/// <param name="ThrowawayHostKey">The throwaway key when one was made; otherwise <see langword="null"/>.</param>
internal sealed record SshHostKeyComposition(SshHostKeySet HostKeys, SshHostKey? ThrowawayHostKey)
{
    /// <summary>The size of the throwaway RSA host key (ADR-0051, decision 4).</summary>
    public const int ThrowawayHostKeyBits = 3072;

    private const string WarningPrefix = "surl: warning: ";

    /// <summary>The <c>--allow-weak-ssh-algorithms</c> warning after its prefix (ADR-0051, decision 11).</summary>
    public const string WeakAlgorithmsWarning =
        "--allow-weak-ssh-algorithms: SHA-1, MD5, CBC, RC4, 3DES and 1024-bit Diffie-Hellman SSH algorithms are offered";

    /// <summary>
    /// Whether any listen URL is an <c>scp</c> or <c>sftp</c> one, which the SSH server answers.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns><see langword="true"/> when the SSH server is served.</returns>
    public static bool IsSshServed(SurlCommandLine commandLine) =>
        commandLine.ListenUrls.Any(listenUrl => IsSshListenUrl(listenUrl));

    /// <summary>
    /// Finds the first <c>scp</c> or <c>sftp</c> listen URL, in command-line order, when neither
    /// <c>--hostkey</c> nor <c>--throwaway-hostkey</c> gives it a host key to serve.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>That listen URL, or <see langword="null"/> when every listen URL has what it needs.</returns>
    public static ListenUrl? FindListenUrlWithoutHostKey(SurlCommandLine commandLine) =>
        commandLine.HostKeyFiles.Count == 0 && !commandLine.ThrowawayHostKey
            ? commandLine.ListenUrls.FirstOrDefault(listenUrl => IsSshListenUrl(listenUrl))
            : null;

    /// <summary>
    /// Formats the <c>(2)</c> message for an SSH listen URL with no host key, after the
    /// <c>surl: </c> prefix, the URL written as the status line writes it, with the port as given.
    /// </summary>
    /// <param name="listenUrl">The first such listen URL.</param>
    /// <returns>The message.</returns>
    public static string FormatMissingHostKey(ListenUrl listenUrl) =>
        $"(2) {ListenerStatusLine.FormatBoundListenUrl(listenUrl with { BoundPort = listenUrl.Port })} needs a host key: "
        + "give --hostkey <file>, or --throwaway-hostkey for a throwaway one";

    /// <summary>
    /// Reads every <c>--hostkey</c> file through <paramref name="readFile"/>, in command-line
    /// order, and makes the throwaway key when <c>--throwaway-hostkey</c> is given and an
    /// <c>scp</c> or <c>sftp</c> listen URL is served.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="readFile">Reads a file's bytes, given its path as given.</param>
    /// <returns>
    /// The host keys; or, when a file cannot be used, <see langword="null"/> with the exit code and
    /// the message after the <c>surl: </c> prefix, which names the file as given and never holds
    /// key material.
    /// </returns>
    public static (SshHostKeyComposition? Composition, SurlExitCode ExitCode, string? FailureMessage) Compose(
        SurlCommandLine commandLine, Func<string, byte[]> readFile)
    {
        var hostKeys = new SshHostKeySet();
        var pathsByKey = new Dictionary<SshHostKey, string>(ReferenceEqualityComparer.Instance);
        foreach (var path in commandLine.HostKeyFiles)
        {
            var (key, exitCode, failureMessage) = ReadHostKey(commandLine, path, readFile);
            if (key is null)
            {
                return (null, exitCode, failureMessage);
            }

            if (!hostKeys.TryAdd(key, out var heldKey))
            {
                return (null, SurlExitCode.FailedInit, $"(2) Host key {path}: a {key.KeyType} host key is already given by {pathsByKey[heldKey]}");
            }

            pathsByKey.Add(key, path);
        }

        return (new SshHostKeyComposition(hostKeys, AddThrowawayHostKeyWhenAsked(commandLine, hostKeys)), SurlExitCode.Ok, null);
    }

    /// <summary>
    /// Formats the verbose note naming one served host key by the values curl's
    /// <c>--hostpubsha256</c> and <c>--hostpubmd5</c> pin it with (ADR-0051, decision 8): the
    /// SHA-256 of its public key blob in base64 with padding, and its MD5 in lower-case hex.
    /// </summary>
    /// <param name="hostKey">The host key.</param>
    /// <returns>The note, <c>* Serving SSH host key &lt;key type&gt;, --hostpubsha256 &lt;base64&gt; --hostpubmd5 &lt;hex&gt;</c>.</returns>
    public static string FormatHostKeyNote(SshHostKey hostKey) =>
        $"* Serving SSH host key {hostKey.KeyType}, --hostpubsha256 {FormatSha256(hostKey)} --hostpubmd5 {FormatMd5(hostKey)}";

    /// <summary>
    /// Writes the <c>--throwaway-hostkey</c> warning from the info level up when the throwaway key
    /// was made, then the <c>--allow-weak-ssh-algorithms</c> warning from the info level up
    /// whenever the option is given (ADR-0051, decision 11), then, from the verbose level up and only when an
    /// <c>scp</c> or <c>sftp</c> listen URL is served, the note for each host key served
    /// (decision 8).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="log">The log stream.</param>
    public void WriteStartLines(SurlCommandLine commandLine, TextWriter log)
    {
        if (commandLine.LogLevel >= LogLevel.Info)
        {
            WriteWarnings(commandLine, log);
        }

        if (commandLine.LogLevel < LogLevel.Verbose || !IsSshServed(commandLine))
        {
            return;
        }

        foreach (var hostKey in HostKeys.Keys)
        {
            log.WriteLine(FormatHostKeyNote(hostKey));
        }
    }

    // The --throwaway-hostkey warning when the key was made, then the --allow-weak-ssh-algorithms
    // one when the option is given (ADR-0051, decision 11).
    private void WriteWarnings(SurlCommandLine commandLine, TextWriter log)
    {
        if (ThrowawayHostKey is { } throwawayHostKey)
        {
            log.WriteLine(
                WarningPrefix + $"--throwaway-hostkey: serving a throwaway SSH host key (--hostpubsha256 {FormatSha256(throwawayHostKey)}); "
                + "clients must pin it or skip the check (curl -k)");
        }

        if (commandLine.AllowWeakSshAlgorithms)
        {
            log.WriteLine(WarningPrefix + WeakAlgorithmsWarning);
        }
    }

    private static bool IsSshListenUrl(ListenUrl listenUrl) => listenUrl.Scheme is "scp" or "sftp";

    // A file that cannot be read is 37, one surl cannot use as a host key 2 (ADR-0051 decision 4).
    private static (SshHostKey? Key, SurlExitCode ExitCode, string? FailureMessage) ReadHostKey(
        SurlCommandLine commandLine, string path, Func<string, byte[]> readFile)
    {
        byte[] content;
        try
        {
            content = readFile(path);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return (null, SurlExitCode.CouldNotReadFile, $"(37) Could not read host key {path}");
        }

        var reading = SshHostKeyFile.Read(content, commandLine.KeyPassphrase, commandLine.AllowWeakSshAlgorithms);
        return reading.Key is { } key
            ? (key, SurlExitCode.Ok, null)
            : (null, SurlExitCode.FailedInit, $"(2) Host key {path}: {reading.Refusal!.Text}");
    }

    // The parser refuses --throwaway-hostkey beside --hostkey, so the set is empty when it is made.
    private static SshHostKey? AddThrowawayHostKeyWhenAsked(SurlCommandLine commandLine, SshHostKeySet hostKeys)
    {
        if (!commandLine.ThrowawayHostKey || !IsSshServed(commandLine))
        {
            return null;
        }

        using var rsa = RSA.Create(ThrowawayHostKeyBits);
        var throwawayHostKey = SshHostKey.FromRsa(rsa);
        hostKeys.TryAdd(throwawayHostKey, out _);
        return throwawayHostKey;
    }

    private static string FormatSha256(SshHostKey hostKey) =>
        Convert.ToBase64String(SHA256.HashData(hostKey.PublicKeyBlob.Span));

    // MD5 names the key as curl's --hostpubmd5 pins it; it secures nothing here.
#pragma warning disable CA5351
    private static string FormatMd5(SshHostKey hostKey) =>
        Convert.ToHexStringLower(MD5.HashData(hostKey.PublicKeyBlob.Span));
#pragma warning restore CA5351
}
