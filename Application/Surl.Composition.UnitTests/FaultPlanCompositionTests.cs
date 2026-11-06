using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class FaultPlanCompositionTests
{
    [TestMethod]
    public void ToRule_EveryKey_IsCarriedOverWithAnEmptyIdentifier()
    {
        var fault = new ConfiguredFault(FaultKind.Drop)
        {
            Listener = "smtp://127.0.0.1:0",
            Scheme = "smtp",
            Account = "alice",
            Path = "/p",
            Command = "RCPT",
            Stage = FaultStage.Command,
            DelayMilliseconds = 5,
            AfterBytes = 10,
            Reply = "451 later",
            Times = 3,
        };

        var rule = FaultPlanComposition.ToRule(fault);

        Assert.AreEqual(
            new FaultRule(string.Empty, FaultKind.Drop)
            {
                Listener = "smtp://127.0.0.1:0",
                Scheme = "smtp",
                Account = "alice",
                Path = "/p",
                Command = "RCPT",
                Stage = FaultStage.Command,
                DelayMilliseconds = 5,
                AfterBytes = 10,
                Reply = "451 later",
                Times = 3,
            },
            rule);
    }

    [TestMethod]
    public void Compose_NoFault_GivesANewEmptyPlanEachTime()
    {
        var first = FaultPlanComposition.Compose([]);
        var second = FaultPlanComposition.Compose([]);

        Assert.IsEmpty(first.Rules);
        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    public void Compose_Faults_AddsThemInOrderAsF1AndF2()
    {
        var plan = FaultPlanComposition.Compose(
            [new ConfiguredFault(FaultKind.RefuseLogin) { Account = "a" }, new ConfiguredFault(FaultKind.OutOfSpace) { Path = "/u" }]);

        CollectionAssert.AreEqual(new[] { "f1", "f2" }, plan.Rules.Select(rule => rule.Id).ToArray());
        CollectionAssert.AreEqual(new[] { FaultKind.RefuseLogin, FaultKind.OutOfSpace }, plan.Rules.Select(rule => rule.Kind).ToArray());
    }

    [TestMethod]
    public void WriteWarning_OneRule_WritesTheAdrsLine()
    {
        using var log = new StringWriter();

        FaultPlanComposition.WriteWarning(
            new SurlCommandLine { Faults = [new ConfiguredFault(FaultKind.RefuseLogin)] }, log);

        Assert.AreEqual(
            $"surl: warning: --fault: 1 fault rule(s) make this server misbehave on purpose{Environment.NewLine}", log.ToString());
    }

    [TestMethod]
    public void WriteWarning_NoRule_WritesNothing()
    {
        using var log = new StringWriter();

        FaultPlanComposition.WriteWarning(new SurlCommandLine(), log);

        Assert.AreEqual(string.Empty, log.ToString());
    }

    [TestMethod]
    public void NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => FaultPlanComposition.Compose(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => FaultPlanComposition.ToRule(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => FaultPlanComposition.WriteWarning(null!, TextWriter.Null));
        Assert.ThrowsExactly<ArgumentNullException>(() => FaultPlanComposition.WriteWarning(new SurlCommandLine(), null!));
    }
}
