namespace Surl.Tls13;

[TestClass]
public sealed class TlsFiniteFieldGroupTests
{
    [TestMethod]
    [DataRow(0x0100, 256)]
    [DataRow(0x0101, 384)]
    public void Of_Ffdhe2048Or3072_IsTheRfc7919PrimeOfItsLength(int group, int byteLength)
    {
        var field = TlsFiniteFieldGroup.Of((TlsNamedGroup)group);

        Assert.AreEqual(UnitTestFiniteFieldPrimes.Of((ushort)group), field.Prime);
        Assert.AreEqual(byteLength, field.ByteLength);
    }

    [TestMethod]
    public void Of_GroupThatIsNotAFiniteFieldGroupTheServerServes_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TlsFiniteFieldGroup.Of((TlsNamedGroup)0x0102));
    }
}
