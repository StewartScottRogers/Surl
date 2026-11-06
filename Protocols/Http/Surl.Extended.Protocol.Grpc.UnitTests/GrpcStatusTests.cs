namespace Surl.Extended.Protocol.Grpc;

[TestClass]
public sealed class GrpcStatusTests
{
    [TestMethod]
    [DataRow(GrpcStatusCode.Ok, "0")]
    [DataRow(GrpcStatusCode.ResourceExhausted, "8")]
    [DataRow(GrpcStatusCode.Unauthenticated, "16")]
    public void StatusValue_Code_IsItsDecimalNumber(GrpcStatusCode code, string expected)
    {
        Assert.AreEqual(expected, new GrpcStatus(code, string.Empty).StatusValue);
    }

    [TestMethod]
    public void StatusCodes_AreTheSeventeenOfStatuscodes()
    {
        var values = Enum.GetValues<GrpcStatusCode>().Select(code => (int)code).ToArray();

        CollectionAssert.AreEqual(Enumerable.Range(0, 17).ToArray(), values);
    }

    [TestMethod]
    public void MessageValue_TextWithPercentAndNonAscii_IsPercentEncoded()
    {
        var status = new GrpcStatus(GrpcStatusCode.Internal, "50% é");

        Assert.AreEqual("50%25 %C3%A9", status.MessageValue);
    }

    [TestMethod]
    public void FromException_GrpcCallException_ReturnsItsStatus()
    {
        var status = GrpcStatus.FromException(new GrpcCallException(GrpcStatus.MalformedGrpcTimeout));

        Assert.AreEqual(GrpcStatus.MalformedGrpcTimeout, status);
    }

    [TestMethod]
    public void FromException_ProtobufFormatException_ReturnsInternalRequestMessageDidNotParse()
    {
        var status = GrpcStatus.FromException(new ProtobufFormatException("bad"));

        Assert.AreEqual(GrpcStatusCode.Internal, status.Code);
        Assert.AreEqual("request message did not parse", status.Message);
    }

    [TestMethod]
    public void FromException_AnyOtherException_ReturnsUnknownServerError()
    {
        var status = GrpcStatus.FromException(new InvalidOperationException("boom"));

        Assert.AreEqual(GrpcStatusCode.Unknown, status.Code);
        Assert.AreEqual("server error", status.Message);
    }

    [TestMethod]
    public void LibraryStatuses_AreTheCodesAndTextsADR0116Names()
    {
        Assert.AreEqual(new GrpcStatus(GrpcStatusCode.Ok, string.Empty), GrpcStatus.Ok);
        Assert.AreEqual(new GrpcStatus(GrpcStatusCode.ResourceExhausted, "call larger than the limit"), GrpcStatus.CallLargerThanTheLimit);
        Assert.AreEqual(new GrpcStatus(GrpcStatusCode.Internal, "message did not decompress"), GrpcStatus.MessageDidNotDecompress);
        Assert.AreEqual(new GrpcStatus(GrpcStatusCode.Unimplemented, "grpc-encoding not served"), GrpcStatus.GrpcEncodingNotServed);
        Assert.AreEqual(new GrpcStatus(GrpcStatusCode.Internal, "compressed flag set without grpc-encoding"), GrpcStatus.CompressedFlagSetWithoutGrpcEncoding);
    }

    [TestMethod]
    public void GrpcCallException_WithInnerException_KeepsStatusMessageAndCause()
    {
        var cause = new InvalidDataException();

        var thrown = new GrpcCallException(GrpcStatus.MessageDidNotDecompress, cause);

        Assert.AreEqual(GrpcStatus.MessageDidNotDecompress, thrown.Status);
        Assert.AreEqual("message did not decompress", thrown.Message);
        Assert.AreSame(cause, thrown.InnerException);
    }
}
