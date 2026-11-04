namespace Surl.Protocol.Ssh;

/// <summary>
/// What reading a host-certificate file's bytes gave: a certificate, or why not.
/// </summary>
/// <param name="Certificate">The certificate; <see langword="null"/> when refused.</param>
/// <param name="Refusal">Why the bytes give no certificate; <see langword="null"/> when one was read.</param>
public sealed record SshHostCertificateReading(SshHostCertificate? Certificate, SshHostCertificateRefusal? Refusal);
