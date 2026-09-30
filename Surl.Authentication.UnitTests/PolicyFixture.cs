using System.Net.Security;
using System.Security.Authentication;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// Builds the policies and requests the policy tests share.
/// </summary>
internal static class PolicyFixture
{
    public static readonly TlsSession Tls =
        new(SslProtocols.Tls13, TlsCipherSuite.TLS_AES_128_GCM_SHA256, null, null, null);

    public static readonly AccountBook NoAccounts = new([]);

    public static readonly AccountBook AliceAndToken =
        new([new Account("alice", "secret"), new Account(string.Empty, "tok")]);

    public static AuthenticationPolicy Create(
        AccountBook accounts,
        TimeProvider clock,
        bool allowAnonymous = false,
        bool allowPlaintextAuth = false,
        IReadOnlySet<AuthenticationMethod>? acceptedMethods = null,
        params IHttpAuthenticationMethod[] httpMethods) =>
        new(
            new AuthenticationSettings(
                accounts, allowAnonymous, allowPlaintextAuth, acceptedMethods ?? AuthenticationMethods.DefaultAccepted),
            httpMethods,
            clock);

    public static HttpAuthenticationRequest Get(params string[] authorization) =>
        Request("GET", false, authorization);

    public static HttpAuthenticationRequest Put(params string[] authorization) =>
        Request("PUT", true, authorization);

    private static HttpAuthenticationRequest Request(string method, bool isWrite, string[] authorization) =>
        new(
            method,
            "/x",
            isWrite,
            [
                new KeyValuePair<string, string>("Host", "localhost"),
                .. authorization.Select(value => new KeyValuePair<string, string>("Authorization", value)),
            ]);
}
