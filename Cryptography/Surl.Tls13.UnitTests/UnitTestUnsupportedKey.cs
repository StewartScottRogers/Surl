using System.Security.Cryptography;

namespace Surl.Tls13;

/// <summary>A key that is neither RSA nor ECDSA, which no TLS 1.3 server signature scheme of ADR-0082 uses.</summary>
internal sealed class UnitTestUnsupportedKey : AsymmetricAlgorithm;
