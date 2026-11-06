namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// draft-ietf-webtrans-http3-16 section 4.4's mapping: <c>first + n + floor(n / 0x1e)</c> with
/// <c>first</c> = <c>0x52e4a40fa8db</c>, skipping the reserved <c>0x1f * N + 0x21</c> codes.
/// </summary>
[TestClass]
public sealed class WebTransportApplicationErrorCodeTests
{
    [TestMethod]
    [DataRow(0u, 0x52e4a40fa8dbUL, DisplayName = "0 is the first code")]
    [DataRow(0x1du, 0x52e4a40fa8f8UL, DisplayName = "0x1d is the last before the first reserved code")]
    [DataRow(0x1eu, 0x52e4a40fa8faUL, DisplayName = "0x1e steps over the reserved 0x52e4a40fa8f9")]
    [DataRow(uint.MaxValue, 0x52e5ac983162UL, DisplayName = "uint.MaxValue is the last code")]
    public void ToHttp3ErrorCode_ApplicationCode_IsTheDraftsFormula(uint applicationCode, ulong expected)
    {
        Assert.AreEqual(expected, WebTransportApplicationErrorCode.ToHttp3ErrorCode(applicationCode));
    }

    [TestMethod]
    [DataRow(0u)]
    [DataRow(1u)]
    [DataRow(0x1du)]
    [DataRow(0x1eu)]
    [DataRow(0x1fu)]
    [DataRow(0x3cu)]
    [DataRow(0x12345678u)]
    [DataRow(uint.MaxValue)]
    public void TryFromHttp3ErrorCode_MappedCode_ReturnsTheApplicationCode(uint applicationCode)
    {
        var found = WebTransportApplicationErrorCode.TryFromHttp3ErrorCode(
            WebTransportApplicationErrorCode.ToHttp3ErrorCode(applicationCode), out var mappedBack);

        Assert.IsTrue(found);
        Assert.AreEqual(applicationCode, mappedBack);
    }

    [TestMethod]
    [DataRow(0x52e4a40fa8daUL, DisplayName = "one below the range")]
    [DataRow(0x52e5ac983163UL, DisplayName = "one above the range")]
    [DataRow(0x52e4a40fa8f9UL, DisplayName = "the first reserved code in the range")]
    [DataRow(0x10cUL, DisplayName = "H3_REQUEST_CANCELLED")]
    public void TryFromHttp3ErrorCode_CodeOutsideTheRangeOrReserved_ReturnsFalse(ulong http3Code)
    {
        var found = WebTransportApplicationErrorCode.TryFromHttp3ErrorCode(http3Code, out var applicationCode);

        Assert.IsFalse(found);
        Assert.AreEqual(0u, applicationCode);
    }

    [TestMethod]
    public void TryFromHttp3ErrorCode_EveryCodeInTheFirstThreeBlocks_IsReservedOrMapsBack()
    {
        var reserved = 0;

        for (var http3Code = WebTransportApplicationErrorCode.FirstHttp3ErrorCode; http3Code < WebTransportApplicationErrorCode.FirstHttp3ErrorCode + 93; http3Code++)
        {
            if (WebTransportApplicationErrorCode.TryFromHttp3ErrorCode(http3Code, out var applicationCode))
            {
                Assert.AreEqual(http3Code, WebTransportApplicationErrorCode.ToHttp3ErrorCode(applicationCode));
            }
            else
            {
                Assert.AreEqual(0UL, (http3Code - 0x21) % 0x1f);
                reserved++;
            }
        }

        Assert.AreEqual(3, reserved);
    }
}
