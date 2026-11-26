namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class HttpWithheldHeaderNamesTests
{
    [TestMethod]
    public void Credentials_AreAuthorizationAndProxyAuthorization()
    {
        CollectionAssert.AreEqual(
            new[] { "Authorization", "Proxy-Authorization" },
            HttpWithheldHeaderNames.Credentials.ToArray());
    }

    [TestMethod]
    public void ApiKeysAndSessionTokens_AreTheFiveFieldsWithheldWhole()
    {
        CollectionAssert.AreEqual(
            new[] { "X-Api-Key", "Api-Key", "X-Goog-Api-Key", "X-Amz-Security-Token", "Ocp-Apim-Subscription-Key" },
            HttpWithheldHeaderNames.ApiKeysAndSessionTokens.ToArray());
    }

    [TestMethod]
    public void All_IsTheCredentialsThenTheApiKeysAndSessionTokens()
    {
        CollectionAssert.AreEqual(
            HttpWithheldHeaderNames.Credentials.Concat(HttpWithheldHeaderNames.ApiKeysAndSessionTokens).ToArray(),
            HttpWithheldHeaderNames.All.ToArray());
    }
}
