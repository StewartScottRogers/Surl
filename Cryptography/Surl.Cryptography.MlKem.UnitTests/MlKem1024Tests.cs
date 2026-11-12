using System.Security.Cryptography;

namespace Surl.Cryptography.MlKem;

/// <summary>
/// ML-KEM-1024 against NIST's ACVP known-answer vectors for FIPS 203 (see
/// <see cref="UnitTestAcvpVectors"/> for the source and URL:
/// https://github.com/usnistgov/ACVP-Server/tree/master/gen-val/json-files).
/// </summary>
[TestClass]
public sealed class MlKem1024Tests
{
    [TestMethod]
    public void GenerateKey_AcvpKeyGenCases_WriteTheVectorsKeys()
    {
        foreach (var testCase in UnitTestAcvpVectors.Cases("ML-KEM-1024", "keyGen"))
        {
            var encapsulationKey = new byte[MlKem1024.EncapsulationKeySize];
            var decapsulationKey = new byte[MlKem1024.DecapsulationKeySize];

            MlKem1024.GenerateKey(testCase.Bytes("d"), testCase.Bytes("z"), encapsulationKey, decapsulationKey);

            var id = testCase.GetProperty("tcId").GetInt32();
            CollectionAssert.AreEqual(testCase.Bytes("ek"), encapsulationKey, $"ek of tcId {id}");
            CollectionAssert.AreEqual(testCase.Bytes("dk"), decapsulationKey, $"dk of tcId {id}");
        }
    }

    [TestMethod]
    public void TryEncapsulate_AcvpEncapsulationCases_WriteTheVectorsCiphertextAndSecret()
    {
        foreach (var testCase in UnitTestAcvpVectors.Cases("ML-KEM-1024", "encapsulation"))
        {
            var ciphertext = new byte[MlKem1024.CiphertextSize];
            var sharedSecret = new byte[MlKem1024.SharedSecretSize];

            var encapsulated = MlKem1024.TryEncapsulate(testCase.Bytes("ek"), testCase.Bytes("m"), ciphertext, sharedSecret);

            var id = testCase.GetProperty("tcId").GetInt32();
            Assert.IsTrue(encapsulated, $"tcId {id}");
            CollectionAssert.AreEqual(testCase.Bytes("c"), ciphertext, $"c of tcId {id}");
            CollectionAssert.AreEqual(testCase.Bytes("k"), sharedSecret, $"k of tcId {id}");
        }
    }

    [TestMethod]
    public void TryDecapsulate_AcvpEncapsulationCases_RecoverTheVectorsSecret()
    {
        foreach (var testCase in UnitTestAcvpVectors.Cases("ML-KEM-1024", "encapsulation"))
        {
            var sharedSecret = new byte[MlKem1024.SharedSecretSize];

            var decapsulated = MlKem1024.TryDecapsulate(testCase.Bytes("dk"), testCase.Bytes("c"), sharedSecret);

            Assert.IsTrue(decapsulated);
            CollectionAssert.AreEqual(testCase.Bytes("k"), sharedSecret);
        }
    }

    /// <summary>Case 97 is a valid ciphertext; case 96 a modified one, which yields the implicit-rejection secret.</summary>
    [TestMethod]
    public void TryDecapsulate_AcvpDecapsulationCases_WriteTheVectorsSecret()
    {
        foreach (var testCase in UnitTestAcvpVectors.Cases("ML-KEM-1024", "decapsulation"))
        {
            var sharedSecret = new byte[MlKem1024.SharedSecretSize];

            var decapsulated = MlKem1024.TryDecapsulate(testCase.Bytes("dk"), testCase.Bytes("c"), sharedSecret);

            Assert.IsTrue(decapsulated);
            CollectionAssert.AreEqual(testCase.Bytes("k"), sharedSecret, testCase.GetProperty("reason").GetString());
        }
    }

