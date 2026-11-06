namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Reading and writing Token scopes (Token "Requesting a Token"; ADR-0115, decisions 4.1 and 4.3).
/// </summary>
[TestClass]
public sealed class OciTokenScopeTests
{
    [TestMethod]
    [DataRow("repository:library/alpine:pull", "library/alpine", (int)(OciActions.Pull))]
    [DataRow("repository:app:pull,push", "app", (int)(OciActions.Pull | OciActions.Push))]
    [DataRow("repository:app:delete", "app", (int)(OciActions.Delete))]
    [DataRow("repository:app:*", "app", (int)(OciActions.Pull | OciActions.Push | OciActions.Delete))]
    [DataRow("repository:app:pull,fly", "app", (int)(OciActions.Pull))]
    public void Parse_RepositoryScope_IsItsNameAndKnownActions(string scope, string name, int actions)
    {
        var parsed = OciTokenScope.Parse(scope);

        Assert.AreEqual((name, (OciActions)actions), parsed);
    }

    [TestMethod]
    [DataRow("registry:catalog:*")]
    [DataRow("repository:App:pull")]
    [DataRow("repository:app")]
    [DataRow("repository:app:pull:x")]
    [DataRow("repository:app:fly")]
    [DataRow("")]
    public void Parse_AnythingElse_IsNull(string scope)
    {
        var parsed = OciTokenScope.Parse(scope);

        Assert.IsNull(parsed);
    }

    [TestMethod]
    [DataRow((int)(OciActions.Pull), "repository:app:pull")]
    [DataRow((int)(OciActions.Push), "repository:app:pull,push")]
    [DataRow((int)(OciActions.Delete), "repository:app:delete")]
    public void Format_RequiredAction_IsTheScopeAChallengeNames(int required, string expected)
    {
        var scope = OciTokenScope.Format("app", (OciActions)required);

        Assert.AreEqual(expected, scope);
    }
}
