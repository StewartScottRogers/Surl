namespace Surl.Protocol.Http;

[TestClass]
public sealed class Http2FrameMalformationsTests
{
    [TestMethod]
    public void ErrorCodeOf_EveryMalformation_IsAnErrorCodeRfc9113Defines()
    {
        foreach (var malformation in Enum.GetValues<Http2FrameMalformation>())
        {
            Assert.IsTrue(Enum.IsDefined(Http2FrameMalformations.ErrorCodeOf(malformation)), malformation.ToString());
        }
    }

    [TestMethod]
    public void IsConnectionError_OnlyTheMalformationsThatTouchOneFullyReadStream_AreStreamErrors()
    {
        var streamErrors = Enum.GetValues<Http2FrameMalformation>().Where(malformation => !Http2FrameMalformations.IsConnectionError(malformation));

        CollectionAssert.AreEquivalent(
            new[]
            {
                Http2FrameMalformation.PriorityLengthNotFive,
                Http2FrameMalformation.PriorityDependsOnItself,
                Http2FrameMalformation.WindowUpdateIncrementZeroOnStream,
            },
            streamErrors.ToArray());
    }
}
