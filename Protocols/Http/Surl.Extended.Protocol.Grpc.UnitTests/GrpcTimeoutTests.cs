namespace Surl.Extended.Protocol.Grpc;

[TestClass]
public sealed class GrpcTimeoutTests
{
    [TestMethod]
    [DataRow("2H", 2L * 60 * 60 * 10_000_000)]
    [DataRow("3M", 3L * 60 * 10_000_000)]
    [DataRow("5S", 5L * 10_000_000)]
    [DataRow("250m", 250L * 10_000)]
    [DataRow("7u", 70L)]
    [DataRow("300n", 3L)]
    [DataRow("1n", 1L)]
    [DataRow("150n", 2L)]
    [DataRow("0S", 0L)]
    [DataRow("00000000m", 0L)]
    [DataRow("99999999H", 99999999L * 60 * 60 * 10_000_000)]
    public void Read_EachUnit_IsReadAtItsScale(string value, long expectedTicks)
    {
        var timeout = GrpcTimeout.Read([value]);

        Assert.AreEqual(TimeSpan.FromTicks(expectedTicks), timeout);
    }

    [TestMethod]
    public void Read_NoField_ReturnsNullForAnInfiniteDeadline()
    {
        Assert.IsNull(GrpcTimeout.Read([]));
    }

    [TestMethod]
    [DataRow("123456789S", DisplayName = "9 digits")]
    [DataRow("10", DisplayName = "no unit")]
    [DataRow("10SS", DisplayName = "two units")]
    [DataRow("-1S", DisplayName = "minus sign")]
    [DataRow("+1S", DisplayName = "plus sign")]
    [DataRow(" 1S", DisplayName = "leading space")]
    [DataRow("1 S", DisplayName = "inner space")]
    [DataRow("1S ", DisplayName = "trailing space")]
    [DataRow("1s", DisplayName = "lower-case seconds")]
    [DataRow("1h", DisplayName = "lower-case hours")]
    [DataRow("1x", DisplayName = "unknown unit")]
    [DataRow("S", DisplayName = "no digits")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("１S", DisplayName = "non-ASCII digit")]
    public void Read_MalformedValue_ThrowsInternalMalformedGrpcTimeout(string value)
    {
        var thrown = Assert.ThrowsExactly<GrpcCallException>(() => GrpcTimeout.Read([value]));

        Assert.AreEqual(GrpcStatusCode.Internal, thrown.Status.Code);
        Assert.AreEqual("malformed grpc-timeout", thrown.Status.Message);
    }

    [TestMethod]
    public void Read_TwoFields_ThrowsInternalMalformedGrpcTimeout()
    {
        var thrown = Assert.ThrowsExactly<GrpcCallException>(() => GrpcTimeout.Read(["1S", "1S"]));

        Assert.AreEqual(GrpcStatus.MalformedGrpcTimeout, thrown.Status);
    }

    [TestMethod]
    public void Read_Null_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => GrpcTimeout.Read(null!));
    }
}
