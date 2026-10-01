using System.Security.Cryptography;
using Surl.Cli;
using Surl.Protocol.Ssh;

namespace Surl.Console;

/// <summary>
/// The algorithms the SSH server offers: ADR-0051 decision 2's default offer, with its weak
/// algorithms under <c>--allow-weak-ssh-algorithms</c>, narrowed by <c>--ssh-ciphers</c> and
/// <c>--ssh-macs</c> (ADR-0066).
/// </summary>
internal static class SshAlgorithmComposition
{
    /// <summary>
    /// The refusal of the first <c>--ssh-ciphers</c> name, then the first <c>--ssh-macs</c> name,
    /// surl cannot offer (ADR-0066): one it does not offer on this machine at all, or a weak one
    /// without <c>--allow-weak-ssh-algorithms</c>.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>The refusal after the <c>surl: </c> prefix, or <see langword="null"/>.</returns>
    public static string? FindRefusal(SurlCommandLine commandLine)
    {
        var everyOffered = SshAlgorithmOffer.Default([], AesGcm.IsSupported, allowWeakAlgorithms: true);
        var strongOffered = SshAlgorithmOffer.Default([], AesGcm.IsSupported);

        return FindRefusal("--ssh-ciphers", "cipher", commandLine.SshCiphers, everyOffered.Cipher, strongOffered.Cipher, commandLine.AllowWeakSshAlgorithms)
            ?? FindRefusal("--ssh-macs", "MAC", commandLine.SshMacs, everyOffered.Mac, strongOffered.Mac, commandLine.AllowWeakSshAlgorithms);
    }

    /// <summary>
    /// The offer for the host keys held: the default one, weak algorithms too under
    /// <c>--allow-weak-ssh-algorithms</c>, its ciphers and MACs narrowed to <c>--ssh-ciphers</c>
    /// and <c>--ssh-macs</c> when given.
    /// </summary>
    /// <param name="heldHostKeyAlgorithms">The host-key algorithms the server's keys sign with.</param>
    /// <param name="commandLine">The parsed command line.</param>
    /// <returns>The offer.</returns>
    public static SshAlgorithmOffer Compose(IEnumerable<string> heldHostKeyAlgorithms, SurlCommandLine commandLine) =>
        SshAlgorithmOffer.Default(heldHostKeyAlgorithms, AesGcm.IsSupported, commandLine.AllowWeakSshAlgorithms)
            .Narrowed(commandLine.SshCiphers, commandLine.SshMacs);

    private static string? FindRefusal(
        string option,
        string kind,
        IReadOnlyList<string>? given,
        IReadOnlyList<string> everyOffered,
        IReadOnlyList<string> strongOffered,
        bool allowWeak)
    {
        foreach (var name in given ?? [])
        {
            if (!everyOffered.Contains(name))
            {
                return $"(2) {option}: surl does not offer the SSH {kind} {name}";
            }

            if (!allowWeak && !strongOffered.Contains(name))
            {
                return $"(2) {option}: {name} needs --allow-weak-ssh-algorithms";
            }
        }

        return null;
    }
}
