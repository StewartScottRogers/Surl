using System.Net;

namespace Surl.Dtls;

/// <summary>
/// <see cref="DtlsCookieSecrets"/>: a cookie binds the endpoint and the hello, and lives between 5
/// and 10 minutes as the secret is replaced (ADR-0087 decision 4).
/// </summary>
[TestClass]
public sealed class DtlsCookieSecretsTests
{
    private static readonly EndPoint Client = new IPEndPoint(IPAddress.Loopback, 50000);

    [TestMethod]
    public void IsValid_CookieForThisEndpointAndHello_IsTrue()
    {
        var secrets = new DtlsCookieSecrets(new ManualTimeProvider());
        var hello = Hello();

        Assert.IsTrue(secrets.IsValid(Client, hello with { Cookie = secrets.MakeCookie(Client, hello) }));
    }

    [TestMethod]
    public void IsValid_OtherPortOrChangedHello_IsFalse()
    {
        var secrets = new DtlsCookieSecrets(new ManualTimeProvider());
        var hello = Hello();
        var cookie = secrets.MakeCookie(Client, hello);

        Assert.IsFalse(secrets.IsValid(new IPEndPoint(IPAddress.Loopback, 50001), hello with { Cookie = cookie }));
        Assert.IsFalse(secrets.IsValid(Client, hello with { Cookie = cookie, CipherSuites = [0xC02F] }));
    }

    [TestMethod]
    public void IsValid_AfterOneReplacement_StillAcceptsThePreviousSecretsCookie()
    {
        var clock = new ManualTimeProvider();
        var made = 0;
        var secrets = new DtlsCookieSecrets(clock, () => [(byte)++made]);
        var hello = Hello();
        var cookie = secrets.MakeCookie(Client, hello);

        clock.Advance(TimeSpan.FromMinutes(6));
        var stillValid = secrets.IsValid(Client, hello with { Cookie = cookie });
        clock.Advance(TimeSpan.FromMinutes(5));
        var expired = secrets.IsValid(Client, hello with { Cookie = cookie });

        Assert.IsTrue(stillValid);
        Assert.IsFalse(expired);
        Assert.AreEqual(3, made);
    }

    [TestMethod]
    public void IsValid_AfterTwoLifetimesIdle_AcceptsNoOldCookie()
    {
        var clock = new ManualTimeProvider();
        var secrets = new DtlsCookieSecrets(clock);
        var hello = Hello();
        var cookie = secrets.MakeCookie(Client, hello);

        clock.Advance(TimeSpan.FromMinutes(10));

        Assert.IsFalse(secrets.IsValid(Client, hello with { Cookie = cookie }));
    }

    private static DtlsClientHello Hello() =>
        new(DtlsRecordHeader.Dtls12Version, new byte[32], [], [], [0xC02B], [0], []);
}
