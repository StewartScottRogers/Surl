namespace Surl.Cli;

[TestClass]
public sealed class FaultReplyShapesTests
{
    [TestMethod]
    public void HasShape_EveryOriginalAndExtendedScheme_HasAnErrorShape()
    {
        string[] schemes = [.. SchemeDefaultPorts.Schemes, .. ExtendedSchemeDefaultPorts.Schemes, "http-proxy", "https-proxy"];

        var withoutShape = schemes.Where(scheme => !FaultReplyShapes.HasShape(scheme)).ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), withoutShape);
    }

    [TestMethod]
    public void HasShape_SchemeNoServerClaims_HasNone() => Assert.IsFalse(FaultReplyShapes.HasShape("nonesuch"));
}
