using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// ADR-0088 decision 2's table: the status and log line for each way an origin cannot be reached.
/// </summary>
[TestClass]
public sealed class HttpProxyStatusTests
{
    [TestMethod]
    [DataRow(OriginConnectionFailure.NotAllowed, 403, "Origin o.test:25 is not an allowed origin; answered 403.")]
    [DataRow(OriginConnectionFailure.HostNotFound, 502, "Origin o.test did not resolve; answered 502.")]
    [DataRow(OriginConnectionFailure.Refused, 502, "Origin o.test:25 could not be reached (Refused); answered 502.")]
    [DataRow(OriginConnectionFailure.Unreachable, 502, "Origin o.test:25 could not be reached (Unreachable); answered 502.")]
    [DataRow(OriginConnectionFailure.TimedOut, 504, "Origin o.test:25 did not accept in time; answered 504.")]
    public void ForOriginFailure_Failure_IsTheAdrsStatusAndLogLine(OriginConnectionFailure failure, int code, string logLine)
    {
        var (status, line) = HttpProxyStatus.ForOriginFailure(failure, "o.test", 25);

        Assert.AreEqual(code, status.Code);
        Assert.AreEqual(logLine, line);
    }

    [TestMethod]
    public void ForOriginFailure_UnknownFailure_IsAnsweredAsUnreachable()
    {
        var (status, line) = HttpProxyStatus.ForOriginFailure((OriginConnectionFailure)99, "o.test", 25);

        Assert.AreEqual(502, status.Code);
        Assert.AreEqual("Origin o.test:25 could not be reached (99); answered 502.", line);
    }
}