    [TestMethod]
    public void TryEncapsulate_AcvpEncapsulationKeyChecks_RejectOnlyTheKeysTheVectorsFail()
    {
        foreach (var testCase in UnitTestAcvpVectors.Cases("ML-KEM-1024", "encapsulationKeyCheck"))
        {
            var ciphertext = new byte[MlKem1024.CiphertextSize];
            var sharedSecret = new byte[MlKem1024.SharedSecretSize];

            var encapsulated = MlKem1024.TryEncapsulate(testCase.Bytes("ek"), new byte[32], ciphertext, sharedSecret);

            Assert.AreEqual(testCase.GetProperty("testPassed").GetBoolean(), encapsulated, testCase.GetProperty("reason").GetString());
        }
    }

    [TestMethod]
    public void TryDecapsulate_AcvpDecapsulationKeyChecks_RejectOnlyTheKeysTheVectorsFail()
    {
        foreach (var testCase in UnitTestAcvpVectors.Cases("ML-KEM-1024", "decapsulationKeyCheck"))
        {
            var sharedSecret = new byte[MlKem1024.SharedSecretSize];

            var decapsulated = MlKem1024.TryDecapsulate(testCase.Bytes("dk"), new byte[MlKem1024.CiphertextSize], sharedSecret);

            Assert.AreEqual(testCase.GetProperty("testPassed").GetBoolean(), decapsulated, testCase.GetProperty("reason").GetString());
        }
    }

    [TestMethod]
    public void TryDecapsulate_RandomKeyPairAndEncapsulation_AgreesOnTheSecret()
    {
        var encapsulationKey = new byte[MlKem1024.EncapsulationKeySize];
        var decapsulationKey = new byte[MlKem1024.DecapsulationKeySize];
        MlKem1024.GenerateKey(RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32), encapsulationKey, decapsulationKey);
        var ciphertext = new byte[MlKem1024.CiphertextSize];
        var sent = new byte[MlKem1024.SharedSecretSize];
        MlKem1024.TryEncapsulate(encapsulationKey, RandomNumberGenerator.GetBytes(32), ciphertext, sent);
        var received = new byte[MlKem1024.SharedSecretSize];

        MlKem1024.TryDecapsulate(decapsulationKey, ciphertext, received);

        CollectionAssert.AreEqual(sent, received);
    }

    [TestMethod]
    [DataRow(31, 32, 1568, 3168, "d")]
    [DataRow(32, 33, 1568, 3168, "z")]
    [DataRow(32, 32, 1567, 3168, "encapsulationKey")]
    [DataRow(32, 32, 1568, 3169, "decapsulationKey")]
    public void GenerateKey_SpanOfTheWrongSize_ThrowsArgumentExceptionNamingIt(int d, int z, int ek, int dk, string name)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => MlKem1024.GenerateKey(new byte[d], new byte[z], new byte[ek], new byte[dk]));

        Assert.AreEqual(name, exception.ParamName);
    }

    [TestMethod]
    [DataRow(1567, 32, 1568, 32, "encapsulationKey")]
    [DataRow(1568, 31, 1568, 32, "m")]
    [DataRow(1568, 32, 1569, 32, "ciphertext")]
    [DataRow(1568, 32, 1568, 64, "sharedSecret")]
    public void TryEncapsulate_SpanOfTheWrongSize_ThrowsArgumentExceptionNamingIt(int ek, int m, int c, int k, string name)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => MlKem1024.TryEncapsulate(new byte[ek], new byte[m], new byte[c], new byte[k]));

        Assert.AreEqual(name, exception.ParamName);
    }

    [TestMethod]
    [DataRow(3167, 1568, 32, "decapsulationKey")]
    [DataRow(3168, 1567, 32, "ciphertext")]
    [DataRow(3168, 1568, 31, "sharedSecret")]
    public void TryDecapsulate_SpanOfTheWrongSize_ThrowsArgumentExceptionNamingIt(int dk, int c, int k, string name)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => MlKem1024.TryDecapsulate(new byte[dk], new byte[c], new byte[k]));

        Assert.AreEqual(name, exception.ParamName);
    }
}
