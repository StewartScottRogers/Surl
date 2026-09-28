using Surl.Protocol.Abstractions;

namespace Surl.Console;

[TestClass]
public sealed class ProgramTests
{
    [TestMethod]
    [DoNotParallelize]
    public async Task Main_AnyCommandLine_WritesNotImplementedAndReturnsFailedInit()
    {
        var originalError = System.Console.Error;
        using var error = new StringWriter();
        System.Console.SetError(error);

        int exitCode;
        try
        {
            exitCode = await Program.Main(["http://127.0.0.1:8080/"]);
        }
        finally
        {
            System.Console.SetError(originalError);
        }

        Assert.AreEqual((int)SurlExitCode.FailedInit, exitCode);
        Assert.AreEqual("surl: not implemented yet" + Environment.NewLine, error.ToString());
    }

    [TestMethod]
    public void Main_NullArguments_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = Program.Main(null!); });
    }
}
