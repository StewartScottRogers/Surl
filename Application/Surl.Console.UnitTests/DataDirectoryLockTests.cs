using Surl.Protocol.Abstractions;

namespace Surl.Console;

[TestClass]
public sealed class DataDirectoryLockTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Take_Twice_RefusesTheSecondWithDataDirectoryInUse()
    {
        var directory = Directory.CreateTempSubdirectory("surl-lock-");
        try
        {
            var first = DataDirectoryLock.Take(directory.FullName);
            using (first.Holder)
            {
                var second = DataDirectoryLock.Take(directory.FullName);

                Assert.IsNotNull(first.Holder);
                Assert.IsNull(second.Holder);
                Assert.AreEqual(SurlExitCode.DataDirectoryInUse, second.ExitCode);
                Assert.AreEqual(
                    $"(124) Directory {directory.FullName} is in use by another surl process", second.FailureMessage);
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Take_AfterTheFirstHolderIsDisposed_Succeeds()
    {
        var directory = Directory.CreateTempSubdirectory("surl-lock-");
        try
        {
            DataDirectoryLock.Take(directory.FullName).Holder!.Dispose();

            var again = DataDirectoryLock.Take(directory.FullName);
            using (again.Holder)
            {
                Assert.IsNotNull(again.Holder);
                Assert.IsNull(again.FailureMessage);
                Assert.IsTrue(File.Exists(Path.Join(directory.FullName, ".surl", "lock")));
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Take_LockFileLeftOnDiskWithNoHolder_Succeeds()
    {
        var directory = Directory.CreateTempSubdirectory("surl-lock-");
        try
        {
            directory.CreateSubdirectory(".surl");
            File.WriteAllText(Path.Join(directory.FullName, ".surl", "lock"), "left by a killed surl");

            var taken = DataDirectoryLock.Take(directory.FullName);
            using (taken.Holder)
            {
                Assert.IsNotNull(taken.Holder);
                Assert.AreEqual(SurlExitCode.Ok, taken.ExitCode);
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Take_StateFolderMissing_CreatesIt()
    {
        var directory = Directory.CreateTempSubdirectory("surl-lock-");
        try
        {
            var taken = DataDirectoryLock.Take(directory.FullName);
            using (taken.Holder)
            {
                Assert.IsTrue(Directory.Exists(Path.Join(directory.FullName, ".surl")));
                Assert.IsNotNull(taken.Holder);
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Take_StateFolderNameIsAFile_RefusesWithCouldNotWriteFile()
    {
        var directory = Directory.CreateTempSubdirectory("surl-lock-");
        try
        {
            var stateFolder = Path.Join(directory.FullName, ".surl");
            File.WriteAllText(stateFolder, "not a folder");

            var taken = DataDirectoryLock.Take(directory.FullName);

            Assert.IsNull(taken.Holder);
            Assert.AreEqual(SurlExitCode.CouldNotWriteFile, taken.ExitCode);
            StringAssert.StartsWith(taken.FailureMessage, $"(23) Could not create {stateFolder}: ");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
