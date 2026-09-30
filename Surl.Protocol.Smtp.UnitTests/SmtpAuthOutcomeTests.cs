using Surl.LineProtocol;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smtp;

// Every enum value, so the maps' fall-through arms are pinned too, including the ones a session
// never reaches (a Challenge never ends an exchange; ResponseRead always carries a response).
[TestClass]
public sealed class SmtpAuthOutcomeTests
{
    [TestMethod]
    [DataRow(MailLoginOutcome.Challenge, "535 5.7.8 Authentication credentials invalid")]
    [DataRow(MailLoginOutcome.Accepted, "235 2.7.0 Authentication successful")]
    [DataRow(MailLoginOutcome.AcceptedUnchecked, "235 2.7.0 Authentication successful")]
    [DataRow(MailLoginOutcome.RefusedCredentials, "535 5.7.8 Authentication credentials invalid")]
    [DataRow(MailLoginOutcome.RefusedPlaintext, "538 5.7.11 Encryption required for requested authentication mechanism")]
    [DataRow(MailLoginOutcome.RefusedMechanism, "504 5.5.4 Unrecognized authentication type")]
    public void LoginEnded_EveryOutcome_AnswersItsReply(MailLoginOutcome outcome, string reply)
    {
        Assert.AreEqual(reply, SmtpReplies.LoginEnded(outcome));
    }

    [TestMethod]
    [DataRow(SaslContinuationOutcome.ResponseRead, null)]
    [DataRow(SaslContinuationOutcome.Cancelled, "501 5.7.0 Authentication cancelled")]
    [DataRow(SaslContinuationOutcome.NotBase64, "501 5.5.2 Cannot decode response")]
    [DataRow(SaslContinuationOutcome.Closed, null)]
    [DataRow(SaslContinuationOutcome.LineTooLong, null)]
    [DataRow(SaslContinuationOutcome.HeadTimedOut, null)]
    public void SaslExchangeAbandoned_EveryOutcome_Answers501OnlyForACancelOrAnUndecodableResponse(SaslContinuationOutcome outcome, string? reply)
    {
        Assert.AreEqual(reply, SmtpReplies.SaslExchangeAbandoned(outcome));
    }

    [TestMethod]
    [DataRow(SaslContinuationOutcome.ResponseRead, CrlfLineReadOutcome.Closed)]
    [DataRow(SaslContinuationOutcome.Cancelled, CrlfLineReadOutcome.Closed)]
    [DataRow(SaslContinuationOutcome.NotBase64, CrlfLineReadOutcome.Closed)]
    [DataRow(SaslContinuationOutcome.Closed, CrlfLineReadOutcome.Closed)]
    [DataRow(SaslContinuationOutcome.LineTooLong, CrlfLineReadOutcome.LineTooLong)]
    [DataRow(SaslContinuationOutcome.HeadTimedOut, CrlfLineReadOutcome.HeadTimedOut)]
    public void AsLineReadOutcome_EveryOutcome_KeepsTheLimitsAndMapsTheRestToClosed(SaslContinuationOutcome outcome, CrlfLineReadOutcome expected)
    {
        Assert.AreEqual(expected, SmtpSession.AsLineReadOutcome(outcome));
    }
}
