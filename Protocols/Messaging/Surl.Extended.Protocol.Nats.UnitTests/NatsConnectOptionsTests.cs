using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats.UnitTests;

[TestClass]
public sealed class NatsConnectOptionsTests
{
    [TestMethod]
    public void Parse_EmptyObject_IsTheDefaults()
    {
        var options = NatsConnectOptions.Parse("{}");

        Assert.IsNotNull(options);
        Assert.IsFalse(options.Verbose);
        Assert.IsFalse(options.Pedantic);
        Assert.IsFalse(options.TlsRequired);
        Assert.IsTrue(options.Echo);
        Assert.IsFalse(options.Headers);
        Assert.IsFalse(options.NoResponders);
        Assert.AreEqual(0, options.Protocol);
        Assert.IsFalse(options.UsesKeys);
        Assert.IsNull(options.Name);
    }

    [TestMethod]
    public void Parse_EveryField_ReadsEach()
    {
        var options = NatsConnectOptions.Parse(
            """{"verbose":true,"pedantic":true,"tls_required":true,"echo":false,"headers":true,"no_responders":true,"name":"n","lang":"go","version":"1.2","user":"u","pass":"p","auth_token":"t","nkey":"k","sig":"s","jwt":"j","protocol":1}""");

        Assert.IsNotNull(options);
        Assert.IsTrue(options.Verbose && options.Pedantic && options.TlsRequired && options.Headers && options.NoResponders);
        Assert.IsFalse(options.Echo);
        Assert.AreEqual("n|go|1.2|u|p|t|k|s|j", string.Join('|', options.Name, options.Lang, options.Version, options.User, options.Pass, options.AuthToken, options.NKey, options.Signature, options.Jwt));
        Assert.AreEqual(1, options.Protocol);
    }

    [TestMethod]
    public void Parse_FieldNamesInAnotherCase_MatchCaseInsensitively()
    {
        var options = NatsConnectOptions.Parse("""{"VERBOSE":true,"User":"alice","Protocol":1}""");

        Assert.IsNotNull(options);
        Assert.IsTrue(options.Verbose);
        Assert.AreEqual("alice", options.User);
        Assert.AreEqual(1, options.Protocol);
    }

    [TestMethod]
    public void Parse_UnknownFieldsAndNulls_AreIgnored()
    {
        var options = NatsConnectOptions.Parse(""" {"x":{"y":[1,2]},"z":3,"verbose":null,"user":null} """);

        Assert.IsNotNull(options);
        Assert.IsFalse(options.Verbose);
        Assert.IsNull(options.User);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("[]")]
    [DataRow("verbose")]
    [DataRow("{\"verbose\":true")]
    [DataRow("{} {}")]
    [DataRow("{\"verbose\":\"true\"}")]
    [DataRow("{\"user\":1}")]
    [DataRow("{\"protocol\":\"1\"}")]
    [DataRow("{\"protocol\":1.5}")]
    public void Parse_NotAnObjectOrAFieldOfTheWrongType_IsNull(string json)
    {
        Assert.IsNull(NatsConnectOptions.Parse(json));
    }

    [TestMethod]
    [DataRow("""{"nkey":"k"}""")]
    [DataRow("""{"sig":"s"}""")]
    [DataRow("""{"jwt":"j"}""")]
    public void UsesKeys_NKeySignatureOrJwt_IsTrue(string json)
    {
        Assert.IsTrue(NatsConnectOptions.Parse(json)!.UsesKeys);
    }

    [TestMethod]
    [DataRow("""{"user":"alice","pass":"secret","auth_token":"t"}""", "alice", "secret")]
    [DataRow("""{"user":"alice"}""", "alice", "(none)")]
    [DataRow("""{"auth_token":"s3cret"}""", "", "s3cret")]
    [DataRow("{}", null, "(none)")]
    public void ToPasswordLogin_Credentials_IsTheLoginTheAdrNames(string json, string? userName, string password)
    {
        var login = NatsConnectOptions.Parse(json)!.ToPasswordLogin("nats", null);

        Assert.AreEqual("nats", login.Scheme);
        Assert.AreEqual(userName, login.UserName);
        Assert.AreEqual(password, UnitTestNatsAuthenticationPolicy.PasswordOf(login));
        Assert.IsNull(login.TlsSession);
    }

    [TestMethod]
    public void ToPasswordLogin_TlsSession_IsPassedOn()
    {
        var tls = InMemoryConnection.DefaultUpgradeTlsSession;

        var login = NatsConnectOptions.Parse("{}")!.ToPasswordLogin("natss", tls);

        Assert.AreSame(tls, login.TlsSession);
    }
}
