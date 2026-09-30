using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ssh.SshTestExchange;
using static Surl.Protocol.Ssh.SshTestKeyExchangeClient;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The <c>ssh-userauth</c> service (RFC 4252; ADR-0051 decisions 6 and 7): the service request,
/// the <c>none</c>, <c>password</c>, <c>keyboard-interactive</c> and <c>publickey</c> methods
/// judged by a policy double, the public-key signatures the server verifies itself, the attempt
/// limit, the fixed user and service, and the notes. Every message is built here by hand from the
/// RFCs, and every signature is made with the base class library, not with the server's code.
/// </summary>
[TestClass]
public sealed class SshUserAuthenticationTests
{
    private const string MethodList = "publickey,password,keyboard-interactive";

    private static readonly byte[] Failure = Concat([51], String(MethodList), [0]);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AnswerServiceRequest_SshUserAuth_IsAcceptedWithItsName()
    {
        var (client, serving) = await OpenAsync(new SshTestAuthenticationPolicy(), requestService: false);

        client.Send(Concat([5], String("ssh-userauth")));

        CollectionAssert.AreEqual(Concat([6], String("ssh-userauth")), await client.ReceiveAsync());
        await CloseAsync(client, serving);
    }

    [TestMethod]
    public async Task AnswerServiceRequest_AnotherService_IsDisconnect7()
    {
        var (client, serving) = await OpenAsync(new SshTestAuthenticationPolicy(), requestService: false);

        client.Send(Concat([5], String("ssh-connection")));

        await AssertDisconnectAsync(client, serving, 7, "Service not available");
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "The client asked for the SSH service ssh-connection; only ssh-userauth is offered.");
    }

    [TestMethod]
    public async Task AnswerServiceRequest_SshUserAuthTwice_IsDisconnect2()
    {
        var (client, serving) = await OpenAsync(new SshTestAuthenticationPolicy());

        client.Send(Concat([5], String("ssh-userauth")));

        await AssertDisconnectAsync(client, serving, 2, "Protocol error");
    }

    [TestMethod]
    public async Task AnswerLoginRequest_BeforeTheServiceRequest_IsDisconnect2()
    {
        var (client, serving) = await OpenAsync(new SshTestAuthenticationPolicy(), requestService: false);

        client.Send(LoginRequest("none"));

        await AssertDisconnectAsync(client, serving, 2, "Protocol error");
    }

    [TestMethod]
    public async Task AnswerLoginRequest_None_IsAnsweredWithTheMethodListAndNotCounted()
    {
        var policy = new SshTestAuthenticationPolicy();
        var (client, serving) = await OpenAsync(policy);

        for (var request = 0; request < SshUserAuthentication.MaxRefusals + 1; request++)
        {
            client.Send(LoginRequest("none"));
            CollectionAssert.AreEqual(Failure, await client.ReceiveAsync());
        }

        await CloseAsync(client, serving);
        Assert.AreEqual(new SshNoneLogin("alice"), policy.Logins.First());
        Assert.IsFalse(client.Log.Notes.Any(note => note.Contains("login", StringComparison.OrdinalIgnoreCase)), "A none request is never noted.");
    }

    [TestMethod]
    public async Task AnswerLoginRequest_NoneUnderAllowAnonymous_SucceedsAndLaterRequestsAreIgnored()
    {
        var (client, serving) = await OpenAsync(new AnonymousAuthenticationPolicy());

        client.Send(LoginRequest("none"));
        CollectionAssert.AreEqual(new byte[] { 52 }, await client.ReceiveAsync());
        client.Send(LoginRequest("none"));
        client.Send([90]);

        CollectionAssert.AreEqual(Concat([3], UInt32(3)), await client.ReceiveAsync(), "The later request had no answer; the channel message is UNIMPLEMENTED.");
        await CloseAsync(client, serving);
    }

    [TestMethod]
    public async Task AnswerLoginRequest_PasswordOfTheAccount_SucceedsAndNotesTheLoginWithoutThePassword()
    {
        var policy = new SshTestAuthenticationPolicy();
        var (client, serving) = await OpenAsync(policy);

        client.Send(LoginRequest("password", [0], String("secret")));

        CollectionAssert.AreEqual(new byte[] { 52 }, await client.ReceiveAsync());
        await CloseAsync(client, serving);
        var login = (SshPasswordLogin)policy.Logins.Single();
        Assert.AreEqual("password", login.Method);
        Assert.AreEqual("alice", login.UserName);
        CollectionAssert.AreEqual(Ascii("secret"), login.Password.ToArray());
        CollectionAssert.IsSubsetOf(new[] { "SSH login request: password for alice", "Login accepted: password alice" }, client.Log.Notes.ToArray());
        Assert.IsFalse(client.Log.Notes.Any(note => note.Contains("secret", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task AnswerLoginRequest_WrongPassword_IsRefusedAndNoted()
    {
        var (client, serving) = await OpenAsync(new SshTestAuthenticationPolicy());

        client.Send(LoginRequest("password", [0], String("wrong")));

        CollectionAssert.AreEqual(Failure, await client.ReceiveAsync());
        await CloseAsync(client, serving);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "Login refused: password alice");
        Assert.IsFalse(client.Log.Notes.Any(note => note.Contains("wrong", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task AnswerLoginRequest_PasswordChangeRequest_IsRefusedUnchecked()
    {
        var policy = new SshTestAuthenticationPolicy();
        var (client, serving) = await OpenAsync(policy);

        client.Send(LoginRequest("password", [1], String("secret"), String("new")));

        CollectionAssert.AreEqual(Failure, await client.ReceiveAsync());
        await CloseAsync(client, serving);
        Assert.IsEmpty(policy.Logins);
    }

    [TestMethod]
    public async Task AnswerLoginRequest_UserNameNotUtf8_IsCheckedWithNoUser()
    {
        var policy = new SshTestAuthenticationPolicy();
        var (client, serving) = await OpenAsync(policy);

        client.Send(Concat([50], Str([0xC3, 0x28]), String("ssh-connection"), String("password"), [0], String("secret")));

        CollectionAssert.AreEqual(Failure, await client.ReceiveAsync());
        await CloseAsync(client, serving);
        Assert.IsNull(((SshPasswordLogin)policy.Logins.Single()).UserName);
        CollectionAssert.IsSubsetOf(new[] { @"SSH login request: password for \xC3(", "Login refused: password" }, client.Log.Notes.ToArray());
    }

    [TestMethod]
    public async Task AnswerLoginRequest_KeyboardInteractive_PromptsForThePasswordAndChecksTheAnswer()
    {
        var policy = new SshTestAuthenticationPolicy();
        var (client, serving) = await OpenAsync(policy);

        client.Send(LoginRequest("keyboard-interactive", String(string.Empty), String(string.Empty)));
        CollectionAssert.AreEqual(
            Concat([60], String(string.Empty), String(string.Empty), String(string.Empty), UInt32(1), String("Password: "), [0]),
            await client.ReceiveAsync());
        client.Send(Concat([61], UInt32(1), String("secret")));

        CollectionAssert.AreEqual(new byte[] { 52 }, await client.ReceiveAsync());
        await CloseAsync(client, serving);
        Assert.AreEqual("keyboard-interactive", ((SshPasswordLogin)policy.Logins.Single()).Method);
        CollectionAssert.IsSubsetOf(
            new[] { "SSH login request: keyboard-interactive for alice", "Login accepted: keyboard-interactive alice" },
            client.Log.Notes.ToArray());
    }

    [TestMethod]
    public async Task AnswerInfoResponse_NotOneResponse_IsRefusedUnchecked()
    {
        var policy = new SshTestAuthenticationPolicy();
        var (client, serving) = await OpenAsync(policy);
        client.Send(LoginRequest("keyboard-interactive", String(string.Empty), String(string.Empty)));
        await client.ReceiveAsync();

        client.Send(Concat([61], UInt32(2), String("secret"), String("secret")));

        CollectionAssert.AreEqual(Failure, await client.ReceiveAsync());
        await CloseAsync(client, serving);
        Assert.IsEmpty(policy.Logins);
    }

    [TestMethod]
    public async Task AnswerInfoResponse_NoPromptAwaitingIt_IsDisconnect2()
    {
        var (client, serving) = await OpenAsync(new SshTestAuthenticationPolicy());
        client.Send(LoginRequest("keyboard-interactive", String(string.Empty), String(string.Empty)));
        await client.ReceiveAsync();
        client.Send(LoginRequest("none"));
        await client.ReceiveAsync();

        client.Send(Concat([61], UInt32(1), String("secret")));

        await AssertDisconnectAsync(client, serving, 2, "Protocol error");
    }

    [TestMethod]
    [DataRow("ecdsa-sha2-nistp256")]
    [DataRow("ecdsa-sha2-nistp384")]
    [DataRow("ecdsa-sha2-nistp521")]
    [DataRow("rsa-sha2-256")]
    [DataRow("rsa-sha2-512")]
    public async Task AnswerLoginRequest_PublicKeyQueryForAnAuthorizedKey_IsAnsweredPkOk(string algorithm)
    {
        var blob = UserKeyBlob(algorithm);
        var (client, serving) = await OpenAsync(new SshTestAuthenticationPolicy { AuthorizedKeyBlob = blob });

        client.Send(LoginRequest("publickey", [0], String(algorithm), Str(blob)));

        CollectionAssert.AreEqual(Concat([60], String(algorithm), Str(blob)), await client.ReceiveAsync());
        await CloseAsync(client, serving);
        var keyType = algorithm.StartsWith("rsa", StringComparison.Ordinal) ? "ssh-rsa" : algorithm;
        CollectionAssert.Contains(
            client.Log.Notes.ToArray(),
            $"SSH login request: publickey for alice, key {keyType} SHA-256 {Convert.ToBase64String(SHA256.HashData(blob))}");
        Assert.IsFalse(client.Log.Notes.Any(note => note.StartsWith("Login", StringComparison.Ordinal)), "A query is never noted as a login.");
    }

    [TestMethod]
    public async Task AnswerLoginRequest_PublicKeyQueryForAnotherKey_IsRefusedAndCounted()
    {
        var (client, serving) = await OpenAsync(new SshTestAuthenticationPolicy { AuthorizedKeyBlob = UserKeyBlob("rsa-sha2-256") });

        await AssertRefusedSixTimesAsync(client, serving, LoginRequest("publickey", [0], String("ecdsa-sha2-nistp256"), Str(UserKeyBlob("ecdsa-sha2-nistp256"))));
    }

    [TestMethod]
    [DataRow("ecdsa-sha2-nistp256")]
    [DataRow("ecdsa-sha2-nistp384")]
    [DataRow("ecdsa-sha2-nistp521")]
    [DataRow("rsa-sha2-256")]
    [DataRow("rsa-sha2-512")]
    public async Task AnswerLoginRequest_ValidSignatureByAnAuthorizedKey_Succeeds(string algorithm)
    {
        var policy = new SshTestAuthenticationPolicy { AuthorizedKeyBlob = UserKeyBlob(algorithm) };
        var (client, serving) = await OpenAsync(policy);

        client.Send(SignedRequest(client, algorithm));

        CollectionAssert.AreEqual(new byte[] { 52 }, await client.ReceiveAsync());
        await CloseAsync(client, serving);
        Assert.AreEqual(SshPublicKeyProof.ValidSignature, ((SshPublicKeyLogin)policy.Logins.Single()).Proof);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "Login accepted: publickey alice");
    }

    [TestMethod]
    [DataRow("ecdsa-sha2-nistp256")]
    [DataRow("rsa-sha2-256")]
    public async Task AnswerLoginRequest_InvalidSignature_IsJudgedInvalidAndRefused(string algorithm)
    {
        var policy = new SshTestAuthenticationPolicy { AuthorizedKeyBlob = UserKeyBlob(algorithm) };
        var (client, serving) = await OpenAsync(policy);

        client.Send(SignedRequest(client, algorithm, tamper: true));

        CollectionAssert.AreEqual(Failure, await client.ReceiveAsync());
        await CloseAsync(client, serving);
        Assert.AreEqual(SshPublicKeyProof.InvalidSignature, ((SshPublicKeyLogin)policy.Logins.Single()).Proof);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "Login refused: publickey alice");
    }

    [TestMethod]
    public async Task AnswerLoginRequest_SignedRequestAnsweredKeyAcceptable_IsRefused()
    {
        var policy = new SshTestAuthenticationPolicy { PublicKeyOutcome = SshLoginOutcome.KeyAcceptable };
        var (client, serving) = await OpenAsync(policy);

        client.Send(SignedRequest(client, "ecdsa-sha2-nistp256"));

        CollectionAssert.AreEqual(Failure, await client.ReceiveAsync());
        await CloseAsync(client, serving);
    }

    [TestMethod]
    public async Task AnswerLoginRequest_QueryAnsweredAccepted_IsRefused()
    {
        var policy = new SshTestAuthenticationPolicy { PublicKeyOutcome = SshLoginOutcome.Accepted };
        var (client, serving) = await OpenAsync(policy);

        client.Send(LoginRequest("publickey", [0], String("rsa-sha2-256"), Str(UserKeyBlob("rsa-sha2-256"))));

        CollectionAssert.AreEqual(Failure, await client.ReceiveAsync());
        await CloseAsync(client, serving);
    }

    [TestMethod]
    [DataRow("ssh-ed25519", "rsa-sha2-256", DisplayName = "An algorithm not verified yet")]
    [DataRow("ssh-rsa", "rsa-sha2-256", DisplayName = "SHA-1 RSA, without --allow-weak-ssh-algorithms")]
    [DataRow("ecdsa-sha2-nistp384", "ecdsa-sha2-nistp256", DisplayName = "A key of another type than the algorithm's")]
    public async Task AnswerLoginRequest_AlgorithmTheKeyCannotSignWith_IsRefusedUnchecked(string algorithm, string keyAlgorithm)
    {
        var policy = new SshTestAuthenticationPolicy { AuthorizedKeyBlob = UserKeyBlob(keyAlgorithm) };
        var (client, serving) = await OpenAsync(policy);

        client.Send(LoginRequest("publickey", [0], String(algorithm), Str(UserKeyBlob(keyAlgorithm))));

        CollectionAssert.AreEqual(Failure, await client.ReceiveAsync());
        await CloseAsync(client, serving);
        Assert.IsEmpty(policy.Logins);
    }

    [TestMethod]
    [DataRow("password")]
    [DataRow("keyboard-interactive")]
    [DataRow("publickey-query")]
    [DataRow("publickey-signed")]
    [DataRow("none")]
    public async Task AnswerLoginRequest_PolicyRefusingEverything_AcceptsNoLogin(string method)
    {
        var policy = new SshTestAuthenticationPolicy { AuthorizedKeyBlob = UserKeyBlob("ecdsa-sha2-nistp256"), RefusesEverything = true };
        var (client, serving) = await OpenAsync(policy);

        switch (method)
        {
            case "password":
                client.Send(LoginRequest("password", [0], String("secret")));
                break;
            case "keyboard-interactive":
                client.Send(LoginRequest("keyboard-interactive", String(string.Empty), String(string.Empty)));
                await client.ReceiveAsync();
                client.Send(Concat([61], UInt32(1), String("secret")));
                break;
            case "publickey-query":
                client.Send(LoginRequest("publickey", [0], String("ecdsa-sha2-nistp256"), Str(UserKeyBlob("ecdsa-sha2-nistp256"))));
                break;
            case "publickey-signed":
                client.Send(SignedRequest(client, "ecdsa-sha2-nistp256"));
                break;
            default:
                client.Send(LoginRequest("none"));
                break;
        }

        CollectionAssert.AreEqual(Failure, await client.ReceiveAsync());
        await CloseAsync(client, serving);
        Assert.IsFalse(client.Log.Notes.Any(note => note.StartsWith("Login accepted", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task AnswerLoginRequest_SixthRefusal_IsFailureThenDisconnect14()
    {
        var (client, serving) = await OpenAsync(new SshTestAuthenticationPolicy());

        await AssertRefusedSixTimesAsync(client, serving, LoginRequest("password", [0], String("wrong")));
    }

    [TestMethod]
    public async Task AnswerLoginRequest_MethodNotOffered_IsRefusedAndCounted()
    {
        var (client, serving) = await OpenAsync(new SshTestAuthenticationPolicy());

        await AssertRefusedSixTimesAsync(client, serving, LoginRequest("hostbased"));
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "SSH login request: hostbased for alice");
    }

    [TestMethod]
    public async Task AnswerLoginRequest_ServiceOtherThanSshConnection_IsDisconnect7()
    {
        var (client, serving) = await OpenAsync(new SshTestAuthenticationPolicy());

        client.Send(Concat([50], String("alice"), String("ssh-userauth"), String("none")));

        await AssertDisconnectAsync(client, serving, 7, "Service not available");
    }

    [TestMethod]
    [DataRow("bob", "ssh-connection")]
    [DataRow("alice", "ssh-other")]
    public async Task AnswerLoginRequest_ChangedUserOrService_IsDisconnect2(string user, string service)
    {
        var (client, serving) = await OpenAsync(new SshTestAuthenticationPolicy());
        client.Send(LoginRequest("none"));
        await client.ReceiveAsync();

        client.Send(Concat([50], String(user), String(service), String("none")));

        await AssertDisconnectAsync(client, serving, 2, "Protocol error");
    }

    [TestMethod]
    public async Task ConnectionMessage_BeforeTheLogin_IsDisconnect2()
    {
        var (client, serving) = await OpenAsync(new SshTestAuthenticationPolicy());

        client.Send(Concat([90], String("session"), UInt32(0), UInt32(1024), UInt32(1024)));

        await AssertDisconnectAsync(client, serving, 2, "Protocol error");
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "The client sent SSH message 90 before it logged in.");
    }

    [TestMethod]
    public async Task Login_Succeeded_StopsTheHeadTimeout()
    {
        var clock = new ManualTimeProvider();
        var (client, serving) = await OpenAsync(new AnonymousAuthenticationPolicy(), clock: clock);
        client.Send(LoginRequest("none"));
        await client.ReceiveAsync();

        clock.Advance(ExchangeLimits.Default.HeadTimeout * 2);
        client.Send([200]);

        CollectionAssert.AreEqual(Concat([3], UInt32(2)), await client.ReceiveAsync());
        await CloseAsync(client, serving);
    }

    [TestMethod]
    public async Task FirstNewKeys_ClientListingExtInfoC_IsFollowedByServerSigAlgs()
    {
        var client = new SshTestTransportClient("aes128-ctr", "hmac-sha2-256", TestContext.CancellationToken, extensionInfo: true);
        var serving = await client.OpenAsync(Server(), TimeProvider.System);

        var extensionInfo = await client.ReceiveAsync();

        CollectionAssert.AreEqual(
            Concat([7], UInt32(1), String("server-sig-algs"), String("ecdsa-sha2-nistp256,ecdsa-sha2-nistp384,ecdsa-sha2-nistp521,rsa-sha2-512,rsa-sha2-256")),
            extensionInfo);
        await client.ReExchangeAsync();
        client.Send([200]);
        CollectionAssert.AreEqual(Concat([3], UInt32(0)), await client.ReceiveAsync(), "A re-exchange sends no second EXT_INFO.");
        await CloseAsync(client, serving);
    }

    private static byte[] LoginRequest(string method, params byte[][] fields) =>
        Concat([.. new[] { new byte[] { 50 }, String("alice"), String("ssh-connection"), String(method) }, .. fields]);

    private static byte[] UserKeyBlob(string algorithm) => algorithm switch
    {
        "rsa-sha2-256" or "rsa-sha2-512" or "ssh-rsa" => SshHostKey.FromRsa(SshTestKeys.Rsa1024).PublicKeyBlob.ToArray(),
        "ecdsa-sha2-nistp384" => SshHostKey.FromEcdsa(SshTestKeys.EcdsaP384).PublicKeyBlob.ToArray(),
        "ecdsa-sha2-nistp521" => SshHostKey.FromEcdsa(SshTestKeys.EcdsaP521).PublicKeyBlob.ToArray(),
        _ => SshHostKey.FromEcdsa(SshTestKeys.EcdsaP256).PublicKeyBlob.ToArray(),
    };

    // RFC 4252 section 7: the signature covers string session identifier, then the request up to it.
    private static byte[] SignedRequest(SshTestTransportClient client, string algorithm, bool tamper = false)
    {
        var request = LoginRequest("publickey", [1], String(algorithm), Str(UserKeyBlob(algorithm)));
        var signature = Sign(algorithm, Concat(Str(client.SessionIdentifier), request));
        if (tamper)
        {
            signature[^1] ^= 1;
        }

        return Concat(request, Str(Concat(String(algorithm), Str(signature))));
    }

    private static byte[] Sign(string algorithm, byte[] data)
    {
        switch (algorithm)
        {
            case "rsa-sha2-256":
                return SshTestKeys.Rsa1024.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            case "rsa-sha2-512":
                return SshTestKeys.Rsa1024.SignData(data, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
        }

        var (key, hash) = algorithm switch
        {
            "ecdsa-sha2-nistp384" => (SshTestKeys.EcdsaP384, HashAlgorithmName.SHA384),
            "ecdsa-sha2-nistp521" => (SshTestKeys.EcdsaP521, HashAlgorithmName.SHA512),
            _ => (SshTestKeys.EcdsaP256, HashAlgorithmName.SHA256),
        };
        var fixedFields = key.SignData(data, hash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var half = fixedFields.Length / 2;

        return Concat(
            Mpint(new BigInteger(fixedFields.AsSpan(0, half), isUnsigned: true, isBigEndian: true)),
            Mpint(new BigInteger(fixedFields.AsSpan(half), isUnsigned: true, isBigEndian: true)));
    }

    private static async Task CloseAsync(SshTestTransportClient client, Task serving)
    {
        client.Connection.CloseClientWrites();
        await serving;
    }

    private static async Task AssertDisconnectAsync(SshTestTransportClient client, Task serving, uint reason, string description)
    {
        CollectionAssert.AreEqual(Concat([1], UInt32(reason), String(description), String(string.Empty)), await client.ReceiveAsync());
        await serving;
        CollectionAssert.Contains(client.Log.Notes.ToArray(), $"SSH disconnect sent: {reason} {description}");
    }

    private static async Task AssertRefusedSixTimesAsync(SshTestTransportClient client, Task serving, byte[] request)
    {
        for (var refusal = 0; refusal < SshUserAuthentication.MaxRefusals; refusal++)
        {
            client.Send(request);
            CollectionAssert.AreEqual(Failure, await client.ReceiveAsync());
        }

        await AssertDisconnectAsync(client, serving, 14, "Too many authentication failures");
    }

    private async Task<(SshTestTransportClient Client, Task Serving)> OpenAsync(
        ISshAuthenticationPolicy policy,
        bool requestService = true,
        TimeProvider? clock = null)
    {
        var client = new SshTestTransportClient("aes128-ctr", "hmac-sha2-256", TestContext.CancellationToken);
        var serving = await client.OpenAsync(Server(policy: policy), clock ?? TimeProvider.System);
        if (requestService)
        {
            client.Send(Concat([5], String("ssh-userauth")));
            CollectionAssert.AreEqual(Concat([6], String("ssh-userauth")), await client.ReceiveAsync());
        }

        return (client, serving);
    }
}
