namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

[TestClass]
public sealed class Mqtt5PropertyDefinitionTests
{
    [TestMethod]
    public void All_Table2_4_HasARowForEveryIdentifier()
    {
        var identifiers = Mqtt5PropertyDefinition.All.Select(definition => definition.Identifier).Order().ToArray();

        CollectionAssert.AreEqual(Enum.GetValues<Mqtt5PropertyIdentifier>().Order().ToArray(), identifiers);
    }

    [TestMethod]
    public void TryFind_UndefinedIdentifier_FindsNothing()
    {
        Assert.IsFalse(Mqtt5PropertyDefinition.TryFind(0x04, out var definition));
        Assert.IsNull(definition);
    }

    [TestMethod]
    public void IsAllowedIn_WillDelayInterval_OnlyInTheWill()
    {
        var definition = Mqtt5PropertyDefinition.Of(Mqtt5PropertyIdentifier.WillDelayInterval);

        Assert.IsTrue(definition.IsAllowedIn(Mqtt5PropertyContext.Will));
        Assert.IsFalse(definition.IsAllowedIn(Mqtt5PropertyContext.Connect));
    }

    [TestMethod]
    public void IsRepeatableIn_UserProperty_EverywhereItIsAllowed()
    {
        var definition = Mqtt5PropertyDefinition.Of(Mqtt5PropertyIdentifier.UserProperty);

        Assert.AreEqual(definition.AllowedIn, definition.RepeatableIn);
    }
}
