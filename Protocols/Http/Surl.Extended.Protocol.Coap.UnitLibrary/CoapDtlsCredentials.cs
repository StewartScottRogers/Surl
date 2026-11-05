using System.Security.Cryptography.X509Certificates;
using Surl.Dtls;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// What a <c>coaps</c> listener authenticates with (ADR-0087 decision 3, ADR-0097 decision 6): the
/// process certificate for the Certificate and RawPublicKey modes, the accounts' pre-shared keys for
/// the PreSharedKey mode, and the <c>--cacert</c> anchors a client certificate must chain to.
/// At least one of <paramref name="Certificate"/> and <paramref name="PreSharedKeys"/> is given.
/// </summary>
/// <param name="Certificate">The certificate and key from <c>--cert</c> or <c>--self-signed</c>, or none.</param>
/// <param name="PreSharedKeys">
/// The key of each PSK identity, built by the caller from the <c>--user</c> and <c>--user-file</c>
/// accounts, or none when no account exists.
/// </param>
/// <param name="ClientCertificateAuthorities">The <c>--cacert</c> anchors, or none when no client certificate is asked for.</param>
public sealed record CoapDtlsCredentials(
    DtlsServerCertificate? Certificate,
    DtlsPreSharedKeyLookup? PreSharedKeys,
    X509Certificate2Collection? ClientCertificateAuthorities = null);
