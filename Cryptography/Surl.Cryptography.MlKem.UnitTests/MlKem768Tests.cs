using System.Security.Cryptography;

namespace Surl.Cryptography.MlKem;

/// <summary>
/// ML-KEM-768 against NIST's ACVP known-answer vectors for FIPS 203 (see
/// <see cref="UnitTestAcvpVectors"/> for the source and URL:
/// https://github.com/usnistgov/ACVP-Server/tree/master/gen-val/json-files).
/// </summary>
[TestClass]
public sealed class MlKem768Tests
{
    [TestMethod]
    public void GenerateKey_AcvpKeyGenCases_WriteTheVectorsKeys()
    {
        foreach (var testCase in UnitTestAcvpVectors.Cases("keyGen"))
        {
            var encapsulationKey = new byte[MlKem768.EncapsulationKeySize];
            var decapsulationKey = new byte[MlKem768.DecapsulationKeySize];

            MlKem768.GenerateKey(testCase.Bytes("d"), testCase.Bytes("z"), encapsulationKey, decapsulationKey);

            var id = testCase.GetProperty("tcId").GetInt32();
            CollectionAssert.AreEqual(testCase.Bytes("ek"), encapsulationKey, $"ek of tcId {id}");
            CollectionAssert.AreEqual(testCase.Bytes("dk"), decapsulationKey, $"dk of tcId {id}");
        }
    }

    [TestMethod]
    public void TryEncapsulate_AcvpEncapsulationCases_WriteTheVectorsCiphertextAndSecret()
    {
        foreach (var testCase in UnitTestAcvpVectors.Cases("encapsulation"))
        {
            var ciphertext = new byte[MlKem768.CiphertextSize];
            var sharedSecret = new byte[MlKem768.SharedSecretSize];

            var encapsulated = MlKem768.TryEncapsulate(testCase.Bytes("ek"), testCase.Bytes("m"), ciphertext, sharedSecret);

            var id = testCase.GetProperty("tcId").GetInt32();
            Assert.IsTrue(encapsulated, $"tcId {id}");
            CollectionAssert.AreEqual(testCase.Bytes("c"), ciphertext, $"c of tcId {id}");
            CollectionAssert.AreEqual(testCase.Bytes("k"), sharedSecret, $"k of tcId {id}");
        }
    }

    [TestMethod]
    public void TryDecapsulate_AcvpEncapsulationCases_RecoverTheVectorsSecret()
    {
        foreach (var testCase in UnitTestAcvpVectors.Cases("encapsulation"))
        {
            var sharedSecret = new byte[MlKem768.SharedSecretSize];

            var decapsulated = MlKem768.TryDecapsulate(testCase.Bytes("dk"), testCase.Bytes("c"), sharedSecret);

            Assert.IsTrue(decapsulated);
            CollectionAssert.AreEqual(testCase.Bytes("k"), sharedSecret);
        }
    }

    /// <summary>Case 86 is a valid ciphertext; case 88 a modified one, which yields the implicit-rejection secret.</summary>
    [TestMethod]
    public void TryDecapsulate_AcvpDecapsulationCases_WriteTheVectorsSecret()
    {
        foreach (var testCase in UnitTestAcvpVectors.Cases("decapsulation"))
        {
            var sharedSecret = new byte[MlKem768.SharedSecretSize];

            var decapsulated = MlKem768.TryDecapsulate(testCase.Bytes("dk"), testCase.Bytes("c"), sharedSecret);

            Assert.IsTrue(decapsulated);
            CollectionAssert.AreEqual(testCase.Bytes("k"), sharedSecret, testCase.GetProperty("reason").GetString());
        }
    }

    [TestMethod]
    public void TryEncapsulate_AcvpEncapsulationKeyChecks_RejectOnlyTheKeysTheVectorsFail()
    {
        foreach (var testCase in UnitTestAcvpVectors.Cases("encapsulationKeyCheck"))
        {
            var ciphertext = new byte[MlKem768.CiphertextSize];
            var sharedSecret = new byte[MlKem768.SharedSecretSize];

            var encapsulated = MlKem768.TryEncapsulate(testCase.Bytes("ek"), new byte[32], ciphertext, sharedSecret);

            Assert.AreEqual(testCase.GetProperty("testPassed").GetBoolean(), encapsulated, testCase.GetProperty("reason").GetString());
        }
    }

    [TestMethod]
    public void TryDecapsulate_AcvpDecapsulationKeyChecks_RejectOnlyTheKeysTheVectorsFail()
    {
        foreach (var testCase in UnitTestAcvpVectors.Cases("decapsulationKeyCheck"))
        {
            var sharedSecret = new byte[MlKem768.SharedSecretSize];

            var decapsulated = MlKem768.TryDecapsulate(testCase.Bytes("dk"), new byte[MlKem768.CiphertextSize], sharedSecret);

            Assert.AreEqual(testCase.GetProperty("testPassed").GetBoolean(), decapsulated, testCase.GetProperty("reason").GetString());
        }
    }

    [TestMethod]
    public void TryDecapsulate_RandomKeyPairAndEncapsulation_AgreesOnTheSecret()
    {
        var encapsulationKey = new byte[MlKem768.EncapsulationKeySize];
        var decapsulationKey = new byte[MlKem768.DecapsulationKeySize];
        MlKem768.GenerateKey(RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32), encapsulationKey, decapsulationKey);
        var ciphertext = new byte[MlKem768.CiphertextSize];
        var sent = new byte[MlKem768.SharedSecretSize];
        MlKem768.TryEncapsulate(encapsulationKey, RandomNumberGenerator.GetBytes(32), ciphertext, sent);
        var received = new byte[MlKem768.SharedSecretSize];

        MlKem768.TryDecapsulate(decapsulationKey, ciphertext, received);

        CollectionAssert.AreEqual(sent, received);
    }

    [TestMethod]
    [DataRow(31, 32, 1184, 2400, "d")]
    [DataRow(32, 33, 1184, 2400, "z")]
    [DataRow(32, 32, 1183, 2400, "encapsulationKey")]
    [DataRow(32, 32, 1184, 2401, "decapsulationKey")]
    public void GenerateKey_SpanOfTheWrongSize_ThrowsArgumentExceptionNamingIt(int d, int z, int ek, int dk, string name)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => MlKem768.GenerateKey(new byte[d], new byte[z], new byte[ek], new byte[dk]));

        Assert.AreEqual(name, exception.ParamName);
    }

    [TestMethod]
    [DataRow(1183, 32, 1088, 32, "encapsulationKey")]
    [DataRow(1184, 31, 1088, 32, "m")]
    [DataRow(1184, 32, 1089, 32, "ciphertext")]
    [DataRow(1184, 32, 1088, 64, "sharedSecret")]
    public void TryEncapsulate_SpanOfTheWrongSize_ThrowsArgumentExceptionNamingIt(int ek, int m, int c, int k, string name)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => MlKem768.TryEncapsulate(new byte[ek], new byte[m], new byte[c], new byte[k]));

        Assert.AreEqual(name, exception.ParamName);
    }

    [TestMethod]
    [DataRow(2399, 1088, 32, "decapsulationKey")]
    [DataRow(2400, 1087, 32, "ciphertext")]
    [DataRow(2400, 1088, 31, "sharedSecret")]
    public void TryDecapsulate_SpanOfTheWrongSize_ThrowsArgumentExceptionNamingIt(int dk, int c, int k, string name)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => MlKem768.TryDecapsulate(new byte[dk], new byte[c], new byte[k]));

        Assert.AreEqual(name, exception.ParamName);
    }
}
