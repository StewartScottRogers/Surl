using Surl.Cli;
using Surl.Dtls;

namespace Surl.Composition;

/// <summary>
/// Turns the served certificate into the one a <c>coaps</c> DTLS handshake presents, and refuses at
/// start a key DTLS cannot sign with (ADR-0087 decision 3, BL-476).
/// </summary>
internal static class CoapsDtlsCertificate
{
    private const string CoapsScheme = "coaps";

    /// <summary>Pairs <paramref name="certificate"/>'s chain with its key for DTLS.</summary>
    /// <param name="certificate">The served certificate.</param>
    /// <returns>The DTLS certificate.</returns>
    /// <exception cref="ArgumentException">The key is not RSA of 2048 bits or more, or ECDSA on P-256 or P-384.</exception>
    public static DtlsServerCertificate From(QuicServerCertificate certificate) =>
        new([.. certificate.Chain.Select(der => der.ToArray())], certificate.Key);

    /// <summary>
    /// The <c>(58)</c> message, after <c>surl: </c>, when a <c>coaps</c> listen URL is given and the
    /// served certificate's key cannot be used for DTLS; <see langword="null"/> otherwise.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="certificate">The served certificate, or <see langword="null"/> when none is.</param>
    /// <returns>The message, or <see langword="null"/>.</returns>
    public static string? FindRefusal(SurlCommandLine commandLine, QuicServerCertificate? certificate)
    {
        if (certificate is null || !commandLine.ListenUrls.Any(listenUrl => listenUrl.Scheme == CoapsScheme))
        {
            return null;
        }

        try
        {
            _ = From(certificate);
            return null;
        }
        catch (ArgumentException failure)
        {
            return $"(58) Could not load the server certificate: {failure.Message}";
        }
    }
}
