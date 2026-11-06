namespace Surl.Cryptography.Srp;

/// <summary>
/// One account of a GnuTLS <c>tpasswd</c> file: the user, the salt, the verifier and the group the
/// verifier was computed in.
/// </summary>
/// <param name="UserName">The user name, compared with the bytes of the client's <c>srp</c> extension.</param>
/// <param name="Salt">The salt <c>s</c>.</param>
/// <param name="Verifier">The verifier <c>v</c>, unsigned big-endian.</param>
/// <param name="Group">The group the line's index names.</param>
public sealed record SrpVerifierEntry(string UserName, byte[] Salt, byte[] Verifier, SrpGroup Group);
