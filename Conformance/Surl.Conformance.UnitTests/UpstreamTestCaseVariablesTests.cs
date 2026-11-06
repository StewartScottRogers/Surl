namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseVariablesTests
{
    [TestMethod]
    public void Names_IncludeEveryFileFormatVariable()
    {
        string[] fileFormat =
        [
            "CLIENT6IP", "CLIENT6IP-NB", "CLIENTIP", "CURL", "DATE", "DEV_NULL", "FILE_PWD", "FTP6PORT",
            "FTPPORT", "FTPSPORT", "FTPTIME2", "GOPHER6PORT", "GOPHERPORT", "GOPHERSPORT", "HOST6IP",
            "HOSTIP", "HTTP2PORT", "HTTP6PORT", "HTTPPORT", "HTTPSPORT", "HTTPSPROXYPORT", "HTTPTLS6PORT",
            "HTTPTLSPORT", "HTTPUNIXPATH", "IMAP6PORT", "IMAPPORT", "LOGDIR", "MQTTPORT", "MQTTSPORT",
            "NOLISTENPORT", "POP36PORT", "POP3PORT", "PROXYPORT", "PWD", "RESOLVE", "RTSP6PORT", "RTSPPORT",
            "SMBPORT", "SMBSPORT", "SMTP6PORT", "SMTPPORT", "SOCKSPORT", "SOCKSUNIXPATH", "SRCDIR", "SCP_PWD",
            "SFTP_PWD", "SSHKEYALGO", "SSHPORT", "SSHSRVMD5", "SSHSRVSHA256", "TELNETPORT", "TESTNUMBER",
            "TFTP6PORT", "TFTPPORT", "USER", "VERNUM", "VERSION",
        ];

        var missing = fileFormat.Except(UpstreamTestCaseVariables.Names).ToArray();

        Assert.IsEmpty(missing, string.Join(", ", missing));
    }

    [TestMethod]
    public void Substitute_LongerNameFirstInUpstreamOrder_IsNotEatenByItsSuffix()
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HTTPSPROXYPORT"] = "1",
            ["PROXYPORT"] = "2",
        };

        var text = UpstreamTestCaseVariables.Substitute("%HTTPSPROXYPORT %PROXYPORT", variables);

        Assert.AreEqual("1 2", text);
    }

    [TestMethod]
    public void RequireKnownNames_UnknownNames_AreNamedInOrdinalOrder()
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal) { ["ZZ"] = "", ["AA"] = "", ["PWD"] = "" };

        var exception = Assert.ThrowsExactly<ArgumentException>(() => UpstreamTestCaseVariables.RequireKnownNames(variables));

        StringAssert.Contains(exception.Message, "AA, ZZ");
    }
}
